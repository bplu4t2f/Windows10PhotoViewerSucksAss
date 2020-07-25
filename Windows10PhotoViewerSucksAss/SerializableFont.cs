using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	/// <summary>
	/// Font descriptor, that can be xml-serialized
	/// </summary>
	public class FontDescriptor
	{
		public string FontFamily { get; set; }
		public float Size { get; set; }
		public int Style { get; set; }

		/// <summary>
		/// Intended for xml serialization purposes only
		/// </summary>
		private FontDescriptor() { }

		public static FontDescriptor FromFont(Font f)
		{
			if (f == null) return null;
			var descriptor = new FontDescriptor();
			descriptor.FontFamily = f.FontFamily.Name;
			descriptor.Size = f.Size;
			descriptor.Style = (int)f.Style;
			return descriptor;
		}

		public Font ToFont()
		{
			return new Font(this.FontFamily, this.Size, (FontStyle)this.Style);
		}
	}
}
