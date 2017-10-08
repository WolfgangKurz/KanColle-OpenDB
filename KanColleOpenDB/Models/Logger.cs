using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KanColleOpenDB.Models
{
	internal class Logger
	{
		private const string LogDirectory = "OpenDB";

		/// <summary>
		/// Log exception to file
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="exception"></param>
		/// <param name="from"></param>
		internal static void LogException(object sender, Exception exception, string from)
		{
			#region const

			const string MessageFormat = @"
===========================================================
ERROR, date = {0}, sender = {1}, from = {2}
{3}
";
			string LogFile = $"opendb-{DateTimeOffset.Now.LocalDateTime.ToString("yyMMdd-HHmmssff")}.log";
			string LogPath = Path.Combine(
				LogDirectory,
				LogFile
			);

			#endregion

			try
			{
				if (!Directory.Exists(LogDirectory))
					Directory.CreateDirectory(LogDirectory);

				Logger.Log($"Exception catched to {LogFile}", from);

				var message = string.Format(MessageFormat, DateTimeOffset.Now, sender, from, exception);
				Debug.WriteLine(message);
				File.AppendAllText(LogPath, message);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

#if DEBUG
		/// <summary>
		/// Log to file
		/// </summary>
		/// <param name="text"></param>
		/// <param name="from"></param>
		internal static void Log(string text, string from)
		{
			const string MessageFormat = @"[{0}] {1} : {2}
";
			const string Path = "opendb.log";

			try
			{
				var message = string.Format(MessageFormat, DateTimeOffset.Now.LocalDateTime.ToString("yy-MM-dd HH:mm:ss.ff"), from, text);
				Debug.WriteLine(message);
				File.AppendAllText(Path, message);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}
#else
		internal static void Log(string text, string from) { }
#endif
	}
}
