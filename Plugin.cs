using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using SiNiSistar2;
using SiNiSistar2.Drama;
using SiNiSistar2.Lc;
using SiNiSistar2.Manager;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiNiSistar2LcExtender
{
    [BepInPlugin("com.Henry1887.SiNiSistar2LcExtender", "SiNiSistar 2 Localization Extender", "1.0.0")]
    internal class Plugin : BasePlugin
    {
        internal static Plugin Instance { get; private set; }

        internal static string LanguageModFolder = "LanguageMods";

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
    }
}
