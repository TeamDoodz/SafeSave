using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SafeSave {
	internal static class NewSaveSystem {
		public const string OLD_EXT = ".bepis";
		public const string NEW_EXT = ".json";

		static ConfigEntry<bool> DoNewSaveSystem = MainPlugin.cfg.Bind(
			"NewSaveSystem",
			nameof(DoNewSaveSystem),
			true,
			"Uses a more secure method of storing save data. Turning this on will change the save directory to prevent breaking old saves."
		);

		internal static void Init() {
			if(DoNewSaveSystem.Value) {
				MainPlugin.logger.LogInfo($"DoNewSaveSystem is enabled. Save files can be found at {NewSavePath}.");
			}
		}

		internal static string NewSavePath => Path.Combine(Application.persistentDataPath, "SafeSave");

		#region Redirection

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.BaseSavePath), MethodType.Getter)]
		static class RedirectSavePathPatch {
			static bool Prefix(ref string __result) {
				if(!DoNewSaveSystem.Value) return true;
				__result = NewSavePath;
				return false;
			}
		}

		[HarmonyPatch]
		static class RedirectSaveFileNamePatch {
			static IEnumerable<MethodBase> TargetMethods() {
				Type gps = typeof(GameProgressSaver);
				BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

				yield return gps.GetMethod(nameof(GameProgressSaver.DifficultySavePath), flags);
				yield return gps.GetMethod(nameof(GameProgressSaver.LevelProgressPath), flags);
				yield return gps.GetProperty(nameof(GameProgressSaver.generalProgressPath), flags).GetMethod;
				yield return gps.GetProperty(nameof(GameProgressSaver.cyberGrindHighScorePath), flags).GetMethod;
				yield return gps.GetProperty(nameof(GameProgressSaver.customLevelProgressPath), flags).GetMethod;
			}

			static void Postfix(ref string __result) {
				if(DoNewSaveSystem.Value) {
					string newRes = __result.Replace(OLD_EXT, NEW_EXT);
					MainPlugin.logger.LogDebug($"{__result} -> {newRes}");
					__result = newRes; 
				}
			}
		}

		#endregion

		#region Deserialization

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.WriteFile))]
		static class UseJsonWritePatch {
			static bool Prefix(string path, object data) {
				if(!DoNewSaveSystem.Value) return true;
				if(data is GameProgressMoneyAndGear progress) {
					GameProgressSaver.PrepareFs();
					JsonSaver.SaveData(path, progress);
				} else if(data is GameProgressData general) {
					GameProgressSaver.PrepareFs();
					JsonSaver.SaveData(path, general);
				} else if(data is RankData rank) {
					GameProgressSaver.PrepareFs();
					JsonSaver.SaveData(path, rank);
				} else if(data is CyberRankData cgrank) {
					GameProgressSaver.PrepareFs();
					JsonSaver.SaveData(path, cgrank);
				}else {
					MainPlugin.logger.LogError($"Unexpected data type {data.GetType().FullName}");
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.GetCyberRankData))]
		static class UseJsonGetCyberRankDataPatch {
			static bool Prefix(ref CyberRankData __result) {
				if(!DoNewSaveSystem.Value) return true;
				GameProgressSaver.PrepareFs();
				CyberRankData cyberRankData = JsonSaver.LoadData<CyberRankData>(GameProgressSaver.cyberGrindHighScorePath);
				if(cyberRankData.preciseWavesByDifficulty == null || cyberRankData.preciseWavesByDifficulty.Length != 6) {
					cyberRankData.preciseWavesByDifficulty = new float[6];
				}
				if(cyberRankData.style == null || cyberRankData.style.Length != 6) {
					cyberRankData.style = new int[6];
				}
				if(cyberRankData.kills == null || cyberRankData.kills.Length != 6) {
					cyberRankData.kills = new int[6];
				}
				if(cyberRankData.time == null || cyberRankData.time.Length != 6) {
					cyberRankData.time = new float[6];
				}
				__result = cyberRankData;
				return false;
			}
		}

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.GetDirectorySlotData))]
		static class UseJsonGetDirectorySlotDataPatch {
			static bool Prefix(ref SaveSlotMenu.SlotData __result, string path) {
				if(!DoNewSaveSystem.Value) return true;
				int num = 0;
				int num2 = 0;
				for(int i = 0; i < 6; i++) {
					string saveName = GameProgressSaver.DifficultySavePath(i);
					GameProgressSaver.PrepareFs();
					GameProgressData gameProgressData = JsonSaver.LoadData<GameProgressData>(Path.Combine(path, saveName));
					if(gameProgressData != null && (gameProgressData.levelNum > num || (gameProgressData.levelNum == num && gameProgressData.difficulty > num2))) {
						num = gameProgressData.levelNum;
						num2 = gameProgressData.difficulty;
					}
				}
				__result = new SaveSlotMenu.SlotData {
					exists = true,
					highestDifficulty = num2,
					highestLvlNumber = num
				};
				return false;
			}
		}

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.GetGameProgress), new Type[] { typeof(string), typeof(int) }, new ArgumentType[] { ArgumentType.Out, ArgumentType.Normal })]
		static class UseJsonGetGameProgressPatch {
			static bool Prefix(ref GameProgressData __result, out string path, int difficulty) {
				path = null;
				if(!DoNewSaveSystem.Value) return true;

				path = difficulty > 0 ? GameProgressSaver.DifficultySavePath(difficulty) : GameProgressSaver.currentDifficultyPath;

				GameProgressSaver.PrepareFs();
				GameProgressData data = JsonSaver.LoadData<GameProgressData>(path);
				if(data.primeLevels == null || data.primeLevels.Length == 0) {
					data.primeLevels = new int[3];
				}

				__result = data;

				return false;
			}
		}

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.GetGeneralProgress))]
		static class UseJsonGetGeneralProgressPatch {
			static bool Prefix(ref GameProgressMoneyAndGear __result) {
				if(!DoNewSaveSystem.Value) return true;

				GameProgressSaver.PrepareFs();
				GameProgressMoneyAndGear data = JsonSaver.LoadData<GameProgressMoneyAndGear>(GameProgressSaver.generalProgressPath);

				if(data.secretMissions == null || data.secretMissions.Length == 0) {
					data.secretMissions = new int[10];
				}
				if(data.limboSwitches == null || data.limboSwitches.Length == 0) {
					data.limboSwitches = new bool[4];
				}
				if(data.newEnemiesFound == null) {
					data.newEnemiesFound = new int[Enum.GetValues(typeof(EnemyType)).Length];
				}

				__result = data;

				return false;
			}
		}

		internal static bool overrideForceNull = false;
		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.GetRankData), new Type[] { typeof(string), typeof(int), typeof(bool) }, new ArgumentType[] { ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
		static class UseJsonGetRankDataPatch {
			static bool Prefix(ref RankData __result, out string path, int lvl, bool returnNull) {
				path = null;
				if(!DoNewSaveSystem.Value) return true;

				if(overrideForceNull) {
					__result = null;
					return false;
				}

				path = lvl > 0 ? GameProgressSaver.LevelProgressPath(lvl) : GameProgressSaver.resolveCurrentLevelPath;

				GameProgressSaver.PrepareFs();
				if(StatsManager.Instance == null) MainPlugin.logger.LogWarning("Stats manager null");
				if(PrefsManager.Instance == null) MainPlugin.logger.LogWarning("Prefs manager null");
				RankData data = JsonSaver.LoadRankData(path, returnNull, StatsManager.Instance);
				__result = data;

				return false;
			}
		}

		[HarmonyPatch(typeof(GameProgressSaver))]
		[HarmonyPatch(nameof(GameProgressSaver.GetSlots))]
		static class UseJsonGetSlotsPatch {
			static bool Prefix(ref SaveSlotMenu.SlotData[] __result) {
				if(!DoNewSaveSystem.Value) return true;

				int currSlot = GameProgressSaver.currentSlot;
				List<SaveSlotMenu.SlotData> list = new List<SaveSlotMenu.SlotData>();

				for(int i = 0; i < 5; i++) {
					GameProgressSaver.currentSlot = i;

					GameProgressSaver.PrepareFs();
					SaveSlotMenu.SlotData data = JsonSaver.LoadData<SaveSlotMenu.SlotData>(GameProgressSaver.generalProgressPath);

					if(data == null) {
						list.Add(new SaveSlotMenu.SlotData {
							exists = false
						});
					} else {
						list.Add(data);
					}
				}

				GameProgressSaver.currentSlot = currSlot;

				__result = list.ToArray();

				return false;
			}
		}

		#endregion

		[HarmonyPatch]
		static class DirectoryGetFilesHack {
			static IEnumerable<MethodBase> GetMethods() {
				Type dir = typeof(Directory);
				BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

				yield return AccessTools.Method(dir, nameof(Directory.GetFiles), new Type[] { typeof(string), typeof(string) });
				yield return AccessTools.Method(dir, nameof(Directory.GetFiles), new Type[] { typeof(string), typeof(string), typeof(SearchOption) });
			}
			static void Prefix(ref string searchPattern) {
				if(searchPattern == "*" + OLD_EXT) searchPattern = "*" + NEW_EXT;
			}
		}
	}
}
