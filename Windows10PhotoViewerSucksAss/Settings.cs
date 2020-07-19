using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Windows10PhotoViewerSucksAss
{
	public class Settings
	{
		public static void Initialize(string appDataFolderName)
		{
			Debug.Assert(_manager == null);
			_manager = new SettingsManager<Settings>(appDataFolderName);
		}

		public static void Load()
		{
			Instance = GetManager().Load();
		}

		public static void QueueSave()
		{
			var tmp = Instance;
			Debug.Assert(tmp != null);
			GetManager().QueueSave(tmp);
		}

		public static void WaitSaveCompleted()
		{
			GetManager().WaitSaveCompleted();
		}

		private static SettingsManager<Settings> GetManager()
		{
			var tmp = _manager;
			Debug.Assert(tmp != null);
			return tmp;
		}

		private static SettingsManager<Settings> _manager;
		public static Settings Instance { get; private set; }

		//    _   _                          _   _   _                 
		//   | | | |___  ___ _ __   ___  ___| |_| |_(_)_ __   __ _ ___ 
		//   | | | / __|/ _ \ '__| / __|/ _ \ __| __| | '_ \ / _` / __|
		//   | |_| \__ \  __/ |    \__ \  __/ |_| |_| | | | | (_| \__ \
		//    \___/|___/\___|_|    |___/\___|\__|\__|_|_| |_|\__, |___/
		//                                                   |___/     

		public int Color { get; set; }
		public int WindowWidth { get; set; }
		public int WindowHeight { get; set; }
		public bool SortCaseSensitive { get; set; }
		public FontDescriptor ApplicationFont { get; set; }
		public int OverviewControlWidth { get; set; } = -1;
		public int SplitterWidth { get; set; } = -1;
		public string MouseWheelMode { get; set; }
		public bool UseCurrentImageAsWindowIcon { get; set; }
	}
}
