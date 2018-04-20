using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Windows10PhotoViewerSucksAss
{
	public partial class OverviewControl : UserControl
	{
		public OverviewControl()
		{
			InitializeComponent();

			this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);

			this.scrollBar.Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
			this.scrollBar.Location = new Point(this.Width - this.scrollBar.Width, 0);
			this.scrollBar.Height = this.Height;
			this.scrollBar.SmallChange = 1;
			this.scrollBar.LargeChange = 10;
			this.scrollBar.ValueChanged += this.ScrollBar_ValueChanged;
			this.Controls.Add(this.scrollBar);
		}

		private readonly VScrollBar scrollBar = new VScrollBar();
		private IList<string> availableFiles;
		private int selectedIndex = -1;

		public void Initialize(IList<string> availableFiles)
		{
			this.availableFiles = availableFiles.Select(x => Path.GetFileName(x)).ToArray();
			if (availableFiles == null)
			{
				return;
			}
			this.scrollBar.Maximum = availableFiles.Count + this.scrollBar.LargeChange - 2;

			this.Invalidate();
		}

		public void SetDisplayIndex(int index)
		{
			this.selectedIndex = index;
			this.Invalidate();

			// Scroll control into center
			var lineHeight = TextRenderer.MeasureText("w", this.Font).Height;
			if (lineHeight <= 0)
			{
				// wtf
				return;
			}
			var linesOnScreen = this.Height / lineHeight;
			var scrollPos_ideal = index - linesOnScreen / 2;
			var scrollPos_actual = Math.Min(Math.Max(scrollPos_ideal, 0), this.availableFiles.Count);
			try
			{
				this.scrollBar.Value = scrollPos_actual;
			}
			catch
			{
				// I don't even
			}
		}

		private Font GetSelectionFont()
		{
			return new Font(this.Font, FontStyle.Bold);
		}

		private void ScrollBar_ValueChanged(object sender, EventArgs e)
		{
			this.Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;

			if (this.availableFiles == null)
			{
				return;
			}

			TextFormatFlags flags = TextFormatFlags.EndEllipsis;
			var lineHeight = TextRenderer.MeasureText(g, "w", this.Font).Height;
			var imageIndex = this.scrollBar.Value;
			int y = 0;
			while (y < this.Height && imageIndex < this.availableFiles.Count)
			{
				var file = this.availableFiles[imageIndex];
				Font font = imageIndex == this.selectedIndex ? this.GetSelectionFont() : this.Font;
				Rectangle textRect = new Rectangle(0, y, this.Width - this.scrollBar.Width, lineHeight);
				TextRenderer.DrawText(g, file, font, textRect, this.ForeColor, this.BackColor, flags);
				y += lineHeight;
				++imageIndex;
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			// Find the selected item
			var lineHeight = TextRenderer.MeasureText("w", this.Font).Height;
			if (lineHeight <= 0)
			{
				// wtf
				return;
			}
			if (e.X > this.Width - this.scrollBar.Width)
			{
				return;
			}
			int clickedOffset = e.Y / lineHeight;
			int clickedIndex = this.scrollBar.Value + clickedOffset;
			this.ImageSelected?.Invoke(null, clickedIndex);
		}

		public event EventHandler<int> ImageSelected;
	}
}
