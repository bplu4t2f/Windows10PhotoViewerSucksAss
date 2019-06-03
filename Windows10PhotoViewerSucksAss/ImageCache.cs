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
			this.cache = new Dictionary<FileListEntry, ImageContainer>();
			this.disposeOneContainerHelperDelegate = this.DisposeOneContainerHelper;
			this.disposeManyContainersHelperDelegate = this.DisposeManyContainersHelper;
		}

		private readonly Dictionary<FileListEntry, ImageContainer> cache;

		public ImageContainer GetOrCreateContainer(FileListEntry key)
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

		public ImageContainer GetExistingContainer(FileListEntry key)
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
			var disposableContainers = new List<ImageContainer>();

			lock (this.cache)
			{
				this.current_work_item = current_work_item;
				foreach (ImageContainer container in this.cache.Values.ToArray())
				{
					if (container.last_requesting_work_item != this.current_work_item && !container.IsHandleAlive)
					{
						// Do not dispose here while we're in the lock - the Dispose might take a relatively long time.
						disposableContainers.Add(container);
						this.cache.Remove(container.Key);
					}
				}
			}

			ThreadPool.QueueUserWorkItem(this.disposeManyContainersHelperDelegate, disposableContainers);
		}

		private readonly WaitCallback disposeManyContainersHelperDelegate;
		private void DisposeManyContainersHelper(object state)
		{
			var containers = (List<ImageContainer>)state;
			Debug.Assert(containers != null);
			foreach (var container in containers)
			{
				this.DisposeContainer(container);
			}
		}

		/// <summary>
		/// Actually disposes the image in a <paramref name="container"/>.
		/// NOTE: Disposing an image might take time. So this should always be called on some low priority background worker thread.
		/// </summary>
		private void DisposeContainer(ImageContainer container)
		{
			Debug.Assert(!Monitor.IsEntered(this.cache));
			Debug.Assert(!container.IsHandleAlive);
			Debug.Assert(container.last_requesting_work_item != this.current_work_item);

			container.image?.Dispose();
			container.image = null;
		}

		/// <summary>
		/// Call this when the container's active handle count has reached 0.
		/// If the container is otherwise unused, it will be disposed.
		/// </summary>
		internal void DisposeContainerIfNecessary(ImageContainer container)
		{
			Debug.Assert(container != null);
			Debug.Assert(!Monitor.IsEntered(this.cache));
			Debug.Assert(!container.IsHandleAlive);
			bool needDispose = false;

			lock (this.cache)
			{
				if (container.last_requesting_work_item != this.current_work_item)
				{
					this.cache.Remove(container.Key);
					needDispose = true;
				}
			}

			if (needDispose)
			{
				// Dispose might actually take a long time if it was a big image.
				ThreadPool.QueueUserWorkItem(this.disposeOneContainerHelperDelegate, container);
			}
		}

		private readonly WaitCallback disposeOneContainerHelperDelegate;
		private void DisposeOneContainerHelper(object state)
		{
			var container = (ImageContainer)state;
			Debug.Assert(container != null);
			this.DisposeContainer(container);
		}

		public void PurgeAll()
		{
			this.PruneUnusedContainers(null);
		}
	}

	public class CacheWorkItem
	{
		public CacheWorkItem(FileListEntry displayPath, FileListEntry[] surroundingPaths)
		{
			this.DisplayPath = displayPath;
			this.SurroundingPaths = surroundingPaths;
		}

		public FileListEntry DisplayPath { get; }
		public FileListEntry[] SurroundingPaths { get; }
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
		public event Action WorkItemCompleted;

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

		public ImageContainer GetOrCreateContainer(FileListEntry key)
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

				this.WorkItemCompleted?.Invoke();

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
				var image = Util.LoadImageFromFile(key.FullPath);
				key.LastFileStatus = LastFileStatus.OK;
				container.SetImage(image);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				key.LastFileStatus = LastFileStatus.Error;
				container.SetImage(null);
			}
			Debug.Assert(container.IsLoaded);
		}
	}

	public class ImageContainer
	{
		internal ImageContainer(ImageCache owner, FileListEntry key)
		{
			this.Key = key ?? throw new ArgumentNullException(nameof(key));
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
		}

		private readonly ImageCache owner;
		public FileListEntry Key { get; }

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
				this.owner.DisposeContainerIfNecessary(this);
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

		public FileListEntry Key => this.Container.Key;
		public Image Image => this.Container.Image;
		public bool IsLoaded => this.Container.IsLoaded;

		public void Dispose()
		{
			this.Container?.RemoveHandle();
			this.Container = null;
		}
	}
}
