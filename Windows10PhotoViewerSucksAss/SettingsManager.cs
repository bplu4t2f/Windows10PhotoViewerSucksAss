using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Windows10PhotoViewerSucksAss
{
	// TODO simplify further - the only concern of this class should be to ensure that only one save operation is going on at the same time.

	class SettingsManager
	{
		public SettingsManager(string subfolderName)
		{
			this.SubfolderName = subfolderName ?? throw new ArgumentNullException(nameof(subfolderName));
		}

		public string SubfolderName { get; }

		private readonly object sync = new object();

		public string GetFullSettingsFilePath()
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(appData, this.SubfolderName, "settings.xml");
		}

		public T Load<T>(Func<Stream, T> deserializer)
		{
			try
			{
				var path = GetFullSettingsFilePath();
				using (var fileStream = FileIO.Open(out int error, path, FileAccess.Read, FileShare.Read, FileMode.Open))
				{
					if (fileStream == null)
					{
						// File not found etc -- we don't actually care about the exact error.
						return default;
					}

					var result = deserializer(fileStream);
					return result;
				}
			}
			catch (Exception ex)
			{
				// We don't actually care.
				Debug.WriteLine(ex);
				return default;
			}
		}

		private byte[] bytesToBeSaved;
		private string saveDestination;
		private object saveTaskToken;

		public void QueueSave<T>(T value, Action<Stream, T> serializer)
		{
			byte[] bytes;
			using (var stream = new MemoryStream())
			{
				serializer(stream, value);
				bytes = stream.ToArray();
			}

			// Queue a background task for actually saving the file.
			// If disk I/O is slow, it would otherwise annoy the user.
			lock (this.sync)
			{
				this.bytesToBeSaved = bytes;
				this.saveDestination = this.GetFullSettingsFilePath();
				if (this.saveTaskToken == null)
				{
					this.saveTaskToken = new object();
					ThreadPool.QueueUserWorkItem(this.BackgroundSaveTask, this.saveTaskToken);
				}
			}
		}

		public void WaitSaveCompleted()
		{
			lock (this.sync)
			{
				while (true)
				{
					if (this.saveTaskToken == null)
					{
						return;
					}
					if (!Monitor.Wait(this.sync, 4000))
					{
						// Timed out. Just let the program terminate instead of bothering the user more.
						return;
					}
				}
			}
		}

		private void BackgroundSaveTask(object token)
		{
			while (true)
			{
				byte[] bytes;
				string saveDestination;
				lock (this.sync)
				{
					if (this.saveTaskToken != token)
					{
						break;
					}
					if (this.bytesToBeSaved == null)
					{
						// Terminate this task.
						this.saveTaskToken = null;
						Monitor.PulseAll(this.sync);
						break;
					}
					bytes = this.bytesToBeSaved;
					saveDestination = this.saveDestination;
					Debug.Assert(saveDestination != null);
					this.bytesToBeSaved = null;
					this.saveDestination = null;
				}

				try
				{
					var dir = Path.GetDirectoryName(saveDestination);
					Directory.CreateDirectory(dir);
					File.WriteAllBytes(saveDestination, bytes);
				}
				catch (Exception ex)
				{
					// We don't actually care.
					Debug.WriteLine(ex);
				}
			}
		}
	}
}
