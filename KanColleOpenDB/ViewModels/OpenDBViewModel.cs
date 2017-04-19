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
#if DEBUG
		private bool DEBUG => true;
#else
		private bool DEBUG => false;
#endif

		/// <summary>
		/// OpenDB report host
		/// </summary>
		private string OpenDBReport => "http://swaytwig.com/opendb/report/";

		/// <summary>
		/// Retry count
		/// </summary>
		private int MAX_TRY => 3;

		/// <summary>
		/// Age of Experimental
		/// </summary>
		private int ExperimentalAge => 1;

		#region Enabled Property

		private bool _Enabled;
		public bool Enabled
		{
			get { return this._Enabled; }
			set
			{
				this._Enabled = value;
				this.RaisePropertyChanged();

				Properties.Settings.Default["Enabled"] = value;
				Properties.Settings.Default.Save();
			}
		}

		#endregion

		#region PluginVersion Property

		public string PluginVersion
		{
			get
			{
				return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
			}
		}

		#endregion

		#region Property for Map Difficulty
		/// <summary>
		/// Map Difficulty Dictionary
		/// </summary>
		private Dictionary<int, int> mapRankDict = new Dictionary<int, int>();
		#endregion


		#region Properties for Experimental statistics
		/// <summary>
		/// Expedition list
		/// </summary>
		kcsapi_mission[] c_mission { get; } = null;
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

			KanColleClient.Current.Proxy.api_start2.TryParse<kcsapi_start2>()
				.Where(x => x.IsSuccess)
				.Subscribe(x =>
				{
					x.Data.api_mst_mission = this.c_mission;
				});
		}

		private bool Initialized { get; set; } = false;
		private void Initialize()
		{
			if (Initialized) return;
			Initialized = true;

			bool IsFirst = Properties.Settings.Default.IsFirst;

			if(IsFirst || DEBUG) // Is the first load after install?
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

						Application.Current.Dispatcher.Invoke(() =>
						{
							this.Enabled = vmodel.DialogResult;
							Properties.Settings.Default.UseExperimental = vmodel.UseExperimental ? this.ExperimentalAge : 0;
						});
					});
				}).Start();
			}
			else
			{
				this.Enabled = Properties.Settings.Default.Enabled;
			}

			// Save IsFirst setting
			Properties.Settings.Default.IsFirst = false;
			Properties.Settings.Default.Save();

			var homeport = KanColleClient.Current.Homeport;
			var proxy = KanColleClient.Current.Proxy;
			var api_session = proxy.ApiSessionSource;

			#region Development (Create slotitem at arsenal)
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
			#endregion

			#region Construction (Build new ship at arsenal)
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
			#endregion

			#region Drop (Get new ship from sea)
			int drop_world = 0;
			int drop_map = 0;
			int drop_node = 0;
			int drop_maprank = 0;

			var drop_prepare = new Action<kcsapi_start_next, bool>((x, y) =>
			{
				drop_world = x.api_maparea_id;
				drop_map = x.api_mapinfo_no;
				drop_node = x.api_no;
				if(y) drop_maprank = x.api_eventmap?.api_selected_rank ?? 0;
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
							"maprank=" + (mapRankDict.ContainsKey(drop_map) ? mapRankDict[drop_map] : drop_maprank),
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
			proxy.api_req_map_start.TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data, true));
			proxy.api_req_map_next.TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data, false));

			// To gether dropped ship
			proxy.api_req_sortie_battleresult.TryParse<kcsapi_battleresult>().Subscribe(x => drop_report(x.Data));
			proxy.api_req_combined_battle_battleresult.TryParse<kcsapi_battleresult>().Subscribe(x => drop_report(x.Data));

			// Map rank getter
			var api_req_select_eventmap_rank = api_session.Where(x => x.Request.PathAndQuery.StartsWith("/kcsapi/api_req_map/select_eventmap_rank"));
			api_req_select_eventmap_rank
				.TryParse<KanColleOpenDB.Models.kcsapi_empty_result>()
				.Where(x => x.IsSuccess)
				.Subscribe(x =>
				{
					int map, rank;
					try
					{
						if (!int.TryParse(x.Request["api_map_no"], out map)) return;
						if (!int.TryParse(x.Request["api_rank"], out rank)) return;
					}
					catch { return; }

					if (mapRankDict.ContainsKey(map))
						mapRankDict.Remove(map);

					mapRankDict.Add(map, rank);
				});

			var api_req_member_rank = api_session.Where(x => x.Request.PathAndQuery.StartsWith("/kcsapi/api_get_member/mapinfo"));
			api_req_member_rank
				.TryParse<kcsapi_mapinfo>()
				.Where(x => x.IsSuccess)
				.Subscribe(x =>
				{
					int eventMapCount = 0;
					foreach (var map in x.Data.api_map_info)
					{
						if (map.api_eventmap == null) continue;

						eventMapCount++;
						try
						{
							if (map.api_eventmap.api_selected_rank.HasValue)
							{
								if (mapRankDict.ContainsKey(eventMapCount))
									mapRankDict.Remove(eventMapCount);

								mapRankDict.Add(eventMapCount, map.api_eventmap.api_selected_rank.Value);
							}
						}
						catch { continue; }
					}
				});
			#endregion

			#region Slotitem Improvement (Remodel slotitem)
			proxy.api_req_kousyou_remodel_slot.TryParse<KanColleOpenDB.Models.kcsapi_remodel_slot>()
				.Where(x => x.IsSuccess).Subscribe(x =>
				{
					///////////////////////////////////////////////////////////////////
					if (!Enabled) return; // Disabled sending statistics data to server

					if (int.Parse(x.Request["api_certain_flag"]) == 1) return; // 100% improvement option used

					var item = x.Data.api_remodel_id[0]; // Slotitem master id
					var flagship = homeport.Organization.Fleets[1].Ships[0].Info.Id; // Flagship (Akashi or Akashi Kai)
					var assistant = x.Data.api_voice_ship_id; // Assistant ship master id
					var level = 0; // After level
					var result = x.Data.api_remodel_flag; // Is succeeded?

					// !!! api_after_slot is null when failed to improve !!!

					if (result == 1)
					{
						level = x.Data.api_after_slot.api_level - 1;
						if (level < 0) level = 10;
					}
					else
					{
						level = homeport.Itemyard.SlotItems[
							int.Parse(x.Request["api_slot_id"])
						].Level;
					}

					new Thread(() =>
					{
						string post = string.Join("&", new string[] {
							"apiver=" + 2,
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


			#region Experimental datas

			#region Expedition result data
			proxy.api_req_mission_result.TryParse<kcsapi_mission_result>()
				.Where(x => x.IsSuccess)
				.Subscribe(x =>
				{
					///////////////////////////////////////////////////////////////////
					if (!Enabled) return; // Disabled sending statistics data to server
					if (this.ExperimentalAge != Properties.Settings.Default.UseExperimental) return; // Disabled or Expired sending experimental statistics data to server

					if (this.c_mission == null) return; // Cannot track expedition id

					var ships = x.Data.api_ship_id.Where(y => y > 0);

					var BrilliantConds = ships.Count(y => homeport.Organization.Ships.FirstOrDefault(z => z.Value.Id == y).Value?.Condition >= 50);
					var Canisters = ships.Sum(y => homeport.Organization.Ships.FirstOrDefault(z => z.Value.Id == y).Value?.Slots.Count(z => z.Item.Info.Id == 75)) ?? 0; // 75 = Canister
					var ObtainItems = x.Data.api_useitem_flag;

					new Thread(() =>
					{
						string post = string.Join("&", new string[] {
							"apiver=" + this.ExperimentalAge,
							"api_id=succeeded_expedition",
							"exp_id=" + c_mission.FirstOrDefault(y=>y.api_name == x.Data.api_quest_name)?.api_id.ToString() ?? "0",
							"brilliant=" + BrilliantConds,
							"canister=" + Canisters,
							"obtain=" + string.Join(",", ObtainItems),
							"result=" + x.Data.api_clear_result.ToString()
						});

						int tries = MAX_TRY;
						while (tries > 0)
						{
							var y = HTTPRequest.Post(OpenDBReport + "experimental.php", post);
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

			#endregion
		}
	}
}
