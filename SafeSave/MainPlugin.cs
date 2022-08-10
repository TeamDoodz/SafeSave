using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SafeSave {
	[BepInPlugin(GUID, Name, Version)]
	public class MainPlugin : BaseUnityPlugin {
		public const string GUID = "io.github.TeamDoodz.SafeSave";
		public const string Name = "SafeSave";
		public const string Version = "1.0.0";

		internal static ManualLogSource logger;
		internal static Harmony harmony;
		internal static ConfigFile cfg;

		private void Awake() {
			logger = Logger;
			cfg = Config;
			harmony = new Harmony(GUID);
			harmony.PatchAll();

			CautionMode.Init();
			NewSaveSystem.Init();

			logger.LogMessage($"{Name} v{Version} loaded!");
		}
	}
}
