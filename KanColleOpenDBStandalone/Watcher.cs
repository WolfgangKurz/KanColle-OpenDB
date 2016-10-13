using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

using KanColleOpenDBStandalone.Libs;
using KanColleOpenDBStandalone.Models;

using Grabacr07.KanColleWrapper.Models;
using Grabacr07.KanColleWrapper.Models.Raw;

namespace KanColleOpenDBStandalone
{
    class Watcher : IDisposable
    {
        private delegate void SessionHandler(Nekoxy.Session session);

        private string OpenDBReport => "http://swaytwig.com/opendb/report/";
        private int MAX_TRY => 3;

        private int WATCHER_PROXY_PORT => 49327;
        private SessionManager manager;

        public Watcher()
        {
            Setup();

            Nekoxy.HttpProxy.Startup(WATCHER_PROXY_PORT, false, false);
            Nekoxy.HttpProxy.AfterSessionComplete += (x) => manager.Call(x);

            Libs.Proxy.Setup(WATCHER_PROXY_PORT);
        }
        public void Dispose()
        {
            Libs.Proxy.Restore();
            Nekoxy.HttpProxy.Shutdown();
        }


        private int Flagship { get; set; }

        private int ShipCount { get; set; }
        private int ShipLimit { get; set; }
        private bool IsShipLimit => this.ShipCount >= this.ShipLimit;

        private void Setup()
        {
            manager = new SessionManager();
            // var homeport = KanColleClient.Current.Homeport;

            this.Flagship = 0;
            this.ShipCount = 0;
            this.ShipLimit = 0;

            // Check for flagship and  ship count
            kcsapi_ship2[] ships = new kcsapi_ship2[0];
            int[][] fleet = new int[][]
            {
                new int[6], new int[6], new int[6], new int[6]
            };

            var updateDeck3 = new Action<kcsapi_deck>(x =>
            {
                fleet[x.api_id - 1] = x.api_ship;

                if (x.api_id == 1)
                {
                    var ship = x.api_ship[0];
                    var sid = ships.FirstOrDefault(y => y.api_id == ship).api_ship_id;
                    this.Flagship = sid;
                }
            });
            var updateDeck = new Action<kcsapi_deck[]>(x =>
            {
                foreach (var y in x) fleet[y.api_id - 1] = y.api_ship;
                updateDeck3(x.FirstOrDefault(y => y.api_id == 1));
            });
            var updateDeck2 = new Action<kcsapi_ship_deck>(x =>
            {
                foreach (var y in x.api_deck_data) fleet[y.api_id - 1] = y.api_ship;
                updateDeck(x.api_deck_data);
            });

            var updateHomeport = new Action<SvData<kcsapi_port>>(x =>
            {
                if (!x.IsSuccess) return;

                ships = x.Data.api_ship;

                this.ShipCount = ships.Length;
                this.ShipLimit = x.Data.api_basic.api_max_chara;
                updateDeck(x.Data.api_deck_port);
            });

            manager.Prepare()
                .Where(x => x.Request.PathAndQuery == "/kcsapi/api_port/port")
                .TryParse<kcsapi_port>().Subscribe(x => updateHomeport(x));


            #region Development (Create slotitem at arsenal)

            manager.Prepare()
                .Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_kousyou/createitem")
                .TryParse<kcsapi_createitem>()
                .Subscribe(x =>
                {
                    if (!x.IsSuccess) return;

                    var item = 0; // Failed to build
                    if (x.Data.api_create_flag == 1)
                        item = x.Data.api_slot_item.api_slotitem_id;

                    var material = new int[] {
                        int.Parse(x.Request["api_item1"]),
                        int.Parse(x.Request["api_item2"]),
                        int.Parse(x.Request["api_item3"]),
                        int.Parse(x.Request["api_item4"])
                    };
                    var flagship = this.Flagship;

                    new Thread(() =>
                    {
                        string post = string.Join("&", new string[] {
                        "flagship=" + flagship,
                        "fuel=" + material[0],
                        "ammo=" + material[1],
                        "steel=" + material[2],
                        "bauxite=" + material[3],
                        "result=" + item
                        });

                        int tries = MAX_TRY;
                        while (tries > 0)
                        {
                            var y = HTTPRequest.Post(OpenDBReport + "equip_dev.php", post);
                            if (y != null)
                            {
                                y?.Close();
                                break;
                            }
                            tries--;
                        }
                    }).Start();
                });

            #endregion

            #region Construction (Build new ship at arsenal)

            bool ship_dev_wait = false;
            int ship_dev_dockid = 0;

            manager.Prepare()
                .Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_kousyou/createship")
                .TryParse<kcsapi_createship>()
                .Subscribe(x =>
                {
                    if (!x.IsSuccess) return;

                    ship_dev_wait = true;
                    ship_dev_dockid = int.Parse(x.Request["api_kdock_id"]);
                });
            manager.Prepare()
                .Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/kdock")
                .TryParse<kcsapi_kdock[]>()
                .Subscribe(x =>
                {
                    if (!x.IsSuccess) return;

                    if (!ship_dev_wait) return; // Not created
                    ship_dev_wait = false;

                    var dock = x.Data.SingleOrDefault(y => y.api_id == ship_dev_dockid);
                    var flagship = this.Flagship;
                    var ship = dock.api_created_ship_id;

                    new Thread(() =>
                    {
                        string post = string.Join("&", new string[] {
                            "flagship=" + flagship,
                            "fuel=" + dock.api_item1,
                            "ammo=" + dock.api_item2,
                            "steel=" + dock.api_item3,
                            "bauxite=" + dock.api_item4,
                            "material=" + dock.api_item5,
                            "result=" + ship
                        });

                        int tries = MAX_TRY;
                        while (tries > 0)
                        {
                            var y = HTTPRequest.Post(OpenDBReport + "ship_dev.php", post);
                            if (y != null)
                            {
                                y?.Close();
                                break;
                            }
                            tries--;
                        }
                    }).Start();
                });

            #endregion

            #region Drop (Get new ship from sea)

            int drop_world = 0;
            int drop_map = 0;
            int drop_node = 0;
            int drop_maprank = 0;

            var drop_prepare = new Action<kcsapi_start_next>(x =>
            {
                drop_world = x.api_maparea_id;
                drop_map = x.api_mapinfo_no;
                drop_node = x.api_no;
                drop_maprank = x.api_eventmap?.api_selected_rank ?? 0;
                // 0:None, 丙:1, 乙:2, 甲:3
            });
            var drop_report = new Action<kcsapi_battleresult>(x =>
            {
                if (this.IsShipLimit) return; // Maximum ship-count

                var drop_shipid = 0;
                var drop_rank = x.api_win_rank;
                if (x.api_get_ship != null) drop_shipid = x.api_get_ship.api_ship_id;

                new Thread(() =>
                {
                    string post = string.Join("&", new string[] {
                            "world=" + drop_world,
                            "map=" + drop_map,
                            "node=" + drop_node,
                            "rank=" + drop_rank,
                            "maprank=" + drop_maprank,
                            "result=" + drop_shipid
                        });

                    int tries = MAX_TRY;
                    while (tries > 0)
                    {
                        var y = HTTPRequest.Post(OpenDBReport + "ship_drop.php", post);
                        if (y != null)
                        {
                            y?.Close();
                            break;
                        }
                        tries--;
                    }
                }).Start();
            });

            // To gether Map-id
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_map/start")
                .TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data));
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_map/next")
                .TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data));

            // To gether dropped ship
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_sortie/battleresult")
                .TryParse<kcsapi_battleresult>().Subscribe(x => drop_report(x.Data));
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_combined_battle/battleresult")
                .TryParse<kcsapi_battleresult>().Subscribe(x => drop_report(x.Data));

            // To check deck update
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/ship")
                .TryParse<kcsapi_ship2[]>().Subscribe(x => ships = x.Data);
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/ship2")
                .TryParse<kcsapi_ship2[]>().Subscribe(x =>
                {
                    ships = x.Data;
                    updateDeck(x.Fleets);
                });
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/ship3")
                .TryParse<kcsapi_ship3>().Subscribe(x => {
                    ships = x.Data.api_ship_data;
                    updateDeck(x.Data.api_deck_data);
                });

            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_hensei/change")
                .TryParse().Subscribe(x =>
                {
                    if (!x.IsSuccess) return;

                    var fleetId = int.Parse(x.Request["api_id"]) - 1;
                    if (fleetId != 0) return; // Need only first fleet

                    var ship = int.Parse(x.Request["api_ship_id"]); // Ship to move
                    var shipIdx = int.Parse(x.Request["api_ship_idx"]); // Destination position
                    int oFleet = -1, oShip = -1;

                    // Target ship's original fleet
                    for (int i = 0; i < fleet.Length; i++)
                        for (int j = 0; j < fleet[i].Length; j++)
                            if (fleet[i][j] == ship)
                            {
                                oFleet = i;
                                oShip = j;
                                break;
                            }

                    // Swap
                    if (oFleet >= 0)
                    {
                        fleet[oFleet][oShip] = fleet[fleetId][shipIdx];
                        fleet[fleetId][shipIdx] = ship;
                    }
                    else
                    {
                        fleet[fleetId][shipIdx] = ship;
                    }

                    this.Flagship = ships.FirstOrDefault(y=>y.api_id==fleet[0][0]).api_ship_id;
                });

            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/deck")
                .TryParse<kcsapi_deck[]>().Subscribe(x => updateDeck(x.Data));
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/deck_port")
                .TryParse<kcsapi_deck[]>().Subscribe(x => updateDeck(x.Data));
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_get_member/ship_deck")
                .TryParse<kcsapi_ship_deck>().Subscribe(x => updateDeck2(x.Data));
            manager.Prepare().Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_hensei/preset_select")
                .TryParse<kcsapi_deck>().Subscribe(x => updateDeck3(x.Data));

            // To check deck update and admiral information
            var memberId = 0;
            manager.Prepare().Where(x=>x.Request.PathAndQuery== "/kcsapi/api_port/port")
                .TryParse<kcsapi_port>().Subscribe(x =>
                {
                    ships = x.Data.api_ship;
                    updateDeck(x.Data.api_deck_port);

                    memberId = 0;
                    int.TryParse(x.Data.api_basic.api_member_id, out memberId);
                });
            manager.Prepare().Where(x=>x.Request.PathAndQuery== "/kcsapi/api_get_member/basic")
                .TryParse<kcsapi_basic>().Subscribe(x =>
                {
                    memberId = 0;
                    int.TryParse(x.Data.api_member_id, out memberId);
                });

            #endregion

            #region Ranking List

            var host = "";
            manager.Prepare()
                .Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_ranking")
                .SubscribeRaw(x => host = x.Request.Headers.Host)
                .TryParse<kcsapi_req_ranking>()
                .Subscribe(x =>
                {
                    if (!x.IsSuccess) return;

                    string json = "";

                    var page = x.Data.api_disp_page;
                    var offset = (page - 1) * 10;
                    if (offset >= 1000) return; // only ~1000

                    var node = "";
                    var nodes = new List<string>();
                    var escape = new Func<string, string>(p => Uri.EscapeDataString(p));

                    for (var i = 0; i < x.Data.api_list.Length; i++)
                    {
                        var named = new named_ranking(x.Data.api_list[i]);

                        node = $"{{\"rank\":{named.rank},\"nick\":\"{escape(named.nick)}\","
                            + $"\"medal\":{named.medal},\"score\":{named.score}}}";

                        nodes.Add(node);
                    }
                    json = string.Join(",", nodes.ToArray());

                    new Thread(() =>
                    {
                        string post = string.Join("&", new string[] {
                            "apiver=" + 1,
                            "server=" + host,
                            "key=" + (memberId % 10),
                            "data=" + System.Uri.EscapeDataString($"[{json}]")
                        });

                        int tries = MAX_TRY;
                        while (tries > 0)
                        {
                            var y = HTTPRequest.Post(OpenDBReport + "rank_list.php", post);
                            if (y != null)
                            {
                                y?.Close();
                                break;
                            }
                            tries--;
                        }
                    }).Start();
                });

            #endregion

            #region Slotitem Improvement (Remodel slotitem)

            manager.Prepare()
                .Where(x => x.Request.PathAndQuery.StartsWith("/kcsapi/api_req_kousyou/remodel_slot"))
                .TryParse<KanColleOpenDBStandalone.Models.kcsapi_remodel_slot>()
                .Subscribe(x =>
                {
                    if (!x.IsSuccess) return;

                    var item = x.Data.api_remodel_id[0]; // Slotitem master id
                    var flagship = this.Flagship; // Flagship (Akashi or Akashi Kai)
                    var assistant = x.Data.api_voice_ship_id; // Assistant ship master id
                    var level = x.Data.api_after_slot.api_level; // After level
                    var result = x.Data.api_remodel_flag; // Is succeeded?

                    if (result == 1)
                    {
                        level--;
                        if (level < 0) level = 10;
                    }

                    new Thread(() =>
                    {
                        string post = string.Join("&", new string[] {
                            "apiver=" + 1,
                            "flagship=" + flagship,
                            "assistant=" + assistant,
                            "item=" + item,
                            "level=" + level,
                            "result=" + result
                        });

                        int tries = MAX_TRY;
                        while (tries > 0)
                        {
                            var y = HTTPRequest.Post(OpenDBReport + "equip_remodel.php", post);
                            if (y != null)
                            {
                                y?.Close();
                                break;
                            }
                            tries--;
                        }
                    }).Start();
                });

            #endregion
        }
    }
}
