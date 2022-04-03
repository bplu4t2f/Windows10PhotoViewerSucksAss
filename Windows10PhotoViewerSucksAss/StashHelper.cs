using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Windows10PhotoViewerSucksAss
{
	using static XmlDocumentHelper;

	static class StashHelper
	{
		public static unsafe List<StashInfo> Stash()
		{
			var topLevelWindows = new List<IntPtr>();
			var handle = GCHandle.Alloc(topLevelWindows);
			{
				var sw = Stopwatch.StartNew();
				EnumWindows(EnumProc, GCHandle.ToIntPtr(handle));
				Debug.WriteLine($"EnumWindows: {sw.ElapsedMilliseconds} ms");
			}
			handle.Free();

			// We're in Win32 land here, not .NET land.
			// We should use the exact same approach of determining the "process executable name" for both the current
			// process, and the candidate process.
			// That means we're limited to using Win32 tech.

			// https://devblogs.microsoft.com/oldnewthing/20040707-00/?p=38523
			// TL;DR use GetWindowPlacement/SetWindowPlacement to save the window positions.

			var infoList = new List<StashInfo>();

			var currentProcessExecutableName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
			foreach (var hWnd in topLevelWindows)
			{
				// Get the process it belongs to, check that it matches the current process' executable name.
				if (0 != GetWindowThreadProcessId(hWnd, out uint processId))
				{
					string candidate;
					try
					{
						using (var process = Process.GetProcessById(unchecked((int)processId)))
						{
							candidate = Path.GetFileName(process.MainModule.FileName);
						}
					}
					catch (Exception ex)
					{
						Debug.WriteLine(ex.ToString());
						continue;
					}
					if (candidate == currentProcessExecutableName)
					{
						int len = GetWindowTextLength(hWnd);
						if (len > 0)
						{
							var sb = new StringBuilder(len + 1);
							if (0 != GetWindowText(hWnd, sb, len + 1))
							{
								// Success.
								// Now the frustrating thing is that the window title is ambiguous.
								var match = Regex.Match(sb.ToString(), @"\((?<Path>[a-zA-Z]:.+)\)");
								if (match.Success)
								{
									var path = match.Groups["Path"].Value;
									var placement = new WINDOWPLACEMENT();
									placement.length = (uint)sizeof(WINDOWPLACEMENT);
									if (GetWindowPlacement(hWnd, ref placement))
									{
										infoList.Add(new StashInfo() { OriginalWindowHandle = hWnd, Path = path, Placement = placement });
									}
								}
							}
						}
					}
				}
			}

			return infoList;
		}

		private static bool EnumProc(IntPtr hWnd, IntPtr lParam)
		{
			var list = (List<IntPtr>)GCHandle.FromIntPtr(lParam).Target;
			if (IsWindowVisible(hWnd))
			{
				list.Add(hWnd);
			}
			return true;
		}

		public static void SaveStash(string fileName, List<StashInfo> stash)
		{
			var doc = new XmlDocument();
			var root = CreateDocumentRootElementAndSetupNamespaces(doc, "Stash");

			foreach (var item in stash)
			{
				var element = AddElement(root, "StashInfo");
				AddElementValueString(element, "Path", item.Path);
				var el_placement = AddElement(element, "WindowPlacement");
				AddElementValueUInt32(el_placement, "flags", item.Placement.flags);
				AddElementValueUInt32(el_placement, "showCmd", item.Placement.showCmd);
				AddElementValuePOINT(el_placement, "ptMinPosition", item.Placement.ptMinPosition);
				AddElementValuePOINT(el_placement, "ptMaxPosition", item.Placement.ptMaxPosition);
				AddElementValueRECT(el_placement, "rcNormalPosition", item.Placement.rcNormalPosition);
			}

			using (var stream = File.Create(fileName))
			using (var writer = new StreamWriter(stream))
			{
				doc.Save(writer);
			}
		}

		public static bool RestoreStash(List<StashInfo> stash, out List<string> failedItems)
		{
			failedItems = null;

			if (!stash.Any()) return true;

			var processFileName = Assembly.GetEntryAssembly().Location;

			foreach (var item in stash)
			{
				if (!DispatchRestoreItem(processFileName, item))
				{
					if (failedItems == null) failedItems = new List<string>();
					failedItems.Add(item.Path);
				}
			}

			return failedItems == null;
		}

		public static unsafe List<StashInfo> LoadStash(string fileName)
		{
			var doc = new XmlDocument();
			using (var stream = FileIO.OpenRead(out int error, fileName))
			{
				if (stream == null) return null;
				using (var reader = new StreamReader(stream))
				{
					doc.Load(reader);
				}
			}

			var root = GetDocumentRootElement(doc);

			var stash = new List<StashInfo>();

			foreach (XmlNode item in root.ChildNodes)
			{
				// Could improve error reporting here.
				// We bail out if we don't understand the XML because this could result in data loss.
				if (!(item is XmlElement element)) return null;

				if (!TryGetElementValueString(element, "Path", out string path)) return null;
				var placement = new WINDOWPLACEMENT();
				// NOTE: length is not set here. It is set when we actually call the API.
				if (TryGetElement(element, "WindowPlacement", out var el_placement))
				{
					{ if (TryGetElementValueUInt32(el_placement, "flags", out var tmp)) placement.flags = tmp; }
					{ if (TryGetElementValueUInt32(el_placement, "showCmd", out var tmp)) placement.showCmd = tmp; }
					{ if (TryGetElementValuePOINT(el_placement, "ptMinPosition", out var tmp)) placement.ptMinPosition = tmp; }
					{ if (TryGetElementValuePOINT(el_placement, "ptMaxPosition", out var tmp)) placement.ptMaxPosition = tmp; }
					{ if (TryGetElementValueRECT(el_placement, "rcNormalPosition", out var tmp)) placement.rcNormalPosition = tmp; }
				}

				stash.Add(new StashInfo() { Path = path, Placement = placement });
			}

			return stash;
		}

		private static unsafe bool DispatchRestoreItem(string processFileName, StashInfo item)
		{
			try
			{
				// Gonna implement the approach recommended by Raymond Chen here:
				// https://devblogs.microsoft.com/oldnewthing/20031211-00/?p=41543
				// Allocate shared memory, inherit the handle (SECURITY_ATTRIBUTES.bInheritHandle = TRUE),
				// give the child process the handle as numeric value.

				STARTUP_PARAMS* startup_params = CreateStartupParams(out IntPtr hMapping);
				if (startup_params == null)
				{
					return false;
				}

				startup_params->Placement = item.Placement;

				var commandLine = new StringBuilder();
				CommandLineArgumentQuoting.ArgvQuote(item.Path, commandLine, false);
				commandLine.Append(" ");
				commandLine.Append(Program.GUID_StartupParams);
				commandLine.Append(" ");
				commandLine.Append(WrapStartupParamsHandleValue(hMapping));
				string args = commandLine.ToString();

				try
				{
					CreateProcessSimple(processFileName, args, new IntPtr[] { hMapping });
				}
				finally
				{
					FreeStartupParams(startup_params, hMapping);
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Could not restore process: " + ex.ToString());
				return false;
			}
		}

		/// <summary>
		/// To be called from child processes that should restore themselves.
		/// <para>Returns true on success.</para>
		/// </summary>
		public static unsafe bool RestoreStashInfoFromMemory(IntPtr targetWindow, string startupParamsHandleValue)
		{
			STARTUP_PARAMS* startup_params = UnwrapAndGetStartupParams(startupParamsHandleValue, out IntPtr hMapping);
			if (startup_params != null)
			{
				var placement = startup_params->Placement;
				placement.length = (uint)sizeof(WINDOWPLACEMENT);
				if (!SetWindowPlacement(targetWindow, ref placement))
				{
					Debug.WriteLine(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);
				}
			}
			FreeStartupParams(startup_params, hMapping);
			return startup_params != null;
		}

		private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowTextLength(IntPtr hWnd);
		[DllImport("user32.dll")]
		private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
		[DllImport("user32.dll")]
		private static extern bool IsWindowVisible(IntPtr hWnd);
		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
		[DllImport("user32.dll")]
		private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT rect);
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT rect);
		[DllImport("kernel32.dll")]
		private static extern IntPtr CreateFileMapping(IntPtr hFile, ref SECURITY_ATTRIBUTES lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, IntPtr lpName);
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern unsafe void* MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);
		[DllImport("kernel32.dll")]
		private static extern unsafe bool UnmapViewOfFile(void* address);
		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr handle);
		[DllImport("kernel32.dll")]
		private static extern unsafe UIntPtr VirtualQuery(void* address, ref MEMORY_BASIC_INFORMATION lpBuffer, UIntPtr dwLength);

		private static IntPtr INVALID_HANDLE_VALUE => (IntPtr)(-1);

		private static unsafe STARTUP_PARAMS* CreateStartupParams(out IntPtr phMapping)
		{
			SECURITY_ATTRIBUTES sa;
			sa.nLength = (uint)sizeof(SECURITY_ATTRIBUTES);
			sa.lpSecurityDescriptor = IntPtr.Zero;
			sa.bInheritHandle = -1;
			IntPtr hMapping = CreateFileMapping(INVALID_HANDLE_VALUE, ref sa, PAGE_READWRITE, 0, (uint)sizeof(STARTUP_PARAMS), IntPtr.Zero);
			STARTUP_PARAMS* psp = null;
			if (hMapping != IntPtr.Zero)
			{
				psp = (STARTUP_PARAMS*)MapViewOfFile(hMapping, FILE_MAP_WRITE, 0, 0, (UIntPtr)0);
				if (psp == null)
				{
					CloseHandle(hMapping);
					hMapping = IntPtr.Zero;
				}
			}
			phMapping = hMapping;
			return psp;
		}

		private static string WrapStartupParamsHandleValue(IntPtr hMapping)
		{
			return ((UInt64)hMapping).ToString(CultureInfo.InvariantCulture);
		}

		private static unsafe void FreeStartupParams(STARTUP_PARAMS* psp, IntPtr hMapping)
		{
			UnmapViewOfFile(psp);
			CloseHandle(hMapping);
		}

		/// <summary>
		/// Returns null on failure.
		/// <para>To be called by a spawned child process when restoring its state using a handle value argument.</para>
		/// </summary>
		private static unsafe STARTUP_PARAMS* UnwrapAndGetStartupParams(string handleValue, out IntPtr hMapping)
		{
			if (!UInt64.TryParse(handleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
			{
				hMapping = IntPtr.Zero;
				return null;
			}

			hMapping = (IntPtr)value;
			STARTUP_PARAMS* psp = (STARTUP_PARAMS*)MapViewOfFile(hMapping, FILE_MAP_READ, 0, 0, UIntPtr.Zero);
			if (psp != null)
			{
				bool success = false;
				// Now that we've mapped it, do some validation
				MEMORY_BASIC_INFORMATION mbi = new MEMORY_BASIC_INFORMATION();
				var mbi_size = (uint)sizeof(MEMORY_BASIC_INFORMATION);
				var ret = VirtualQuery(psp, ref mbi, (UIntPtr)mbi_size);
				if (ret.ToUInt64() >= mbi_size)
				{
					if (mbi.State == MEM_COMMIT &&
						mbi.BaseAddress == (IntPtr)psp &&
						mbi.RegionSize.ToUInt64() >= (uint)sizeof(STARTUP_PARAMS))
					{
						// Success!
						success = true;
					}
				}

				if (!success)
				{
					// Memory block was invalid - toss it
					UnmapViewOfFile(psp);
					psp = null;
				}
			}
			return psp;
		}

		private const uint PAGE_READWRITE = 4;
		private const uint FILE_MAP_WRITE = 2;
		private const uint FILE_MAP_READ = 4;
		private const uint MEM_COMMIT = 0x00001000;

		private struct STARTUP_PARAMS
		{
			public WINDOWPLACEMENT Placement;
		}

		private struct SECURITY_ATTRIBUTES
		{
			public uint nLength;
			public IntPtr lpSecurityDescriptor;
			// This is originally BOOL but cannot be bool in C# because bool is not blittable.
			public int bInheritHandle;
		}

		private struct MEMORY_BASIC_INFORMATION
		{
			public IntPtr BaseAddress;
			public IntPtr AllocationBase;
			public uint AllocationProtect;
			public UIntPtr RegionSize;
			public uint State;
			public uint Protect;
			public uint Type;
		}

		public struct WINDOWPLACEMENT
		{
			public uint length;
			public uint flags;
			public uint showCmd;
			public POINT ptMinPosition;
			public POINT ptMaxPosition;
			public RECT rcNormalPosition;

			public override string ToString()
			{
				var sb = new StringBuilder();
				foreach (var field in this.GetType().GetFields())
				{
					sb.AppendLine(field.Name + " = " + field.GetValue(this).ToString());
				}
				return sb.ToString();
				//return $"0x{flags:X} {showCmd} -- {rcNormalPosition}";
			}
		}

		public struct POINT
		{
			public int x;
			public int y;
			public override string ToString()
			{
				return $"{x},{y}";
			}
		}

		public struct RECT
		{
			public int l;
			public int t;
			public int r;
			public int b;

			public override string ToString()
			{
				return $"{l},{t},{r},{b}";
			}
		}

		private static unsafe void CreateProcessSimple(string ApplicationName, string CommandLineArgs, IntPtr[] handlesToInherit)
		{
			var startup_info = new STARTUPINFOW();
			startup_info.cb = sizeof(STARTUPINFOW);
			fixed (IntPtr* handles = handlesToInherit)
			{
				var pi = new PROCESS_INFORMATION();
				// Nasty: We pass the FULL command line here, including the actual executable name, which would normally go to argv[0] in a C program...
				//        If we omit this, the first argument is missing in C#.
				var realCommandLine = new StringBuilder();
				CommandLineArgumentQuoting.ArgvQuote(ApplicationName, realCommandLine, false);
				realCommandLine.Append(" ");
				realCommandLine.Append(CommandLineArgs);
				if (CreateProcessWithExplicitHandles(ApplicationName, realCommandLine.ToString(), null, null, true, 0, null, null, ref startup_info, &pi, (uint)handlesToInherit.Length, handles))
				{
					CloseHandle(pi.hProcess);
					CloseHandle(pi.hThread);
				}
			}
		}

		private static unsafe bool CreateProcessWithExplicitHandles(
			string lpApplicationName,
			string lpCommandLine,
			SECURITY_ATTRIBUTES* lpProcessAttributes,
			SECURITY_ATTRIBUTES* lpThreadAttributes,
			bool bInheritHandles,
			uint dwCreationFlags,
			void* lpEnvironment,
			string lpCurrentDirectory,
			ref STARTUPINFOW lpStartupInfo,
			PROCESS_INFORMATION* lpProcessInformation,
			// here is the new stuff
			uint cHandlesToInherit,
			IntPtr* rgHandlesToInherit
			)
		{
			// https://devblogs.microsoft.com/oldnewthing/20111216-00/?p=8873

			bool fSuccess;
			bool fInitialized = false;
			UIntPtr size = UIntPtr.Zero;
			byte[] attributeListBuffer = null;
			fSuccess = cHandlesToInherit < 0xFFFFFFFF / sizeof(IntPtr) && lpStartupInfo.cb == sizeof(STARTUPINFOW);
			if (!fSuccess) {
				return false;
			}
			if (fSuccess) {
				fSuccess = !InitializeProcThreadAttributeList(null, 1, 0, ref size);
			}
			if (fSuccess) {
				fSuccess = Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER;
			}
			if (fSuccess)
			{
				attributeListBuffer = new byte[(int)size];
				fixed (void* lpAttributeList = attributeListBuffer)
				{
					fSuccess = InitializeProcThreadAttributeList(lpAttributeList, 1, 0, ref size);
					if (fSuccess)
					{
						fInitialized = true;
						fSuccess = UpdateProcThreadAttribute(
							lpAttributeList,
							0, (UIntPtr)PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
							rgHandlesToInherit,
							(UIntPtr)(cHandlesToInherit * sizeof(IntPtr)), null, UIntPtr.Zero
							);
					}
					if (fSuccess)
					{
						var info = new STARTUPINFOEXW();
						info.StartupInfo = lpStartupInfo;
						info.StartupInfo.cb = sizeof(STARTUPINFOEXW);
						info.lpAttributeList = lpAttributeList;
						fSuccess = CreateProcessW(
							lpApplicationName,
							lpCommandLine,
							lpProcessAttributes,
							lpThreadAttributes,
							bInheritHandles,
							dwCreationFlags,
							lpEnvironment,
							lpCurrentDirectory,
							(STARTUPINFOW*)&info,
							lpProcessInformation
							);
						if (!fSuccess)
						{
							Debug.WriteLine(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);
						}
					}
					if (fInitialized) DeleteProcThreadAttributeList(lpAttributeList);
				}
			}
			return fSuccess;
		}

		private unsafe struct STARTUPINFOW
		{
			public int cb;
			public IntPtr lpReserved;
			public IntPtr lpDesktop;
			public IntPtr lpTitle;
			public uint dwX;
			public uint dwY;
			public uint dwXSize;
			public uint dwYSize;
			public uint dwXCountChars;
			public uint dwYCountChars;
			public uint dwFillAttribute;
			public uint dwFlags;
			public ushort wShowWindow;
			public ushort cbReserved2;
			public byte* lpReserved2;
			public IntPtr hStdInput;
			public IntPtr hStdOutput;
			public IntPtr hStdError;
		}

		private const int ERROR_INSUFFICIENT_BUFFER = 122;

		private unsafe struct STARTUPINFOEXW
		{
			public STARTUPINFOW StartupInfo;
			public void* lpAttributeList;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern unsafe bool InitializeProcThreadAttributeList(
			void* lpAttributeList,
			uint dwAttributeCount,
			uint dwFlags,
			ref UIntPtr lpSize
		);

		[DllImport("kernel32.dll")]
		private static extern unsafe void DeleteProcThreadAttributeList(void* lpAttributeList);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern unsafe bool UpdateProcThreadAttribute(
			void* lpAttributeList,
			uint dwFlags,
			UIntPtr Attribute,
			void* lpValue,
			UIntPtr cbSize,
			void* lpPreviousValue,
			UIntPtr lpReturnSize
		);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern unsafe bool CreateProcessW(
			[MarshalAs(UnmanagedType.LPWStr)] string                lpApplicationName,
			[MarshalAs(UnmanagedType.LPWStr)] string                lpCommandLine,
			SECURITY_ATTRIBUTES*  lpProcessAttributes,
			SECURITY_ATTRIBUTES*  lpThreadAttributes,
			bool                  bInheritHandles,
			uint                  dwCreationFlags,
			void*                 lpEnvironment,
			[MarshalAs(UnmanagedType.LPWStr)] string                lpCurrentDirectory,
			STARTUPINFOW*         lpStartupInfo,
			PROCESS_INFORMATION*  lpProcessInformation
		);

		private struct PROCESS_INFORMATION
		{
			public IntPtr hProcess;
			public IntPtr hThread;
			public uint dwProcessId;
			public uint dwThreadId;
		}

		private const uint PROC_THREAD_ATTRIBUTE_HANDLE_LIST = 0x00020002;
		private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

		[Flags()]
		private enum SetWindowPosFlags : uint
		{
			/// <summary>If the calling thread and the thread that owns the window are attached to different input queues,
			/// the system posts the request to the thread that owns the window. This prevents the calling thread from
			/// blocking its execution while other threads process the request.</summary>
			/// <remarks>SWP_ASYNCWINDOWPOS</remarks>
			AsynchronousWindowPosition = 0x4000,
			/// <summary>Prevents generation of the WM_SYNCPAINT message.</summary>
			/// <remarks>SWP_DEFERERASE</remarks>
			DeferErase = 0x2000,
			/// <summary>Draws a frame (defined in the window's class description) around the window.</summary>
			/// <remarks>SWP_DRAWFRAME</remarks>
			DrawFrame = 0x0020,
			/// <summary>Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to
			/// the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE
			/// is sent only when the window's size is being changed.</summary>
			/// <remarks>SWP_FRAMECHANGED</remarks>
			FrameChanged = 0x0020,
			/// <summary>Hides the window.</summary>
			/// <remarks>SWP_HIDEWINDOW</remarks>
			HideWindow = 0x0080,
			/// <summary>Does not activate the window. If this flag is not set, the window is activated and moved to the
			/// top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter
			/// parameter).</summary>
			/// <remarks>SWP_NOACTIVATE</remarks>
			DoNotActivate = 0x0010,
			/// <summary>Discards the entire contents of the client area. If this flag is not specified, the valid
			/// contents of the client area are saved and copied back into the client area after the window is sized or
			/// repositioned.</summary>
			/// <remarks>SWP_NOCOPYBITS</remarks>
			DoNotCopyBits = 0x0100,
			/// <summary>Retains the current position (ignores X and Y parameters).</summary>
			/// <remarks>SWP_NOMOVE</remarks>
			IgnoreMove = 0x0002,
			/// <summary>Does not change the owner window's position in the Z order.</summary>
			/// <remarks>SWP_NOOWNERZORDER</remarks>
			DoNotChangeOwnerZOrder = 0x0200,
			/// <summary>Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to
			/// the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent
			/// window uncovered as a result of the window being moved. When this flag is set, the application must
			/// explicitly invalidate or redraw any parts of the window and parent window that need redrawing.</summary>
			/// <remarks>SWP_NOREDRAW</remarks>
			DoNotRedraw = 0x0008,
			/// <summary>Same as the SWP_NOOWNERZORDER flag.</summary>
			/// <remarks>SWP_NOREPOSITION</remarks>
			DoNotReposition = 0x0200,
			/// <summary>Prevents the window from receiving the WM_WINDOWPOSCHANGING message.</summary>
			/// <remarks>SWP_NOSENDCHANGING</remarks>
			DoNotSendChangingEvent = 0x0400,
			/// <summary>Retains the current size (ignores the cx and cy parameters).</summary>
			/// <remarks>SWP_NOSIZE</remarks>
			IgnoreResize = 0x0001,
			/// <summary>Retains the current Z order (ignores the hWndInsertAfter parameter).</summary>
			/// <remarks>SWP_NOZORDER</remarks>
			IgnoreZOrder = 0x0004,
			/// <summary>Displays the window.</summary>
			/// <remarks>SWP_SHOWWINDOW</remarks>
			ShowWindow = 0x0040,
		}
		
		private static bool TryGetElementValuePOINT(XmlElement parent, string elementName, out POINT value)
			=> TryGetElementValue(parent, elementName, out value, TryParsePOINT);
		private static XmlElement AddElementValuePOINT(XmlElement parent, string elementName, POINT value)
			=> AddElementValue(parent, elementName, value, FormatPOINT);
		
		private static bool TryGetElementValueRECT(XmlElement parent, string elementName, out RECT value)
			=> TryGetElementValue(parent, elementName, out value, TryParseRECT);
		private static XmlElement AddElementValueRECT(XmlElement parent, string elementName, RECT value)
			=> AddElementValue(parent, elementName, value, FormatRECT);

		private static bool TryParsePOINT(string text, out POINT point)
		{
			if (text == null)
			{
				point = default;
				return false;
			}
			var parts = text.Split(',');
			if (parts.Length != 2)
			{
				point = default;
				return false;
			}
			point = new POINT();
			if (!Int32.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out point.x)) return false;
			if (!Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out point.y)) return false;
			return true;
		}

		private static string FormatPOINT(POINT point)
		{
			return String.Format(CultureInfo.InvariantCulture, "{0},{1}", point.x, point.y);
		}

		private static bool TryParseRECT(string text, out RECT rect)
		{
			if (text == null)
			{
				rect = default;
				return false;
			}
			var parts = text.Split(',');
			if (parts.Length != 4)
			{
				rect = default;
				return false;
			}
			rect = new RECT();
			if (!Int32.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out rect.l)) return false;
			if (!Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out rect.t)) return false;
			if (!Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out rect.r)) return false;
			if (!Int32.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out rect.b)) return false;
			return true;
		}

		private static string FormatRECT(RECT rect)
		{
			return String.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", rect.l, rect.t, rect.r, rect.b);
		}
	}

	sealed class StashInfo
	{
		/// <summary>
		/// This can be used to close the window after stashing.
		/// </summary>
		public IntPtr OriginalWindowHandle { get; set; }
		public string Path { get; set; }
		public StashHelper.WINDOWPLACEMENT Placement { get; set; }

		public override string ToString()
		{
			return $"{Path} -- {Placement}";
		}
	}
}
