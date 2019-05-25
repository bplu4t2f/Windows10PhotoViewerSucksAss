﻿using System;
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
	// TODO left pane resizable
	// TODO f1 help menu overlay
	// TODO f2 rename
	// TODO let's have an icon I guess
	// TODO "shell" menu
	// TODO file associations
	// TODO "save user settings" check box
	// TODO option to save user settings with application exe
	// TODO show save error upon switching save mode, if applicable
	// TODO reset user settings button
	// TODO other media files
	// TODO refresh should reload the image
	// TODO file system watcher
	// TODO choose extensions
	// TODO move/copy targets, with optional counter
	// TODO file list colors
	// TODO custom scroll bar colors
	// TODO save session (use WM_APP messages with EnumWindows for communication)

	public class Form1 : Form
	{
		public Form1()
		{
			this.ApplyDefaultSettings();
			this.ApplyUserSettings();

			this.optionsButton.Text = "Options...";
			this.optionsButton.Width = this.overviewControl.Width;
			this.optionsButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

			this.overviewControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			this.overviewControl.Height = this.ClientSize.Height - this.optionsButton.Height;

			this.mainImageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			this.mainImageControl.Left = this.overviewControl.Width;
			this.mainImageControl.Size = new Size(this.ClientSize.Width - this.overviewControl.Width, this.ClientSize.Height);

			this.SuspendLayout();
			this.Controls.Add(this.optionsButton);
			this.Controls.Add(this.mainImageControl);
			this.Controls.Add(this.overviewControl);
			this.ResumeLayout();

			this.SetOptionsButtonHeight();

			this.overviewControl.ImageSelected += this.OverviewControl_ImageSelected;
			this.optionsButton.Click += this.HandleOptionButtonClick;

			this.AllowDrop = true;

			this.mi_file_name = this.fileListContextMenu.MenuItems.Add(String.Empty);
			this.mi_file_name.Enabled = false;
			var mi_show = this.fileListContextMenu.MenuItems.Add("Show");
			mi_show.Click += this.HandleMenuShow;
			var mi_zoom_to_fit = this.fileListContextMenu.MenuItems.Add("Zoom to fit (T)");
			mi_zoom_to_fit.Click += this.HandleMenuZoomToFit;
			this.fileListContextMenu.MenuItems.Add("-"); // separator
			var mi_explore_to = this.fileListContextMenu.MenuItems.Add("Explore to (&E)");
			mi_explore_to.Click += this.HandleMenuExploreTo;
			var mi_copy_full_path = this.fileListContextMenu.MenuItems.Add("Copy full path (&F)");
			mi_copy_full_path.Click += this.HandleMenuCopyFullPath;
			var mi_copy_file = this.fileListContextMenu.MenuItems.Add("Copy file (&C)");
			mi_copy_file.Click += this.HandleMenuCopyFile;
			var mi_cut_file = this.fileListContextMenu.MenuItems.Add("Cut file (&X)");
			mi_cut_file.Click += this.HandleMenuCutFile;
			var mi_fork = this.fileListContextMenu.MenuItems.Add("Fork (&G)");
			mi_fork.Click += this.HandleMenuFork;
			var mi_file_properties = this.fileListContextMenu.MenuItems.Add("File properties (&P)");
			mi_file_properties.Click += this.HandleMenuFileProperties;
			var mi_refresh_files = this.fileListContextMenu.MenuItems.Add("Refresh files (F5)");
			mi_refresh_files.Click += this.HandleMenuRefreshFiles;
			var mi_delete_file = this.fileListContextMenu.MenuItems.Add("Move to Recycle Bin (Del)");
			mi_delete_file.Click += this.HandleMenuDeleteFile;

			this.imageCacheWorker.NotFound += this.HandleImageCacheItemNotFound;
			this.imageCacheWorker.DisplayItemLoaded += this.HandleImageCacheDisplayItemLoaded;
			this.imageCacheWorker.StartWorkerThread();
		}

		private readonly ContextMenu fileListContextMenu = new ContextMenu();
		private readonly MenuItem mi_file_name;

		private readonly Button optionsButton = new Button();
		private readonly MainImageControl mainImageControl = new MainImageControl();
		private readonly OverviewControl overviewControl = new OverviewControl();

		private readonly ImageCacheWorker imageCacheWorker = new ImageCacheWorker();

		protected override void OnClosing(CancelEventArgs e)
		{
			Settings.WaitSaveCompleted();
			base.OnClosing(e);
		}

		private void ApplyDefaultSettings()
		{
			this.Size = new Size(960, 640);
			this.mainImageControl.BackColor = Color.FromArgb(32, 64, 96);
		}

		private void ApplyUserSettings()
		{
			try
			{
				if (Settings.Instance.Color != 0)
				{
					this.mainImageControl.BackColor = Color.FromArgb(Settings.Instance.Color);
				}
				if (Settings.Instance.WindowWidth != 0)
				{
					this.Width = Settings.Instance.WindowWidth;
				}
				if (Settings.Instance.WindowHeight != 0)
				{
					this.Height = Settings.Instance.WindowHeight;
				}
				if (Settings.Instance.ApplicationFont != null)
				{
					var font = Settings.Instance.ApplicationFont.ToFont();
					this.Font = font;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private void SetOptionsButtonHeight()
		{
			int lineHeight = this.optionsButton.Font.Height;
			this.optionsButton.Height = lineHeight + 10;
			this.optionsButton.Top = this.ClientRectangle.Height - this.optionsButton.Height;
		}


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

		private void OverviewControl_ImageSelected(object sender, ImageSelectionEventArgs e)
		{
			int index = e.Index;

			if (e.RightClick)
			{
				if (!this.TryGetFile(index, out string filePath))
				{
					return;
				}
				this.menuItemFileIndex = index;
				this.mi_file_name.Text = Path.GetFileName(filePath);
				this.fileListContextMenu.Show(this.overviewControl, e.ClickLocation);
			}
			else
			{
				int effectiveIndex = Math.Min(Math.Max(index, 0), this.currentFileList.Count - 1);
				this.currentDisplayIndex = effectiveIndex;
				this.SetCurrentDisplayFileFromCurrentDisplayIndex();
				this.DisplayCurrent(scrollSelectedItemIntoView: false);
			}
		}

		private SettingsForm currentSettingsForm;

		private static void CenterControl(Control container, Control content)
		{
			content.Location = new Point(
				(int)(container.Left + (container.Width - content.Width) / 2.0),
				(int)(container.Top + (container.Height - content.Height) / 2.0)
				);
		}

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
			CenterControl(this, form);
			form.Show();
		}

		public bool Setting_SortCaseSensitive
		{
			get { return Settings.Instance.SortCaseSensitive; }
			set
			{
				Settings.Instance.SortCaseSensitive = value;
				this.UpdateCurrentFileList(scrollSelectedItemIntoView: false);
			}
		}

		public Color Setting_BackColor
		{
			get { return this.mainImageControl.BackColor; }
			set
			{
				this.mainImageControl.BackColor = value;
				Settings.Instance.Color = value.ToArgb();
				Settings.QueueSave();
			}
		}

		public Font Setting_Font
		{
			get { return this.Font; }
			set
			{
				this.Font = value;
				if (this.currentSettingsForm != null)
				{
					this.currentSettingsForm.Font = value;
				}
				this.SetOptionsButtonHeight();
				Settings.Instance.ApplicationFont = new FontDescriptor(value);
				Settings.QueueSave();
			}
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
			else
			{
				return base.ProcessCmdKey(ref msg, keyData);
			}
		}

		private int menuItemFileIndex;

		private void HandleMenuShow(object sender, EventArgs e)
		{
			this.currentDisplayIndex = this.menuItemFileIndex;
			this.SetCurrentDisplayFileFromCurrentDisplayIndex();
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

		private void HandleMenuDeleteFile(object sender, EventArgs e)
		{
			this.DeleteFile(this.menuItemFileIndex);
		}

		private void HandleMenuZoomToFit(object sender, EventArgs e)
		{
			this.mainImageControl.ZoomToFit();
		}

		private bool TryGetFile(int index, out string file)
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
			if (!this.TryGetFile(fileIndex, out string displayFile))
			{
				return;
			}
			try
			{
				int hresult = FileIO.SelectInFileExplorer(displayFile);
				System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hresult);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void CopyFullPath(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out string file))
			{
				try
				{
					Clipboard.SetText(file);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void CopyFile(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out string file))
			{
				try
				{
					Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { file });
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void CutFile(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out string file))
			{
				Util.ClipboardCutFileList(new string[] { file });
			}
		}

		private void DeleteFile(int fileIndex)
		{
			if (!this.TryGetFile(fileIndex, out string file))
			{
				return;
			}
			FileIO.Send(file, FileIO.FileOperationFlags.FOF_SILENT);
			// Who knows that that function is really doing...
			if (File.Exists(file))
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
			if (!this.TryGetFile(fileIndex, out string file))
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
			this.SetCurrentDisplayFileFromCurrentDisplayIndex();
			this.overviewControl.Initialize(this.currentFileList);
			this.DisplayCurrent(scrollSelectedItemIntoView: false);
		}

		private void Fork(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out string file))
			{
				try
				{
					string exe_name = Process.GetCurrentProcess().MainModule.FileName;
					Process.Start(exe_name, $"\"{file}\"");
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
		}

		private void FileProperties(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out string file))
			{
				FileIO.ShowFileProperties(file);
			}
		}

		private void RefreshFiles()
		{
			this.UpdateDisplayPath(scrollSelectedItemIntoView: false);
		}

		private void Next()
		{
			if (this.currentDisplayIndex >= this.currentFileList.Count - 1)
			{
				this.currentDisplayIndex = 0;
			}
			else
			{
				this.currentDisplayIndex += 1;
			}
			this.SetCurrentDisplayFileFromCurrentDisplayIndex();
			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}

		private void Previous()
		{
			if (this.currentDisplayIndex <= 0)
			{
				this.currentDisplayIndex = this.currentFileList.Count - 1;
			}
			else
			{
				this.currentDisplayIndex -= 1;
			}
			this.SetCurrentDisplayFileFromCurrentDisplayIndex();
			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}


		//
		// end user events
		//



		/// <summary>
		/// Displays a specific file or folder.
		/// Automatically determines whether <paramref name="path"/> is a file or directory.
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
				this.currentDisplayFile = displayFile;
				this.UpdateDisplayPath(scrollSelectedItemIntoView: true);
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
		private List<string> currentFileList;
		// Full path to the file that is currently being displayed. This must be synchronized with currentDisplayIndex.
		// Requried because the sorting order in the list may change. In that case, the selected file's display index may change.
		private string currentDisplayFile;
		private int currentDisplayIndex;

		/// <summary>
		/// Call this when assigning <see cref="currentDisplayIndex"/> to update <see cref="currentDisplayFile"/>.
		/// </summary>
		private void SetCurrentDisplayFileFromCurrentDisplayIndex()
		{
			this.TryGetFile(this.currentDisplayIndex, out string file);
			this.currentDisplayFile = file;
		}
		
		/// <summary>
		/// This assumes that <see cref="currentDisplayDir"/> and <see cref="currentDisplayFile"/> have been set to their desired values.
		/// Scans <see cref="currentDisplayDir"/> for files and updates the file list.
		/// </summary>
		private void UpdateDisplayPath(bool scrollSelectedItemIntoView)
		{
			if (this.currentDisplayDir == null)
			{
				this.currentFileList = null;
				this.UpdateCurrentFileList(scrollSelectedItemIntoView);
				return;
			}

			var files = Directory.GetFiles(this.currentDisplayDir);
#if false
			var regex = new Regex(@"(\.png$)|(\.jpg$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			var matchingFiles = files.Where(x => regex.IsMatch(x)).ToList();
#else
			List<string> matchingFiles = files.ToList();
#endif
			this.currentFileList = matchingFiles;
			this.UpdateCurrentFileList(scrollSelectedItemIntoView);
		}

		/// <summary>
		/// Call this after <see cref="currentFileList"/> has been assigned, or when the sorting order has changed.
		/// </summary>
		private void UpdateCurrentFileList(bool scrollSelectedItemIntoView)
		{
			if (this.currentFileList != null)
			{
#if DEBUG
				var sw = Stopwatch.StartNew();
#endif
				this.currentFileList.Sort(Settings.Instance.SortCaseSensitive ? NatnumSort.Instance_CaseSensitive : NatnumSort.Instance_CaseInsensitive);
#if DEBUG
				Debug.WriteLine($"Sorting took {sw.ElapsedMilliseconds} ms");
				foreach (var f in this.currentFileList)
				{
					Debug.WriteLine(f);
				}
#endif

				var displayIndex = this.currentFileList.IndexOf(this.currentDisplayFile);

				if (displayIndex != -1)
				{
					this.currentDisplayIndex = displayIndex;
				}
				else
				{
					// If the file suddenly does not exist anymore, at least try to keep the currently selected index selected.
					if (this.currentDisplayIndex >= this.currentFileList.Count)
					{
						this.currentDisplayIndex = this.currentFileList.Count - 1;
					}
				}

				this.overviewControl.Initialize(this.currentFileList);
			}

			this.DisplayCurrent(scrollSelectedItemIntoView);
		}



		private void HandleImageCacheItemNotFound(string file)
		{
			this.BeginInvoke(new MethodInvoker(() =>
			{
				if (this.currentFileList == null)
				{
					return;
				}
				int index = this.currentFileList.IndexOf(file);
				this.ForgetFile(index);
				// Don't need to update the displayed image -- a HandleImageCacheDisplayItemLoaded will follow if applicable.
			}));
		}

		static readonly int[] fileLoadOrder = new int[] { 0, 1, -1, 2, -2 };

		private CacheWorkItem GetCacheWorkItemForCurrentDisplayIndex()
		{
			var items = new string[fileLoadOrder.Length];
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

		private void DisplayCurrent(bool scrollSelectedItemIntoView)
		{
			if (this.currentFileList == null || this.currentDisplayIndex == -1 || this.currentDisplayIndex >= this.currentFileList.Count)
			{
				this.overviewControl.SetDisplayIndex(-1, false);

				this.SetDisplayedImageHandle(null);

				this.imageCacheWorker.SetCacheWorkItem(new CacheWorkItem(null, null));
			}
			else
			{
				this.overviewControl.SetDisplayIndex(this.currentDisplayIndex, scrollSelectedItemIntoView);
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				this.Text = displayFile;

				ImageContainer displayedContainer = this.imageCacheWorker.GetOrCreateContainer(displayFile);
				if (displayedContainer.IsLoaded)
				{
					this.SetDisplayedImageHandle(displayedContainer.AddHandle());
				}
				else
				{
					this.pendingImageContainer = displayedContainer;
				}

				this.imageCacheWorker.SetCacheWorkItem(this.GetCacheWorkItemForCurrentDisplayIndex());
			}
		}

		private ImageContainer pendingImageContainer;
		private ImageContainer lastLoadedItem;

		private void HandleImageCacheDisplayItemLoaded(ImageContainer lastLoadedItem)
		{
			if (this.pendingImageContainer == lastLoadedItem)
			{
				this.lastLoadedItem = lastLoadedItem;
				this.BeginInvoke(new MethodInvoker(() => this.DisplayPendingImage()));
			}
		}

		private void DisplayPendingImage()
		{
			if (this.pendingImageContainer == this.lastLoadedItem)
			{
				this.SetDisplayedImageHandle(this.lastLoadedItem.AddHandle());
			}
		}

		private ImageHandle displayedHandle;

		private void SetDisplayedImageHandle(ImageHandle handle)
		{
			if (handle?.IsLoaded == false || handle == this.displayedHandle)
			{
				return;
			}
			this.displayedHandle?.Dispose();
			this.displayedHandle = handle;

			this.mainImageControl.Image = this.displayedHandle?.Image;
		}
	}
}
