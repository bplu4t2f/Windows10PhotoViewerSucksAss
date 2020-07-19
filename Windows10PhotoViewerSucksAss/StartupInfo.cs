using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	public class StartupInfo
	{
		public StartupInfo(string ApplicationPathForFileAssociationCommand, string FriendlyAppName)
		{
			this.ApplicationPathForFileAssociationCommand = ApplicationPathForFileAssociationCommand;
			this.FriendlyAppName = FriendlyAppName;
		}

		/// <summary>
		/// May be null. In that case the open verb for file associations cannot be installed.
		/// </summary>
		public string ApplicationPathForFileAssociationCommand { get; }
		/// <summary>
		/// May be null. In that case no friendly app name is registered.
		/// </summary>
		public string FriendlyAppName { get; }
	}
}
