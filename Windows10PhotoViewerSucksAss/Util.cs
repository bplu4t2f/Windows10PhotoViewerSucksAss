using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	static class Util
	{
		public static Image LoadImageFromFile(string file)
		{
			// Must do it this way, with Image.FromFile, or with the Bitmap(string) constructor, the file stays locked.
			// Bitmap(Stream) says we must leave the stream open.
			using (var tmpImage = Image.FromFile(file))
			{
				return new Bitmap(tmpImage);
			}
		}

		public static void ClipboardCutFileList(string[] files)
		{
			// Test code:
			//IDataObject test = Clipboard.GetDataObject();
			//string[] formats = test.GetFormats();
			//List<object> objects = new List<object>();
			//foreach (var format in formats)
			//{
			//	object tmp = test.GetData(format);
			//	if (tmp is MemoryStream ms)
			//	{
			//		objects.Add(ms.ToArray());
			//	}
			//	else
			//	{
			//		objects.Add(tmp);
			//	}
			//}
			// As you can find out with the test code, it seems IDataObject is basically a Dictionary from
			// string to object.
			// To move files, we apparently need to set the "FileDrop" entry to a string array
			// containing the files, and the "Preferred DropEffect" entry to a memory stream
			// containing the bytes { 2, 0, 0, 0 }.

			byte[] moveEffect = new byte[] { 5, 0, 0, 0 };
			MemoryStream dropEffect = new MemoryStream();
			dropEffect.Write(moveEffect, 0, moveEffect.Length);
			dropEffect.Position = 0;

			var dataObject = new DataObject();
			dataObject.SetData("FileDrop", files);
			dataObject.SetData("Preferred DropEffect", dropEffect);

			Clipboard.Clear();
			Clipboard.SetDataObject(dataObject, true);
		}
	}
}
