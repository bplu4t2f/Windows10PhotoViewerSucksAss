using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		public static void Initialize(string applicationName)
		{
			Debug.Assert(Manager == null);
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string fullPath = Path.Combine(appData, $@"{applicationName}\settings.xml");
			Manager = new SettingsManager(fullPath);
		}

		internal static SettingsManager Manager { get; private set; }
		internal static Settings Instance { get; set; } = new Settings();

		[DataMember]
		public int Color { get; set; }
		[DataMember]
		public int WindowWidth { get; set; }
		[DataMember]
		public int WindowHeight { get; set; }
	}
}
