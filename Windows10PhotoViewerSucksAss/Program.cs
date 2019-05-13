using System;
using System.Collections.Generic;
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

			ExecutablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
			if (String.IsNullOrWhiteSpace(ExecutablePath))
			{
				ExecutablePath = "Windows10PhotoViewerSucksAss";
			}
			string applicationName = System.IO.Path.GetFileNameWithoutExtension(ExecutablePath);
			Settings.Initialize(applicationName);
			Settings.Load();

			var form = new Form1();
			if (args.Length >= 1)
			{
				form.SetDisplayPath_NoThrowInteractive(args[0]);
			}
			Application.Run(form);
		}

		public static string ExecutablePath { get; private set; }
	}
}
