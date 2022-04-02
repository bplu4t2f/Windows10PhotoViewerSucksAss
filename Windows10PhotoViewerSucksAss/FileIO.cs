using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	static class FileIO
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
			[MarshalAs(UnmanagedType.LPTStr)] string filename,
			[MarshalAs(UnmanagedType.U4)] FileAccess access,
			[MarshalAs(UnmanagedType.U4)] FileShare share,
			IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
			[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
			[MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
			IntPtr templateFile
			);

		/// <summary>
		/// Exception-free file opening because fuck exceptions.
		/// </summary>
		public static FileStream Open(out int error, string filename, FileAccess fileAccess = FileAccess.ReadWrite, FileShare fileShare = FileShare.None, FileMode creationDisposition = FileMode.OpenOrCreate)
		{
			var handle = CreateFile(filename, fileAccess, fileShare, IntPtr.Zero, creationDisposition, FileAttributes.Normal, IntPtr.Zero);
			if (handle.IsInvalid)
			{
				error = Marshal.GetLastWin32Error();
				return null;
			}
			error = 0;
			return new FileStream(handle, FileAccess.ReadWrite);
		}

		public static int ReadEntireFile(out byte[] data, string filename)
		{
			using (var fileStream = Open(out int error, filename, FileAccess.Read, FileShare.Read, FileMode.Open))
			{
				if (fileStream == null)
				{
					data = null;
					return error;
				}
				try
				{
					data = new byte[fileStream.Length];
					fileStream.Read(data, 0, data.Length);
					return 0;
				}
				finally
				{
					fileStream.Dispose();
				}
			}
		}

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct SHELLEXECUTEINFO
		{
			public int cbSize;
			public uint fMask;
			public IntPtr hwnd;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpVerb;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpFile;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpParameters;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpDirectory;
			public int nShow;
			public IntPtr hInstApp;
			public IntPtr lpIDList;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpClass;
			public IntPtr hkeyClass;
			public uint dwHotKey;
			public IntPtr hIcon;
			public IntPtr hProcess;
		}

		private const int SW_SHOW = 5;
		private const uint SEE_MASK_INVOKEIDLIST = 12;
		public static bool ShowFileProperties(string Filename)
		{
			SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
			info.cbSize = Marshal.SizeOf(info);
			info.lpVerb = "properties";
			info.lpFile = Filename;
			info.nShow = SW_SHOW;
			info.fMask = SEE_MASK_INVOKEIDLIST;
			return ShellExecuteEx(ref info);
		}


		public static int SelectInFileExplorer(string fullPath)
		{
			fullPath = Path.GetFullPath(fullPath);

			IntPtr pidlList = ILCreateFromPathW(fullPath);
			if (pidlList == IntPtr.Zero)
			{
				return -1;
			}

			try
			{
				// Open parent folder and select item
				return SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0);
			}
			finally
			{
				ILFree(pidlList);
			}
		}

		[DllImport("shell32.dll", ExactSpelling = true)]
		private static extern void ILFree(IntPtr pidlList);

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		private static extern IntPtr ILCreateFromPathW(string pszPath);

		[DllImport("shell32.dll", ExactSpelling = true)]
		private static extern int SHOpenFolderAndSelectItems(IntPtr pidlList, uint cild, IntPtr children, uint dwFlags);



		/// <summary>
		/// Possible flags for the SHFileOperation method.
		/// </summary>
		[Flags]
		public enum FileOperationFlags : ushort
		{
			/// <summary>
			/// Do not show a dialog during the process
			/// </summary>
			FOF_SILENT = 0x0004,
			/// <summary>
			/// Do not ask the user to confirm selection
			/// </summary>
			FOF_NOCONFIRMATION = 0x0010,
			/// <summary>
			/// Delete the file to the recycle bin.  (Required flag to send a file to the bin
			/// </summary>
			FOF_ALLOWUNDO = 0x0040,
			/// <summary>
			/// Do not show the names of the files or folders that are being recycled.
			/// </summary>
			FOF_SIMPLEPROGRESS = 0x0100,
			/// <summary>
			/// Surpress errors, if any occur during the process.
			/// </summary>
			FOF_NOERRORUI = 0x0400,
			/// <summary>
			/// Warn if files are too big to fit in the recycle bin and will need
			/// to be deleted completely.
			/// </summary>
			FOF_WANTNUKEWARNING = 0x4000,
		}

		/// <summary>
		/// File Operation Function Type for SHFileOperation
		/// </summary>
		public enum FileOperationType : uint
		{
			/// <summary>
			/// Move the objects
			/// </summary>
			FO_MOVE = 0x0001,
			/// <summary>
			/// Copy the objects
			/// </summary>
			FO_COPY = 0x0002,
			/// <summary>
			/// Delete (or recycle) the objects
			/// </summary>
			FO_DELETE = 0x0003,
			/// <summary>
			/// Rename the object(s)
			/// </summary>
			FO_RENAME = 0x0004,
		}



		/// <summary>
		/// SHFILEOPSTRUCT for SHFileOperation from COM
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct SHFILEOPSTRUCT
		{
			public IntPtr hwnd;
			[MarshalAs(UnmanagedType.U4)]
			public FileOperationType wFunc;
			public string pFrom;
			public string pTo;
			public FileOperationFlags fFlags;
			[MarshalAs(UnmanagedType.Bool)]
			public bool fAnyOperationsAborted;
			public IntPtr hNameMappings;
			public string lpszProgressTitle;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

		/// <summary>
		/// Send file to recycle bin.
		/// Returns true on success.
		/// </summary>
		/// <param name="path">Location of directory or file to recycle</param>
		/// <param name="flags">FileOperationFlags to add in addition to FOF_ALLOWUNDO</param>
		public static bool Send(string path, FileOperationFlags flags)
		{
			// TODO find out what this does if recycle bin is disabled
			try
			{
				var fs = new SHFILEOPSTRUCT
				{
					wFunc = FileOperationType.FO_DELETE,
					pFrom = path + '\0' + '\0',
					fFlags = FileOperationFlags.FOF_ALLOWUNDO | flags
				};
				int error = SHFileOperation(ref fs);
				return error == 0;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Send file to recycle bin.  Display dialog, display warning if files are too big to fit (FOF_WANTNUKEWARNING)
		/// </summary>
		/// <param name="path">Location of directory or file to recycle</param>
		public static bool Send(string path)
		{
			return Send(path, FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_WANTNUKEWARNING);
		}
	}
}
