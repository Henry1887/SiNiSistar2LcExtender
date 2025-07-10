using System;
using System.Collections.Generic;

namespace SiNiSistar2LcExtender
{
    [Serializable]
    public class ModConfig
    {
        public ModConfigUnflattened ToUnflattened()
        {
            return new ModConfigUnflattened
            {
                Author = Author,
                Version = Version,
                LocalizationTable = Util.UnflattenAndSort(LocalizationTable),
                LocalizationTableChoice = Util.UnflattenAndSort(LocalizationTableChoice),
                DramaEvents = DramaEvents,
                DramaActorNames = DramaActorNames,
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
            };
        }
        public string Author;
        public string Version;
        public Dictionary<string, string> LocalizationTable;
        public Dictionary<string, string> LocalizationTableChoice;
        public Dictionary<DramaID, DramaEvent> DramaEvents;
        public Dictionary<string, string> DramaActorNames;
    }

    [Serializable]
    public class ModConfigUnflattened
    {
        public ModConfig ToFlattened()
        {
            return new ModConfig
            {
                Author = Author,
                Version = Version,
                LocalizationTable = Util.Flatten(LocalizationTable),
                LocalizationTableChoice = Util.Flatten(LocalizationTableChoice),
                DramaEvents = DramaEvents,
                DramaActorNames = DramaActorNames,
            };
        }
        public string Author;
        public string Version;
        public Dictionary<string, object> LocalizationTable;
        public Dictionary<string, object> LocalizationTableChoice;
        public Dictionary<DramaID, DramaEvent> DramaEvents;
        public Dictionary<string, string> DramaActorNames;
    }

    // Can be simplified, but doing so breaks older mods
    [Serializable]
    public class DramaEvent
    {
        public List<string> OneTalks;
    }
}
