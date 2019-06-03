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
		private IList<OverviewFileListEntry> availableFiles;
		private int selectedIndex = -1;

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
				this.scrollBar.Maximum = availableFiles.Count + this.scrollBar.LargeChange - 2;
			}
			this.scrollBar.Enabled = availableFiles != null;

			this.Invalidate();
		}

		/// <summary>
		/// Positive is down, negative is up.
		/// </summary>
		public void ScrollList(int amount)
		{
			this.scrollBar.Value = Math.Min(Math.Max(this.scrollBar.Value + amount, 0), this.scrollBar.Maximum - this.scrollBar.LargeChange + 1);
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
				OverviewFileListEntry file = this.availableFiles[imageIndex];
				Font font = imageIndex == this.selectedIndex ? this.GetSelectionFont() : this.Font;
				Rectangle textRect = new Rectangle(0, y, this.Width - this.scrollBar.Width, lineHeight);
				Color textColor = GetFileStatusColor(file.fileListEntry.LastFileStatus);
				TextRenderer.DrawText(g, file.fileName, font, textRect, textColor, this.BackColor, flags);
				y += lineHeight;
			}
		}

		private static Color GetFileStatusColor(LastFileStatus status)
		{
			switch (status)
			{
				case LastFileStatus.Unknown:
				case LastFileStatus.OK:
					return Color.Black;
				case LastFileStatus.Error:
				default:
					return Color.Firebrick;
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
