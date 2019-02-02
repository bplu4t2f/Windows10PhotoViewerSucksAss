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
	class SettingsManager
	{
		public SettingsManager(string fullPath)
		{
			this.fullPath = fullPath;
		}

		private readonly string fullPath;

		private TSettings Settings
		{
			get { return TSettings.Instance; }
			set { TSettings.Instance = value; }
		}

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
