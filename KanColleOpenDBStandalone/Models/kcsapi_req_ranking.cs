using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KanColleOpenDBStandalone.Models
{
	public class kcsapi_req_ranking
	{
		public int api_count { get; set; }
		public int api_page_count { get; set; }
		public int api_disp_page { get; set; }
		public member_ranking[] api_list { get; set; }
	}

	public class member_ranking
	{
		public int api_mxltvkpyuklh { get; set; } // Rank
		public string api_mtjmdcwtvhdr { get; set; } // Nickname
		public int api_pbgkfylkbjuy { get; set; } // Flag
		public int api_pcumlrymlujh { get; set; } // Rank Level
		public string api_itbrdpdbkynm { get; set; } // Comment
		public int api_itslcqtmrxtf { get; set; } // Medal (Encoded)
		public int api_wuhnhojjxmke { get; set; } // Score (Encoded)
	}

	public class named_ranking : member_ranking
	{
		public int rank => this.api_mxltvkpyuklh;
		public string nick => this.api_mtjmdcwtvhdr;
		public int medal => this.api_itslcqtmrxtf;
		public int score => this.api_wuhnhojjxmke;

		public int flag => this.api_pbgkfylkbjuy;
		public int level => this.api_pcumlrymlujh;
		public string comment => this.api_itbrdpdbkynm;

		public named_ranking(member_ranking original)
		{
			this.api_mxltvkpyuklh = original.api_mxltvkpyuklh;
			this.api_mtjmdcwtvhdr = original.api_mtjmdcwtvhdr;
			this.api_pbgkfylkbjuy = original.api_pbgkfylkbjuy;
			this.api_pcumlrymlujh = original.api_pcumlrymlujh;
			this.api_itbrdpdbkynm = original.api_itbrdpdbkynm;
			this.api_itslcqtmrxtf = original.api_itslcqtmrxtf;
			this.api_wuhnhojjxmke = original.api_wuhnhojjxmke;
		}
	}
}
