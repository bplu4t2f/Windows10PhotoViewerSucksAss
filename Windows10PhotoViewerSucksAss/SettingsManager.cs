using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

using TSettings = Windows10PhotoViewerSucksAss.Settings;

namespace Windows10PhotoViewerSucksAss
{
	class SettingsManager<T>
		where T : new()
	{
		public SettingsManager(string applicationName)
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			this.SettingsFilePath = Path.Combine(appData, $@"{applicationName}\settings.xml");
		}

		public string SettingsFilePath { get; }

		public T Load()
		{
			try
			{
				if (File.Exists(this.SettingsFilePath))
				{
					var ser = new XmlSerializer(typeof(T));
					using (var stream = File.OpenRead(this.SettingsFilePath))
					{
						return (T)ser.Deserialize(stream);
					}
				}
			}
			catch
			{
				// We don't actually care.
			}
			return new T();
		}

		public void Save(T value)
		{
			try
			{
				var dir = Path.GetDirectoryName(this.SettingsFilePath);
				Directory.CreateDirectory(dir);
				var ser = new XmlSerializer(typeof(T));
				using (var stream = File.Create(this.SettingsFilePath))
				{
					ser.Serialize(stream, value);
				}
			}
			catch
			{
				// We don't actually care.
			}
		}
	}
}
