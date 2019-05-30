using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	static class Program
	{
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
			if (String.IsNullOrWhiteSpace(executablePath))
			{
				ApplicationName = "Windows10PhotoViewerSucksAss";
			}
			else
			{
				ApplicationName = System.IO.Path.GetFileNameWithoutExtension(executablePath); ;
			}
			Settings.Initialize(ApplicationName);
			Settings.Load();

			var form = new Form1();
			if (args.Length >= 1)
			{
				form.SetDisplayPath_NoThrowInteractive(args[0]);
			}
			Application.Run(form);
		}

		public static string ApplicationName { get; private set; }

		private static void HandleAppDomainException(object sender, UnhandledExceptionEventArgs e)
		{
			var message = "Unhandled AppDomain Exception:\r\n" + (e.ExceptionObject?.ToString() ?? "<null>");
			Debug.WriteLine(message);
			MessageBox.Show(message);
		}
	}
}
