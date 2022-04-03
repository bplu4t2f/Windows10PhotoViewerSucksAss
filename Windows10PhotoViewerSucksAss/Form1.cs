using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	// TODO maybe create downscaled icon image with the scan0 bitmap constructor
	// TODO f1 help menu overlay
	// TODO "shell" menu
	// TODO "save user settings" check box
	// TODO show save error upon switching save mode, if applicable
	// TODO reset user settings button
	// TODO other media files (animated gif isn't that bad actually)
	// TODO refresh should reload the image
	// TODO file system watcher
	// TODO choose extensions (file list filter)
	// TODO move/copy targets, with optional counter
	// TODO file list colors
	// TODO custom scroll bar colors
	// TODO save session (use WM_APP messages with EnumWindows for communication) (maybe RegisterWindowMessageA instead with HWND_BROADCAST?)
	// TODO maybe use an explicit low priority thread for disposing images rather than the thread pool
	// TODO maybe it's time to do something about unifying keyboard commands and menu item commands :/
	// TODO pixel perfect mode

#warning TODO cancel loading if outdated
#warning TODO display 'loading' if loading for too long

	class Form1 : Form
	{
		public Form1(StartupInfo startupInfo)
		{
			this.StartupInfo = startupInfo ?? throw new ArgumentNullException(nameof(startupInfo));

			this.Icon = Properties.Resources.generic_picture;

			// This seems to have only positive effects: Reduces artifacts when moving controls around (such as the splitter).
			this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

			// Design defaults
			this.mainImageControl.BackColor = Color.FromArgb(32, 64, 96);
			this.overviewControl.BackColor = Color.FromArgb(24, 29, 46);
			this.overviewControl.ForeColor = Color.FromArgb(214, 203, 188);
			this.optionsButton.BackColor = this.overviewControl.BackColor;
			this.optionsButton.ForeColor = this.overviewControl.ForeColor;
			this.splitter.Width = 5;
			this.SetApplicationFont(SystemFonts.MenuFont); // MenuFont is normally Segoe UI 9, which, in this application, looks way better than Microsoft Sans Serif 8 (which, contrary to what MSDN says, is the default application font).

			// ==================================================================================================================================================================
			// ==================================================================================================================================================================
			//
			// User settings (from the AppData settings file)
			//

			foreach (var setting in applicableSettings)
			{
				try
				{
					setting.ApplyStored(this);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex);
				}
			}

			foreach (var setting in applicableSettings)
			{
				setting.ValueSet += (sender, e) => Settings.QueueSave();
			}

			// ==================================================================================================================================================================
			// ==================================================================================================================================================================
			//
			// GUI initialization
			//

			this.optionsButton.Text = "Options...";
			this.optionsButton.Width = this.overviewControl.Width;
			this.optionsButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

			this.overviewControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			// Height is set in UpdateOptionsButtonHeight

			this.splitter.BackColor = Color.Black;
			this.splitter.Left = this.overviewControl.Right;
			this.splitter.Height = this.ClientSize.Height;
			this.splitter.LeftControls.Add(this.optionsButton);
			this.splitter.LeftControls.Add(this.overviewControl);
			this.splitter.RightControls.Add(this.mainImageControl);
			this.splitter.DragStopped += this.HandleSplitterDragStopped;
			this.splitter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;

			this.mainImageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			this.mainImageControl.Left = this.splitter.Right;
			this.mainImageControl.Size = new Size(this.ClientSize.Width - this.mainImageControl.Left, this.ClientSize.Height);
			this.mainImageControl.BeginDragHandler = this.HandleBeginDrag;

			this.SuspendLayout();
			this.Controls.Add(this.optionsButton);
			this.Controls.Add(this.mainImageControl);
			this.Controls.Add(this.overviewControl);
			this.Controls.Add(this.splitter);
			this.ResumeLayout();

			// This will make sure that the splitter is visible inside its parent
			this.splitter.MoveBy(0);

			this.UpdateOptionsButtonHeight();

			this.overviewControl.ImageSelected += this.OverviewControl_ImageSelected;
			this.optionsButton.Click += this.HandleOptionButtonClick;

			this.AllowDrop = true;

			this.mi_file_name = this.fileListContextMenu.MenuItems.Add(String.Empty);
			this.mi_file_name.Enabled = false;
			var mi_show = this.fileListContextMenu.MenuItems.Add("Show");
			mi_show.Click += this.HandleMenuShow;
			var mi_zoom_to_fit = this.fileListContextMenu.MenuItems.Add("Zoom to fit\tT");
			mi_zoom_to_fit.Click += this.HandleMenuZoomToFit;
			var mi_original_size = this.fileListContextMenu.MenuItems.Add("Original size\tO");
			mi_original_size.Click += this.HandleMenuOriginalSize;
			this.fileListContextMenu.MenuItems.Add("-"); // separator
			var mi_explore_to = this.fileListContextMenu.MenuItems.Add("Explore to\tE");
			mi_explore_to.Click += this.HandleMenuExploreTo;
			var mi_copy_full_path = this.fileListContextMenu.MenuItems.Add("Copy full path\tF");
			mi_copy_full_path.Click += this.HandleMenuCopyFullPath;
			var mi_copy_file = this.fileListContextMenu.MenuItems.Add("Copy file\tC");
			mi_copy_file.Click += this.HandleMenuCopyFile;
			var mi_cut_file = this.fileListContextMenu.MenuItems.Add("Cut file\tX");
			mi_cut_file.Click += this.HandleMenuCutFile;
			var mi_fork = this.fileListContextMenu.MenuItems.Add("Fork\tG");
			mi_fork.Click += this.HandleMenuFork;
			var mi_file_properties = this.fileListContextMenu.MenuItems.Add("File properties\tP");
			mi_file_properties.Click += this.HandleMenuFileProperties;
			var mi_refresh_files = this.fileListContextMenu.MenuItems.Add("Refresh files\tF5");
			mi_refresh_files.Click += this.HandleMenuRefreshFiles;
			var mi_rename_file = this.fileListContextMenu.MenuItems.Add("Rename file...\tF2");
			mi_rename_file.Click += this.HandleMenuRenameFile;
			var mi_delete_file = this.fileListContextMenu.MenuItems.Add("Move to Recycle Bin\tDel");
			mi_delete_file.Click += this.HandleMenuDeleteFile;
			this.fileListContextMenu.MenuItems.Add("-"); // separator
			var mi_stash = this.fileListContextMenu.MenuItems.Add("Stash...\tF8");
			mi_stash.Click += this.HandleMenuStash;
			var mi_restore_from_stash = this.fileListContextMenu.MenuItems.Add("Restore from stash...\tF9");
			mi_restore_from_stash.Click += this.HandleMenuRestoreFromStash;

			this.synchronizationContext = SynchronizationContext.Current;

			this.displayWantedImageDelegate = _ => this.DisplayWantedImage();
			this.refreshOverviewDelegate = _ => this.RefreshOverview();

			this.imageCacheWorker.DisplayItemLoaded += this.HandleImageCacheDisplayItemLoaded;
			this.imageCacheWorker.WorkItemCompleted += this.HandleImageCacheWorkItemCompleted;
			this.imageCacheWorker.StartWorkerThread();
		}

		protected override Size DefaultSize => new Size(960, 640);

		public StartupInfo StartupInfo { get; }

		private readonly SynchronizationContext synchronizationContext;
		private readonly ContextMenu fileListContextMenu = new ContextMenu();
		private readonly MenuItem mi_file_name;

		private readonly DifferentButton optionsButton = new DifferentButton();
		private readonly MainImageControl mainImageControl = new MainImageControl();
		private readonly OverviewControl overviewControl = new OverviewControl();
		private readonly SplitterControl splitter = new SplitterControl();

		private readonly ImageCacheWorker imageCacheWorker = new ImageCacheWorker();


		// ==================================================================================================================================================================
		// ==================================================================================================================================================================
		//
		// Initialize static setting descriptors
		//


		static Form1()
		{
			Setting_BackColor = applicableSettings.AddReturn(MakeSetting(
				Setting("ImageBackColor", s => s.ImageBackColor, (s, v) => s.ImageBackColor = v),
				(x, v) => { if (!v.IsEmpty) x.mainImageControl.BackColor = v; },
				x => x.mainImageControl.BackColor
				));
			applicableSettings.Add(MakeSetting(
				Setting("WindowWidth", s => s.WindowWidth, (s, v) => s.WindowWidth = v),
				(x, v) => { if (v != 0) x.Width = v; }
				));
			applicableSettings.Add(MakeSetting(
				Setting("WindowHeight", s => s.WindowHeight, (s, v) => s.WindowHeight = v),
				(x, v) => { if (v != 0) x.Height = v; }
				));
			Setting_SortCaseSensitive = applicableSettings.AddReturn(MakeSetting(
				Setting("SortCaseSensitive", s => s.SortCaseSensitive, (s, v) => s.SortCaseSensitive = v),
				(x, v) => x.SetSortCaseSensitive(v)
				));
			Setting_ApplicationFont = applicableSettings.AddReturn(MakeSetting(
				Setting("ApplicationFont", s => s.ApplicationFont, (s, v) => s.ApplicationFont = v).Encoding(x => x?.ToFont(), x => FontDescriptor.FromFont(x)),
				(x, v) => { if (v != null) x.SetApplicationFont(v); },
				x => x.Font
				));
			applicableSettings.Add(MakeSetting(
				Setting("OverviewControlWidth", s => s.OverviewControlWidth, (s, v) => s.OverviewControlWidth = v),
				(x, v) => { if (v >= 0) x.overviewControl.Width = v; }
				));
			Setting_SplitterWidth = applicableSettings.AddReturn(MakeSetting(
				Setting("SplitterWidth", s => s.SplitterWidth, (s, v) => s.SplitterWidth = v),
				(x, v) => { if (v >= 0) x.splitter.ChangeWidth(v); },
				x => x.splitter.Width
				));
			Setting_MouseWheelMode = applicableSettings.AddReturn(MakeSetting(
				Setting("MouseWheelMode", s => s.MouseWheelMode, (s, v) => s.MouseWheelMode = v),
				(x, v) => x.MouseWheelMode = v,
				x => x.MouseWheelMode
				));
			Setting_UseCurrentImageAsWindowIcon = applicableSettings.AddReturn(MakeSetting(
				Setting("UseCurrentImageAsWindowIcon", s => s.UseCurrentImageAsWindowIcon, (s, v) => s.UseCurrentImageAsWindowIcon = v),
				(x, v) => x.SetUseCurrentImageAsWindowIcon(v)
				));
			Setting_FileListBackColor = applicableSettings.AddReturn(MakeSetting(
				Setting("FileListBackColor", s => s.FileListBackColor, (s, v) => s.FileListBackColor = v),
				(x, v) => { if (!v.IsEmpty) { x.overviewControl.BackColor = v; x.optionsButton.BackColor = v; } },
				x => x.overviewControl.BackColor
				));
			Setting_FileListForeColor = applicableSettings.AddReturn(MakeSetting(
				Setting("FileListForeColor", s => s.FileListForeColor, (s, v) => s.FileListForeColor = v),
				(x, v) => { if (!v.IsEmpty) { x.overviewControl.ForeColor = v; x.optionsButton.ForeColor = v; } },
				x => x.overviewControl.ForeColor
				));
			Setting_FileListForeColorError = applicableSettings.AddReturn(MakeSetting(
				Setting("FileListForeColorError", s => s.FileListForeColorError, (s, v) => s.FileListForeColorError = v),
				(x, v) => { if (!v.IsEmpty) x.overviewControl.ForeColorError = v; },
				x => x.overviewControl.ForeColorError
				));
		}

		private static GetSet<Settings, TValue> Setting<TValue>(string debugName, Func<Settings, TValue> getter, Action<Settings, TValue> setter)
		{
			return new GetSet<Settings, TValue>(debugName, getter, setter);
		}

		private static IApplicableSetting2<Form1, T> MakeSetting<T>(IGetSet<Settings, T> accessor, Action<Form1, T> apply, Func<Form1, T> get = null)
		{
			return new ApplicableSetting<T>(accessor, apply, get);
		}

		// ====================================================================================================================================================================
		// ====================================================================================================================================================================
		//
		// IApplicableSetting2<TSettings>
		// Used to encapsulate an individual setting that can be changed at runtime and is stored in the user settings file.
		//

		interface IApplicableSetting2<TSettings>
		{
			string DebugName { get; }
			/// <summary>
			/// Used on program startup to apply values from the stored object.
			/// </summary>
			void ApplyStored(TSettings from);

			/// <summary>
			/// Used to notify the application that a setting was changed in the settings dialog. The application uses this event to save the settings to disk.
			/// </summary>
			event EventHandler ValueSet;
		}

		interface IApplicableSetting2<TSettings, T> : IApplicableSetting2<TSettings>, IGetSet<TSettings, T>
		{
		}

		private sealed class ApplicableSetting<T> : IApplicableSetting2<Form1, T>
		{
			public ApplicableSetting(IGetSet<Settings, T> accessor, Action<Form1, T> apply, Func<Form1, T> get)
			{
				this.accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
				// Apply cannot be null because this is used on program startup to apply the initial setting values.
				this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
				this.get = get;
			}

			private readonly IGetSet<Settings, T> accessor;
			private readonly Action<Form1, T> apply;
			private readonly Func<Form1, T> get;

			public string DebugName => this.accessor.DebugName;

			public event EventHandler ValueSet;

			public T Get(Form1 from)
			{
				if (this.get != null)
				{
					// This setting will be gotten directly from the Form instance.
					return this.get(from);
				}
				else
				{
					return this.accessor.Get(Settings.Instance);
				}
			}

			public void Set(Form1 to, T value)
			{
				this.accessor.Set(Settings.Instance, value);
				this.ValueSet?.Invoke(this, EventArgs.Empty);
				this.apply(to, value);
			}

			public void ApplyStored(Form1 target)
			{
				T value = this.accessor.Get(Settings.Instance);
				this.apply(target, value);
			}
		}

		private static readonly List<IApplicableSetting2<Form1>> applicableSettings = new List<IApplicableSetting2<Form1>>();

		// These are used by the settings form to change settings live.
		// Setting these raises the ValueSet event as attached in the constructor.
		public static IGetSet<Form1, Color> Setting_BackColor { get; }
		public static IGetSet<Form1, bool> Setting_SortCaseSensitive { get; }
		public static IGetSet<Form1, Font> Setting_ApplicationFont { get; }
		public static IGetSet<Form1, int> Setting_SplitterWidth { get; }
		public static IGetSet<Form1, MouseWheelMode> Setting_MouseWheelMode { get; }
		public static IGetSet<Form1, bool> Setting_UseCurrentImageAsWindowIcon { get; }
		public static IGetSet<Form1, Color> Setting_FileListBackColor { get; }
		public static IGetSet<Form1, Color> Setting_FileListForeColor { get; }
		public static IGetSet<Form1, Color> Setting_FileListForeColorError { get; }


		// ==================================================================================================================================================================
		// ==================================================================================================================================================================


		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			// If this process was started by restoring a stash,
			// then StartupInfo will contain the handle value to a STARTUP_PARAMS file mapping.
			// Restore that if possible.
			var value = this.StartupInfo.StartupParamsHandleValue;
			if (value != null)
			{
				Debug.Assert(this.IsHandleCreated);
				var handle = this.Handle;
				StashHelper.RestoreStashInfoFromMemory(handle, value);
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			Settings.WaitSaveCompleted();
			base.OnClosing(e);
		}

		/// <summary>
		/// Must be called when the font of the options button has changed (and after initial allocation).
		/// </summary>
		private void UpdateOptionsButtonHeight()
		{
			int lineHeight = this.optionsButton.Font.Height;
			this.optionsButton.Height = lineHeight + 10;
			int optionsButtonTop = this.ClientRectangle.Height - this.optionsButton.Height;
			this.optionsButton.Top = optionsButtonTop;
			this.overviewControl.Height = optionsButtonTop;
		}

		public MouseWheelMode MouseWheelMode { get; set; }


		//
		// User events
		//


		protected override void OnDragEnter(DragEventArgs drgevent)
		{
			base.OnDragEnter(drgevent);
			if (drgevent.Data.GetDataPresent(DataFormats.FileDrop))
			{
				drgevent.Effect = DragDropEffects.Copy;
			}
		}

		protected override void OnDragDrop(DragEventArgs drgevent)
		{
			base.OnDragDrop(drgevent);
			var files = (string[])drgevent.Data.GetData(DataFormats.FileDrop);
			if (files == null || files.Length < 1)
			{
				return;
			}
			this.BeginInvoke(new MethodInvoker(() => this.SetDisplayPath_NoThrowInteractive(files[0])));
		}

		// Begin drag operation from the current file
		private void HandleBeginDrag()
		{
			if (!this.TryGetFile(this.currentDisplayIndex, out var file)) return;
			string[] dragFiles = new string[]
			{
				file.FullPath
			};
			this.DoDragDrop(new DataObject(DataFormats.FileDrop, dragFiles), DragDropEffects.Copy);
		}

		private void OverviewControl_ImageSelected(object sender, ImageSelectionEventArgs e)
		{
			if (this.currentFileList == null)
			{
				return;
			}

			int index = e.Index;

			if (e.RightClick)
			{
				if (!this.TryGetFile(index, out FileListEntry file))
				{
					return;
				}
				this.menuItemFileIndex = index;
				this.mi_file_name.Text = Path.GetFileName(file.FullPath);
				this.fileListContextMenu.Show(this.overviewControl, e.ClickLocation);
			}
			else
			{
				if (index >= 0 && index < this.currentFileList.Count)
				{
					this.currentDisplayIndex = index;
					this.DisplayCurrent(scrollSelectedItemIntoView: false);
				}
			}
		}

		private SettingsForm currentSettingsForm;

		private void HandleOptionButtonClick(object sender, EventArgs e)
		{
			if (this.currentSettingsForm != null)
			{
				this.currentSettingsForm.Activate();
				this.currentSettingsForm.BringToFront();
				if (this.currentSettingsForm.WindowState == FormWindowState.Minimized)
				{
					this.currentSettingsForm.WindowState = FormWindowState.Normal;
				}
				return;
			}
			var form = new SettingsForm(this);
			this.currentSettingsForm = form;
			form.FormClosed += (sender1, e1) => { if (this.currentSettingsForm == form) { this.currentSettingsForm = null; } };
			form.StartPosition = FormStartPosition.Manual;
			form.Font = this.Font;
			Util.CenterControl(this, form);
			form.Show();
		}

		private void SetSortCaseSensitive(bool value)
		{
			this.TryGetFile(this.currentDisplayIndex, out var file);
			this.UpdateCurrentFileList(scrollSelectedItemIntoView: false, file?.FullPath);
		}

		private void SetApplicationFont(Font value)
		{
			this.Font = value;
			if (this.currentSettingsForm != null)
			{
				this.currentSettingsForm.Font = value;
			}
			this.UpdateOptionsButtonHeight();
		}

		private void SetUseCurrentImageAsWindowIcon(bool value)
		{
			// TODO might need to set field
			if (value)
			{
				this.UpdateWindowIcon();
			}
			else
			{
				this.Icon = Properties.Resources.generic_picture;
			}
		}

		private void HandleSplitterDragStopped(object sender, EventArgs e)
		{
			int splitterPos = this.splitter.Left;
			Settings.Instance.OverviewControlWidth = splitterPos;
			Settings.QueueSave();
		}

		protected override void OnResizeEnd(EventArgs e)
		{
			base.OnResizeEnd(e);
			Settings.Instance.WindowWidth = this.Width;
			Settings.Instance.WindowHeight = this.Height;
			Settings.QueueSave();
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);

			switch (this.MouseWheelMode)
			{
				case MouseWheelMode.NextPrevious:
				default:
					if (this.currentFileList == null)
					{
						return;
					}
					if (e.Delta < 0)
					{
						this.Next();
					}
					else if (e.Delta > 0)
					{
						this.Previous();
					}
					break;

				case MouseWheelMode.ZoomAndScroll:
					Control scrollTarget = this.GetChildAtPoint(e.Location);
					if (scrollTarget == this.mainImageControl)
					{
						Point clientPoint = new Point(e.Location.X - this.mainImageControl.Location.X, e.Location.Y - this.mainImageControl.Location.Y);
						const float scroll_zoom_factor = 1.25f;
						if (e.Delta > 0)
						{
							this.mainImageControl.ZoomAtLocation(clientPoint, scroll_zoom_factor);
						}
						else if (e.Delta < 0)
						{
							this.mainImageControl.ZoomAtLocation(clientPoint, 1.0f / scroll_zoom_factor);
						}
					}
					else if (scrollTarget == this.overviewControl)
					{
						this.overviewControl.ScrollList(-Math.Sign(e.Delta) * SystemInformation.MouseWheelScrollLines);
					}
					break;
			}
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.S || keyData == Keys.D || keyData == Keys.Down || keyData == Keys.Right)
			{
				this.Next();
				return true;
			}
			else if (keyData == Keys.W || keyData == Keys.A || keyData == Keys.Up || keyData == Keys.Left)
			{
				this.Previous();
				return true;
			}
			else if (keyData == Keys.T)
			{
				this.mainImageControl.ZoomToFit();
				return true;
			}
			else if (keyData == Keys.O)
			{
				this.mainImageControl.ZoomOriginalSize();
				return true;
			}
			else if (keyData == Keys.E)
			{
				this.ExploreTo(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.F)
			{
				this.CopyFullPath(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.C)
			{
				this.CopyFile(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.G)
			{
				this.Fork(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.P)
			{
				this.FileProperties(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.F5)
			{
				this.RefreshFiles();
				return true;
			}
			else if (keyData == Keys.X)
			{
				this.CutFile(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.Delete)
			{
				this.DeleteFile(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.F2)
			{
				this.RenameFile(this.currentDisplayIndex);
				return true;
			}
			else if (keyData == Keys.F8)
			{
				this.ShowStashOptionsDialog();
				return true;
			}
			else if (keyData == Keys.F9)
			{
				this.ShowRestoreFromStashDialog();
				return true;
			}
			else
			{
				return base.ProcessCmdKey(ref msg, keyData);
			}
		}

		private int menuItemFileIndex;

		private void HandleMenuShow(object sender, EventArgs e)
		{
			this.currentDisplayIndex = this.menuItemFileIndex;
			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}

		private void HandleMenuExploreTo(object sender, EventArgs e)
		{
			this.ExploreTo(this.menuItemFileIndex);
		}

		private void HandleMenuCopyFullPath(object sender, EventArgs e)
		{
			this.CopyFullPath(this.menuItemFileIndex);
		}

		private void HandleMenuCopyFile(object sender, EventArgs e)
		{
			this.CopyFile(this.menuItemFileIndex);
		}

		private void HandleMenuCutFile(object sender, EventArgs e)
		{
			this.CutFile(this.menuItemFileIndex);
		}

		private void HandleMenuFork(object sender, EventArgs e)
		{
			this.Fork(this.menuItemFileIndex);
		}

		private void HandleMenuFileProperties(object sender, EventArgs e)
		{
			this.FileProperties(this.menuItemFileIndex);
		}

		private void HandleMenuRefreshFiles(object sender, EventArgs e)
		{
			this.RefreshFiles();
		}

		private void HandleMenuRenameFile(object sender, EventArgs e)
		{
			this.RenameFile(this.menuItemFileIndex);
		}

		private void HandleMenuDeleteFile(object sender, EventArgs e)
		{
			this.DeleteFile(this.menuItemFileIndex);
		}

		private void HandleMenuZoomToFit(object sender, EventArgs e)
		{
			this.mainImageControl.ZoomToFit();
		}

		private void HandleMenuOriginalSize(object sender, EventArgs e)
		{
			this.mainImageControl.ZoomOriginalSize();
		}

		private bool TryGetFile(int index, out FileListEntry file)
		{
			if (this.currentFileList == null || index < 0 || index >= this.currentFileList.Count)
			{
				file = null;
				return false;
			}
			file = this.currentFileList[index];
			return true;
		}

		private void ExploreTo(int fileIndex)
		{
			if (!this.TryGetFile(fileIndex, out FileListEntry file))
			{
				return;
			}
			string fullPath = file.FullPath;
			try
			{
				if (!File.Exists(fullPath))
				{
					MessageBox.Show($"File \"{fullPath}\" doesn't exist.");
					return;
				}
				int hresult = FileIO.SelectInFileExplorer(fullPath);
				System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hresult);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				MessageBox.Show($"Unable to navigate to the specified file: {fullPath}\r\n\r\n{ex.Message}");
			}
		}

		private void CopyFullPath(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out FileListEntry file))
			{
				try
				{
					Clipboard.SetText(file.FullPath);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void CopyFile(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out FileListEntry file))
			{
				try
				{
					Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { file.FullPath });
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void CutFile(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out FileListEntry file))
			{
				try
				{
					Util.ClipboardCutFileList(new string[] { file.FullPath });
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void DeleteFile(int fileIndex)
		{
			if (!this.TryGetFile(fileIndex, out FileListEntry file))
			{
				return;
			}
			FileIO.Send(file.FullPath, FileIO.FileOperationFlags.FOF_SILENT);
			// Who knows that that function is really doing...
			if (File.Exists(file.FullPath))
			{
				return;
			}
			this.ForgetFile(fileIndex);
		}

		/// <summary>
		/// Removes a file from the current file list and rebuilds the overview. Must be called on GUI thread.
		/// </summary>
		private void ForgetFile(int fileIndex)
		{
			if (!this.TryGetFile(fileIndex, out _))
			{
				return;
			}
			this.currentFileList.RemoveAt(fileIndex);
			if (fileIndex < this.currentDisplayIndex)
			{
				this.currentDisplayIndex -= 1;
			}
			if (fileIndex == this.currentDisplayIndex)
			{
				if (this.currentDisplayIndex >= this.currentFileList.Count)
				{
					this.currentDisplayIndex = this.currentFileList.Count - 1;
				}
			}
			this.overviewControl.Initialize(this.currentFileList);
			this.DisplayCurrent(scrollSelectedItemIntoView: false);
		}

		private void RenameFile(int fileIndex)
		{
			if (!this.TryGetFile(fileIndex, out var file))
			{
				return;
			}

			var dir = this.currentDisplayDir;
			string from_full = file.FullPath;
			var from = Path.GetFileName(from_full);
			string choice;
			using (var renameForm = new RenameForm())
			{
				renameForm.StartPosition = FormStartPosition.Manual;
				Util.CenterControl(this, renameForm);
				renameForm.Initialize(dir, from);
				renameForm.ShowDialog();
				choice = renameForm.Choice;
			}

			if (string.IsNullOrEmpty(choice))
			{
				// Canceled by user. Also avoiding empty string because that will give a weird error message.
				return;
			}

			string to_full = Path.Combine(Path.GetDirectoryName(from_full), choice);
			try
			{
				File.Move(from_full, to_full);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				MessageBox.Show($"Error renaming file from\r\n\"{from_full}\"\r\nto\r\n\"{to_full}\"\r\n\r\n{ex.Message}");
				return;
			}

			// Now the file list must be updated. This is a bit awkward since it caches the file names.
			this.currentFileList[fileIndex].FullPath = to_full;
			this.overviewControl.Initialize(this.currentFileList);
			if (fileIndex == this.currentDisplayIndex)
			{
				this.UpdateWindowTitle();
			}
		}

		private void Fork(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out FileListEntry file))
			{
				try
				{
					string exe_name = Process.GetCurrentProcess().MainModule.FileName;
					string arg0 = file.FullPath;
					Process.Start(exe_name, $"\"{arg0}\"");
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void FileProperties(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out FileListEntry file))
			{
				FileIO.ShowFileProperties(file.FullPath);
			}
		}

		private void RefreshFiles()
		{
			this.TryGetFile(this.currentDisplayIndex, out var file);
			this.UpdateDisplayPath(scrollSelectedItemIntoView: false, file?.FullPath);
		}

		private void Next()
		{
			if (this.currentFileList == null)
			{
				return;
			}

			if (this.currentDisplayIndex >= this.currentFileList.Count - 1)
			{
				this.currentDisplayIndex = 0;
			}
			else
			{
				this.currentDisplayIndex += 1;
			}
			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}

		private void Previous()
		{
			if (this.currentFileList == null)
			{
				return;
			}

			if (this.currentDisplayIndex <= 0)
			{
				this.currentDisplayIndex = this.currentFileList.Count - 1;
			}
			else
			{
				this.currentDisplayIndex -= 1;
			}
			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}


		//
		// end user events
		//



		/// <summary>
		/// Displays a specific file or folder.
		/// Automatically determines whether <paramref name="path"/> is a file or directory.
		/// This is for when a specific file or folder should be displayed, either because it's the process startup argument, or because of drag and drop.
		/// </summary>
		public void SetDisplayPath_NoThrowInteractive(string path)
		{
			if (path == null)
			{
				return;
			}
			try
			{
				path = Path.GetFullPath(path);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				MessageBox.Show(ex.Message);
				return;
			}

			string dir, displayFile;
			// This is bad, but we're going to have a race anyway.
			if (File.Exists(path))
			{
				dir = Path.GetDirectoryName(path);
				displayFile = path;
			}
			else if (Directory.Exists(path))
			{
				dir = path;
				displayFile = null;
			}
			else
			{
				string parent = Path.GetDirectoryName(path);
				if (!Directory.Exists(parent))
				{
					MessageBox.Show("Specified file doesn't exist: " + path);
					return;
				}

				dir = parent;
				displayFile = null;
			}

			try
			{
				this.currentDisplayDir = dir;
				this.UpdateDisplayPath(scrollSelectedItemIntoView: true, displayFile);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				MessageBox.Show(ex.Message);
				return;
			}
		}

		// Needed for Refresh.
		private string currentDisplayDir;
		// currentFlieList may be null, and the display index may be invalid.
		private List<FileListEntry> currentFileList;
		private int currentDisplayIndex;

		/// <summary>
		/// This assumes that <see cref="currentDisplayDir"/> has been set to its desired value.
		/// Scans <see cref="currentDisplayDir"/> for files and updates the file list.
		/// Keeps the currently selected file visible if possible.
		/// </summary>
		private void UpdateDisplayPath(bool scrollSelectedItemIntoView, string selectedFilePath)
		{
			if (this.currentDisplayDir == null)
			{
				this.currentFileList = null;
				this.UpdateCurrentFileList(scrollSelectedItemIntoView, selectedFilePath);
				return;
			}

			var files = Directory.GetFiles(this.currentDisplayDir);
#if false
			var regex = new Regex(@"(\.png$)|(\.jpg$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			var matchingFiles = files.Where(x => regex.IsMatch(x)).ToList();
#else
			List<FileListEntry> matchingFiles = files.Select(x => new FileListEntry(x)).ToList();
#endif
			this.currentFileList = matchingFiles;
			this.UpdateCurrentFileList(scrollSelectedItemIntoView, selectedFilePath);
		}

		private sealed class FileListComparer : IComparer<FileListEntry>
		{
			public int Compare(FileListEntry x, FileListEntry y)
			{
				return NatnumSort.Sort(x.FullPath, y.FullPath, !Settings.Instance.SortCaseSensitive);
			}
		}

		private readonly FileListComparer fileListComparer = new FileListComparer();

		/// <summary>
		/// Call this after <see cref="currentFileList"/> has been assigned, or when the sorting order has changed.
		/// Keeps the currently selected file, passed via <paramref name="selectedFilePath"/>, visible if possible.
		/// </summary>
		private void UpdateCurrentFileList(bool scrollSelectedItemIntoView, string selectedFilePath)
		{
			if (this.currentFileList != null)
			{
#if DEBUG
				var sw = Stopwatch.StartNew();
#endif
				this.currentFileList.Sort(this.fileListComparer);
#if DEBUG
				Debug.WriteLine($"Sorting took {sw.ElapsedMilliseconds} ms");
				foreach (var f in this.currentFileList)
				{
					Debug.WriteLine(f.FullPath);
				}
#endif
				
				// Try to preselect the currently displayed file, if possible.
				int displayIndex = -1;
				if (selectedFilePath != null)
				{
					for (int i = 0; i < this.currentFileList.Count; ++i)
					{
						if (String.Equals(this.currentFileList[i].FullPath, selectedFilePath, StringComparison.OrdinalIgnoreCase))
						{
							displayIndex = i;
							break;
						}
					}
				}

				this.currentDisplayIndex = displayIndex;

				this.overviewControl.Initialize(this.currentFileList);
			}

			this.DisplayCurrent(scrollSelectedItemIntoView);
		}




		


		/// <summary>
		/// Displays <see cref="currentDisplayIndex"/>, so this must be called after that value changes.
		/// If <see cref="currentFileList"/> is null, or if it doesn't contain the specified index, no image can be displayed.
		/// </summary>
		private void DisplayCurrent(bool scrollSelectedItemIntoView)
		{
			if (this.currentFileList == null || this.currentDisplayIndex == -1 || this.currentDisplayIndex >= this.currentFileList.Count)
			{
				// TODO this isn't ideal because it requires that displaying a file requires that it is in a browsable directory. It should be possible to display an item by its full path alone. But that makes rename extremely complicated because it would have to rename the direct display path too.
				this.overviewControl.SetDisplayIndex(-1, false);

				this.wantedImageHandle = null;
				this.DisplayWantedImage();

				this.imageCacheWorker.SetCacheWorkItem(new CacheWorkItem(null, null));
			}
			else
			{
				this.overviewControl.SetDisplayIndex(this.currentDisplayIndex, scrollSelectedItemIntoView);
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				// Update window title with the current file name:
				this.UpdateWindowTitle();

				ImageContainer displayedContainer = this.imageCacheWorker.GetOrCreateContainer(displayFile);
				if (this.wantedImageHandle?.Container != displayedContainer)
				{
					this.wantedImageHandle?.Dispose();
					this.wantedImageHandle = displayedContainer.AddHandle();
				}

				if (this.wantedImageHandle.IsLoaded)
				{
					// We can display this item immediately, rather than waiting for the event from the ImageCache worker.
					this.DisplayWantedImage();
				}

				// This makes sure we preload the files around the current file.
				this.imageCacheWorker.SetCacheWorkItem(this.GetCacheWorkItemForCurrentDisplayIndex());
			}
		}

		private void UpdateWindowTitle()
		{
			if (this.TryGetFile(this.currentDisplayIndex, out var displayFile))
			{
				this.Text = String.Format("{0} ({1})", Path.GetFileName(displayFile.FullPath), displayFile.FullPath);
			}
			else
			{
				this.Text = this.StartupInfo.FriendlyAppName;
			}
		}

		static readonly int[] fileLoadOrder = new int[] { 0, 1, -1, 2, -2 };

		private CacheWorkItem GetCacheWorkItemForCurrentDisplayIndex()
		{
			var items = new FileListEntry[fileLoadOrder.Length];
			var list = this.currentFileList;
			for (int i = 0; i < items.Length; ++i)
			{
				int offset = fileLoadOrder[i];
				int index = this.currentDisplayIndex + offset;
				int tmp_wrapped_around = index % list.Count;
				if (tmp_wrapped_around < 0)
				{
					tmp_wrapped_around += list.Count;
				}
				items[i] = list[tmp_wrapped_around];
			}
			return new CacheWorkItem(items[0], items);
		}


		/// <summary>
		/// The image we *want* to display. This is not necessarily the currently displayed image container because
		/// it might not be loaded yet.
		/// <para>While it is not loaded yet, the previous image container (if any) continues to be displayed instead.</para>
		/// </summary>
		private ImageHandle wantedImageHandle;

		private void HandleImageCacheDisplayItemLoaded(ImageContainer loadedImageContainer)
		{
			// The wanted image might have changed already. Even though DisplayWantedImage will check this, we can avoid
			// posting to the UI thread if we already know that it won't work.
			if (this.wantedImageHandle.Container == loadedImageContainer)
			{
				this.synchronizationContext.Post(this.displayWantedImageDelegate, null);
			}
		}

		private ImageHandle displayedHandle;

		/// <summary>
		/// Call this to display <see cref="wantedImageHandle"/> (if it is loaded).
		/// <para>It will display it if possible and set <see cref="displayedHandle"/> accordingly.</para>
		/// <para>There is no point calling this if <see cref="wantedImageHandle"/> is not loaded yet.</para>
		/// </summary>
		private void DisplayWantedImage()
		{
			if (this.wantedImageHandle?.IsLoaded == false || this.wantedImageHandle?.Container == this.displayedHandle?.Container)
			{
				return;
			}

			this.displayedHandle?.Dispose();
			this.displayedHandle = this.wantedImageHandle?.Container.AddHandle();

#if DEBUG
			if (this.displayedHandle?.Image != null && this.mainImageControl.Image == this.displayedHandle?.Image)
			{
				Debug.WriteLine("Displaying the same image again #FIXME");
			}
#endif

			this.mainImageControl.Image = this.displayedHandle?.Image;
			if (Settings.Instance.UseCurrentImageAsWindowIcon)
			{
				this.UpdateWindowIcon();
			}
		}

		/// <summary>
		/// For reduced memory allocation overhead in <see cref="HandleImageCacheDisplayItemLoaded"/>.
		/// </summary>
		private readonly SendOrPostCallback displayWantedImageDelegate;

		private void UpdateWindowIcon()
		{
			if (this.mainImageControl.Image != null)
			{
				try
				{
#if DEBUG
					var sw = Stopwatch.StartNew();
#endif
					using (var bitmap = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
					{
						using (var g = Graphics.FromImage(bitmap))
						{
							g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
							RectangleF rect = Util.ResizeProportionalFit(bitmap.Size, this.mainImageControl.Image.Size);
							g.DrawImage(this.mainImageControl.Image, rect);
							g.DrawRectangle(HalfTransparentBlackPen, rect.Left + 0.5f, rect.Top + 0.5f, rect.Width - 1.0f, rect.Height - 1.0f);
						}
						var icon_builder = new IconBuilder();
						icon_builder.IconImages.Add(IconBuilder.IconImage.FromBitmap(bitmap));
						this.Icon = icon_builder.ConvertToIcon();
					}
#if DEBUG
					Debug.WriteLine($"Icon generation: {sw.ElapsedMilliseconds}");
#endif
				}
				catch (Exception ex)
				{
					// Not sure what kind of exception could happen here, but I don't trust the Icon(Stream) constructor.
					Debug.WriteLine(ex);
					this.Icon = Properties.Resources.generic_picture;
				}
			}
			else
			{
				this.Icon = Properties.Resources.generic_picture;
			}
		}

		private static readonly Pen HalfTransparentBlackPen = new Pen(Color.FromArgb(128, 0, 0, 0));


		private readonly SendOrPostCallback refreshOverviewDelegate;
		private void HandleImageCacheWorkItemCompleted()
		{
			// All we need to do here is redraw the overview control because the file state
			// of some files other than the current display index might have changed.
			this.synchronizationContext.Post(this.refreshOverviewDelegate, null);
		}

		private void RefreshOverview()
		{
			this.overviewControl.Invalidate();
		}

		private void HandleMenuStash(object sender, EventArgs e)
		{
			this.ShowStashOptionsDialog();
		}

		private void ShowStashOptionsDialog()
		{
			var sw = Stopwatch.StartNew();
			var stashInfoList = StashHelper.Stash();
			Debug.WriteLine($"Stashing took {sw.ElapsedMilliseconds} ms");
			using (var form = new StashOptionsForm(stashInfoList, "Select the windows which should be saved in the stash file."))
			{
				form.Font = this.Font;
				if (form.ShowDialog() != DialogResult.OK) return;
				stashInfoList = form.SelectedItems;
				Debug.Assert(stashInfoList != null);
			}

			using (var dialog = new SaveFileDialog())
			{
				dialog.Title = "Save windows to stash file";
				dialog.Filter = "Image Viewer Stash|*.stash|All files|*.*";
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					var fileName = dialog.FileName;
					StashHelper.SaveStash(fileName, stashInfoList);
				}
			}
		}

		private void HandleMenuRestoreFromStash(object sender, EventArgs e)
		{
			this.ShowRestoreFromStashDialog();
		}

		private void ShowRestoreFromStashDialog()
		{
			List<StashInfo> stash;
			using (var dialog = new OpenFileDialog())
			{
				dialog.Title = "Restore windows from stash file";
				dialog.Filter = "Image Viewer Stash|*.stash|All files|*.*";
				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				string fileName = dialog.FileName;
				stash = StashHelper.LoadStash(fileName);
			}

			if (stash == null)
			{
				MessageBox.Show("The stash file cannot be loaded because it doesn't seem to be a valid image stash file.");
				return;
			}

			using (var form = new StashOptionsForm(stash, "Select the windows which should be restored from the stash file."))
			{
				form.Font = this.Font;
				if (form.ShowDialog() != DialogResult.OK) return;
				stash = form.SelectedItems;
				Debug.Assert(stash != null);
			}

			if (!StashHelper.RestoreStash(stash, out var failedItems))
			{
				Debug.Assert(failedItems != null);
				MessageBox.Show("For one reason or another, it was not possible to restore the following items from the stash:" + Environment.NewLine + String.Join(Environment.NewLine, failedItems));
			}
		}
	}


	public class FileListEntry
	{
		public FileListEntry(string fullPath)
		{
			this.FullPath = fullPath;
		}

		// NOTE: This can change if the file gets renamed.
		public string FullPath { get; set; }
		public LastFileStatus LastFileStatus { get; set; }
	}


	public enum LastFileStatus
	{
		Unknown,
		OK,
		Error,
		NotAnImageFile
	}


	public enum MouseWheelMode
	{
		NextPrevious,
		/// <summary>
		/// Zoom while in image, scroll while in file list.
		/// </summary>
		ZoomAndScroll
	}
}
