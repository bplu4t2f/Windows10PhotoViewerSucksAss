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

namespace Windows10PhotoViewerSucksAss
{
	class SettingsManager<TSettings>
		where TSettings : new()
	{
		public SettingsManager(string fullPath)
		{
			this.fullPath = fullPath;
			this.Settings = new TSettings();
		}

		private readonly string fullPath;

		public TSettings Settings { get; private set; }

		public void Load()
		{
			if (String.IsNullOrEmpty(this.fullPath))
			{
				goto __noexist;
			}

			try
			{
				int fileError = FileIO.Open(out FileStream stream, this.fullPath, FileAccess.Read, FileShare.Read, FileMode.Open);
				if (fileError != 0)
				{
					goto __noexist;
				}
				try
				{
					DataContractSerializer serializer = new DataContractSerializer(typeof(TSettings));
					var obj = (TSettings)serializer.ReadObject(stream);
					this.Settings = obj;
				}
				finally
				{
					stream.Dispose();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				goto __noexist;
			}


			return;

			__noexist:
			this.Settings = new TSettings();
		}

		public void Save()
		{
			var settings = this.Settings;
			if (String.IsNullOrEmpty(this.fullPath) || settings == null)
			{
				return;
			}

			try
			{
				string directoryPath = Path.GetDirectoryName(this.fullPath);
				Directory.CreateDirectory(directoryPath);
				var xmlWriterSettings = new XmlWriterSettings();
				xmlWriterSettings.Indent = true;
				xmlWriterSettings.NewLineHandling = NewLineHandling.Entitize;
				using (var stream = File.Create(this.fullPath))
				using (var writer = XmlWriter.Create(stream, xmlWriterSettings))
				{
					DataContractSerializer serializer = new DataContractSerializer(typeof(TSettings));
					serializer.WriteObject(writer, settings);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}
	}
}
