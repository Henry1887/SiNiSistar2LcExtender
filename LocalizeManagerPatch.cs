using HarmonyLib;
using SiNiSistar2.Lc;
using SiNiSistar2.Manager;
using UnityEngine;

namespace SiNiSistar2LcExtender
{
    [HarmonyPatch(typeof(LocalizeManager))]
    internal class LocalizeManagerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetLcText")]
        public static void GetLcTextPostfix(LocalizeID id, ref string __result)
        {
            if (Plugin.Instance.DiscoveredLanguageMods.TryGetValue(Plugin.Instance.CurrentLanguage, out var modConfig) && modConfig.LocalizationTable != null && modConfig.LocalizationTable.ContainsKey(id.ToString()))
            {
                __result = modConfig.LocalizationTable[id.ToString()].Replace("[[PlayerName]]", ManagerList.PlayerStatus.PlayerName);
                Plugin.Instance.Log.LogInfo($"LocalizeID: {id}, Result: {__result}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetLcTextChoice")]
        public static void GetLcTextChoicePostfix(ChoiceLocalizeID id, ref string __result)
        {
            if (Plugin.Instance.DiscoveredLanguageMods.TryGetValue(Plugin.Instance.CurrentLanguage, out var modConfig) && modConfig.LocalizationTableChoice != null && modConfig.LocalizationTableChoice.ContainsKey(id.ToString()))
            {
                __result = modConfig.LocalizationTableChoice[id.ToString()].Replace("[[PlayerName]]", ManagerList.PlayerStatus.PlayerName);
                Plugin.Instance.Log.LogInfo($"ChoiceLocalizeID: {id}, Result: {__result}");
            }
        }
    }
}
