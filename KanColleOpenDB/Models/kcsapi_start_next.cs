using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grabacr07.KanColleWrapper;

namespace KanColleOpenDB.Models
{
    public class kcsapi_start_next
    {
        public int api_rashin_flg { get; set; }
        public int api_rashin_id { get; set; }
        public int api_maparea_id { get; set; }
        public int api_mapinfo_no { get; set; }
        public int api_no { get; set; }
        public int api_color_no { get; set; }
        public int api_event_id { get; set; }
        public int api_event_kind { get; set; }
        public int api_next { get; set; }
        public int api_bosscell_no { get; set; }
        public int api_bosscomp { get; set; }

        public kcsapi_eventmap api_eventmap { get; set; }
    }
}
