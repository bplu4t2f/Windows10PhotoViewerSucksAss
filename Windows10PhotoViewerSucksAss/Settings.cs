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
	public class Settings
	{
		public static void Initialize(string applicationName)
		{
			Debug.Assert(_manager == null);
			_manager = new SettingsManager<Settings>(applicationName);
		}

		public static void Load()
		{
			Instance = GetManager().Load();
		}

		public static void Save()
		{
			// TODO save in background
			var tmp = Instance;
			Debug.Assert(tmp != null);
			GetManager().Save(tmp);
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
	}
}
