using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	class DifferentButton : Control
	{
		public DifferentButton()
		{
			this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.CacheText | ControlStyles.UserMouse | ControlStyles.Opaque, true);
			this.SetStyle(ControlStyles.StandardDoubleClick | ControlStyles.Selectable | ControlStyles.StandardClick, false);
		}

		protected override Size DefaultSize => new Size(75, 23);

		private MouseButtons mouseDown;
		private bool mouseInside;

		protected override void OnTextChanged(EventArgs e)
		{
			base.OnTextChanged(e);
			this.Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			this.mouseInside = false;
			this.mouseDown = MouseButtons.None;
			this.Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			// Cancel previous button
			this.mouseDown = e.Button;
			this.Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			var oldMouseDown = this.mouseDown;
			if (this.mouseDown != MouseButtons.None)
			{
				this.mouseDown = MouseButtons.None;
				this.Invalidate();
			}
			if (e.Button == MouseButtons.Left && oldMouseDown == MouseButtons.Left && this.IsInside(e.Location))
			{
				// Click!
				// NOTE: Even order according to MSDN is MouseDown - Click - MouseClick - MouseUp
				// Similarly, for double click: MouseDown - Click - MouseClick - MouseUp - MouseDown - DoubleClick - MouseDoubleClick - MouseUp
				// Background: Click and DoubleClick are more like abstract "Activate" or "Confirm" events that can also be raised from keyboard or other events.
				this.OnClick(e);
				this.OnMouseClick(e);
			}
			base.OnMouseUp(e);
		}

		private bool IsInside(Point location)
		{
			return location.X >= 0 && location.X < this.Width && location.Y >= 0 && location.Y < this.Height;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			bool nowInside = this.IsInside(e.Location);
			if (nowInside != this.mouseInside)
			{
				this.mouseInside = nowInside;
				this.Invalidate();
			}
			if ((e.Button & this.mouseDown) != this.mouseDown)
			{
				// "Cancel" "capture"
				this.mouseDown = MouseButtons.None;
				this.Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

			Color back;
			Color fore;
			if (this.mouseDown == MouseButtons.Left && this.mouseInside)
			{
				// Super hot
				var b = this.BackColor;
				var f = this.ForeColor;
				if (b.GetBrightness() > f.GetBrightness())
				{
					// Back color is lighter than fore color ("light mode")
					// In this case, blend back color towards white, and fore color towards black.
					back = BlendColors(this.BackColor, Color.White, 0.5f);
					fore = BlendColors(this.ForeColor, Color.Black, 0.7f);
				}
				else
				{
					// Back color is darker than fore color ("dark mode")
					// Swap colors
					back = this.ForeColor;
					fore = this.BackColor;
				}
			}
			else if (this.mouseDown != MouseButtons.None || this.mouseInside)
			{
				// Luke warm
				back = BlendColors(this.BackColor, this.ForeColor, 0.4f);
				fore = BlendColors(this.ForeColor, this.BackColor, -0.2f);
			}
			else
			{
				// Cold
				back = BlendColors(this.BackColor, this.ForeColor, 0.25f);
				fore = this.ForeColor;
			}

			using (var backBrush = new SolidBrush(back))
			{
				var rect = new Rectangle(0, 0, this.Width, this.Height);
				e.Graphics.FillRectangle(backBrush, rect);
				if (!string.IsNullOrWhiteSpace(this.Text))
				{
					TextRenderer.DrawText(e.Graphics, this.Text, this.Font, rect, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
				}
			}
		}

		private static Color BlendColors(Color from, Color to, float ratio)
		{
			return Color.FromArgb(BlendOne(from.R, to.R, ratio), BlendOne(from.G, to.G, ratio), BlendOne(from.B, to.B, ratio));
		}

		private static int BlendOne(int a, int b, float ratio)
		{
			return Math.Min(Math.Max((int)Math.Round(a + (b - a) * ratio), 0), 255);
		}
	}
}
