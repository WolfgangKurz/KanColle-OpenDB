using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Nekoxy;
using MetroTrilithon.Mvvm;
using Livet;

using Grabacr07.KanColleViewer;
using Grabacr07.KanColleWrapper.Models.Raw;
using Grabacr07.KanColleWrapper;

using Logger = KanColleOpenDB.Models.Logger;

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
		private const int MAX_TRY = 3;

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

				Properties.Settings.Default.Enabled = value;
				Properties.Settings.Default.Save();
			}
		}
		#endregion

		#region UseExperimental Property
		public bool UseExperimental
		{
			get { return Properties.Settings.Default.UseExperimental == ExperimentalAge; }
			set
			{
				Properties.Settings.Default.UseExperimental = value ? ExperimentalAge : 0;
				Properties.Settings.Default.Save();

				this.RaisePropertyChanged();
			}
		}
		#endregion

		#region PluginVersion Property
		public string PluginVersion
			=> System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
		#endregion

		#region Property for Map Difficulty
		/// <summary>
		/// Map Difficulty Dictionary
		/// </summary>
		private Dictionary<int, int> mapRankDict = new Dictionary<int, int>();
		#endregion


		#region Properties for Experimental statistics
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

		private bool Initialized { get; set; } = false;
		private void Initialize()
		{
			if (Initialized) return;
			Initialized = true;

			bool IsFirst = Properties.Settings.Default.IsFirst;

			if (IsFirst || DEBUG) // Is the first load after install?
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
					try
					{
						Logger.Log("found", "equip_build");

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

						Logger.Log($"item:{item}, material:{{{string.Join(",", material)}}}, flagship:{flagship}", "equip_build");

						new Thread(() =>
						{
							try
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
									var y = HTTPRequest.Post(OpenDBReport + "equip_build.php", post);
									if (y != null)
									{
										Logger.Log("reported", "equip_build");
										y?.Close();
										break;
									}
									Logger.Log($"failed, retrying ({tries}/{MAX_TRY})", "equip_build");
									tries--;
								}
							}
							catch (Exception e)
							{
								Logger.LogException(this, e, "equip_build reporting");
							}
						}).Start();
					}
					catch (Exception e)
					{
						Logger.LogException(this, e, "equip_build assembling");
					}
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
					try
					{
						if (!ship_dev_wait) return; // Not created
						ship_dev_wait = false;

						Logger.Log("found", "ship_build");

						///////////////////////////////////////////////////////////////////
						if (!Enabled) return; // Disabled sending statistics data to server

						var dock = x.Data.SingleOrDefault(y => y.api_id == ship_dev_dockid);
						var flagship = homeport.Organization.Fleets[1].Ships[0].Info.Id;
						var ship = dock.api_created_ship_id;

						Logger.Log($"res:{{{dock.api_item1},{dock.api_item2},{dock.api_item3},{dock.api_item4},{dock.api_item5}}}, flagship:{flagship}, result:{ship}", "ship_build");

						new Thread(() =>
						{
							try
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
									var y = HTTPRequest.Post(OpenDBReport + "ship_build.php", post);
									if (y != null)
									{
										Logger.Log("reported", "ship_build");
										y?.Close();
										break;
									}
									Logger.Log($"failed, retrying ({tries}/{MAX_TRY})", "ship_build");
									tries--;
								}
							}
							catch (Exception e)
							{
								Logger.LogException(this, e, "ship_build assembling");
							}
						}).Start();
					}
					catch (Exception e)
					{
						Logger.LogException(this, e, "ship_build assembling");
					}
				});
			#endregion

			#region Drop (Get new ship from sea)
			int drop_world = 0;
			int drop_map = 0;
			int drop_node = 0;
			int drop_maprank = 0;

			int drop_formation = 0;
			int[] drop_ships = new int[6];
			int[] drop_ships2 = null;
			Action<battle_base, int> drop_update_enemy = (data, formation) =>
			{
				drop_formation = formation;

				drop_ships = new int[6];
				Array.Copy(data.api_ship_ke, drop_ships, data.api_ship_ke.Length);

				if (data.api_ship_ke_combined != null)
				{
					drop_ships2 = new int[6];
					Array.Copy(data.api_ship_ke_combined, drop_ships2, data.api_ship_ke_combined.Length);
				}
				else
					drop_ships2 = null;

				
			};
			Func<string> drop_make_enemy = () =>
			{
				var sb = new StringBuilder();
				sb.Append("{");
				sb.AppendFormat("\"formation\":{0}", drop_formation);
				sb.AppendFormat(",\"ships\":[{0}]", string.Join(",", drop_ships));
				if (drop_ships2 != null)
					sb.AppendFormat(",\"ships2\":[{0}]", string.Join(",", drop_ships2));
				sb.Append("}");
				return sb.ToString();
			};

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
				try
				{
					///////////////////////////////////////////////////////////////////
					if (!Enabled) return; // Disabled sending statistics data to server

					Logger.Log("found", "ship_drop");

					if (homeport.Organization.Ships.Count >= homeport.Admiral.MaxShipCount)
						return; // Maximum ship-count

					var drop_inventory = 0;
					var drop_shipid = 0;
					var drop_rank = x.api_win_rank;
					if (x.api_get_ship != null) drop_shipid = x.api_get_ship.api_ship_id;

					Logger.Log("inventory calculating", "ship_drop");

					var tree = new List<int>();
					if (drop_shipid > 0)
					{
						var root = drop_shipid;
						while (true)
						{
							var ship = KanColleClient.Current.Master.Ships.Where(y => int.Parse(y.Value?.RawData?.api_aftershipid ?? "0") == root);
							if (!ship.Any()) break;

							root = ship.FirstOrDefault().Value?.Id ?? 0;
						}

						while (!tree.Contains(root) && root > 0)
						{
							tree.Add(root);

							var ship = KanColleClient.Current.Master.Ships.FirstOrDefault(y => y.Value.Id == root);
							root = int.Parse(ship.Value?.RawData?.api_aftershipid ?? "0");
						}
						drop_inventory = homeport.Organization.Ships.Count(y => tree.Contains(y.Value.Info.Id));
					}

					Logger.Log($"world:{drop_world}, map:{drop_map}, node:{drop_node}, rank:{drop_rank}, maprank:{(mapRankDict.ContainsKey(drop_map) ? mapRankDict[drop_map] : drop_maprank)}, inventory:{drop_inventory}, result:{drop_shipid}", "ship_drop");

					new Thread(() =>
					{
						try
						{
							string post = string.Join("&", new string[] {
								"apiver=" + 5,
								"world=" + drop_world,
								"map=" + drop_map,
								"node=" + drop_node,
								"rank=" + drop_rank,
								"maprank=" + (mapRankDict.ContainsKey(drop_map) ? mapRankDict[drop_map] : drop_maprank),
								"enemy=" + drop_make_enemy(),
								"inventory=" + drop_inventory,
								"result=" + drop_shipid
							});

							int tries = MAX_TRY;
							while (tries > 0)
							{
								var y = HTTPRequest.Post(OpenDBReport + "ship_drop.php", post);
								if (y != null)
								{
									Logger.Log("reported", "ship_drop");
									y?.Close();
									break;
								}
								Logger.Log($"failed, retrying ({tries}/{MAX_TRY})", "ship_drop");
								tries--;
							}
						}
						catch (Exception e)
						{
							Logger.LogException(this, e, "ship_drop reporting");
						}
					}).Start();
				}
				catch (Exception e)
				{
					Logger.LogException(this, e, "ship_drop assembling");
				}
			});

			// To gether Map-id
			proxy.api_req_map_start.TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data, true));
			proxy.api_req_map_next.TryParse<kcsapi_start_next>().Subscribe(x => drop_prepare(x.Data, false));


			#region 통상 - 주간전
			proxy.api_req_sortie_battle
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));
			#endregion

			#region 통상 - 개막야전
			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_battle_midnight/sp_midnight")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));
			#endregion

			#region 항공전 - 주간전 / 공습전 - 주간전
			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_sortie/airbattle")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));

			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_sortie/ld_airbattle")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));
			#endregion

			#region 연합함대 - 주간전
			proxy.api_req_combined_battle_battle
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));

			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_combined_battle/battle_water")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));
			#endregion

			#region 연합vs연합 - 주간전
			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_combined_battle/ec_battle")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));

			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_combined_battle/each_battle")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));

			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_combined_battle/each_battle_water")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));
			#endregion

			#region 연합함대 - 항공전 / 공습전
			proxy.api_req_combined_battle_airbattle
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));

			proxy.ApiSessionSource.Where(x => x.Request.PathAndQuery == "/kcsapi/api_req_combined_battle/ld_airbattle")
				.TryParse<battle_base>().Subscribe(x => drop_update_enemy(x.Data, x.Data.api_formation[1]));
			#endregion

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
					try
					{
						Logger.Log("found", "equip_remodel");

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

						Logger.Log($"flagship:{flagship}, assistant:{assistant}, item:{item}, level:{level}, result:{result}", "equip_remodel");

						new Thread(() =>
						{
							try
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
										Logger.Log("reported", "equip_remodel");
										y?.Close();
										break;
									}
									Logger.Log($"failed, retrying ({tries}/{MAX_TRY})", "equip_remodel");
									tries--;
								}
							}
							catch (Exception e)
							{
								Logger.LogException(this, e, "equip_remodel assembling");
							}
						}).Start();
					}
					catch (Exception e)
					{
						Logger.LogException(this, e, "equip_remodel assembling");
					}
				});
			#endregion


			#region Experimental datas

			#endregion
		}
	}
}
