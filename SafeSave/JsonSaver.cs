using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using Newtonsoft.Json;
using UnityEngine;

namespace SafeSave {
	public static class JsonSaver {
		static ConfigEntry<Formatting> SaveFileFormatting = MainPlugin.cfg.Bind(
			"NewSaveSystem",
			nameof(SaveFileFormatting),
			Formatting.Indented,
			"What formatting to use for save files. Indented is more readable, but None takes up less space. Changing this will not break existing saves."
		);

		static JsonSerializerSettings settings = new JsonSerializerSettings() { 
			TypeNameHandling = TypeNameHandling.None
		};

		private struct TempRankData {
			public int[] ranks;

			public int secretsAmount;

			public bool[] secretsFound;

			public bool challenge;

			public int levelNumber;

			public bool[] majorAssists;
		}

		public static RankData LoadRankData(string path, bool returnNull, StatsManager sm) {
			try {
				if(!File.Exists(path)) {
					if(!returnNull && sm == null) {
						MainPlugin.logger.LogError("A null StatsManager was passed.");
						return null;
					}
					return returnNull ? null : new RankData(sm);
				}
				string data = File.ReadAllText(path);
				var temp = JsonConvert.DeserializeObject<TempRankData>(data, settings);
				NewSaveSystem.overrideForceNull = true;
				var surrogate = new RankData(sm);
				NewSaveSystem.overrideForceNull = false;
				surrogate.ranks = temp.ranks;
				surrogate.secretsAmount = temp.secretsAmount;
				surrogate.secretsFound = temp.secretsFound;
				surrogate.challenge = temp.challenge;
				surrogate.levelNumber = temp.levelNumber;
				surrogate.majorAssists = temp.majorAssists;
				return surrogate;
			} catch(Exception e) {
				MainPlugin.logger.LogError(e);
				MainPlugin.logger.LogError("An error occoured while loading saves and the game will now close.");
				Application.Quit();
				return new RankData(sm);
			}
		}
		public static T LoadData<T>(string path) where T : new() {
			try {
				if(!File.Exists(path)) {
					return new T();
				}
				string data = File.ReadAllText(path);
				return JsonConvert.DeserializeObject<T>(data, settings);
			} catch(Exception e) {
				MainPlugin.logger.LogError(e);
				MainPlugin.logger.LogError("An error occoured while loading saves and the game will now close.");
				Application.Quit();
				return default(T);
			}
		}
		public static void SaveData<T>(string path, T data) {
			try {
				settings.Formatting = SaveFileFormatting.Value;
				string dataString = JsonConvert.SerializeObject(data, settings);
				File.WriteAllText(path, dataString);
			} catch(Exception e) {
				MainPlugin.logger.LogError(e);
				MainPlugin.logger.LogError("An error occoured while saving.");
			}
		}
	}
}
