using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace KanColleOpenDBStandalone
{
	class TrayIcon : IDisposable
	{
		private string ver => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

		private ContextMenuStrip menu { get; set; }
		private NotifyIcon tray { get; set; }

		public TrayIcon()
		{
			// Menu for tray icon
			menu = new ContextMenuStrip();
			menu.Items.Add("OpenDB Standalone " + ver).Enabled = false;
			menu.Items.Add("-");
			menu.Items.Add("Project Website").Click += (s, e) => System.Diagnostics.Process.Start("http://swaytwig.com/opendb/");
			menu.Items.Add("Source Code").Click += (s, e) => System.Diagnostics.Process.Start("https://github.com/WolfgangKurz/KanColle-OpenDB");
			menu.Items.Add("WolfgangKurz").Enabled = false;
			menu.Items.Add("-");

			var item = menu.Items.Add("Shutdown");
			item.Click += (s, e) => Application.Exit();


			// Tray icon
			tray = new NotifyIcon();
			tray.Icon = Properties.Resources.app;
			tray.Text = "OpenDB Standalone";
			tray.ContextMenuStrip = menu;
			tray.Visible = true;


			// Notify
			tray.ShowBalloonTip(10 * 1000, "OpenDB Standalone", "Watching and Reporting has started!", ToolTipIcon.Info);
		}

		public void Dispose()
		{
			tray.Visible = false;

			this.tray?.Dispose();
			this.menu?.Dispose();
		}
	}
}
