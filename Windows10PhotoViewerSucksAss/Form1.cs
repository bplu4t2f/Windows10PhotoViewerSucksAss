using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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

			this.mainImageControl = new MainImageControl();
			this.mainImageControl.Dock = DockStyle.Fill;

			this.overviewControl = new OverviewControl();
			this.overviewControl.Dock = DockStyle.Left;

			this.SuspendLayout();
			this.Controls.Add(this.mainImageControl);
			this.Controls.Add(this.overviewControl);
			this.ResumeLayout();

			this.overviewControl.ImageSelected += this.OverviewControl_ImageSelected;

			this.AllowDrop = true;
		}

		private readonly MainImageControl mainImageControl;
		private readonly OverviewControl overviewControl;
		private Thread cacheBuildWorker;

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.cacheBuildWorker = new Thread(() => this.CacheBuildWorkerThreadProc());
			this.cacheBuildWorker.IsBackground = true;
			this.cacheBuildWorker.Start();
		}

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

		private void OverviewControl_ImageSelected(object sender, int e)
		{
			this.currentDisplayIndex = Math.Min(Math.Max(e, 0), this.currentFileList.Count - 1);
			this.DisplayCurrent(scrollSelectedItemIntoView: false);
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
				// explore to
				try
				{
					var displayFile = this.currentFileList[this.currentDisplayIndex];
					// TODO use the SH file operation stuff maybe?
					Process.Start("explorer.exe", $"/select,\"{displayFile}\"");
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
				return true;
			}
			else if (keyData == Keys.F)
			{
				// copy file path
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				Clipboard.SetText(displayFile);
				return true;
			}
			else if (keyData == Keys.C)
			{
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { displayFile });
				return true;
			}
			// TODO F5 refresh
			else
			{
				return base.ProcessCmdKey(ref msg, keyData);
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
			catch (FileNotFoundException)
			{
				MessageBox.Show("Specified file doesn't exist: " + path);
				return;
			}
			catch (Exception ex)
			{
				MessageBox.Show("Cannot access requested file: " + ex.Message);
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
