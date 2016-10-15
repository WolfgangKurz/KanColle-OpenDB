using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KanColleOpenDBStandalone.Libs
{
	internal class Proxy
	{
		[DllImport("wininet.dll")]
		private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
		private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
		private const int INTERNET_OPTION_REFRESH = 37;

		private static string proxyKey =>
			@"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

		private static RegistryKey registry =>
			Registry.CurrentUser.OpenSubKey(proxyKey, true);

		private static bool bSetup { get; set; } = false;
		private static int bEnable { get; set; }
		private static string bServer { get; set; }

		public static void Setup(int port)
		{
			if (bSetup) return;

			bSetup = true;
			bEnable = (int)registry.GetValue("ProxyEnable", 0);
			bServer = registry.GetValue("ProxyServer", "") as string;

			registry.SetValue("ProxyEnable", 1);
			registry.SetValue("ProxyServer", "localhost:49327");

			InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
			InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
		}
		public static void Restore()
		{
			bSetup = false;

			registry.SetValue("ProxyEnable", bEnable);
			registry.SetValue("ProxyServer", bServer);

			InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
			InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
		}
	}
}
