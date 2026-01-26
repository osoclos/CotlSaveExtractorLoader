using System;
using System.IO;
using System.Security.Cryptography;

using UnityEngine;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using SaveFileManager = MMJsonDataReadWriter<DataManager>;

namespace CotlSaveExtractorLoader
{
    public struct Metadata
    {
        public const string Id = "mod.osoclos.cotl.save-extractor-loader";
        public const string Name = "Cult of the Lamb Save File Extractor and Loader";

        public const string Version = "1.0.0.0";
    }

    [BepInPlugin(Metadata.Id, Metadata.Name, Metadata.Version)]
    [BepInProcess("Cult of the Lamb.exe")]

    [HarmonyPatch]
    public class Plugin: BaseUnityPlugin
    {
		public static ConfigEntry<bool> isEnabled;

        public static ConfigEntry<string> jsonSuffix;
        public static ConfigEntry<bool> forceLoadJson;

		public static string saveDirPath;

		private static ManualLogSource _logger;

        private void Awake()
        {
            isEnabled = Config.Bind("Enabled/Disabled", "ExtractSaveFiles", true, "Enable extraction of save files.");
			if (!isEnabled.Value)
			{
				Logger.LogInfo("Plugin is disabled! No extraction of save files nor loading of extracted .json files will occur!");
				return;
			}

            jsonSuffix = Config.Bind("General", "ExtractedJsonSuffix", "extracted", "The string that will be appended after the filename to prevent overwriting of the default slot_#.json file. Leaving it empty will overwrite it.");
            forceLoadJson = Config.Bind("General", "ForceLoadJsonFiles", true, "Whether to read the extracted .json save files instead of the .mp save files, if available.");

            saveDirPath = Path.Combine(Application.persistentDataPath, "saves");

			_logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
			Logger.LogMessage("Plugin has been loaded!");
        }

        [HarmonyPatch(typeof(SaveFileManager), "Write")]
        [HarmonyPostfix]
        public static void SaveFileManager_Write(SaveFileManager __instance, DataManager data, string filename, bool encrypt, bool backup)
        {
            bool isSaveFile = filename.StartsWith("slot_");
			if (!isSaveFile) return;

            var WriteJson = AccessTools.MethodDelegate<Action<SaveFileManager, DataManager, string, bool, bool>>(AccessTools.Method(typeof(SaveFileManager), "WriteJson"));

			string parsedFilename = ParseRawFilename(filename);
            WriteJson.Invoke(__instance, data, parsedFilename, false, false);

            _logger.LogMessage("Extraction of \"" + filename + "\" is complete and saved as \"" + parsedFilename + "\".");
        }

        [HarmonyPatch(typeof(SaveFileManager), "Read")]
        [HarmonyPrefix]
        public static bool SaveFileManager_Read(ref string filename)
        {
            string parsedFilename = ParseRawFilename(filename);

            if (forceLoadJson.Value && File.Exists(Path.Combine(saveDirPath, parsedFilename))) filename = parsedFilename;
            return true;
        }

        private static string ParseRawFilename(string filename)
		{
			return Path.ChangeExtension(jsonSuffix.Value == "" ? filename : Path.GetFileNameWithoutExtension(filename) + "-" + jsonSuffix.Value, ".json");
		}
    }
}
