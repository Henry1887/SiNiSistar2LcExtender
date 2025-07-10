using Cysharp.Threading.Tasks;
using Il2CppSystem.Threading;
using SiNiSistar2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SiNiSistar2LcExtender
{
    internal class LcExtenderGUI : MonoBehaviour
    {
        public static LcExtenderGUI Instance { get; private set; }
        public List<int> DramaTasks = new List<int>();
        public bool DramaCleanRequested = false;
        public bool IsVisible = true;
        public System.Action OnDramaTasksCleaned;


        public void Awake()
        {
            Instance = this;
        }

        public void Update()
        {
            if (Keyboard.current.altKey.wasPressedThisFrame)
            {
                IsVisible = !IsVisible;
            }
            if (Keyboard.current.digit1Key.wasPressedThisFrame && IsVisible)
            {
                Util.GenerateTranslationTemplate();
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame && IsVisible)
            {
                Plugin.Instance.HotReloadLanguageMods();
            }
            if (Keyboard.current.digit3Key.wasPressedThisFrame && IsVisible)
            {
                Plugin.Instance.SwitchLanguage();
            }

            // Queue up drama files that are being loaded (indicated by initial null value)
            foreach (var kvp in DramaLoader.DramaFileDictionary)
            {
                if (kvp.Value == null && !DramaTasks.Contains((int)kvp.Key))
                {
                    DramaTasks.Add((int)kvp.Key);
                }
            }

            // Process the Queued up tasks, if they are done loading they shouldnt be null anymore
            for (int i = DramaTasks.Count - 1; i >= 0; i--)
            {
                if (DramaLoader.DramaFileDictionary.TryGetValue((DramaID)DramaTasks[i], out var dramaFile) 
                    && dramaFile != null)
                {
                    if (Plugin.Instance.CurrentLanguage != "" && !DramaCleanRequested)
                    {
                        Plugin.Instance.ApplyTranslation(dramaFile);
                    }
                    DramaTasks.RemoveAt(i);
                }
            }
            if (DramaCleanRequested && DramaTasks.Count == 0)
            {
                OnDramaTasksCleaned?.Invoke();
                DramaCleanRequested = false;
                OnDramaTasksCleaned = null;
                DramaLoader.ClearLoader(); // Clear the clean drama files
            }
        }

        public void TriggerAllDramaTasksClean()
        {
            if (DramaCleanRequested) return; // Avoid multiple triggers
            DramaCleanRequested = true;
            DramaLoader.ClearLoader();

            // foreach drama id load and forget
            for (int i = 1; i < typeof(DramaID).GetEnumValues().Length; i++)
            {
                DramaLoader.LoadDramaFile((DramaID)i, CancellationToken.None).Forget();
            }
        }

        public void OnGUI()
        {
            if (!IsVisible) return;
            GUI.Label(new Rect(10, 400, 700, 20), $"Alt: Hide | 1: Generate Translation Template | 2: Reload Languages | 3: Switch Language | Current Language: {(Plugin.Instance.CurrentLanguage == "" ? "None" : Plugin.Instance.CurrentLanguage)}");
        }
    }
}
