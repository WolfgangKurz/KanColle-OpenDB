using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

using Livet;
using Grabacr07.KanColleViewer.Models;
using KanColleOpenDB.Properties;

namespace KanColleOpenDB.Libs
{
	public class ResourceService : NotificationObject
	{
		public static ResourceService Current { get; } = new ResourceService();

		public Resources Resources { get; }
		public IReadOnlyCollection<CultureInfo> SupportedCultures { get; }

		public ResourceService()
		{
			this.Resources = new Resources();

			string[] CultureList = { "en", "ja", "ko-KR" };

			this.SupportedCultures = CultureList.Select(x =>
			{
				try
				{
					return CultureInfo.GetCultureInfo(x);
				}
				catch (CultureNotFoundException)
				{
					return null;
				}
			})
				.Where(x => x != null)
				.ToList();
		}

		public void ChangeCulture(string name)
		{
			Resources.Culture = this.SupportedCultures.SingleOrDefault(x => x.Name == name);
			this.RaisePropertyChanged(nameof(this.Resources));
		}
	}
}
