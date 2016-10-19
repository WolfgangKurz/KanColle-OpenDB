using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetroTrilithon.Mvvm;

namespace KanColleOpenDB.ViewModels
{
	public class DialogViewModel : WindowViewModel
	{
		public DialogViewModel()
		{
			this.DialogResult = true;
		}

		public void OK()
		{
			this.DialogResult = true;
			this.Close();
		}

		public void Cancel()
		{
			this.DialogResult = false;
			this.Close();
		}
	}
}
