using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	public class StartupInfo
	{
		public StartupInfo(string ApplicationPathForFileAssociationCommand, string FriendlyAppName, string StartupParamsHandleValue)
		{
			this.ApplicationPathForFileAssociationCommand = ApplicationPathForFileAssociationCommand;
			this.FriendlyAppName = FriendlyAppName;
			this.StartupParamsHandleValue = StartupParamsHandleValue;
		}

		/// <summary>
		/// May be null. In that case the open verb for file associations cannot be installed.
		/// </summary>
		public string ApplicationPathForFileAssociationCommand { get; }
		/// <summary>
		/// May be null. In that case no friendly app name is registered.
		/// </summary>
		public string FriendlyAppName { get; }
		/// <summary>
		/// May be null. This is used to restore the window position using <see cref="StashHelper"/>.
		/// </summary>
		public string StartupParamsHandleValue { get; }
	}
}
