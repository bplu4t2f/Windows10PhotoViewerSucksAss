using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
	}
}
