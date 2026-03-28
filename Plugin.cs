using System;
using System.IO;

using UnityEngine;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using Data.Serialization;
using Newtonsoft.Json;

using SaveFileManager = MMJsonDataReadWriter<DataManager>;

namespace CotlSaveExtractorLoader
{
    public struct Metadata
    {
        public const string Id = "mod.osoclos.cotl.save-extractor-loader";
        public const string Name = "Cult of the Lamb Save File Extractor and Loader";

        public const string Version = "1.0.1.0";
    }

    [BepInPlugin(Metadata.Id, Metadata.Name, Metadata.Version)]
    [BepInProcess("Cult of the Lamb.exe")]

    [HarmonyPatch]
    public class Plugin: BaseUnityPlugin
    {
        private static ManualLogSource _logger;

        public static class Settings
        {
            public readonly struct SECTION_NAMES
            {
                public const string ENABLED_DISABLED = "Enabled/Disabled";
                public const string GENERAL = "General";
            }

            public static ConfigEntry<bool> isEnabled;

            public static ConfigEntry<string> jsonSuffix;
            public static ConfigEntry<bool> forceLoadJson;

            public static ConfigEntry<bool> formatJson;

            public static void Init(ConfigFile config)
            {
                isEnabled = config.Bind(SECTION_NAMES.ENABLED_DISABLED, "ExtractSaveFiles", true, "Enable extraction of save files.");
                if (!isEnabled.Value)
                {
                    _logger.LogInfo("Plugin is disabled! No extraction of save files nor loading of extracted .json files will occur!");
                    return;
                }

                jsonSuffix = config.Bind(SECTION_NAMES.GENERAL, "ExtractedJsonSuffix", "extracted", "The string that will be appended after the filename to prevent overwriting of the default slot_#.json file. Leaving it empty will overwrite it.");
                forceLoadJson = config.Bind(SECTION_NAMES.GENERAL, "ForceLoadJsonFiles", true, "Whether to read the extracted .json save files instead of the .mp save files, if available.");
                
                formatJson = config.Bind(SECTION_NAMES.GENERAL, "FormatJsonFiles", true, "Whether to prettify the extracted .json save file into a nice format upon saving.");
            }
        }

        public static string saveDirPath;

        private static bool _needsSerializerPatchUpdate = true;

        protected void Awake()
        {
            _logger = Logger;

            Settings.Init(Config);
            Settings.formatJson.SettingChanged += (_self, _args) => _needsSerializerPatchUpdate = true;

            Harmony.CreateAndPatchAll(typeof(Plugin));

            saveDirPath = Path.Combine(Application.persistentDataPath, "saves");

            _logger.LogMessage("Plugin has been loaded!");
        }

        [HarmonyPatch(typeof(SaveFileManager), "Write")]
        [HarmonyPostfix]
        public static void SaveFileManager_Write(SaveFileManager __instance, DataManager data, string filename, bool encrypt, bool backup)
        {
            if (_needsSerializerPatchUpdate) PatchSerializer();

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
            bool isSaveFile = filename.StartsWith("slot_");
            if (!isSaveFile) return true;

            string parsedFilename = ParseRawFilename(filename);

            if (Settings.forceLoadJson.Value && File.Exists(Path.Combine(saveDirPath, parsedFilename))) filename = parsedFilename;
            _logger.LogMessage("Loading extracted \"" + filename + "\" save file...");

            return true;
        }

        private static void PatchSerializer()
        {
            _needsSerializerPatchUpdate = false;

            bool doFormatting = Settings.formatJson.Value;

            MMSerialization.JsonSerializerSettings.Formatting = doFormatting ? Formatting.Indented : Formatting.None;
            MMSerialization.JsonSerializer = JsonSerializer.Create(MMSerialization.JsonSerializerSettings);
        }

        private static string ParseRawFilename(string filename)
        {
            return Path.ChangeExtension(Settings.jsonSuffix.Value == "" ? filename : Path.GetFileNameWithoutExtension(filename) + "-" + Settings.jsonSuffix.Value, ".json");
        }
    }
}
