using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	public class ImageCache
	{
		public ImageCache()
		{
			this.cache = new Dictionary<string, ImageContainer>(StringComparer.OrdinalIgnoreCase);
		}

		private readonly Dictionary<string, ImageContainer> cache;

		public ImageContainer GetOrCreateContainer(string key)
		{
			lock (this.cache)
			{
				if (!this.cache.TryGetValue(key, out ImageContainer container))
				{
					container = new ImageContainer(this, key);
					this.cache.Add(key, container);
				}
				return container;
			}
		}

		public ImageContainer GetExistingContainer(string key)
		{
			lock (this.cache)
			{
				this.cache.TryGetValue(key, out ImageContainer container);
				return container;
			}
		}

		public void SetPersistent(IEnumerable<string> persistentFiles)
		{
			lock (this.cache)
			{
				foreach (var kv in this.cache.ToArray())
				{
					kv.Value.persist = persistentFiles.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
					if (!kv.Value.IsAlive)
					{
						this.DisposeContainer(kv.Value);
					}
				}
				foreach (var key in persistentFiles)
				{
					if (!this.cache.ContainsKey(key))
					{
						var container = new ImageContainer(this, key);
						container.persist = true;
						this.cache.Add(key, container);
					}
				}
			}
		}

		private void DisposeContainer(ImageContainer container)
		{
			Debug.Assert(Monitor.IsEntered(this.cache));
			container.image?.Dispose();
			container.image = null;
			// We can't leave the lock in this state - we must remove it from the dictionary
			// immediately after we dispose of the initial handle.
			this.cache.Remove(container.Key);
		}

		internal void UnregisterContainer(ImageContainer container)
		{
			Debug.Assert(!Monitor.IsEntered(this.cache));
			lock (this.cache)
			{
				this.DisposeContainer(container);
			}
		}

		public void PurgeAll()
		{
			this.SetPersistent(new string[0]);
		}

		public bool ContainsKey(string key)
		{
			lock (this.cache)
			{
				return this.cache.ContainsKey(key);
			}
		}
	}

	public struct CacheWorkItem
	{
		public CacheWorkItem(string displayPath, string[] surroundingPaths)
		{
			this.DisplayPath = displayPath;
			this.SurroundingPaths = surroundingPaths;
		}

		public string DisplayPath { get; }
		public string[] SurroundingPaths { get; }
	}

	public class ImageCacheWorker
	{
		private readonly ImageCache imageCache = new ImageCache();
		private readonly ManualResetEventSlim cacheWorkWait = new ManualResetEventSlim();
		private readonly object sync = new object();
		private Thread cacheBuildWorker;
		private CacheWorkItem cacheWorkItem;

		public event Action<ImageContainer> DisplayItemLoaded;
		public event Action<string> NotFound;

		public void StartWorkerThread()
		{
			Debug.Assert(this.cacheBuildWorker == null);
			this.cacheBuildWorker = new Thread(() => this.CacheBuildWorkerThreadProc());
			this.cacheBuildWorker.IsBackground = true;
			this.cacheBuildWorker.Start();
		}

		public void SetCacheWorkItem(CacheWorkItem item)
		{
			lock (this.sync)
			{
				this.cacheWorkItem = item;
				this.cacheWorkWait.Set();
			}
		}

		public ImageContainer GetOrCreateContainer(string key)
		{
			return this.imageCache.GetOrCreateContainer(key);
		}

		public bool TryGetExistingImageHandle(string key, out ImageHandle handle)
		{
			var container = this.imageCache.GetExistingContainer(key);
			if (container != null)
			{
				handle = container.AddHandle();
				return true;
			}
			else
			{
				handle = null;
				return false;
			}
		}

		private void NotFound_QueueForget(string file)
		{
			if (!File.Exists(file))
			{
				this.NotFound?.Invoke(file);
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
					this.LoadContainer(displayedImageContainer);
				}

				if (this.cacheWorkWait.IsSet)
				{
					continue;
				}

				// displayedImageContainer.Image can still be null if the file is not a valid image.
				this.DisplayItemLoaded?.Invoke(displayedImageContainer);

				this.imageCache.SetPersistent(item.SurroundingPaths);

				foreach (var key in item.SurroundingPaths)
				{
					if (this.cacheWorkWait.IsSet)
					{
						break;
					}
					var container = this.imageCache.GetExistingContainer(key);
					if (!container.IsLoaded)
					{
						this.LoadContainer(container);
					}
				}
			}
		}

		private void LoadContainer(ImageContainer container)
		{
			Debug.Assert(container != null);
			var key = container.Key;
			Debug.Assert(key != null);
			try
			{
				var image = Util.LoadImageFromFile(key);
				container.SetImage(image);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				container.SetImage(null);
				this.NotFound_QueueForget(key);
			}
			Debug.Assert(container.IsLoaded);
		}
	}

	public class ImageContainer
	{
		internal ImageContainer(ImageCache owner, string key)
		{
			this.Key = key ?? throw new ArgumentNullException(nameof(key));
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
		}

		private readonly ImageCache owner;
		public string Key { get; }

		internal Image image;
		internal bool persist;

		public Image Image => this.image;
		public bool IsLoaded { get; private set; }

		private int openHandleCount;

		public bool IsAlive => this.openHandleCount > 0 || this.persist;

		internal ImageHandle AddHandle()
		{
			var handle = new ImageHandle(this);
			this.openHandleCount += 1;
			return handle;
		}

		internal void RemoveHandle()
		{
			this.openHandleCount -= 1;
			if (!this.IsAlive)
			{
				this.owner.UnregisterContainer(this);
			}
		}

		public void SetImage(Image image)
		{
			this.image = image;
			Thread.MemoryBarrier();
			this.IsLoaded = true;
		}
	}

	public sealed class ImageHandle
	{
		internal ImageHandle(ImageContainer container)
		{
			this.container = container;
		}

		private ImageContainer container;

		public string Key => this.container.Key;
		public Image Image => this.container.Image;
		public bool IsLoaded => this.container.IsLoaded;

		public void Dispose()
		{
			this.container?.RemoveHandle();
			this.container = null;
		}
	}
}
