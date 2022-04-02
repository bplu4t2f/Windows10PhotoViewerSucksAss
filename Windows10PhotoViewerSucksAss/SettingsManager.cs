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
	class SettingsManager<T>
		where T : new()
	{
		public SettingsManager(string appDataFolderName)
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			this.SettingsFilePath = Path.Combine(appData, $@"{appDataFolderName}\settings.xml");
		}

		private readonly object sync = new object();
		public string SettingsFilePath { get; }

		public T Load()
		{
			try
			{
				using (var fileStream = FileIO.Open(out int error, this.SettingsFilePath, FileAccess.Read, FileShare.Read, FileMode.Open))
				{
					if (fileStream == null)
					{
						// File not found etc -- we don't actually care about the exact error.
						return new T();
					}
					var ser = new XmlSerializer(typeof(T));
					return (T)ser.Deserialize(fileStream);
				}
			}
			catch (Exception ex)
			{
				// We don't actually care.
				Debug.WriteLine(ex);
			}
			return new T();
		}

		private byte[] bytesToBeSaved;
		private object saveTaskToken;

		public void QueueSave(T value)
		{
			var ser = new XmlSerializer(typeof(T));
			byte[] bytes;
			using (var stream = new MemoryStream())
			{
				ser.Serialize(stream, value);
				bytes = stream.ToArray();
			}

			// Queue a background task for actually saving the file.
			// If disk I/O is slow, it would otherwise annoy the user.
			lock (this.sync)
			{
				this.bytesToBeSaved = bytes;
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
					this.bytesToBeSaved = null;
				}

				try
				{
					var dir = Path.GetDirectoryName(this.SettingsFilePath);
					Directory.CreateDirectory(dir);
					File.WriteAllBytes(this.SettingsFilePath, bytes);
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
