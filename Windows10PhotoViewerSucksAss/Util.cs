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
			var memoryStream = new MemoryStream();
#warning TODO use a better file stream
			using (FileStream fileStream = File.OpenRead(file))
			{
				fileStream.CopyTo(memoryStream);
			}
			Image tmpImage = Image.FromStream(memoryStream);
			return tmpImage;
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

			byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
			MemoryStream dropEffect = new MemoryStream();
			dropEffect.Write(moveEffect, 0, moveEffect.Length);
			dropEffect.Position = 0;

			var dataObject = new DataObject();
			dataObject.SetData("FileDrop", files);
			dataObject.SetData("Preferred DropEffect", dropEffect);

			Clipboard.Clear();
			Clipboard.SetDataObject(dataObject, true);
		}

		public static RectangleF ResizeProportionalFit(Size container, Size content)
		{
			float scale_w = (float)container.Width / content.Width;
			float scale_h = (float)container.Height / content.Height;
			float scale = Math.Min(scale_w, scale_h);
			float new_w = scale * content.Width;
			float new_h = scale * content.Height;
			float x = (container.Width - new_w) / 2.0f;
			float y = (container.Height - new_h) / 2.0f;
			float bad_x = (float)(x - Math.Truncate(x));
			float bad_y = (float)(y - Math.Truncate(y));
			x -= bad_x;
			y -= bad_y;
			new_w -= bad_x;
			new_h -= bad_y;
			return new RectangleF(x, y, new_w, new_h);
		}
	}
}
