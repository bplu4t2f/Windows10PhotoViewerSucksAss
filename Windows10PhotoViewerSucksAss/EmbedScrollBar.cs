using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CatalogExh
{
	/// <summary>
	/// Scroll bar that is intended to be embedded in another custom-drawn <see cref="Control"/> without technically being an additional control. Saves window handle
	/// and all sorts of control processing overhead.
	/// You need to call all public Handle... methods from their corresponding events in their parent control. Also <see cref="Paint"/> and <see cref="InvalidateCallback"/>.
	/// </summary>
	class EmbedScrollBar
	{
		public EmbedScrollBar()
		{
			this.Timer.Tick += this.HandleTimerTickEvent;
		}

		/// <summary>
		/// We'll put the timer in this class because even though we sort of take over the wheel from the application here, this is just way more convenient.
		/// In practice, the containing control would require an extra timer just for this scroll bar anyway.
		/// </summary>
		private readonly System.Windows.Forms.Timer Timer = new System.Windows.Forms.Timer();

		/// <summary>
		/// This is the current scroll value that is used by the input handling and drawing methods. If this is changed programmatically, you need to manually
		/// trigger a repaint.
		/// </summary>
		public int ScrollValue { get; set; }

		/// <summary>
		/// Only called when something changes through user interaction (mouse clicks). Callback should invalidate to trigger a repaint.
		/// If this is not handled properly, the visuals won't update.
		/// </summary>
		public Action InvalidateCallback { get; set; }

		public struct LayoutInfo
		{
			public readonly Rectangle Bounds;
			public readonly int SmallChange;
			public readonly int LargeChange;
			public readonly int ViewportHeight;
			public readonly int TotalScrollableHeight;

			public LayoutInfo(Rectangle Bounds, int SmallChange, int LargeChange, int ViewportHeight, int TotalScrollableHeight)
			{
				this.Bounds = Bounds;
				this.SmallChange = SmallChange;
				this.LargeChange = LargeChange;
				this.ViewportHeight = ViewportHeight;
				this.TotalScrollableHeight = TotalScrollableHeight;
			}
		}

		private enum ScrollBarElement
		{
			None,
			TopButton,
			TrackAboveThumb,
			Thumb,
			TrackBelowThumb,
			BottomButton
		}

		/// <summary>
		/// It doesn't matter whether it's in screen coordinates or control coordinates as long as both <paramref name="Bounds"/> and <paramref name="Position"/>
		/// are in the same reference frame.
		/// </summary>
		private static ScrollBarElement GetElementAtPosition(Rectangle Bounds, ref ControlMetrics Metrics, Point Position)
		{
			if (!Bounds.Contains(Position)) return ScrollBarElement.None;
			int y = Position.Y - Bounds.Top;
			if (y < 0) return ScrollBarElement.None;
			if (y < Metrics.TrackStart) return ScrollBarElement.TopButton;
			if (y < Metrics.ThumbStart) return ScrollBarElement.TrackAboveThumb;
			if (y < Metrics.ThumbStart + Metrics.ThumbSize) return ScrollBarElement.Thumb;
			if (y < Metrics.BottomButtonStart) return ScrollBarElement.TrackBelowThumb;
			if (y < Bounds.Height) return ScrollBarElement.BottomButton;
			return ScrollBarElement.None;
		}

		private struct ControlMetrics
		{
			public int TrackStart;
			public int ThumbStart;
			public int ThumbSize;
			public int BottomButtonStart;
			public int TrackSpaceAvailableForThumbMovement;
		}

		/// <summary>
		/// Calculates where the things in the scroll bar are (in pixels).
		/// </summary>
		private static ControlMetrics GetControlMetrics(Size Size, int ViewportHeight, int TotalScrollableHeight, int ScrollValue)
		{
			var Metrics = new ControlMetrics();
			int ButtonHeight = Size.Width;
			Metrics.TrackStart = ButtonHeight;
			Metrics.BottomButtonStart = Size.Height - ButtonHeight;

			float ThumbRatio;
			if (ViewportHeight == 0)
			{
				ThumbRatio = 0;
			}
			else
			{
				ThumbRatio = (float)ViewportHeight / (TotalScrollableHeight + ViewportHeight);
			}

			int MinimumThumbSize = Size.Width;

			int TrackSize = Math.Max(0, Metrics.BottomButtonStart - Metrics.TrackStart);
			Metrics.ThumbSize = Math.Min(Math.Max(MinimumThumbSize, (int)(TrackSize * ThumbRatio)), TrackSize); // Size of thumb in pixels, proportional to ViewportHeight / TotalScrollableHeight
			Metrics.TrackSpaceAvailableForThumbMovement = TrackSize - Metrics.ThumbSize;
			int ThumbPosRelativeToTrackStart;
			if (TotalScrollableHeight == 0)
			{
				ThumbPosRelativeToTrackStart = 0;
			}
			else
			{
				ThumbPosRelativeToTrackStart = (int)((float)ScrollValue / TotalScrollableHeight * Metrics.TrackSpaceAvailableForThumbMovement);
			}
			Metrics.ThumbStart = ThumbPosRelativeToTrackStart + Metrics.TrackStart;

			return Metrics;
		}

		private ControlMetrics GetControlMetrics()
		{
			return GetControlMetrics(this.LastLayoutInfo.Bounds.Size, this.LastLayoutInfo.ViewportHeight, this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue);
		}

		private bool SetScrollValue(int TotalScrollableHeight, int Value)
		{
			var NewValue = Math.Min(Math.Max(Value, 0), TotalScrollableHeight);
			if (NewValue != this.ScrollValue)
			{
				this.ScrollValue = NewValue;
				this.InvalidateCallback?.Invoke();
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Set in MouseDown, cleared in MouseUp/MouseLeave, required in MouseMove to detect when a mouse button was released while the window didn't have focus.
		/// </summary>
		private MouseButtons LastMouseButton;
		/// <summary>
		/// Need this for a similar reason as why we can't just use the screen mouse coordinates to determine the hovered item in <see cref="Paint"/>.
		/// In the timer tick event, we need the mouse position to update the hovered item, but we can't use the mouse screen coordinates because another control
		/// might have mouse capture, in which case the mouse would never be hovering over us even if it appears to be.
		/// Set in all mouse event functions. Null means the mouse is totally gone (MouseLeave).
		/// This is awkward and nobody would really want it but unfortunately it has to be that way.
		/// </summary>
		private Point? LastMousePosition_ClientCoordinates;
		private ScrollBarElement _CapturedItem;
		private ScrollBarElement CapturedItem => this._CapturedItem;
		private bool _IsCapturedItemActive;
		private bool IsCapturedItemActive => this._IsCapturedItemActive;
		private int ThumbDragStart;
		private int ThumbDragStartScrollValue;

		private void SetCapturedItem(ScrollBarElement Value)
		{
			if (this._CapturedItem != Value)
			{
				this._CapturedItem = Value;
				this.InvalidateCallback?.Invoke();
			}
		}

		private void SetActiveItem(bool IsCapturedItemActive)
		{
			if (this._IsCapturedItemActive != IsCapturedItemActive)
			{
				this._IsCapturedItemActive = IsCapturedItemActive;
				if (!IsCapturedItemActive)
				{
					this.RequestStartTimer(Timeout.Infinite);
				}
				this.InvalidateCallback?.Invoke();
			}
		}

		private void RequestStartTimer(int Timeout_ms)
		{
			if (Timeout_ms < 0)
			{
				this.Timer.Enabled = false;
			}
			else
			{
				this.Timer.Enabled = false;
				this.Timer.Interval = Timeout_ms;
				this.Timer.Enabled = true;
			}
		}

		private static int RepeatPreDelay => (SystemInformation.KeyboardDelay + 1) * 250;
		private static int RepeatPeriod => 960 / (SystemInformation.KeyboardSpeed + 1); // arbitrary

		private void SetHoveredItem(ScrollBarElement CurrentPosition, bool InvalidateIfChanged)
		{
			if (this.LastHoveredItem != CurrentPosition)
			{
				// Possible optimization: Can omit invalidate event if hovering over the tracks because they have no hover effect.
				// It's not that simple though because it depends on captured item, active item and all sorts of stuff... So pretty error prone.
				this.LastHoveredItem = CurrentPosition;
				if (InvalidateIfChanged)
				{
					this.InvalidateCallback?.Invoke();
				}
			}
		}

		private ScrollBarElement UpdateHoveredItem(ref ControlMetrics Metrics, bool InvalidateIfChanged = true)
		{
			var CurrentPosition = this.LastMousePosition_ClientCoordinates == null ? ScrollBarElement.None : GetElementAtPosition(this.LastLayoutInfo.Bounds, ref Metrics, this.LastMousePosition_ClientCoordinates.Value);
			this.SetHoveredItem(CurrentPosition, InvalidateIfChanged);
			return CurrentPosition;
		}

		public bool HandleMouseDown(MouseEventArgs e)
		{
			this.LastMousePosition_ClientCoordinates = e.Location;
			var Metrics = this.GetControlMetrics();
			var CurrentClickPosition = this.UpdateHoveredItem(ref Metrics);

			if (this.CapturedItem == ScrollBarElement.None && !this.LastLayoutInfo.Bounds.Contains(e.Location)) return false;

			this.LastMouseButton |= e.Button;

			if (this.CapturedItem == ScrollBarElement.None)
			{
				// Capture new item.
				// This will let the MouseMove event know what to do. It also controls mouse capture generally for mouse events.
				Debug.Assert(!this.IsCapturedItemActive);
				this.SetCapturedItem(CurrentClickPosition);
			}

			if (this.CapturedItem != CurrentClickPosition)
			{
				// An item can only become active if it's actually being clicked on. Being the captured item isn't enough.
				return true;
			}

			if (e.Button == MouseButtons.Left)
			{
				this.SetActiveItem(true);
				switch (CurrentClickPosition)
				{
					case ScrollBarElement.TopButton:
						this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue - this.LastLayoutInfo.SmallChange);
						this.RequestStartTimer(RepeatPreDelay);
						break;

					case ScrollBarElement.TrackAboveThumb:
						this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue - this.LastLayoutInfo.LargeChange);
						this.RequestStartTimer(RepeatPreDelay);
						break;

					case ScrollBarElement.TrackBelowThumb:
						this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue + this.LastLayoutInfo.LargeChange);
						this.RequestStartTimer(RepeatPreDelay);
						break;

					case ScrollBarElement.BottomButton:
						this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue + this.LastLayoutInfo.SmallChange);
						this.RequestStartTimer(RepeatPreDelay);
						break;

					case ScrollBarElement.Thumb:
						this.ThumbDragStartScrollValue = this.ScrollValue;
						this.ThumbDragStart = e.Y;
						break;
				}
			}
			else if (e.Button == MouseButtons.Right)
			{
				switch (CurrentClickPosition)
				{
					case ScrollBarElement.Thumb:
					case ScrollBarElement.TrackAboveThumb:
					case ScrollBarElement.TrackBelowThumb:
						// "Scroll here" feature
						// Calculate target scroll position -- mouse position becomes center of thumb.
						// y = 0: Topmost thumb position (center of thumb when it's in top position)
						int y = e.Y - this.LastLayoutInfo.Bounds.Top - Metrics.TrackStart - Metrics.ThumbSize / 2;
						float Ratio = this.LastLayoutInfo.TotalScrollableHeight == 0 ? 0 : (float)y / Metrics.TrackSpaceAvailableForThumbMovement; // NOTE: Can be out of range, that's checked by SetScrollValue.
						this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, (int)(Ratio * this.LastLayoutInfo.TotalScrollableHeight));
						// Do the same thing a left click on the thumb would have done:
						this.SetCapturedItem(ScrollBarElement.Thumb);
						this.SetActiveItem(true);
						this.ThumbDragStartScrollValue = this.ScrollValue;
						this.ThumbDragStart = e.Y;
						break;
				}
			}

			return true;
		}

		public bool HandleMouseMove(MouseEventArgs e)
		{
			this.LastMousePosition_ClientCoordinates = e.Location;
			if ((e.Button & this.LastMouseButton) != this.LastMouseButton)
			{
				// Failsafe mechanism: Mouse button was released. Reset drag state if necessary. This is important if the window lost the focus during a drag operation.
				this.SetCapturedItem(ScrollBarElement.None);
				this.SetActiveItem(false);
			}

			// We always need to update the hover position, regardless of whether this event is in in bounds or not.
			var Metrics = this.GetControlMetrics();
			this.UpdateHoveredItem(ref Metrics);

			if (this.CapturedItem == ScrollBarElement.None && !this.LastLayoutInfo.Bounds.Contains(e.Location))
			{
				// Mouse not here at all.
				return false;
			}

			if (this.CapturedItem == ScrollBarElement.Thumb && this.IsCapturedItemActive)
			{
				int DragDelta = e.Y - this.ThumbDragStart;

				int ScrollDelta = this.LastLayoutInfo.TotalScrollableHeight == 0 ? 0 : (int)((float)DragDelta / Metrics.TrackSpaceAvailableForThumbMovement * this.LastLayoutInfo.TotalScrollableHeight);
				this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ThumbDragStartScrollValue + ScrollDelta);
				// We don't have to update the hovered item if this changes technically because there is no visual difference, however, that is a special case only
				// because the thumb's hover effect is a little different and thumb dragging is the only thing that would make a difference here.
				// So we're still updating for brevity.
			}

			return true;
		}

		public void HandleMouseLeave()
		{
			this.LastMousePosition_ClientCoordinates = null;
			this.LastMouseButton = MouseButtons.None;
			this.SetCapturedItem(ScrollBarElement.None); // Actually should already be none because if it wasn't none, the parent Control should have mouse capture.
			this.SetActiveItem(false);
			this.SetHoveredItem(ScrollBarElement.None, InvalidateIfChanged: true);
		}

		public bool HandleMouseUp(MouseEventArgs e)
		{
			this.LastMousePosition_ClientCoordinates = e.Location;
			this.LastMouseButton = MouseButtons.None; // Always release, see comment below.
			var Metrics = this.GetControlMetrics();
			this.UpdateHoveredItem(ref Metrics);

			if (this.CapturedItem == ScrollBarElement.None && !this.LastLayoutInfo.Bounds.Contains(e.Location)) return false;

			// Release regardless of which mouse button it was. Reason: Windows' MouseCapture also releases regardless of the button, so it would be buggy anyway.
			this.SetCapturedItem(ScrollBarElement.None);
			this.SetActiveItem(false);

			return true;
		}

		/// <summary>
		/// This event is different from the others in that you have to check manually whether you want to call it. It doesn't check the event location.
		/// This is because you might want to call this even if the user scrolls anywhere on the viewport (not just on the scrollbar directly).
		/// </summary>
		public void HandleMouseWheel(MouseEventArgs e, int LineHeight)
		{
			// Only do mouse wheel stuff if the thumb is not being dragged right now because that would be confusing.
			if (!(this.CapturedItem == ScrollBarElement.Thumb && this.IsCapturedItemActive))
			{
				// NOTE: Positive Delta -> Scroll up -> Decrease ScrollValue
				float Notches = e.Delta / SystemInformation.MouseWheelScrollDelta;
				float ScrollAmount = -Notches * SystemInformation.MouseWheelScrollLines * LineHeight;
				if (this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue + (int)ScrollAmount) && this.LastMousePosition_ClientCoordinates != null)
				{
					var Metrics = this.GetControlMetrics();
					this.UpdateHoveredItem(ref Metrics);
				}
			}
		}

		private void HandleTimerTickEvent(object sender, EventArgs e)
		{
			if (!this.IsCapturedItemActive) return;

			var Metrics = this.GetControlMetrics();
			if (this.LastMousePosition_ClientCoordinates == null) return;
			var HoveredItem = this.UpdateHoveredItem(ref Metrics);
			if (HoveredItem != this.CapturedItem) return;

			switch (this.CapturedItem)
			{
				case ScrollBarElement.TopButton:
					this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue - this.LastLayoutInfo.SmallChange);
					this.RequestStartTimer(RepeatPeriod);
					break;

				case ScrollBarElement.TrackAboveThumb:
					this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue - this.LastLayoutInfo.LargeChange);
					this.RequestStartTimer(RepeatPeriod);
					break;

				case ScrollBarElement.TrackBelowThumb:
					this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue + this.LastLayoutInfo.LargeChange);
					this.RequestStartTimer(RepeatPeriod);
					break;

				case ScrollBarElement.BottomButton:
					this.SetScrollValue(this.LastLayoutInfo.TotalScrollableHeight, this.ScrollValue + this.LastLayoutInfo.SmallChange);
					this.RequestStartTimer(RepeatPeriod);
					break;
			}
		}

		// ================================================================================================================================================
		// ================================================================================================================================================
		//
		// Paint
		//

		private enum Hotness
		{
			/// <summary>Neither hover nor active.</summary>
			Inactive,
			/// <summary>Not active, but hover OR capture. This is where mouse events will go, but currently no active mouse thing is going on.</summary>
			Warm,
			/// <summary>Active and hover. Some things can also be very hot without hover.</summary>
			VeryHot
		}

		private Hotness GetItemHotness(ScrollBarElement HoveredItem, ScrollBarElement TestItem, bool CanBeHotWithoutHover = false)
		{
			if (this.CapturedItem == TestItem && this.IsCapturedItemActive)
			{
				// Active
				if (CanBeHotWithoutHover || HoveredItem == TestItem)
				{
					return Hotness.VeryHot;
				}
				else
				{
					return Hotness.Warm;
				}
			}
			if (this.CapturedItem == TestItem || (HoveredItem == TestItem && this.CapturedItem == ScrollBarElement.None)) return Hotness.Warm;
			return Hotness.Inactive;
		}

		private static Brush GetTrackBrush(Hotness Hotness)
		{
			switch (Hotness)
			{
				default:
					return SystemBrushes.ScrollBar;
				case Hotness.VeryHot:
					return SystemBrushes.ButtonHighlight;
			}
		}

		private static Brush GetArrowButtonBackgroundBrush(ScrollBarArrowButtonStyle Style, Hotness Hotness, out Brush TriangleBrush)
		{
			switch (Hotness)
			{
				case Hotness.VeryHot:
					TriangleBrush = SystemBrushes.ButtonHighlight;
					return SystemBrushes.ControlText;
				case Hotness.Warm:
					TriangleBrush = SystemBrushes.ControlText;
					return SystemBrushes.ButtonHighlight;
				default:
					TriangleBrush = SystemBrushes.ControlText;
					return Style == ScrollBarArrowButtonStyle.FlatBorderless ? SystemBrushes.ScrollBar : SystemBrushes.ButtonFace;
			}
		}

		/// <summary>
		/// This exists because we can avoid redrawing on every mouse event if the hovered item hasn't actually changed.
		/// </summary>
		private ScrollBarElement LastHoveredItem;

		/// <summary>
		/// This is used to handle mouse events. Set when painting.
		/// This has the advantage that the user doesn't need to pass the stuff to the mouse event all the time (which is annoying), and also that mouse event handling
		/// always matches what's actually visible on screen (rather than potentially being one frame ahead, potentially causing incorrect mouse event responses).
		/// </summary>
		private LayoutInfo LastLayoutInfo;

		public enum ScrollBarArrowButtonStyle
		{
			/// <summary>
			/// Looks more obvious.
			/// </summary>
			FlatWithBorder,
			/// <summary>
			/// Usually looks better if there's a border around the whole thing because it avoids double-borders near the arrow buttons.
			/// </summary>
			FlatBorderless
		}

		/// <summary>
		/// Call this in <see cref="Control.OnPaint"/> with the most recent known values.
		/// </summary>
		public void Paint(Graphics g, ref LayoutInfo LayoutInfo, ScrollBarArrowButtonStyle ArrowButtonStyle)
		{
			this.LastLayoutInfo = LayoutInfo;

			var Metrics = this.GetControlMetrics();

			// NOTE: We can't just check the current mouse position here. Even if the mouse is actually hovering over an element, it might not be in the hover state if another
			//       control has capture. We have to do this with the last known mouse position, set in the mouse events after all.
			var HoveredItem = this.UpdateHoveredItem(ref Metrics, InvalidateIfChanged: false);

			//Debug.WriteLine($"paint ({frame++})");

			var OldSmoothingMode = g.SmoothingMode;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

			var OldTransform = g.Transform;
			g.TranslateTransform(LayoutInfo.Bounds.Left, LayoutInfo.Bounds.Top);

			// Track (above thumb)
			{
				Brush Brush = GetTrackBrush(this.GetItemHotness(HoveredItem, ScrollBarElement.TrackAboveThumb));
				g.FillRectangle(Brush, 0, Metrics.TrackStart, LayoutInfo.Bounds.Width, Metrics.ThumbStart - Metrics.TrackStart);
			}

			// Track (below thumb)
			{
				Brush Brush = GetTrackBrush(this.GetItemHotness(HoveredItem, ScrollBarElement.TrackBelowThumb));
				g.FillRectangle(Brush, 0, Metrics.ThumbStart + Metrics.ThumbSize, LayoutInfo.Bounds.Width, Metrics.BottomButtonStart - (Metrics.ThumbStart + Metrics.ThumbSize));
			}

			// Thumb
			{
				Brush Brush;
				switch (this.GetItemHotness(HoveredItem, ScrollBarElement.Thumb, CanBeHotWithoutHover: true))
				{
					case Hotness.VeryHot:
						Brush = SystemBrushes.ButtonHighlight;
						break;
					case Hotness.Warm:
						Brush = SystemBrushes.ControlDarkDark;
						break;
					default:
						Brush = SystemBrushes.ControlDark;
						break;
				}
				g.FillRectangle(Brush, 0, Metrics.ThumbStart, LayoutInfo.Bounds.Width, Metrics.ThumbSize);
			}

			float TriangleScale = LayoutInfo.Bounds.Width / 4.0f;

			// Top Button
			{
				Brush BackgroundBrush = GetArrowButtonBackgroundBrush(ArrowButtonStyle, this.GetItemHotness(HoveredItem, ScrollBarElement.TopButton), out Brush TriangleBrush);
				g.FillRectangle(BackgroundBrush, 0, 0, LayoutInfo.Bounds.Width, Metrics.TrackStart);
				if (ArrowButtonStyle != ScrollBarArrowButtonStyle.FlatBorderless)
				{
					g.DrawRectangle(SystemPens.ControlText, 0.5f, 0 + 0.5f, LayoutInfo.Bounds.Width - 1.0f, Metrics.TrackStart - 0 - 1.0f); // "0" stands for top button start
				}
				this.DrawTriangle(g, new PointF(LayoutInfo.Bounds.Width / 2.0f, 0 + (Metrics.TrackStart - 0) / 2.0f), TriangleScale, TriangleBrush);
			}

			// Bottom Button
			{
				Brush BackgroundBrush = GetArrowButtonBackgroundBrush(ArrowButtonStyle, this.GetItemHotness(HoveredItem, ScrollBarElement.BottomButton), out Brush TriangleBrush);
				g.FillRectangle(BackgroundBrush, 0, Metrics.BottomButtonStart, LayoutInfo.Bounds.Width, LayoutInfo.Bounds.Height - Metrics.BottomButtonStart);
				if (ArrowButtonStyle != ScrollBarArrowButtonStyle.FlatBorderless)
				{
					g.DrawRectangle(SystemPens.ControlText, 0.5f, Metrics.BottomButtonStart + 0.5f, LayoutInfo.Bounds.Width - 1.0f, LayoutInfo.Bounds.Height - Metrics.BottomButtonStart - 1.0f);
				}
				this.DrawTriangle(g, new PointF(LayoutInfo.Bounds.Width / 2.0f, Metrics.BottomButtonStart + (LayoutInfo.Bounds.Height - Metrics.BottomButtonStart) / 2.0f), -TriangleScale, TriangleBrush);
			}

			// Restore all state
			g.Transform = OldTransform;
			g.SmoothingMode = OldSmoothingMode;

			//TextRenderer.DrawText(g, this.CapturedItem + Environment.NewLine + this.HoveredItem + Environment.NewLine + this.IsCapturedItemActive, new Font("Microsoft Sans Serif", 9.75f), new Point(0, 0), Color.Red);
		}

		private readonly PointF[] DrawTriangleHelper = new PointF[3];

		/// <summary>
		/// Pointing upward. Use scale and transform matrix to relocate.
		/// </summary>
		private void DrawTriangle(Graphics g, PointF Origin, float Scale, Brush Brush)
		{
			var Transform = g.Transform;

			g.TranslateTransform(Origin.X, Origin.Y);
			g.ScaleTransform(Scale, Scale);
			g.TranslateTransform(0, 0.4f);

			this.DrawTriangleHelper[0] = new PointF(-1.0f, 0);
			this.DrawTriangleHelper[1] = new PointF(0, -1.0f);
			this.DrawTriangleHelper[2] = new PointF(1.0f, 0);
			g.FillPolygon(Brush, this.DrawTriangleHelper);

			g.Transform = Transform;
		}
	}
}
