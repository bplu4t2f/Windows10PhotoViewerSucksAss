using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
    static class ImageLoader
    {
        private static readonly byte[] bmp = new byte[] { 0x42, 0x4D };               // BMP "BM"
        private static readonly byte[] gif87a = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 };     // "GIF87a"
        private static readonly byte[] gif89a = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };     // "GIF89a"
        private static readonly byte[] png = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };   // PNG "\x89PNG\x0D\0xA\0x1A\0x0A"
        private static readonly byte[] tiffI = new byte[] { 0x49, 0x49, 0x2A, 0x00 }; // TIFF II "II\x2A\x00"
        private static readonly byte[] tiffM = new byte[] { 0x4D, 0x4D, 0x00, 0x2A }; // TIFF MM "MM\x00\x2A"
        private static readonly byte[] jpeg = new byte[] { 0xFF, 0xD8, 0xFF };        // JPEG JFIF (SOI "\xFF\xD8" and half next marker xFF)

        // NOTE: ico/cur support is currently very funky... but at least there is an attempt!
        private static readonly byte[] cur = new byte[] { 0x00, 0x00, 0x01, 0x00 };           // .CUR
        private static readonly byte[] ico = new byte[] { 0x00, 0x00, 0x02, 0x00 };           // .ICO


        public static Image LoadFromFile(string file, out int fileError, out bool notAnImageFile)
        {
            using (var fileStream = FileIO.OpenRead(out fileError, file))
            {
                if (fileStream == null)
                {
                    notAnImageFile = default;
                    return default;
                }

                notAnImageFile = !IsValidImageFile(fileStream);
                if (notAnImageFile)
                {
                    fileStream.Dispose();
                    return default;
                }

                var memoryStream = new MemoryStream((int)Math.Min(Int32.MaxValue, fileStream.Length));
                fileStream.CopyTo(memoryStream);

                Image image = Image.FromStream(memoryStream);
                return image;
            }
        }

        /// <summary>
        /// Reads the header of different image formats
        /// </summary>
        private static bool IsValidImageFile(Stream fs)
        {
            byte[] buffer = new byte[8];

            try
            {
                if (fs.Length > buffer.Length)
                {
                    fs.Read(buffer, 0, buffer.Length);
                    fs.Position = 0;
                }

                if (ByteArrayStartsWith(buffer, bmp) ||
                    ByteArrayStartsWith(buffer, gif87a) ||
                    ByteArrayStartsWith(buffer, gif89a) ||
                    ByteArrayStartsWith(buffer, png) ||
                    ByteArrayStartsWith(buffer, tiffI) ||
                    ByteArrayStartsWith(buffer, tiffM) ||
                    ByteArrayStartsWith(buffer, cur) ||
                    ByteArrayStartsWith(buffer, ico) ||
                    ByteArrayStartsWith(buffer, jpeg))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return false;
        }

        /// <summary>
        /// Returns a value indicating whether a specified subarray occurs within array
        /// </summary>
        /// <param name="a">Main array</param>
        /// <param name="b">Subarray to seek within main array</param>
        /// <returns>true if a array starts with b subarray or if b is empty; otherwise false</returns>
        private static bool ByteArrayStartsWith(byte[] a, byte[] b)
        {
            if (a.Length < b.Length)
            {
                return false;
            }

            for (int i = 0; i < b.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
