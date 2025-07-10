using Newtonsoft.Json.Linq;
using SiNiSistar2;
using SiNiSistar2.Lc;
using SiNiSistar2.Manager;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiNiSistar2LcExtender
{
    internal class Util
    {
        internal static Dictionary<string, object> UnflattenAndSort(Dictionary<string, string> flatDict)
        {
            return SortDictionaryRecursivelyInternal(CollapseSinglePathsInternal(UnflattenDictionaryInternal(flatDict)));
        }

        internal static Dictionary<string, string> Flatten(Dictionary<string, object> nested)
        {
            var flat = new Dictionary<string, string>();
            FlattenNestedTableInternal(nested, "", flat);
            return flat;
        }

        // This method is only convenience when making translations it has no technical use
        private static Dictionary<string, object> SortDictionaryRecursivelyInternal(Dictionary<string, object> input)
        {
            var sorted = new SortedDictionary<string, object>();
            foreach (var kvp in input)
            {
                if (kvp.Value is Dictionary<string, object> dict)
                {
                    sorted[kvp.Key] = SortDictionaryRecursivelyInternal(dict);
                }
                else if (kvp.Value is JObject jObj)
                {
                    sorted[kvp.Key] = SortDictionaryRecursivelyInternal(jObj.ToObject<Dictionary<string, object>>());
                }
                else
                {
                    sorted[kvp.Key] = kvp.Value;
                }
            }
            return new Dictionary<string, object>(sorted);
        }

        private static Dictionary<string, object> UnflattenDictionaryInternal(Dictionary<string, string> flatDict)
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

            return root;
        }

        private static Dictionary<string, object> CollapseSinglePathsInternal(Dictionary<string, object> node)
        {
            var collapsed = new Dictionary<string, object>();

            foreach (var kvp in node)
            {
                string key = kvp.Key;
                object value = kvp.Value;

                if (value is Dictionary<string, object> childDict)
                {
                    // Recursively collapse the child first
                    childDict = CollapseSinglePathsInternal(childDict);

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
                        collapsed[combinedKey] = CollapseSinglePathsInternal(finalDict);
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

        private static void FlattenNestedTableInternal(Dictionary<string, object> nested, string prefix, Dictionary<string, string> flatOut)
        {
            foreach (var (key, value) in nested)
            {
                string fullKey = string.IsNullOrEmpty(prefix) ? key : prefix + (key.Equals("_value") ? "" : "_" + key);

                if (value is JObject jObj)
                {
                    FlattenNestedTableInternal(jObj.ToObject<Dictionary<string, object>>(), fullKey, flatOut);
                }
                else if (value is Dictionary<string, object> dict)
                {
                    FlattenNestedTableInternal(dict, fullKey, flatOut);
                }
                else
                {
                    flatOut[fullKey] = value?.ToString() ?? "";
                }
            }
        }

        internal static void GenerateTranslationTemplate()
        {
            if (LcExtenderGUI.Instance.DramaCleanRequested)
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

            LcExtenderGUI.Instance.OnDramaTasksCleaned += () =>
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
                        if (!string.IsNullOrEmpty(name) && !templateConfig.DramaActorNames.ContainsKey(actor.m_IdentifiedName))
                        {
                            templateConfig.DramaActorNames[actor.m_IdentifiedName] = name;
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
                templateConfig.DramaEvents = dramaEvents;

                string templatePath = Path.Combine(Plugin.LanguageModFolder, "TranslationTemplate.json");
                File.WriteAllText(templatePath, Newtonsoft.Json.JsonConvert.SerializeObject(templateConfig.ToUnflattened(), Newtonsoft.Json.Formatting.Indented));
                Plugin.Instance.Log.LogInfo($"Translation template generated at {templatePath}. Please fill in the translations and save it as a new mod in the {Plugin.LanguageModFolder} folder.");
            };
            LcExtenderGUI.Instance.TriggerAllDramaTasksClean();
        }
    }
}
