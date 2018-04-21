using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
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

		public ImageHandle TryGetHandle(string key)
		{
			lock (this.cache)
			{
				if (this.cache.TryGetValue(key, out var container))
				{
					var handle = container.CreateHandle();
					return handle;
				}
			}
			return null;
		}

		public void Add(string key, ImageContainer container)
		{
			lock (this.cache)
			{
				this.cache.Add(key, container);
			}
		}

		public void Purge(IEnumerable<string> keysThatShouldRemain)
		{
			foreach (var kv in this.cache.ToArray())
			{
				lock (this.cache)
				{
					if (!keysThatShouldRemain.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
					{
						kv.Value.InitialHandle.Dispose();
						// We can't leave the lock in this state - we must remove it from the dictionary
						// immediately after we dispose of the initial handle.
						this.cache.Remove(kv.Key);
					}
				}
			}
		}

		public void PurgeAll()
		{
			lock (this.cache)
			{
				foreach (var image in this.cache.Values)
				{
					image.InitialHandle.Dispose();
				}
				this.cache.Clear();
			}
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
		public ImageContainer(Image image)
		{
			this.Image = image;
			this.InitialHandle = new ImageHandle(this);
		}

		public Image Image { get; }
		public ImageHandle InitialHandle { get; }

		private int openHandleCount;

		public ImageHandle CreateHandle()
		{
			return new ImageHandle(this);
		}

		public void AddHandle()
		{
			this.openHandleCount += 1;
		}

		public void RemoveHandle()
		{
			this.openHandleCount -= 1;
			if (this.openHandleCount == 0)
			{
				this.Image.Dispose();
			}
		}
	}

	public sealed class ImageHandle : IDisposable
	{
		public ImageHandle(ImageContainer container)
		{
			this.container = container;
			this.container.AddHandle();
		}

		private readonly ImageContainer container;

		public Image Image
		{
			get { return this.container.Image; }
		}

		public void Dispose()
		{
			this.container.RemoveHandle();
		}
	}
}
