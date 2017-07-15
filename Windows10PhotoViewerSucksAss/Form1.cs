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

		private readonly Dictionary<string, ImageContainer> imageCache = new Dictionary<string, ImageContainer>();
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
						foreach (var img in this.imageCache.Values)
						{
							img.InitialHandle.Dispose();
						}
						this.imageCache.Clear();
						continue;
					}

					ImageContainer imageContainer;
					if (!this.imageCache.TryGetValue(item.DisplayPath, out imageContainer))
					{
						try
						{
							var image = Image.FromFile(item.DisplayPath);
							imageContainer = new ImageContainer(image);
							this.imageCache.Add(item.DisplayPath, imageContainer);
						}
						catch (Exception ex)
						{
							Debug.WriteLine(ex.ToString());
						}
					}

					if (imageContainer != null)
					{
						var displayedImageHandle = imageContainer.CreateHandle();
						this.BeginInvoke(new MethodInvoker(() => this.DisplayAction(displayedImageHandle)));
					}
					
					var surroundingFiles = new List<string>();
					for (int i = -2; i <= 2; ++i)
					{
						var tmp = this.currentDisplayIndex + i;
						if (tmp < 0 || tmp >= this.currentFileList.Count)
						{
							continue;
						}
						surroundingFiles.Add(this.currentFileList[tmp]);
					}

					foreach (var k in this.imageCache.Keys.ToList())
					{
						if (!surroundingFiles.Contains(k))
						{
							this.imageCache[k].InitialHandle.Dispose();
							this.imageCache.Remove(k);
						}
					}
					foreach (var k in surroundingFiles)
					{
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

				lock (this.sync)
				{
					this.cacheWorkItem = new CacheWorkItem(displayFile);
					this.cacheWorkWait.Set();
				}
			}
		}

		private void DisplayAction(ImageHandle image)
		{
			this.mainImageControl.ImageHandle = image;
		}
	}
}
