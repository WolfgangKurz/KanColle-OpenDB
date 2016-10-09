using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Nekoxy;
using MetroTrilithon.Mvvm;
using Livet;

using Grabacr07.KanColleViewer;
using Grabacr07.KanColleWrapper;
using Grabacr07.KanColleWrapper.Models.Raw;

using KanColleOpenDB.Libs;
using KanColleOpenDB.Models;
using KanColleOpenDB.Views;

using System.Threading;

namespace KanColleOpenDB.ViewModels
{
    public class OpenDBViewModel : ViewModel
    {
        // OpenDB host
        private string OpenDBReport => "http://swaytwig.com/opendb/report/";
        private int MAX_TRY => 3;

        #region Enabled Property

        private bool _Enabled;
        public bool Enabled
        {
            get { return this._Enabled; }
            set
            {
                this._Enabled = value;

                Properties.Settings.Default["Enabled"] = value;
                Properties.Settings.Default.Save();

                RaisePropertyChanged();
            }
        }

        #endregion

        #region PluginVersion Property

        public string PluginVersion
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            }
        }

        #endregion

        public OpenDBViewModel()
        {
            Initialized = false;

            var client = KanColleClient.Current;
            client.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(client.IsStarted))
                    Initialize();
            };
        }

        private bool Initialized;
        private void Initialize()
        {
            if (Initialized) return;
            Initialized = true;

            bool IsFirst = (bool)Properties.Settings.Default["IsFirst"];
            Enabled = (bool)Properties.Settings.Default["Enabled"];

            if(IsFirst) // Is the first load after install?
            {
                new Thread(() =>
                {
                    Thread.Sleep(1000);

                    // Show alert popup
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var vmodel = new DialogViewModel();
                        var window = new FirstPopup
                        {
                            DataContext = vmodel,
                            Owner = Application.Current.MainWindow,
                        };
                        window.ShowDialog();

                        vmodel.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == "DialogResult")
                                Application.Current.Dispatcher.Invoke(() => Enabled = vmodel.DialogResult);
                        };
                    });
                }).Start();
            }

            // Save IsFirst setting
            Properties.Settings.Default["IsFirst"] = false;
            Properties.Settings.Default.Save();

            var homeport = KanColleClient.Current.Homeport;
            var proxy = KanColleClient.Current.Proxy;
            var api_session = proxy.ApiSessionSource;

            // Development (Create slotitem at arsenal)
            proxy.api_req_kousyou_createitem
                .TryParse<kcsapi_createitem>()
                .Where(x => x.IsSuccess).Subscribe(x =>
                {
                    ///////////////////////////////////////////////////////////////////
                    if (!Enabled) return; // Disabled sending statistics data to server

                    var item = 0; // Failed to build
                    if (x.Data.api_create_flag == 1)
                        item = x.Data.api_slot_item.api_slotitem_id;

                    var material = new int[] {
                        int.Parse(x.Request["api_item1"]),
                        int.Parse(x.Request["api_item2"]),
                        int.Parse(x.Request["api_item3"]),
                        int.Parse(x.Request["api_item4"])
                    };
                    var flagship = homeport.Organization.Fleets[1].Ships[0].Info.Id;

                    new Thread(() =>
                    {
                        string post = string.Join("&", new string[] {
                            "apiver=" + 2,
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

            // Construction (Build new ship at arsenal)
            bool ship_dev_wait = false;
            int ship_dev_dockid = 0;

            proxy.api_req_kousyou_createship
                .TryParse<kcsapi_createship>()
                .Where(x => x.IsSuccess).Subscribe(x =>
                {
                    ship_dev_wait = true;
                    ship_dev_dockid = int.Parse(x.Request["api_kdock_id"]);
                });
            proxy.api_get_member_kdock
                .TryParse<kcsapi_kdock[]>()
                .Where(x => x.IsSuccess)
                .Subscribe(x =>
                {
                    if (!ship_dev_wait) return; // Not created
                    ship_dev_wait = false;

                    ///////////////////////////////////////////////////////////////////
                    if (!Enabled) return; // Disabled sending statistics data to server

                    var dock = x.Data.SingleOrDefault(y => y.api_id == ship_dev_dockid);
                    var flagship = homeport.Organization.Fleets[1].Ships[0].Info.Id;
                    var ship = dock.api_created_ship_id;

                    new Thread(() =>
                    {
                        string post = string.Join("&", new string[] {
                            "apiver=" + 2,
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

            // Drop (Get new ship from sea)
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
                ///////////////////////////////////////////////////////////////////
                if (!Enabled) return; // Disabled sending statistics data to server

                if (homeport.Organization.Ships.Count >= homeport.Admiral.MaxShipCount)
                    return; // Maximum ship-count

                var drop_shipid = 0;
                var drop_rank = x.api_win_rank;
                if (x.api_get_ship != null) drop_shipid = x.api_get_ship.api_ship_id;

                new Thread(() =>
                {
                    string post = string.Join("&", new string[] {
                            "apiver=" + 3,
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
            proxy.api_req_map_start.TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data));
            proxy.api_req_map_next.TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data));

            // To gether dropped ship
            proxy.api_req_sortie_battleresult.TryParse<kcsapi_battleresult>().Subscribe(x => drop_report(x.Data));
            proxy.api_req_combined_battle_battleresult.TryParse<kcsapi_battleresult>().Subscribe(x => drop_report(x.Data));

            // Ranking List
            var host = "";
            var api_req_ranking = api_session.Where(x => x.Request.PathAndQuery.StartsWith("/kcsapi/api_req_ranking/"));
            api_req_ranking.Subscribe(x => host = x.Request.Headers.Host);

            api_req_ranking.TryParse<kcsapi_req_ranking>()
                .Where(x => x.IsSuccess)
                .Subscribe(x =>
                {
                    ///////////////////////////////////////////////////////////////////
                    if (!Enabled) return; // Disabled sending statistics data to server

                    int MemberId;
                    if (!int.TryParse(KanColleClient.Current.Homeport.Admiral.MemberId, out MemberId)) return;
                    // Cannot got memberid

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
                            "key=" + (MemberId % 10),
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
        }
    }
}
