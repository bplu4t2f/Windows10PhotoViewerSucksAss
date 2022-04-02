using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
			try
			{
				using (var stream = FileIO.Open(out int error, FilePath, FileAccess.Read, FileShare.Read, FileMode.Open))
				{
					if (stream == null) return null;
					var doc = new XmlDocument();
					doc.Load(stream);
					var root = XmlDocumentHelper.GetDocumentRootNode(doc);
					var data = new BootstrapData();
					XmlDocumentHelper.AutoDeserializeSimpleObject(data, root);
					Debug.WriteLine("Bootstrap data loaded.");
					return data;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error loading bootstrap data.\r\n\r\n{ex}");
				return null;
			}
		}
	}
}
