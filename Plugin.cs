using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using SiNiSistar2;
using SiNiSistar2.Drama;
using SiNiSistar2.Lc;
using SiNiSistar2.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiNiSistar2LcExtender
{
    [BepInPlugin("com.Henry1887.SiNiSistar2LcExtender", "SiNiSistar 2 Localization Extender", "1.0.0")]
    internal class Plugin : BasePlugin
    {
        internal static Plugin Instance { get; private set; }

        internal static string LanguageModFolder = "LanguageMods";

        internal static ModConfig temporaryExport;

        internal Dictionary<string, ModConfig> DiscoveredLanguageMods = new Dictionary<string, ModConfig>();

        internal string CurrentLanguage = "";

        private Harmony _harmony;
        public override void Load()
        {
            Instance = this;

            DiscoverLanguageMods();
            LoadLastActiveLanguage();

            _harmony = new Harmony("com.Henry1887.SiNiSistar2LcExtender");
            _harmony.PatchAll();

            ClassInjector.RegisterTypeInIl2Cpp<LcExtenderGUI>();

            SceneManager.add_sceneLoaded((UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)OnSceneLoaded);

            Log.LogInfo("Mod loaded successfully!");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameObject.Find("LcExtenderGUI") == null)
            {
                var obj = new GameObject("LcExtenderGUI");
                obj.AddComponent<LcExtenderGUI>();
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }
        }

        public override bool Unload()
        {
            _harmony.UnpatchSelf();
            Log.LogInfo("Mod unloaded!");
            return true;
        }

        internal void DiscoverLanguageMods()
        {
            if (!Directory.Exists(LanguageModFolder))
            {
                Directory.CreateDirectory(LanguageModFolder);
            }
            var directories = Directory.GetDirectories(LanguageModFolder);
            DiscoveredLanguageMods.Clear();
            foreach (var dir in directories)
            {
                if (File.Exists(Path.Combine(dir, "mod.json")))
                {
                    string languageName = Path.GetFileName(dir);

                    ModConfigUnflattened unflattenedConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<ModConfigUnflattened>(File.ReadAllText(Path.Combine(dir, "mod.json")));

                    DiscoveredLanguageMods[languageName] = unflattenedConfig.ToFlattened();

                    Log.LogInfo($"Discovered language mod {languageName} by {DiscoveredLanguageMods[languageName].Author} ({DiscoveredLanguageMods[languageName].Version})");
                }
            }
        }

        internal static Dictionary<string, object> SortDictionaryRecursively(Dictionary<string, object> input)
        {
            var sorted = new SortedDictionary<string, object>();
            foreach (var kvp in input)
            {
                if (kvp.Value is Dictionary<string, object> dict)
                {
                    sorted[kvp.Key] = SortDictionaryRecursively(dict);
                }
                else if (kvp.Value is JObject jObj)
                {
                    sorted[kvp.Key] = SortDictionaryRecursively(jObj.ToObject<Dictionary<string, object>>());
                }
                else
                {
                    sorted[kvp.Key] = kvp.Value;
                }
            }
            return new Dictionary<string, object>(sorted);
        }

        internal static Dictionary<string, object> UnflattenDictionary(Dictionary<string, string> flatDict)
        {
            var root = new Dictionary<string, object>();

            foreach (var (fullKey, value) in flatDict)
            {
                var parts = fullKey.Split('_');
                var current = root;

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];

                    if (i == parts.Length - 1)
                    {
                        current[part] = value;
                    }
                    else
                    {
                        // If this part already exists and is a string, promote it to a dictionary
                        if (current.TryGetValue(part, out var existing))
                        {
                            if (existing is string)
                            {
                                // Replace the string with a new dictionary and move old value to a "_value" key
                                var newDict = new Dictionary<string, object>
                                {
                                    ["_value"] = existing
                                };
                                current[part] = newDict;
                                current = newDict;
                            }
                            else
                            {
                                current = (Dictionary<string, object>)existing;
                            }
                        }
                        else
                        {
                            var newDict = new Dictionary<string, object>();
                            current[part] = newDict;
                            current = newDict;
                        }
                    }
                }
            }

            return CollapseSinglePaths(root);
        }

        internal static Dictionary<string, object> CollapseSinglePaths(Dictionary<string, object> node)
        {
            var collapsed = new Dictionary<string, object>();

            foreach (var kvp in node)
            {
                string key = kvp.Key;
                object value = kvp.Value;

                if (value is Dictionary<string, object> childDict)
                {
                    // Recursively collapse the child first
                    childDict = CollapseSinglePaths(childDict);

                    // Build a collapsed key path as long as there's only one child and it's a Dictionary
                    string combinedKey = key;
                    object currentValue = childDict;

                    while (currentValue is Dictionary<string, object> currentDict && currentDict.Count == 1)
                    {
                        var nextKey = currentDict.Keys.First();
                        combinedKey += "_" + nextKey;
                        currentValue = currentDict[nextKey];
                    }

                    // Now either currentValue is the final value, or a dict with >1 entries
                    if (currentValue is Dictionary<string, object> finalDict)
                    {
                        collapsed[combinedKey] = CollapseSinglePaths(finalDict);
                    }
                    else
                    {
                        collapsed[combinedKey] = currentValue;
                    }
                }
                else
                {
                    collapsed[key] = value;
                }
            }

            return collapsed;
        }

        internal static void FlattenNestedTable(Dictionary<string, object> nested, string prefix, Dictionary<string, string> flatOut)
        {
            foreach (var (key, value) in nested)
            {
                string fullKey = string.IsNullOrEmpty(prefix) ? key : prefix + (key.Equals("_value") ? "" : "_" + key);

                if (value is JObject jObj)
                {
                    FlattenNestedTable(jObj.ToObject<Dictionary<string, object>>(), fullKey, flatOut);
                }
                else if (value is Dictionary<string, object> dict)
                {
                    FlattenNestedTable(dict, fullKey, flatOut);
                }
                else
                {
                    flatOut[fullKey] = value?.ToString() ?? "";
                }
            }
        }

        internal void SwitchLanguage()
        {
            var languages = new List<string>(DiscoveredLanguageMods.Keys);
            if (languages.Count == 0) return;
            int currentIndex = languages.IndexOf(CurrentLanguage);
            if (currentIndex == -1)
            {
                CurrentLanguage = languages[0];
            }
            else if (currentIndex >= languages.Count - 1)
            {
                CurrentLanguage = "";
            }
            else
            {
                CurrentLanguage = languages[currentIndex + 1];
            }

            if (CurrentLanguage == "")
            {
                DramaLoader.ClearLoader(); // Clear DramaLoader to reset translations
            }
            UpdateAllTranslations();

            Log.LogInfo($"Switched to language: {CurrentLanguage}");
            UpdateLastActive();
        }

        internal void HotReloadLanguageMods()
        {
            Log.LogInfo("Hot reloading language mods...");
            DiscoverLanguageMods();
            UpdateAllTranslations();
            Log.LogInfo("Hot reload complete.");
        }

        internal void UpdateLastActive()
        {
            if (CurrentLanguage == "") return;
            string lastActivePath = Path.Combine(LanguageModFolder, "last_active.txt");
            File.WriteAllText(lastActivePath, CurrentLanguage);
        }

        internal void LoadLastActiveLanguage()
        {
            string lastActivePath = Path.Combine(LanguageModFolder, "last_active.txt");
            if (File.Exists(lastActivePath))
            {
                string lastActiveLanguage = File.ReadAllText(lastActivePath).Trim();
                if (DiscoveredLanguageMods.ContainsKey(lastActiveLanguage))
                {
                    CurrentLanguage = lastActiveLanguage;
                    Log.LogInfo($"Loaded last active language: {CurrentLanguage}");
                }
                else if (lastActiveLanguage != "") // Dont log warning if the file is empty
                {
                    Log.LogWarning($"Last active language {lastActiveLanguage} not found in discovered mods.");
                }
            }
        }

        internal void ApplyTranslation(DramaFile dramaFile)
        {
            if (DiscoveredLanguageMods.TryGetValue(CurrentLanguage, out var modConfig) && modConfig.DramaEvents != null && modConfig.DramaEvents.TryGetValue(dramaFile.DramaID, out var translatedDramaFile))
            {
                // Apply actor names
                if (modConfig.DramaActorNames != null)
                {
                    foreach (var actor in dramaFile.m_DramaActors)
                    {
                        if (modConfig.DramaActorNames.TryGetValue(actor.m_IdentifiedName, out var translatedName))
                        {
                            actor.m_NameText.m_English = translatedName;
                            actor.m_NameText.m_Japanese = translatedName;
                            actor.m_NameText.m_SimplifiedChinese = translatedName;
                            actor.m_NameText.m_TraditionalChinese = translatedName;
                        }
                    }
                }
                // Apply one talks
                if (translatedDramaFile.OneTalks != null && translatedDramaFile.OneTalks.Count == dramaFile.m_OneTalks.Count)
                {
                    for (int i = 0; i < dramaFile.m_OneTalks.Count; i++)
                    {
                        var oneTalk = dramaFile.m_OneTalks[i];
                        string translatedText = translatedDramaFile.OneTalks[i];
                        oneTalk.m_DramaText.m_English = translatedText;
                        oneTalk.m_DramaText.m_Japanese = translatedText;
                        oneTalk.m_DramaText.m_SimplifiedChinese = translatedText;
                        oneTalk.m_DramaText.m_TraditionalChinese = translatedText;
                    }
                }
                Log.LogInfo($"Applied translation for DramaID: {dramaFile.DramaID}");
            }
        }

        internal void UpdateAllTranslations()
        {
            foreach (var dramaFile in DramaLoader.DramaFileDictionary.Values)
            {
                if (dramaFile == null || dramaFile.DramaID == DramaID.None) continue;
                ApplyTranslation(dramaFile);
            }
            // Cause the game to mark current text as dirty and reload it
            LanguageType curLang = ManagerList.Localize.LanguageType;
            if (curLang == LanguageType.English)
            {
                ManagerList.Localize.SetLanguage(LanguageType.Japanese);
            }
            else
            {
                ManagerList.Localize.SetLanguage(LanguageType.English);
            }
            ManagerList.Localize.SetLanguage(curLang);
        }
        internal static void FinishExporting()
        {
            Dictionary<DramaID, DramaEvent> dramaEvents = new Dictionary<DramaID, DramaEvent>();

            foreach (var dramaFile in DramaLoader.DramaFileDictionary.Values)
            {
                if (dramaFile == null || dramaFile.DramaID == DramaID.None) continue;
                DramaEvent dramaEvent = new DramaEvent
                {
                    OneTalks = new List<string>()
                };

                foreach (var actor in dramaFile.m_DramaActors)
                {
                    if (actor.m_NameText == null) continue;
                    string name = actor.m_NameText.m_English;
                    if (!string.IsNullOrEmpty(name) && !temporaryExport.DramaActorNames.ContainsKey(actor.m_IdentifiedName))
                    {
                        temporaryExport.DramaActorNames[actor.m_IdentifiedName] = name;
                    }
                }

                foreach (var oneTalk in dramaFile.m_OneTalks)
                {
                    if (oneTalk.m_DramaText == null) continue;
                    string text = oneTalk.m_DramaText.m_English;
                    if (!string.IsNullOrEmpty(text))
                    {
                        dramaEvent.OneTalks.Add(text);
                    }
                }

                dramaEvents[dramaFile.DramaID] = dramaEvent;
            }
            temporaryExport.DramaEvents = dramaEvents;

            string templatePath = Path.Combine(LanguageModFolder, "TranslationTemplate.json");
            File.WriteAllText(templatePath, Newtonsoft.Json.JsonConvert.SerializeObject(temporaryExport.ToUnflattened(), Newtonsoft.Json.Formatting.Indented));
            temporaryExport = null;
            Plugin.Instance.Log.LogInfo($"Translation template generated at {templatePath}. Please fill in the translations and save it as a new mod in the {LanguageModFolder} folder.");
        }

        internal static void GenerateTranslationTemplate()
        {
            if (temporaryExport != null)
            {
                return;
            }
            ModConfig templateConfig = ModConfig.CreateEmpty();

            string[] localizeEnglishArray = ManagerList.Localize.Table.GetLanguageTextArray(LanguageType.English);
            string[] choiceLocalizeEnglishArray = ManagerList.Localize.TableChoice.GetLanguageTextArray(LanguageType.English);

            Dictionary<string, string> flatLocalizationTable = new Dictionary<string, string>();
            Dictionary<string, string> flatLocalizationTableChoice = new Dictionary<string, string>();

            for (int i = 0; i < localizeEnglishArray.Length; i++)
            {
                LocalizeID localizeId = (LocalizeID)(i + 1); // Skip the first None entry
                string text = localizeEnglishArray[i];
                flatLocalizationTable[localizeId.ToString()] = text;
            }

            for (int i = 0; i < choiceLocalizeEnglishArray.Length; i++)
            {
                ChoiceLocalizeID choiceId = (ChoiceLocalizeID)(i + 1); // Skip the first None entry
                string text = choiceLocalizeEnglishArray[i];
                flatLocalizationTableChoice[choiceId.ToString()] = text;
            }

            templateConfig.LocalizationTable = flatLocalizationTable;
            templateConfig.LocalizationTableChoice = flatLocalizationTableChoice;

            temporaryExport = templateConfig;
            LcExtenderGUI.TriggerAllDramaTasksClean();

            Plugin.Instance.Log.LogInfo("Template is being generated. Drama Files are being loaded...");
        }

        [Serializable]
        public class ModConfig
        {
            public ModConfigUnflattened ToUnflattened()
            {
                return new ModConfigUnflattened
                {
                    Author = Author,
                    Version = Version,
                    LocalizationTable = SortDictionaryRecursively(UnflattenDictionary(this.LocalizationTable)),
                    LocalizationTableChoice = SortDictionaryRecursively(UnflattenDictionary(this.LocalizationTableChoice)),
                    DramaEvents = DramaEvents,
                    DramaActorNames = DramaActorNames,
                    FontAssetBundle = FontAssetBundle,
                    FontAssetBundleAssetPath = FontAssetBundleAssetPath
                };
            }

            public static ModConfig CreateEmpty()
            {
                return new ModConfig
                {
                    Author = "Your Name",
                    Version = "1.0.0",
                    LocalizationTable = new Dictionary<string, string>(),
                    LocalizationTableChoice = new Dictionary<string, string>(),
                    DramaEvents = new Dictionary<DramaID, DramaEvent>(),
                    DramaActorNames = new Dictionary<string, string>(),
                    FontAssetBundle = "YourFontBundle.bundle",
                    FontAssetBundleAssetPath = "Assets\\Fonts\\YourFont.ttf"
                };
            }
            public string Author;
            public string Version;
            public Dictionary<string, string> LocalizationTable;
            public Dictionary<string, string> LocalizationTableChoice;
            public Dictionary<DramaID, DramaEvent> DramaEvents;
            public Dictionary<string, string> DramaActorNames;
            public string FontAssetBundle;
            public string FontAssetBundleAssetPath;
        }

        [Serializable]
        public class ModConfigUnflattened
        {
            public ModConfig ToFlattened()
            {
                Dictionary<string, string> FlattenedTable = new Dictionary<string, string>();
                Dictionary<string, string> FlattenedTableChoice = new Dictionary<string, string>();
                FlattenNestedTable(this.LocalizationTable, "", FlattenedTable);
                FlattenNestedTable(this.LocalizationTableChoice, "", FlattenedTableChoice);
                return new ModConfig
                {
                    Author = Author,
                    Version = Version,
                    LocalizationTable = FlattenedTable,
                    LocalizationTableChoice = FlattenedTableChoice,
                    DramaEvents = DramaEvents,
                    DramaActorNames = DramaActorNames,
                    FontAssetBundle = FontAssetBundle,
                    FontAssetBundleAssetPath = FontAssetBundleAssetPath
                };
            }
            public string Author;
            public string Version;
            public Dictionary<string, object> LocalizationTable;
            public Dictionary<string, object> LocalizationTableChoice;
            public Dictionary<DramaID, DramaEvent> DramaEvents;
            public Dictionary<string, string> DramaActorNames;
            public string FontAssetBundle;
            public string FontAssetBundleAssetPath;
        }

        [Serializable]
        public class DramaEvent
        {
            //public Dictionary<int, string> ActorNames;
            public List<string> OneTalks;
        }
    }
}
