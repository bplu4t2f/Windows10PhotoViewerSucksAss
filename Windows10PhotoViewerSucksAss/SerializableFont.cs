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

		public FontDescriptor(Font f)
		{
			this.FontFamily = f.FontFamily.Name;
			this.Size = f.Size;
			this.Style = (int)f.Style;
		}

		public Font ToFont()
		{
			return new Font(this.FontFamily, this.Size, (FontStyle)this.Style);
		}
	}
}
