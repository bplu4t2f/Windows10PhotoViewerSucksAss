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
	public partial class Form1 : Form
	{
		public Form1()
		{
			this.Size = new Size(960, 640);

			this.optionButton.Text = "Option";
			this.optionButton.Width = this.overviewControl.Width;
			this.optionButton.Top = this.ClientRectangle.Height - this.optionButton.Height;
			this.optionButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

			this.overviewControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			this.overviewControl.Height = this.ClientSize.Height - this.optionButton.Height;

			this.mainImageControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			this.mainImageControl.Left = this.overviewControl.Width;
			this.mainImageControl.Size = new Size(this.ClientSize.Width - this.overviewControl.Width, this.ClientSize.Height);

			this.SuspendLayout();
			this.Controls.Add(this.optionButton);
			this.Controls.Add(this.mainImageControl);
			this.Controls.Add(this.overviewControl);
			this.ResumeLayout();

			this.overviewControl.ImageSelected += this.OverviewControl_ImageSelected;
			this.optionButton.Click += this.HandleOptionButtonClick;

			this.AllowDrop = true;

			this.mi_file_name = this.fileListContextMenu.MenuItems.Add(String.Empty);
			this.mi_file_name.Enabled = false;
			var mi_show = this.fileListContextMenu.MenuItems.Add("Show");
			mi_show.Click += this.HandleMenuShow;
			var mi_explore_to = this.fileListContextMenu.MenuItems.Add("Explore to (&E)");
			mi_explore_to.Click += this.HandleMenuExploreTo;
			var mi_copy_full_path = this.fileListContextMenu.MenuItems.Add("Copy full path (&F)");
			mi_copy_full_path.Click += this.HandleMenuCopyFullPath;
			var mi_copy_file = this.fileListContextMenu.MenuItems.Add("Copy file (&C)");
			mi_copy_file.Click += this.HandleMenuCopyFile;
			var mi_fork = this.fileListContextMenu.MenuItems.Add("Fork (&G)");
			mi_fork.Click += this.HandleMenuFork;
			var mi_file_properties = this.fileListContextMenu.MenuItems.Add("File properties (&G)");
			mi_file_properties.Click += this.HandleMenuFileProperties;
		}

		private readonly ContextMenu fileListContextMenu = new ContextMenu();
		private readonly MenuItem mi_file_name;
		private readonly ColorDialog colorDialog = new ColorDialog();


		private readonly Button optionButton = new Button();
		private readonly MainImageControl mainImageControl = new MainImageControl();
		private readonly OverviewControl overviewControl = new OverviewControl();
		private Thread cacheBuildWorker;

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.cacheBuildWorker = new Thread(() => this.CacheBuildWorkerThreadProc());
			this.cacheBuildWorker.IsBackground = true;
			this.cacheBuildWorker.Start();

			Settings.Manager.Load();
			try
			{
				if (Settings.Manager.Settings.Color != 0)
				{
					this.mainImageControl.BackColor = Color.FromArgb(Settings.Manager.Settings.Color);
				}
				if (Settings.Manager.Settings.WindowWidth != 0)
				{
					this.Width = Settings.Manager.Settings.WindowWidth;
				}
				if (Settings.Manager.Settings.WindowHeight != 0)
				{
					this.Height = Settings.Manager.Settings.WindowHeight;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
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
			this.BeginInvoke(new MethodInvoker(() => this.SetDisplayPath(files[0])));
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
				this.DisplayCurrent(scrollSelectedItemIntoView: false);
			}
		}

		private void HandleOptionButtonClick(object sender, EventArgs e)
		{
			this.colorDialog.FullOpen = true;
			this.colorDialog.Color = this.mainImageControl.BackColor;
			if (this.colorDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			Color color = this.colorDialog.Color;
			this.mainImageControl.BackColor = color;
			Settings.Manager.Settings.Color = color.ToArgb();
			Settings.Manager.Save();
		}

		protected override void OnResizeEnd(EventArgs e)
		{
			base.OnResizeEnd(e);
			Settings.Manager.Settings.WindowWidth = this.Width;
			Settings.Manager.Settings.WindowHeight = this.Height;
			Settings.Manager.Save();
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
			// TODO F5 refresh
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

		private void HandleMenuFork(object sender, EventArgs e)
		{
			this.Fork(this.menuItemFileIndex);
		}

		private void HandleMenuFileProperties(object sender, EventArgs e)
		{
			this.FileProperties(this.menuItemFileIndex);
		}

		private bool TryGetFile(int index, out string file)
		{
			if (index < 0 || index >= this.currentFileList.Count)
			{
				file = default(string);
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
				// TODO use the SH file operation stuff maybe?
				Process.Start("explorer.exe", $"/select,\"{displayFile}\"");
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
				Clipboard.SetText(file);
			}
		}

		private void CopyFile(int fileIndex)
		{
			if (this.TryGetFile(fileIndex, out string file))
			{
				Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { file });
			}
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
			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}


		//
		// end user events
		//



		/// <summary>
		/// Displays a specific file or folder.
		/// </summary>
		public void SetDisplayPath(string path)
		{
			FileInfo fileInfo;
			try
			{
				fileInfo = new FileInfo(path);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Cannot access requested file: " + ex.Message);
				return;
			}
			if (!fileInfo.Exists)
			{
				MessageBox.Show("Specified file doesn't exist: " + path);
				return;
			}

			path = fileInfo.FullName;
			var attributes = fileInfo.Attributes;
			string dir;
			string displayFile;
			if ((attributes & FileAttributes.Directory) != 0)
			{
				dir = path;
				displayFile = null;
			}
			else
			{
				dir = Path.GetDirectoryName(path);
				displayFile = path;
			}
			this.SetDisplayPath2(dir, displayFile);
		}

		// currentFlieList may be null, and the display index may be invalid.
		private IList<string> currentFileList;
		private int currentDisplayIndex;

		private void SetDisplayPath2(string dir, string displayFile)
		{
			var files = Directory.GetFiles(dir);
#if false
			var regex = new Regex(@"(\.png$)|(\.jpg$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			var matchingFiles = files.Where(x => regex.IsMatch(x)).ToList();
#else
			IList<string> matchingFiles = files;
#endif
			foreach (var f in matchingFiles)
			{
				Debug.WriteLine(f);
			}

			var displayIndex = matchingFiles.IndexOf(displayFile);

			if (displayIndex == -1)
			{
				displayIndex = 0;
			}

			this.currentFileList = matchingFiles;
			this.currentDisplayIndex = displayIndex;
			this.overviewControl.Initialize(this.currentFileList);

			this.DisplayCurrent(scrollSelectedItemIntoView: true);
		}

		private readonly ImageCache imageCache = new ImageCache();
		private CacheWorkItem cacheWorkItem;
		private readonly ManualResetEventSlim cacheWorkWait = new ManualResetEventSlim();
		private readonly object sync = new object();

		private struct CacheWorkItem
		{
			public CacheWorkItem(string displayPath, string[] surroundingPaths)
			{
				this.DisplayPath = displayPath;
				this.SurroundingPaths = surroundingPaths;
			}

			public string DisplayPath { get; }
			public string[] SurroundingPaths { get; }
		}

		private void SetCacheWorkItem(CacheWorkItem item)
		{
			lock (this.sync)
			{
				this.cacheWorkItem = item;
				this.cacheWorkWait.Set();
			}
		}

		private void CacheBuildWorkerThreadProc()
		{
			while (true)
			{
				this.cacheWorkWait.Wait();
				CacheWorkItem item;
				lock (this.sync)
				{
					this.cacheWorkWait.Reset();
					item = this.cacheWorkItem;
				}

				if (item.DisplayPath == null)
				{
					this.imageCache.PurgeAll();
					continue;
				}

				ImageContainer displayedImageContainer = this.imageCache.GetExistingContainer(item.DisplayPath);
				// It can be null if it wasn't one of the surrounding ones from the last time, and the GUI decided that it doesn't want it anymore after we started the work item.
				if (displayedImageContainer != null && !displayedImageContainer.IsLoaded)
				{
					try
					{
						var image = Util.LoadImageFromFile(item.DisplayPath);
						displayedImageContainer.SetImage(image);
					}
					catch (Exception ex)
					{
						Debug.WriteLine(ex.ToString());
						displayedImageContainer.SetImage(null);
					}
					Debug.Assert(displayedImageContainer.IsLoaded);
				}

				if (this.cacheWorkWait.IsSet)
				{
					continue;
				}

				// displayedImageContainer.Image can still be null if the file is not a valid image.
				this.BeginInvoke(new MethodInvoker(() => this.DisplayAction()));

				this.imageCache.SetPersistent(item.SurroundingPaths);

				foreach (var key in item.SurroundingPaths)
				{
					if (this.cacheWorkWait.IsSet)
					{
						break;
					}
					var container = this.imageCache.GetExistingContainer(key);
					if (container.Image == null)
					{
						try
						{
							var image = Util.LoadImageFromFile(key);
							container.SetImage(image);
						}
						catch (Exception ex)
						{
							Debug.WriteLine(ex.ToString());
						}
					}
				}
			}
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

				this.SetCacheWorkItem(new CacheWorkItem(null, null));
			}
			else
			{
				this.overviewControl.SetDisplayIndex(this.currentDisplayIndex, scrollSelectedItemIntoView);
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				this.Text = displayFile;

				this.SetDisplayedImageHandle(this.imageCache.GetOrCreateHandle(displayFile));

				this.SetCacheWorkItem(this.GetCacheWorkItemForCurrentDisplayIndex());
			}
		}

		private void SetDisplayedImageHandle(ImageHandle handle)
		{
			if (this.pendingHandle != this.displayedHandle)
			{
				this.pendingHandle?.Dispose();
				this.pendingHandle = null;
			}
			this.pendingHandle = handle;
			this.DisplayAction();
		}

		private ImageHandle pendingHandle;
		private ImageHandle displayedHandle;

		private void DisplayAction()
		{
			if (this.pendingHandle == this.displayedHandle)
			{
				return;
			}

			if (this.pendingHandle?.IsLoaded == false)
			{
				// Keep displaying the old image if the new one isn't loaded yet to prevent unnecessary flicker.
				return;
			}

			this.displayedHandle?.Dispose();
			this.displayedHandle = this.pendingHandle;
			this.mainImageControl.Image = this.displayedHandle?.Image;
		}
	}
}
