using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Windows10PhotoViewerSucksAss
{
	public class IconBuilder
	{
		public List<IconImage> IconImages { get; } = new List<IconImage>();

		public Icon ConvertToIcon()
		{
			if (this.IconImages.Count == 0)
			{
				return null;
			}

			MemoryStream ms = new MemoryStream();
			this.Save(ms);
			ms.Position = 0;
			Icon icon = new Icon(ms);
			return icon;
		}

		public unsafe void Save(Stream stream)
		{
			ICONDIR iconDir = ICONDIR.Initalizated;
			iconDir.idCount = (ushort)this.IconImages.Count;
			iconDir.Write(stream);

			int entryPos = sizeof(ICONDIR);
			// Placeholder for an array if ICONDIRENTRY here
			int imagesPos = entryPos + iconDir.idCount * sizeof(ICONDIRENTRY);
			foreach (IconImage iconImage in this.IconImages)
			{
				// IconImage
				stream.Seek(imagesPos, SeekOrigin.Begin);
				// Header
				BITMAPINFOHEADER header = Make_BITMAPINFOHEADER(iconImage);
				header.Write(stream);

				// Palette - (none)

				// XOR Image
				stream.Write(iconImage.XOR, 0, iconImage.XOR.Length);

				// AND Image - (not needed)


				long bytesInRes = stream.Position - imagesPos;

				// IconDirHeader
				stream.Seek(entryPos, SeekOrigin.Begin);
				ICONDIRENTRY iconEntry = Make_ICONDIRENTRY(iconImage);
				stream.Seek(entryPos, SeekOrigin.Begin);
				iconEntry.dwImageOffset = (uint)imagesPos;
				iconEntry.dwBytesInRes = (uint)bytesInRes;
				iconEntry.Write(stream);

				entryPos += sizeof(ICONDIRENTRY);
				imagesPos += (int)bytesInRes;
			}
		}


		const byte NumColorsInPalette = 0; // Not using any color palette
		const ushort NumPlanes = 1;
		const ushort BitCount = 32; // (Because 32bppArgb)

		private static unsafe ICONDIRENTRY Make_ICONDIRENTRY(IconImage iconImage)
		{
			ICONDIRENTRY iconEntry = new ICONDIRENTRY();
			iconEntry.bColorCount = NumColorsInPalette;
			iconEntry.bHeight = (byte)iconImage.ImageSize.Height;
			iconEntry.bReserved = 0;
			iconEntry.bWidth = (byte)iconImage.ImageSize.Width;
			iconEntry.dwBytesInRes = (uint)(sizeof(BITMAPINFOHEADER) + iconImage.XOR.Length/* + this.AND.Length*/);
			iconEntry.dwImageOffset = (uint)(sizeof(ICONDIR) + sizeof(ICONDIRENTRY));
			iconEntry.wBitCount = BitCount;
			iconEntry.wPlanes = NumPlanes;
			return iconEntry;
		}

		private static unsafe BITMAPINFOHEADER Make_BITMAPINFOHEADER(IconImage iconImage)
		{
			BITMAPINFOHEADER infoHeader = new BITMAPINFOHEADER();
			infoHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
			infoHeader.biWidth = (uint)iconImage.ImageSize.Width;
			infoHeader.biHeight = (uint)iconImage.ImageSize.Height * 2; // Inexplicably
			infoHeader.biPlanes = NumPlanes;
			infoHeader.biBitCount = BitCount;
			infoHeader.biCompression = IconImageFormat.BMP;
			infoHeader.biSizeImage = (uint)iconImage.XOR.Length;
			infoHeader.biXPelsPerMeter = 0;
			infoHeader.biYPelsPerMeter = 0;
			infoHeader.biClrUsed = NumColorsInPalette;
			infoHeader.biClrImportant = 0;
			return infoHeader;
		}


		public sealed class IconImage
		{
			public static IconImage FromBitmap(Bitmap bitmap)
			{
				var icon_image = new IconImage();
				icon_image.Set(bitmap);
				return icon_image;
			}

			private void Set(Bitmap bitmap)
			{
				Debug.Assert(bitmap != null);
				Debug.Assert(bitmap.PixelFormat == PixelFormat.Format32bppArgb);

				this.ImageSize = bitmap.Size;

				// XOR Image (AND mask is not needed it seems)
				BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
				try
				{
					IntPtr scanColor = bmpData.Scan0;
					this.XOR = new byte[Math.Abs(bmpData.Stride) * bmpData.Height];
					// Need to vertically flip the bitmap for some obscure reason - so we're copying the rows in reverse
					for (int y = 0; y < bmpData.Height; ++y)
					{
						int dest_offset = (bmpData.Height - y - 1) * bmpData.Stride;
						Marshal.Copy(
							source: IntPtr.Add(scanColor, y * bmpData.Stride),
							destination: this.XOR,
							startIndex: dest_offset,
							length: bmpData.Stride
							);
					}
				}
				finally
				{
					bitmap.UnlockBits(bmpData);
				}
			}

			public Size ImageSize { get; set; }
			public byte[] XOR { get; set; }
		}


		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private unsafe struct BITMAPINFOHEADER
		{
			public uint biSize;
			public uint biWidth;
			public uint biHeight;
			public ushort biPlanes;
			public ushort biBitCount;
			public IconImageFormat biCompression;
			public uint biSizeImage;
			public int biXPelsPerMeter;
			public int biYPelsPerMeter;
			public uint biClrUsed;
			public uint biClrImportant;

			public void Write(Stream stream)
			{
				byte[] array = new byte[sizeof(BITMAPINFOHEADER)];
				fixed (BITMAPINFOHEADER* ptr = &this)
				{
					Marshal.Copy((IntPtr)ptr, array, 0, sizeof(BITMAPINFOHEADER));
				}
				stream.Write(array, 0, sizeof(BITMAPINFOHEADER));
			}
		}

		private enum IconImageFormat : int
		{
			BMP = 0,
			PNG = 5,
			UNKNOWN = 255
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private unsafe struct ICONDIR
		{
			public ushort idReserved;
			public ushort idType;
			public ushort idCount;

			public ICONDIR(ushort reserved, ushort type, ushort count)
			{
				this.idReserved = reserved;
				this.idType = type;
				this.idCount = count;
			}

			public static ICONDIR Initalizated
			{
				get { return new ICONDIR(0, 1, 0); }
			}

			public void Write(Stream stream)
			{
				byte[] array = new byte[sizeof(ICONDIR)];
				fixed (ICONDIR* ptr = &this)
				{
					Marshal.Copy((IntPtr)ptr, array, 0, sizeof(ICONDIR));
				}
				stream.Write(array, 0, sizeof(ICONDIR));
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private unsafe struct ICONDIRENTRY
		{
			public byte bWidth;
			public byte bHeight;
			public byte bColorCount;
			public byte bReserved;
			public ushort wPlanes;
			public ushort wBitCount;
			public uint dwBytesInRes;
			public uint dwImageOffset;

			public void Write(Stream stream)
			{
				byte[] array = new byte[sizeof(ICONDIRENTRY)];
				fixed (ICONDIRENTRY* ptr = &this)
				{
					Marshal.Copy((IntPtr)ptr, array, 0, sizeof(ICONDIRENTRY));
				}
				stream.Write(array, 0, sizeof(ICONDIRENTRY));
			}
		}
	}
}
