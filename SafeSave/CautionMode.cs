using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SafeSave {
	/// <summary>
	/// Force-closes the game if <see cref="BinaryFormatter"/> is used to deserialize something.
	/// </summary>
	internal static class CautionMode {
		static ConfigEntry<bool> CautionModeEnabled = MainPlugin.cfg.Bind(
			"General",
			nameof(CautionModeEnabled),
			true,
			"Force-closes the game if BinaryFormatter is used to deserialize something."
		);
		public static void Init() {
			if(!CautionModeEnabled.Value) {
				MainPlugin.logger.LogWarning("Caution mode is not enabled. You are not fully secure!");
			}
		}
		[HarmonyPatch(typeof(BinaryFormatter))]
		[HarmonyPatch(nameof(BinaryFormatter.Deserialize), typeof(Stream))]
		static class CautionModePatch {
			static bool Prefix() {
				if(CautionModeEnabled.Value) {
					MainPlugin.logger.LogError("BinaryFormatter.Deserialize was called. Since Caution Mode is enabled, the game will now close.");
					Application.Quit();
					return false;
				} else {
					MainPlugin.logger.LogWarning("BinaryFormatter.Deserialize was called. Since Caution Mode is disabled, the game will continue to run.");
				}
				return true;
			}
		}
	}
}
