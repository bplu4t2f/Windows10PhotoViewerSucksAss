using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
			this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
			this.SetStyle(ControlStyles.StandardDoubleClick, false);

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
			if (availableFiles == null)
			{
				this.availableFiles = null;
			}
			else
			{
				this.availableFiles = availableFiles.Select(x => Path.GetFileName(x)).ToArray();
				this.scrollBar.Maximum = availableFiles.Count + this.scrollBar.LargeChange - 2;
			}
			this.scrollBar.Enabled = availableFiles != null;

			this.Invalidate();
		}

		public void SetDisplayIndex(int index, bool scrollSelectedItemIntoView)
		{
			this.selectedIndex = index;
			this.Invalidate();

			// Scroll control into center
			if (scrollSelectedItemIntoView)
			{
				int lineHeight = this.GetLineHeight();
				int linesOnScreen = this.Height / lineHeight;
				int scrollPos_ideal = index - linesOnScreen / 2;
				int scrollPos_actual = Math.Min(Math.Max(scrollPos_ideal, 0), this.availableFiles.Count);
				try
				{
					this.scrollBar.Value = scrollPos_actual;
				}
				catch
				{
					// I don't even
				}
			}
		}

		private Font GetSelectionFont()
		{
			return new Font(this.Font, FontStyle.Bold);
		}

		private int GetLineHeight()
		{
			int lineHeight = TextRenderer.MeasureText("w", this.Font).Height;
			if (lineHeight < 2)
			{
				// wtf
				lineHeight = 2;
			}
			return lineHeight;
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
			int lineHeight = this.GetLineHeight();
			int imageIndex = this.scrollBar.Value;
			for (int y = 0; y < this.Height && imageIndex < this.availableFiles.Count; ++imageIndex)
			{
				var file = this.availableFiles[imageIndex];
				Font font = imageIndex == this.selectedIndex ? this.GetSelectionFont() : this.Font;
				Rectangle textRect = new Rectangle(0, y, this.Width - this.scrollBar.Width, lineHeight);
				TextRenderer.DrawText(g, file, font, textRect, this.ForeColor, this.BackColor, flags);
				y += lineHeight;
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			bool rightClick;
			if (e.Button == MouseButtons.Left)
			{
				rightClick = false;
			}
			else if (e.Button == MouseButtons.Right)
			{
				rightClick = true;
			}
			else
			{
				return;
			}

			// Find the selected item
			var lineHeight = this.GetLineHeight();
			if (e.X > this.Width - this.scrollBar.Width)
			{
				return;
			}
			int clickedOffset = e.Y / lineHeight;
			int clickedIndex = this.scrollBar.Value + clickedOffset;
			this.ImageSelected?.Invoke(null, new ImageSelectionEventArgs(clickedIndex, rightClick, e.Location));
		}

		/// <summary>
		/// The reported index might be out of bounds.
		/// </summary>
		public event EventHandler<ImageSelectionEventArgs> ImageSelected;
	}


	public struct ImageSelectionEventArgs
	{
		public ImageSelectionEventArgs(int index, bool rightClick, Point clickLocation)
		{
			this.Index = index;
			this.RightClick = rightClick;
			this.ClickLocation = clickLocation;
		}

		public int Index { get; }
		public bool RightClick { get; }
		public Point ClickLocation { get; }
	}
}
