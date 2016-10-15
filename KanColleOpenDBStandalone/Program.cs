using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace KanColleOpenDBStandalone
{
	static class Program
	{
		/// <summary>
		/// 해당 응용 프로그램의 주 진입점입니다.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			TrayIcon trayicon = new TrayIcon();
			Watcher watcher = new Watcher();

			Application.Run();

			watcher.Dispose();
			trayicon.Dispose();

			Application.ExitThread();
			Environment.Exit(0);
		}
	}
}
