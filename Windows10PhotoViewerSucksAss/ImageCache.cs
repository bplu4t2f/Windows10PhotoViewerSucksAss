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

		internal CacheWorkItem current_work_item;

		/// <summary>
		/// Deletes all unused containers where the work item does not match <paramref name="current_work_item"/>.
		/// Must be called after <see cref="ImageContainer.last_requesting_work_item"/> has been set for all elements that should remain.
		/// </summary>
		internal void PruneUnusedContainers(CacheWorkItem current_work_item)
		{
			lock (this.cache)
			{
				this.current_work_item = current_work_item;
				foreach (ImageContainer container in this.cache.Values.ToArray())
				{
					if (container.last_requesting_work_item != this.current_work_item && !container.IsHandleAlive)
					{
						this.DisposeContainer(container);
					}
				}
			}
		}

		private void DisposeContainer(ImageContainer container)
		{
			Debug.Assert(Monitor.IsEntered(this.cache));
			Debug.Assert(!container.IsHandleAlive);
			Debug.Assert(container.last_requesting_work_item != this.current_work_item);

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
				if (container.last_requesting_work_item != this.current_work_item)
				{
					this.DisposeContainer(container);
				}
			}
		}

		public void PurgeAll()
		{
			this.PruneUnusedContainers(null);
		}

		public bool ContainsKey(string key)
		{
			lock (this.cache)
			{
				return this.cache.ContainsKey(key);
			}
		}
	}

	public class CacheWorkItem
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

		/// <summary>
		/// Does not necessarily mean that loading was successful.
		/// If it was not successful, <see cref="ImageContainer.Image"/> will be null.
		/// </summary>
		public event Action<ImageContainer> DisplayItemLoaded;

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

		private void CacheBuildWorkerThreadProc()
		{
			while (true)
			{
				_retry:

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
				// It can be null if it wasn't one of the surrounding ones from the last time, and the GUI
				// decided that it doesn't want it anymore after we started the work item.
				if (displayedImageContainer != null)
				{
					displayedImageContainer.last_requesting_work_item = item;
					if (!displayedImageContainer.IsLoaded)
					{
						this.LoadContainer(displayedImageContainer);
					}
				}

				if (this.cacheWorkWait.IsSet)
				{
					goto _retry;
				}

				// displayedImageContainer.Image can still be null if the file is not a valid image.
				this.DisplayItemLoaded?.Invoke(displayedImageContainer);

				foreach (var key in item.SurroundingPaths)
				{
					if (this.cacheWorkWait.IsSet)
					{
						goto _retry;
					}
					var container = this.imageCache.GetOrCreateContainer(key);
					container.last_requesting_work_item = item;
					if (!container.IsLoaded)
					{
						this.LoadContainer(container);
					}
				}


				if (this.cacheWorkWait.IsSet)
				{
					goto _retry;
				}

				// Prune old containers
				// NOTE: We must do this after we have set last_requesting_work_item on each container.
				this.imageCache.PruneUnusedContainers(item);
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
		internal CacheWorkItem last_requesting_work_item;

		public Image Image => this.image;
		public bool IsLoaded { get; private set; }

		private int openHandleCount;

		public bool IsHandleAlive => this.openHandleCount > 0;

		internal ImageHandle AddHandle()
		{
			var handle = new ImageHandle(this);
			this.openHandleCount += 1;
			return handle;
		}

		internal void RemoveHandle()
		{
			this.openHandleCount -= 1;
			if (!this.IsHandleAlive)
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
			Debug.Assert(container != null);
			this.Container = container;
		}

		public ImageContainer Container { get; private set; }

		public string Key => this.Container.Key;
		public Image Image => this.Container.Image;
		public bool IsLoaded => this.Container.IsLoaded;

		public void Dispose()
		{
			this.Container?.RemoveHandle();
			this.Container = null;
		}
	}
}
