using System;
using System.IO;

using UnityEngine;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using Newtonsoft.Json;

using Data.Serialization;

using SaveFileDataIO = MMJsonDataReadWriter<DataManager>;
using SaveFileMetaIO = MMJsonDataReadWriter<MetaData>;

namespace CotlSaveExtractorLoader
{
    public struct PluginMetadata
    {
        public const string Id = "mod.osoclos.cotl.save-extractor-loader";
        public const string Name = "Cult of the Lamb Save File Extractor and Loader";

        public const string Version = "1.1.0.0";
    }

    [BepInPlugin(PluginMetadata.Id, PluginMetadata.Name, PluginMetadata.Version)]
    [BepInProcess("Cult of the Lamb.exe")]

    [HarmonyPatch]
    public class Plugin: BaseUnityPlugin
    {
        private static ManualLogSource logger;

        public static class Settings
        {
            public readonly struct SECTION_NAMES
            {
                public const string EXTRACTION_LOADING = "Extraction/Loading";
                public const string BEHAVIOR = "Behavior";

                public const string FORMATTING = "Formatting";
            }

            public static ConfigEntry<bool> isEnabled;
            public static ConfigEntry<bool> loadExtractedFiles;

            public static ConfigEntry<bool> overwriteOriginalFiles;
            public static ConfigEntry<bool> lockAndExtractMetaFiles;

            public static ConfigEntry<string> jsonSuffix;
            public static ConfigEntry<bool> formatJson;

            public static void Init(ConfigFile config)
            {
                isEnabled = config.Bind(SECTION_NAMES.EXTRACTION_LOADING, "ExtractSaveFiles", true, "Enable extraction of save files.");
                loadExtractedFiles = config.Bind(SECTION_NAMES.EXTRACTION_LOADING, "LoadExtractedFiles", true, "Whether to read the extracted .json save files instead of the .mp save files, if available.");

                overwriteOriginalFiles = config.Bind(SECTION_NAMES.BEHAVIOR, "OverwriteOriginalFiles", true, "Allow the plugin to overwrite the original encrypted .mp save files.");
                lockAndExtractMetaFiles = config.Bind(SECTION_NAMES.BEHAVIOR, "LockAndExtractMetaFiles", false, "Include meta_#.mp/json files in the extraction and loading process and lock extracted meta_#.json files from being saved by the game.");
                
                jsonSuffix = config.Bind(SECTION_NAMES.FORMATTING, "ExtractedJsonSuffix", "extracted", "The string that will be appended after the filename to prevent overwriting of the default slot_#.json file. Leaving it empty will overwrite it.");
                formatJson = config.Bind(SECTION_NAMES.FORMATTING, "FormatJsonFiles", true, "Whether to prettify the extracted .json save file into a nice format upon saving.");
            }
        }

        public static readonly string FILE_SLOT_PREFIX = "slot_";
        public static readonly string FILE_META_PREFIX = "meta_";

        public static string saveDirPath;

        protected void Awake()
        {
            Settings.Init(Config);

            bool isEnabled = Settings.isEnabled.Value;

            logger = Logger;
            logger.LogMessage(isEnabled ? "Plugin has been loaded!" : "Plugin is disabled! No extraction of save files nor loading of extracted .json files will occur!");

            if (!isEnabled) return;

            saveDirPath = Path.Combine(Application.persistentDataPath, "saves");

            bool doFormatting = Settings.formatJson.Value;

            MMSerialization.JsonSerializerSettings.Formatting = doFormatting ? Formatting.Indented : Formatting.None;
            MMSerialization.JsonSerializer = JsonSerializer.Create(MMSerialization.JsonSerializerSettings);

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(SaveFileDataIO), "Write")]
        [HarmonyPrefix]
        public static bool SaveFileDataIO_Write(SaveFileDataIO __instance, DataManager data, string filename, bool encrypt, bool backup)
        {
            if (filename.StartsWith(FILE_SLOT_PREFIX)) WriteToIO(__instance, data, filename);
            return Settings.overwriteOriginalFiles.Value;
        }

        [HarmonyPatch(typeof(SaveFileDataIO), "Read")]
        [HarmonyPrefix]
        public static bool SaveFileDataIO_Read(ref string filename)
        {
            if (filename.StartsWith(FILE_SLOT_PREFIX)) filename = ParseForIORead(filename);
            return true;
        }

        [HarmonyPatch(typeof(SaveFileMetaIO), "Write")]
        [HarmonyPrefix]
        public static bool SaveFileMetaIO_Write(SaveFileMetaIO __instance, MetaData data, string filename, bool encrypt, bool backup)
        {
            bool overwriteOriginalFiles = Settings.overwriteOriginalFiles.Value;
            bool lockAndExtractMetaFiles = Settings.lockAndExtractMetaFiles.Value;

            if (!lockAndExtractMetaFiles) return true;

            string parsedFilename = ParseRawFilename(filename);
            
            if (filename.StartsWith(FILE_META_PREFIX) && !File.Exists(Path.Combine(saveDirPath, parsedFilename))) WriteToIO(__instance, data, filename);
            return overwriteOriginalFiles;
        }

        [HarmonyPatch(typeof(SaveFileMetaIO), "Read")]
        [HarmonyPrefix]
        public static bool SaveFileMetaIO_Read(ref string filename)
        {
            if (filename.StartsWith(FILE_META_PREFIX)) filename = ParseForIORead(filename);
            return true;
        }

        private static void WriteToIO<T>(MMJsonDataReadWriter<T> io, T data, string filename)
        {
            string parsedFilename = ParseRawFilename(filename);

            var WriteJson = AccessTools.MethodDelegate<Action<MMJsonDataReadWriter<T>, T, string, bool, bool>>(AccessTools.Method(typeof(MMJsonDataReadWriter<T>), "WriteJson"));
            WriteJson.Invoke(io, data, parsedFilename, false, false);

            logger.LogMessage("Extraction of \"" + filename + "\" is complete and saved as \"" + parsedFilename + "\".");
        }

        private static string ParseForIORead(string filename)
        {
            string parsedFilename = ParseRawFilename(filename);

            bool canExtract = Settings.loadExtractedFiles.Value && File.Exists(Path.Combine(saveDirPath, parsedFilename));

            string finalFilename = canExtract ? parsedFilename : filename;

            logger.LogMessage("Loading " + (canExtract ? "extracted" : "encrypted") + " \"" + finalFilename + "\" save file...");
            return finalFilename;
        }

        private static string ParseRawFilename(string filename)
        {
            string jsonSuffix = Settings.jsonSuffix.Value;
            return Path.ChangeExtension(jsonSuffix == "" ? filename : Path.GetFileNameWithoutExtension(filename) + "-" + jsonSuffix, ".json");
        }
    }
}
