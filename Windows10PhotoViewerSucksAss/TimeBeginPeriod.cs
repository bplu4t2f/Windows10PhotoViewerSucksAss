using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	public static class TimeBeginPeriod
	{
		[DllImport("winmm.dll", SetLastError = true)]
		public static extern uint timeBeginPeriod(uint uMilliseconds);

		[DllImport("winmm.dll", SetLastError = true)]
		public static extern uint timeEndPeriod(uint uMilliseconds);
	}
}
