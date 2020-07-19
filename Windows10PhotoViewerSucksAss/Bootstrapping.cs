using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Windows10PhotoViewerSucksAss
{
	public static class Bootstrapping
	{
		/// <summary>
		/// Pass full path to the bootstrap xml file.
		/// Returns null if the file doesn't exist.
		/// </summary>
		public static BootstrapData Load(string FilePath)
		{
			if (!File.Exists(FilePath))
			{
				Debug.WriteLine("Bootstrap file doesn't exist.");
				return null;
			}

			try
			{
				XmlSerializer Serializer = new XmlSerializer(typeof(BootstrapData));
				using (var Stream = File.OpenRead(FilePath))
				{
					var Data = (BootstrapData)Serializer.Deserialize(Stream);
					Debug.WriteLine("Bootstrap data loaded successfully.");
					return Data;
				}
			}
			catch (FileNotFoundException)
			{
				Debug.WriteLine("Bootstrap existed at some point but then not.");
				return null;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error loading bootstrap data.\r\n\r\n{ex}");
				return null;
			}
		}
	}
}
