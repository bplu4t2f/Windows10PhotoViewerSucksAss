using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace Windows10PhotoViewerSucksAss
{
	public partial class OverviewControl : Control
	{
		public OverviewControl()
		{
			this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
			this.SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
			this.scrollBar.InvalidateCallback = this.Invalidate;
		}

		protected override Size DefaultSize => new Size(225, 150);

		private readonly EmbedScrollBar scrollBar = new EmbedScrollBar();
		private IList<OverviewFileListEntry> availableFiles;
		private int selectedIndex = -1;

		private Color _foreColorError = Color.Firebrick;
		[DefaultValue(typeof(Color), nameof(Color.Firebrick))]
		public Color ForeColorError
		{
			get { return this._foreColorError; }
			set
			{
				this._foreColorError = value;
				this.Invalidate();
			}
		}

		private sealed class OverviewFileListEntry
		{
			public OverviewFileListEntry(FileListEntry fileListEntry)
			{
				Debug.Assert(fileListEntry != null);
				this.fileListEntry = fileListEntry;
				this.fileName = Path.GetFileName(this.fileListEntry.FullPath);
			}

			public readonly FileListEntry fileListEntry;
			public readonly string fileName;
		}

		public void Initialize(IList<FileListEntry> availableFiles)
		{
			if (availableFiles == null)
			{
				this.availableFiles = null;
			}
			else
			{
				this.availableFiles = availableFiles.Select(x => new OverviewFileListEntry(x)).ToArray();
			}

			this.Invalidate();
		}

		/// <summary>
		/// Positive is down, negative is up.
		/// </summary>
		public void ScrollList(int amount)
		{
			var maximum = this.availableFiles?.Count ?? 0;
			this.scrollBar.ScrollValue = Math.Min(Math.Max(this.scrollBar.ScrollValue + amount, 0), maximum);
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
				this.scrollBar.ScrollValue = scrollPos_actual;
			}
		}

		private Font GetSelectionFont()
		{
			return new Font(this.Font, FontStyle.Bold);
		}

		private int GetLineHeight()
		{
			// NOTE: Do NOT replace this with Font.Height. Font.Height sucks and is inaccurate.
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
			var g = e.Graphics;
			g.Clear(this.BackColor);

			if (this.availableFiles == null)
			{
				return;
			}

			var scrollBarWidth = SystemInformation.VerticalScrollBarWidth;

			TextFormatFlags flags = TextFormatFlags.EndEllipsis;
			int lineHeight = this.GetLineHeight();
			int imageIndex = this.scrollBar.ScrollValue;
			for (int y = 0; y < this.Height && imageIndex < this.availableFiles.Count; ++imageIndex)
			{
				OverviewFileListEntry file = this.availableFiles[imageIndex];
				Font font = imageIndex == this.selectedIndex ? this.GetSelectionFont() : this.Font;
				Rectangle textRect = new Rectangle(0, y, this.Width - scrollBarWidth, lineHeight);
				Color textColor = this.GetFileStatusColor(file.fileListEntry.LastFileStatus);
				TextRenderer.DrawText(g, file.fileName, font, textRect, textColor, this.BackColor, flags);
				y += lineHeight;
			}


			int numFilesThatFitOnScreen = (this.Height + lineHeight - 1) / lineHeight;

			var scrollBarLayoutInfo = new EmbedScrollBar.LayoutInfo(
				Bounds: new Rectangle(this.Width - scrollBarWidth, 0, scrollBarWidth, this.Height),
				SmallChange: 1,
				LargeChange: 10,
				ViewportSize: numFilesThatFitOnScreen,
				TotalScrollableDistance: Math.Max(0, this.availableFiles.Count - 1)
				);
			this.scrollBar.Paint(g, ref scrollBarLayoutInfo, EmbedScrollBar.ScrollBarArrowButtonStyle.FlatBorderless);

			base.OnPaint(e);
		}

		private Color GetFileStatusColor(LastFileStatus status)
		{
			switch (status)
			{
				case LastFileStatus.Unknown:
				case LastFileStatus.OK:
					return this.ForeColor;
				case LastFileStatus.Error:
				default:
					return this.ForeColorError;
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (this.scrollBar.HandleMouseDown(e))
			{
				return;
			}

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
			int clickedOffset = e.Y / lineHeight;
			int clickedIndex = this.scrollBar.ScrollValue + clickedOffset;
			this.ImageSelected?.Invoke(null, new ImageSelectionEventArgs(clickedIndex, rightClick, e.Location));
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (this.scrollBar.HandleMouseUp(e))
			{
				return;
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			this.scrollBar.HandleMouseLeave();
		}

		// NOTE: Not handling mouse wheel event in this application.

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (this.scrollBar.HandleMouseMove(e))
			{
				return;
			}
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
