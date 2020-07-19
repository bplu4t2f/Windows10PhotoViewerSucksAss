using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	class SplitterControl : Control
	{
		public SplitterControl()
		{
			this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
			this.SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
			this.Cursor = Cursors.SizeWE;
		}

		public List<Control> LeftControls { get; } = new List<Control>();
		public List<Control> RightControls { get; } = new List<Control>();

		public event EventHandler DragStopped;

		private bool dragging;
		private Point dragStart;

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			this.dragging = true;
			this.dragStart = e.Location;
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			this.dragging = false;
			base.OnMouseUp(e);
			this.DragStopped?.Invoke(this, e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (this.dragging)
			{
				int dx = e.X - this.dragStart.X;
				this.MoveBy(dx);
			}
		}

		public void MoveBy(int dx)
		{
			if (this.Parent != null)
			{
				// Make sure the splitter doesn't go outside of its parent.
				int theoretical_left = this.Left + dx;
				int real_left = Math.Min(Math.Max(theoretical_left, 0), this.Parent.ClientSize.Width - this.Width);
				int required_correction = real_left - theoretical_left;
				dx += required_correction;
			}

			this.Left += dx;
			foreach (var c in this.LeftControls)
			{
				c.Width += dx;
			}
			foreach (var c in this.RightControls)
			{
				c.Left += dx;
				c.Width -= dx;
			}
		}

		public void MoveLeftEdgeTo(int targetX)
		{
			var dx = targetX - this.Left;
			this.Left = targetX;
			this.MoveBy(dx);
		}

		/// <summary>
		/// Changes <see cref="Control.Width"/> while also moving the <see cref="RightControls"/>.
		/// </summary>
		public void ChangeWidth(int targetWidth)
		{
			int dx = targetWidth - this.Width;
			this.Width += dx;
			foreach (var c in this.RightControls)
			{
				c.Left += dx;
				c.Width -= dx;
			}
		}
	}
}
