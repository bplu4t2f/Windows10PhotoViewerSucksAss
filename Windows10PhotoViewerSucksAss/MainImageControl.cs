using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace Windows10PhotoViewerSucksAss
{
	public class MainImageControl : Control
	{
		public MainImageControl()
		{
			this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
			this.animationTimer.Tick += this.HandleAnimationTimerTick;
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

		private readonly Matrix transform = new Matrix();
		private bool panning;
		private bool zooming;
		private bool zoomToFitEnabled;
		private Point actionStartPosition;
		private Point actionLastIterationPosition;

		public Action BeginDragHandler { get; set; }

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (Control.ModifierKeys.HasFlag(Keys.Control))
			{
				// Begin drag drop operation.
				this.BeginDragHandler?.Invoke();
				return;
			}

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
			this.ZoomOriginalSize();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (this.panning || this.zooming)
			{
				this.panning = false;
				this.zooming = false;
				this.Invalidate();
			}
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

		public void ZoomToFit()
		{
			this.transform.Reset();
			this.zoomToFitEnabled = true;
			var image = this.Image;
			if (image != null)
			{
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

		public void ZoomOriginalSize()
		{
			this.zoomToFitEnabled = false;
			var image = this.Image;
			if (image != null)
			{
				this.transform.Reset();
				this.transform.Translate(-image.Width / 2, -image.Height / 2);
			}
			this.Invalidate();
		}

		private struct AnimationInfo
		{
			public AnimationInfo(Image image, bool animated = false, int frameCount = 0, int[] frameDelayValues_10ms = null)
			{
				this.Image = image;
				this.Animated = animated;
				this.FrameCount = frameCount;
				this.FrameDelayValues_10ms = frameDelayValues_10ms;
			}

			public Image Image { get; }
			public bool Animated { get; }
			public int FrameCount { get; }
			public int[] FrameDelayValues_10ms { get; }
		}

		private static unsafe AnimationInfo GetAnimationinfo(Image image)
		{
			const int PropertyTagFrameDelay = 0x5100;

			if (!image.FrameDimensionsList.Any(x => x.Equals(FrameDimension.Time.Guid)))
			{
				// No time dimension -> not animated.
				return new AnimationInfo(image);
			}
			int frameCount = image.GetFrameCount(FrameDimension.Time);
			var propertyItem = image.GetPropertyItem(PropertyTagFrameDelay);
			if (propertyItem.Value.Length != frameCount * 4)
			{
				Debug.WriteLine($"Bogus property item length {propertyItem.Value.Length} · frame count {frameCount}");
				return new AnimationInfo(image);
			}

			int[] frameDelayValues_10ms = new int[frameCount];
			fixed (byte* src = propertyItem.Value)
			{
				int* intsrc = (int*)src;
				for (int i = 0; i < frameCount; ++i)
				{
					frameDelayValues_10ms[i] = intsrc[i];
				}
			}

			return new AnimationInfo(image, true, frameCount, frameDelayValues_10ms);
		}

		private AnimationInfo lastAnimationInfo;
		private int currentAnimationFrame;
		private readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
		private bool timeBeginPeriodCalled;

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			g.Clear(this.BackColor);

			var image = this.Image;
			if (image == null)
			{
				return;
			}

			if (image != this.lastAnimationInfo.Image)
			{
				this.lastAnimationInfo = GetAnimationinfo(image);
				this.currentAnimationFrame = 0;
				if (this.lastAnimationInfo.FrameCount > 1)
				{
					int frameDelay_10ms = this.GetFrameDelayValueSafe(0);
					this.animationTimer.Stop();
					this.animationTimer.Interval = frameDelay_10ms * 10;
					this.animationTimer.Start();
					if (!this.timeBeginPeriodCalled)
					{
						TimeBeginPeriod.timeBeginPeriod(1);
						this.timeBeginPeriodCalled = true;
					}
				}
				else
				{
					this.animationTimer.Stop();
					if (this.timeBeginPeriodCalled)
					{
						TimeBeginPeriod.timeEndPeriod(1);
						this.timeBeginPeriodCalled = false;
					}
				}
			}

			if (this.lastAnimationInfo.Animated)
			{
				image.SelectActiveFrame(FrameDimension.Time, this.currentAnimationFrame);
			}

			var transform = this.transform;
			// If we're zoomed in somewhat, disable interpolation because it looks worse.
			// This is only correct because we don't rotate, mirror, or shear.
			if (transform.Elements[0] > 2.8f)
			{
				g.InterpolationMode = InterpolationMode.NearestNeighbor;
			}
			else
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			}
			g.SmoothingMode = SmoothingMode.None;
			g.PixelOffsetMode = PixelOffsetMode.Half;

			g.TranslateTransform(this.Width / 2, this.Height / 2);
			g.MultiplyTransform(this.transform);

			g.DrawImage(image, 0, 0, image.Width, image.Height);
			if (this.zooming || this.panning)
			{
				g.ResetTransform();
				g.TranslateTransform(this.actionStartPosition.X, this.actionStartPosition.Y);
				g.DrawRectangle(Pens.Black, -9.5f, -9.5f, 20.0f, 20.0f);
				g.ResetTransform();
				g.TranslateTransform(this.actionLastIterationPosition.X, this.actionLastIterationPosition.Y);
				DrawCross(g, Pens.Black, 5);
			}

			base.OnPaint(e);
		}

		private void HandleAnimationTimerTick(object sender, EventArgs e)
		{
			if (this.lastAnimationInfo.FrameCount <= 1)
			{
				Debug.WriteLine("Stray timer tick event.");
				return;
			}

			this.currentAnimationFrame += 1;
			if (this.currentAnimationFrame >= this.lastAnimationInfo.FrameCount)
			{
				this.currentAnimationFrame = 0;
			}

			int currentFrameDelay_10ms = 1;
			if (this.currentAnimationFrame < this.lastAnimationInfo.FrameDelayValues_10ms.Length)
			{
				currentFrameDelay_10ms = this.GetFrameDelayValueSafe(this.currentAnimationFrame);
			}
			else
			{
				Debug.WriteLine($"Buggy animation state: current {this.currentAnimationFrame} - frame count {this.lastAnimationInfo.FrameCount} - fallback to {currentFrameDelay_10ms}");
			}

			// NOTE: In order to do this correctly, whenever we change the timer, we would have to clear any pending callbacks.
			//       If the timer is set to very fast, and a second callback is already underway while we're still here, then that callback would otherwise get
			//       executed even though we didn't want it.
			//       This also applies to the initial timer set call (if the timer was already running).
			//       I can't really find a way to get this working with absolute certainty without OS level support. So I'll just have to assume that the timer
			//       already handles that correctly.
			// NOTE: The setter of Interval only does something if the value actually changes.
			this.animationTimer.Interval = currentFrameDelay_10ms * 10;

			this.Invalidate();
		}

		private int GetFrameDelayValueSafe(int frame)
		{
			// Apparently there are gifs where the animation frame delay value is zero.
			// In that case a commonly appcompat behavior seems to be to implicitly set it to 100 ms (which is value 10).
			int value = this.lastAnimationInfo.FrameDelayValues_10ms[frame];
			if (value < 1)
			{
				value = 10;
			}
			return value;
		}

		private static void DrawCross(Graphics g, Pen pen, int size)
		{
			g.DrawLine(pen, 0.5f, -size + 0.5f, 0.5f, 0.5f);
			g.DrawLine(pen, 0.5f, size + 0.5f, 0.5f, 0.5f);
			g.DrawLine(pen, -size + 0.5f, 0.5f, 0.5f, 0.5f);
			g.DrawLine(pen, size + 0.5f, 0.5f, 0.5f, 0.5f);
		}

		public void ZoomAtLocation(Point center, float factor)
		{
			this.zoomToFitEnabled = false;
			this.transform.Translate(-center.X + this.Width / 2, -center.Y + this.Height / 2, MatrixOrder.Append);
			this.transform.Scale(factor, factor, MatrixOrder.Append);
			this.transform.Translate(center.X - this.Width / 2, center.Y - this.Height / 2, MatrixOrder.Append);
			this.Invalidate();
		}
	}
}
