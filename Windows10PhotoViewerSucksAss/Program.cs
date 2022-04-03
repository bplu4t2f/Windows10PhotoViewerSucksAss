using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	static class Program
	{
		/// <summary>
		/// This is for interop with other processes using <see cref="StashHelper"/>.
		/// <para>If this argument is present, then the next argument will be a handle value to a file mapping object that contains the STARTUP_PARAMS.</para>
		/// </summary>
		public static string GUID_StartupParams => "{4B2F7527-4FF2-4444-81F3-4A51C533B10E}";

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			AppDomain.CurrentDomain.UnhandledException += HandleAppDomainException;

			var executablePath = Application.ExecutablePath;
			string BootstrapFilePath = executablePath == null ? null : executablePath + ".xml";
			BootstrapData BootstrapData = BootstrapFilePath == null ? null : Bootstrapping.Load(BootstrapFilePath);
			if (BootstrapData?.AppDataFolderName != null)
			{
				if (executablePath != null)
				{
					string ExeDir = Path.GetDirectoryName(executablePath);
					// TODO: This is actually super broken !!
					//       It causes Path.Combine("c:\users\...\appdata\", "d:\...\windows10photoviewersucksass\bin\debug") to be called later in the settings manager thing.
					//       The only reason this works is because Path.Combine just returns the second argument if it sees an absolute path there... bUGgY
					BootstrapData.AppDataFolderName = BootstrapData.AppDataFolderName.Replace("%%EXE_DIR%%", ExeDir);
				}
			}

			// Load user settings
			string AppDataFolderName = BootstrapData?.AppDataFolderName ?? GetFallbackAppDataFolderName(executablePath);
			Settings.Initialize(AppDataFolderName);
			Settings.Load();

			string startupDisplayPath = null;
			bool gotStartupParamsArg = false;
			string StartupParamsHandleValue = null;
			for (int i = 0; i < args.Length; ++i)
			{
				if (gotStartupParamsArg)
				{
					// This is the file mapping handle value  for StashHelper.
					StartupParamsHandleValue = args[i];
					gotStartupParamsArg = false;
					//MessageBox.Show(StartupParamsFileMappingHandleValueString);
				}
				else if (args[i] == GUID_StartupParams)
				{
					// The next argument will be the value of the file mapping handle value to load STARTUP_PARAMS in StashHelper.
					gotStartupParamsArg = true;
				}
				else if (startupDisplayPath == null)
				{
					// File path to the image that should be displayed.
					startupDisplayPath = args[i];
				}
				// else: stray argument; ignore it.
			}

			var StartupInfo = new StartupInfo(executablePath, BootstrapData?.FriendlyApplicationName ?? AppDataFolderName, StartupParamsHandleValue);

			var form = new Form1(StartupInfo);
			if (startupDisplayPath != null)
			{
				form.SetDisplayPath_NoThrowInteractive(startupDisplayPath);
			}
			Application.Run(form);
		}

		/// <summary>
		/// For settings folder if BootstrapData doesn't exist.
		/// </summary>
		private static string GetFallbackAppDataFolderName(string ExecutablePath)
		{
			if (String.IsNullOrWhiteSpace(ExecutablePath))
			{
				return "Windows10PhotoViewerSucksAss";
			}
			else
			{
				return System.IO.Path.GetFileNameWithoutExtension(ExecutablePath);
			}
		}

		private static void HandleAppDomainException(object sender, UnhandledExceptionEventArgs e)
		{
			var message = "Unhandled AppDomain Exception:\r\n" + (e.ExceptionObject?.ToString() ?? "<null>");
			Debug.WriteLine(message);
			MessageBox.Show(message);
		}
	}
}
