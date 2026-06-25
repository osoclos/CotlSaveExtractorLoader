using System;
using System.IO;

using UnityEngine;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using Data.Serialization;
using Newtonsoft.Json;

using SaveFileDataIO = MMJsonDataReadWriter<DataManager>;
using SaveFileMetaIO = MMJsonDataReadWriter<MetaData>;

namespace CotlSaveExtractorLoader
{
    public struct PluginMetadata
    {
        public const string Id = "mod.osoclos.cotl.save-extractor-loader";
        public const string Name = "Cult of the Lamb Save File Extractor and Loader";

        public const string Version = "1.0.2.0";
    }

    [BepInPlugin(PluginMetadata.Id, PluginMetadata.Name, PluginMetadata.Version)]
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

            public static ConfigEntry<bool> includeMetaFiles;

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

                includeMetaFiles = config.Bind(SECTION_NAMES.GENERAL, "IncludeMetaFiles", true, "Include meta_#.mp/json files in the extraction and loading processes.");
            }
        }

        public static string saveDirPath;

        private static bool _needsSerializerPatchUpdate = true;

        protected void Awake()
        {
            _logger = Logger;

            Settings.Init(Config);
            Settings.formatJson.SettingChanged += (_self, args) =>_needsSerializerPatchUpdate = true;

            Harmony.CreateAndPatchAll(typeof(Plugin));

            saveDirPath = Path.Combine(Application.persistentDataPath, "saves");

            _logger.LogMessage("Plugin has been loaded!");
        }

        [HarmonyPatch(typeof(SaveFileDataIO), "Write")]
        [HarmonyPostfix]
        public static void SaveFileDataIO_Write(SaveFileDataIO __instance, DataManager data, string filename, bool encrypt, bool backup)
        {
            if (_needsSerializerPatchUpdate) PatchSerializer();
            if (filename.StartsWith("slot_")) WriteToIO(__instance, data, filename);
        }

        [HarmonyPatch(typeof(SaveFileDataIO), "Read")]
        [HarmonyPrefix]
        public static bool SaveFileDataIO_Read(ref string filename)
        {
            if (filename.StartsWith("slot_")) filename = ParseForIORead(filename);
            return true;
        }

        [HarmonyPatch(typeof(SaveFileMetaIO), "Write")]
        [HarmonyPostfix]
        public static void SaveFileMetaIO_Write(SaveFileMetaIO __instance, MetaData data, string filename, bool encrypt, bool backup)
        {
            if (_needsSerializerPatchUpdate) PatchSerializer();
            if (Settings.includeMetaFiles.Value && filename.StartsWith("meta_")) WriteToIO(__instance, data, filename);
        }

        [HarmonyPatch(typeof(SaveFileMetaIO), "Read")]
        [HarmonyPrefix]
        public static bool SaveFileMetaIO_Read(ref string filename)
        {
            if (Settings.includeMetaFiles.Value && filename.StartsWith("meta_")) filename = ParseForIORead(filename);
            return true;
        }

        private static void WriteToIO<T>(MMJsonDataReadWriter<T> io, T data, string filename)
        {
            string parsedFilename = ParseRawFilename(filename);

            var WriteJson = AccessTools.MethodDelegate<Action<MMJsonDataReadWriter<T>, T, string, bool, bool>>(AccessTools.Method(typeof(MMJsonDataReadWriter<T>), "WriteJson"));
            WriteJson.Invoke(io, data, parsedFilename, false, false);

            _logger.LogMessage("Extraction of \"" + filename + "\" is complete and saved as \"" + parsedFilename + "\".");
        }

        private static string ParseForIORead(string filename)
        {
            string parsedFilename = ParseRawFilename(filename);

            bool canExtract = Settings.forceLoadJson.Value && File.Exists(Path.Combine(saveDirPath, parsedFilename));

            _logger.LogMessage("Loading " + (canExtract ? "extracted" : "encrypted") + " \"" + filename + "\" save file...");
            return canExtract ? parsedFilename : filename;
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
