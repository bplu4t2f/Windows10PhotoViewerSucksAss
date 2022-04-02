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
	/// <summary>
	/// This thing ensures that only one save operation is happening at the same time.
	/// </summary>
	class SettingsSaveManager
	{
		private readonly object sync = new object();

		private Action saveAction;
		private object saveTaskToken;

		public void QueueSave(Action saveAction)
		{
			// Queue a background task for actually saving the file.
			// If disk I/O is slow, it would otherwise annoy the user.
			lock (this.sync)
			{
				this.saveAction = saveAction;
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
				Action saveAction;
				lock (this.sync)
				{
					if (this.saveTaskToken != token)
					{
						break;
					}
					if (this.saveAction == null)
					{
						// Terminate this task.
						this.saveTaskToken = null;
						Monitor.PulseAll(this.sync);
						break;
					}
					saveAction = this.saveAction;
					this.saveAction = null;
				}

				try
				{
					saveAction();
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
