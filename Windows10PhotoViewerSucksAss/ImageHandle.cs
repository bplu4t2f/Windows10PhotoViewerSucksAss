using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
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
