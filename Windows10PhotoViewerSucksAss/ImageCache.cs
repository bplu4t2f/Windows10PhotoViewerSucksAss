using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

		public ImageHandle GetOrCreateHandle(string key)
		{
			lock (this.cache)
			{
				ImageContainer container;
				if (!this.cache.TryGetValue(key, out container))
				{
					container = new ImageContainer(this, key);
					this.cache.Add(key, container);
				}
				var handle = container.AddHandle();
				return handle;
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

	public sealed class ImageHandle : IDisposable
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
