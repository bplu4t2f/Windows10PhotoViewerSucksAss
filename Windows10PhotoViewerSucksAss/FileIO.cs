﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	static class FileIO
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
			[MarshalAs(UnmanagedType.LPTStr)] string filename,
			[MarshalAs(UnmanagedType.U4)] FileAccess access,
			[MarshalAs(UnmanagedType.U4)] FileShare share,
			IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
			[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
			[MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
			IntPtr templateFile
			);

		/// <summary>
		/// Exception-free file opening because fuck exceptions.
		/// </summary>
		public static int Open(out FileStream fileStream, string filename, FileAccess fileAccess = FileAccess.ReadWrite, FileShare fileShare = FileShare.None, FileMode creationDisposition = FileMode.OpenOrCreate)
		{
			var handle = CreateFile(filename, fileAccess, fileShare, IntPtr.Zero, creationDisposition, FileAttributes.Normal, IntPtr.Zero);
			if (handle.IsInvalid)
			{
				int error = Marshal.GetLastWin32Error();
				fileStream = null;
				return error;
			}
			fileStream = new FileStream(handle, FileAccess.ReadWrite);
			return 0;
		}

		public static int ReadEntireFile(out byte[] data, string filename)
		{
			int error = Open(out var fileStream, filename, FileAccess.Read, FileShare.Read, FileMode.Open);
			if (error != 0)
			{
				data = null;
				return error;
			}
			try
			{
				data = new byte[fileStream.Length];
				fileStream.Read(data, 0, data.Length);
				return 0;
			}
			finally
			{
				fileStream.Dispose();
			}
		}
	}
}