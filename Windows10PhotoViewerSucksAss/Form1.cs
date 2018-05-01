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
			InitializeComponent();
			this.mainImageControl = new MainImageControl();
			this.mainImageControl.Dock = DockStyle.Fill;
			this.Controls.Add(this.mainImageControl);

			this.overviewControl = new OverviewControl();
			this.overviewControl.Dock = DockStyle.Left;
			this.Controls.Add(this.overviewControl);

			this.overviewControl.ImageSelected += this.OverviewControl_ImageSelected;

			this.AllowDrop = true;

			this.KeyPreview = true;
		}

		private readonly MainImageControl mainImageControl;
		private readonly OverviewControl overviewControl;
		private Thread cacheBuildWorker;
		private readonly CancellationTokenSource cacheBuildCancellation = new CancellationTokenSource();
		private bool reallyClose;

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.cacheBuildWorker = new Thread(() => this.CacheBuildWorkerThreadProc(this.cacheBuildCancellation.Token));
			this.cacheBuildWorker.Start();
		}

		protected override async void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			if (this.reallyClose)
			{
				return;
			}

			e.Cancel = true;
			this.cacheBuildCancellation.Cancel();
			await Task.Run(() => this.cacheBuildWorker.Join(3500));
			this.reallyClose = true;
			this.Close();
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
			this.DisplayCurrent();
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

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.S || e.KeyData == Keys.D)
			{
				this.Next();
			}
			else if (e.KeyCode == Keys.W || e.KeyData == Keys.A)
			{
				this.Previous();
			}
			else if (e.KeyCode == Keys.E)
			{
				// explore to
				try
				{
					var displayFile = this.currentFileList[this.currentDisplayIndex];
					Process.Start("explorer.exe", $"/select,\"{displayFile}\"");
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
				}
			}
			else if (e.KeyCode == Keys.F)
			{
				// copy file path
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				Clipboard.SetText(displayFile);
			}
			else if (e.KeyCode == Keys.C)
			{
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection() { displayFile });
			}
			else
			{
				base.OnKeyDown(e);
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
			this.DisplayCurrent();
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
			this.DisplayCurrent();
		}

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
			path = fileInfo.FullName;
			var attributes = File.GetAttributes(path);
			if ((attributes & FileAttributes.Directory) != 0)
			{
				this.SetDisplayPath2(path, null);
			}
			else
			{
				var dir = Path.GetDirectoryName(path);
				this.SetDisplayPath2(dir, path);
			}
		}

		private IList<string> currentFileList;
		private int currentDisplayIndex;

		private void SetDisplayPath2(string dir, string displayFile)
		{
			var files = Directory.GetFiles(dir);
#if false
			var regex = new Regex(@"(\.png$)|(\.jpg$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			var matchingFiles = files.Where(x => regex.IsMatch(x)).ToList();
#else
			var matchingFiles = files.ToList();
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

			this.DisplayCurrent();
		}

		private readonly ImageCache imageCache = new ImageCache();
		private CacheWorkItem cacheWorkItem;
		private readonly ManualResetEventSlim cacheWorkWait = new ManualResetEventSlim();
		private readonly object sync = new object();

		private struct CacheWorkItem
		{
			public CacheWorkItem(string displayPath)
			{
				this.DisplayPath = displayPath;
			}

			public string DisplayPath { get; }
		}

		private void CacheBuildWorkerThreadProc(CancellationToken ct)
		{
			try
			{
				while (true)
				{
					this.cacheWorkWait.Wait(ct);
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

					ImageHandle displayedImageHandle = this.imageCache.TryGetHandle(item.DisplayPath);
					if (displayedImageHandle == null)
					{
						try
						{
							var image = Image.FromFile(item.DisplayPath);
							var newImageContainer = new ImageContainer(image);
							displayedImageHandle = newImageContainer.CreateHandle();
							this.imageCache.Add(item.DisplayPath, newImageContainer);
						}
						catch (Exception ex)
						{
							Debug.WriteLine(ex.ToString());
						}
					}

					// It can be null if the file is not a valid image.
					this.BeginInvoke(new MethodInvoker(() => this.DisplayAction(displayedImageHandle)));
					
					var surroundingFiles = new List<string>();
					for (int i = -2; i <= 2; ++i)
					{
						var tmp = this.currentDisplayIndex + i;
						var tmp_wrapped_around = tmp % this.currentFileList.Count;
						if (tmp_wrapped_around < 0)
						{
							tmp_wrapped_around += this.currentFileList.Count;
						}
						Debug.Assert(tmp_wrapped_around >= 0 && tmp_wrapped_around < this.currentFileList.Count);
						surroundingFiles.Add(this.currentFileList[tmp_wrapped_around]);
					}

					this.imageCache.Purge(surroundingFiles);

					foreach (var k in surroundingFiles)
					{
						if (this.cacheWorkWait.IsSet)
						{
							break;
						}
						if (!this.imageCache.ContainsKey(k))
						{
							try
							{
								var image = Image.FromFile(k);
								this.imageCache.Add(k, new ImageContainer(image));
							}
							catch (Exception ex)
							{
								Debug.WriteLine(ex.ToString());
							}
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private void DisplayCurrent()
		{
			if (this.currentFileList == null || this.currentDisplayIndex == -1 || this.currentDisplayIndex >= this.currentFileList.Count)
			{
				this.overviewControl.SetDisplayIndex(-1);
				this.mainImageControl.ImageHandle = null;

				lock (this.sync)
				{
					this.cacheWorkItem = new CacheWorkItem(null);
					this.cacheWorkWait.Set();
				}
			}
			else
			{
				this.overviewControl.SetDisplayIndex(this.currentDisplayIndex);
				var displayFile = this.currentFileList[this.currentDisplayIndex];
				this.Text = displayFile;

				ImageHandle existingHandle = this.imageCache.TryGetHandle(displayFile);
				if (existingHandle != null)
				{
					this.DisplayAction(existingHandle);
				}

				lock (this.sync)
				{
					this.cacheWorkItem = new CacheWorkItem(displayFile);
					this.cacheWorkWait.Set();
				}
			}
		}

		private void DisplayAction(ImageHandle image)
		{
			// Displayed image handle may be null.
			this.mainImageControl.ImageHandle = image;
		}
	}
}
