using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace Windows10PhotoViewerSucksAss
{
	public partial class MainImageControl : Control
	{
		public MainImageControl()
		{
			this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		}

		private Image _image;

		public Image Image
		{
			get { return this._image; }
			set
			{
				this._image = value;
				this.ZoomToFit();
			}
		}

		private Matrix transform = new Matrix();
		private bool panning;
		private bool zooming;
		private bool zoomToFitEnabled;
		private Point actionStartPosition;
		private Point actionLastIterationPosition;

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			this.actionStartPosition = e.Location;
			this.actionLastIterationPosition = e.Location;
			if (e.Button == MouseButtons.Left)
			{
				this.panning = true;
			}
			else if (e.Button == MouseButtons.Right)
			{
				this.zooming = true;
			}
		}

		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			base.OnMouseDoubleClick(e);
			this.zoomToFitEnabled = false;
			var image = this.Image;
			if (image != null)
			{
				this.transform.Reset();
				this.transform.Translate(-image.Width / 2, -image.Height / 2);
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			this.panning = false;
			this.zooming = false;
			this.Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (this.panning)
			{
				this.zoomToFitEnabled = false;
				var movementX = e.X - this.actionLastIterationPosition.X;
				var movementY = e.Y - this.actionLastIterationPosition.Y;
				this.transform.Translate(movementX, movementY, MatrixOrder.Append);
				this.actionLastIterationPosition = e.Location;
				this.Invalidate();
			}
			else if (this.zooming)
			{
				this.zoomToFitEnabled = false;
				double dx = e.X - this.actionLastIterationPosition.X - e.Y + this.actionLastIterationPosition.Y;
				this.actionLastIterationPosition = e.Location;
				float scaling = (float)Math.Pow(2.0, dx / 100.0);
				this.transform.Translate(-this.actionStartPosition.X + this.Width / 2, -this.actionStartPosition.Y + this.Height / 2, MatrixOrder.Append);
				this.transform.Scale(scaling, scaling, MatrixOrder.Append);
				this.transform.Translate(this.actionStartPosition.X - this.Width / 2, this.actionStartPosition.Y - this.Height / 2, MatrixOrder.Append);
				this.Invalidate();
			}
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			if (this.zoomToFitEnabled)
			{
				this.ZoomToFit();
			}
			this.Invalidate();
		}

		private void ZoomToFit()
		{
			this.transform.Reset();
			this.zoomToFitEnabled = true;
			var image = this.Image;
			if (image != null)
			{
				// TODO make this a function; persist zoom-to-fit mode until manual panning or zooming
				this.transform.Translate(-image.Width / 2, -image.Height / 2);
				if (image.Height > this.Height || image.Width > this.Width)
				{
					var ratioH = (float)this.Height / (float)image.Height;
					var ratioW = (float)this.Width / (float)image.Width;
					var min = Math.Min(ratioH, ratioW);
					this.transform.Scale(min, min, MatrixOrder.Append);
				}
			}
			this.Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.HighQuality;

			g.Clear(Color.DarkCyan);
			var image = this.Image;
			if (image == null)
			{
				return;
			}

			g.Transform = this.transform;
			g.TranslateTransform(this.Width / 2, this.Height / 2, MatrixOrder.Append);
			g.DrawImage(image, 0, 0, image.Width, image.Height);
			if (this.zooming || this.panning)
			{
				g.ResetTransform();
				g.TranslateTransform(this.actionStartPosition.X, this.actionStartPosition.Y);
				g.DrawRectangle(Pens.Black, -10, -10, 20, 20);
				g.ResetTransform();
				g.TranslateTransform(this.actionLastIterationPosition.X, this.actionLastIterationPosition.Y);
				DrawCross(g, Pens.Black, 5);
			}
		}

		private static void DrawCross(Graphics g, Pen pen, int size)
		{
			g.DrawLine(pen, 0, -size, 0, 0);
			g.DrawLine(pen, 0, size, 0, 0);
			g.DrawLine(pen, -size, 0, 0, 0);
			g.DrawLine(pen, size, 0, 0, 0);
		}
	}
}
