using HarmonyLib;
using SiNiSistar2.Lc;
using SiNiSistar2.Manager;

namespace SiNiSistar2LcExtender
{
    [HarmonyPatch(typeof(LocalizeManager))]
    internal class LocalizeManagerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetLcText")]
        public static void GetLcTextPostfix(LocalizeID id, ref string __result)
        {
            if (Plugin.Instance.DiscoveredLanguageMods.TryGetValue(Plugin.Instance.CurrentLanguage, out var modConfig)
                && modConfig.LocalizationTable != null
                && modConfig.LocalizationTable.TryGetValue(id.ToString(), out var translation))
            {
                __result = translation.Replace("[[PlayerName]]", ManagerList.PlayerStatus.PlayerName);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetLcTextChoice")]
        public static void GetLcTextChoicePostfix(ChoiceLocalizeID id, ref string __result)
        {
            if (Plugin.Instance.DiscoveredLanguageMods.TryGetValue(Plugin.Instance.CurrentLanguage, out var modConfig)
                && modConfig.LocalizationTableChoice != null
                && modConfig.LocalizationTableChoice.TryGetValue(id.ToString(), out var translation))
            {
                __result = translation.Replace("[[PlayerName]]", ManagerList.PlayerStatus.PlayerName);
            }
        }
    }
}
