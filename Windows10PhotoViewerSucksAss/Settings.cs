using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	[DataContract(Namespace="")]
	public class Settings
	{
		static Settings()
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string fullPath = Path.Combine(appData, @"Windows10PhotoViewerSucksAss\settings.xml");
			Manager = new SettingsManager(fullPath);
		}

		internal static SettingsManager Manager { get; }
		internal static Settings Instance { get; set; } = new Settings();

		[DataMember]
		public int Color { get; set; }
		[DataMember]
		public int WindowWidth { get; set; }
		[DataMember]
		public int WindowHeight { get; set; }
	}
}
