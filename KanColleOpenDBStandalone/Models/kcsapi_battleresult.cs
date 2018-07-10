using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KanColleOpenDBStandalone.Models
{
	// ReSharper disable InconsistentNaming
	public class kcsapi_battleresult
	{
		public kcsapi_battleresult_getship api_get_ship { get; set; }
		public string api_win_rank { get; set; }
	}
	public class kcsapi_battleresult_getship
	{
		public int api_ship_id { get; set; }
		public string api_ship_type { get; set; }
		public string api_ship_name { get; set; }
		public string api_ship_getmes { get; set; }
	}
	// ReSharper restore InconsistentNaming
}
