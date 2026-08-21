using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    public readonly struct TowerNetworkHudState
    {
        public TowerNetworkHudState(
            string selectedText,
            string chainText,
            string queueText,
            string feedbackText,
            bool towerSelectionEnabled,
            bool unlinkEnabled,
            bool startWaveEnabled,
            string startWaveText)
        {
            SelectedText = selectedText ?? string.Empty;
            ChainText = chainText ?? string.Empty;
            QueueText = queueText ?? string.Empty;
            FeedbackText = feedbackText ?? string.Empty;
            TowerSelectionEnabled = towerSelectionEnabled;
            UnlinkEnabled = unlinkEnabled;
            StartWaveEnabled = startWaveEnabled;
            StartWaveText = startWaveText ?? string.Empty;
        }

        public string SelectedText { get; }
        public string ChainText { get; }
        public string QueueText { get; }
        public string FeedbackText { get; }
        public bool TowerSelectionEnabled { get; }
        public bool UnlinkEnabled { get; }
        public bool StartWaveEnabled { get; }
        public string StartWaveText { get; }
    }

    [DisallowMultipleComponent]
    public sealed class TowerNetworkHudView : MonoBehaviour
    {
        private readonly List<TowerCombatDefinition> definitions = new List<TowerCombatDefinition>();

        private TowerNetworkHudControls controls;

        public event Action<TowerCombatDefinition> TowerRequested;
        public event Action UnlinkRequested;
        public event Action StartWaveRequested;

        public bool IsInitialized => controls != null;

        public void Initialize(Transform safeArea, TowerCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            Shutdown();
            definitions.Clear();
            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                TowerCombatDefinition definition = catalog.Definitions[index];
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            controls = TowerNetworkHudLayoutBuilder.Build(safeArea, definitions);
            for (int index = 0; index < controls.TowerButtons.Count; index++)
            {
                int definitionIndex = index;
                controls.TowerButtons[index].onClick.AddListener(
                    () => TowerRequested?.Invoke(definitions[definitionIndex]));
            }

            controls.UnlinkButton.onClick.AddListener(HandleUnlinkRequested);
            controls.StartWaveButton.onClick.AddListener(HandleStartWaveRequested);
            controls.Root.SetActive(true);
        }

        public void Render(TowerNetworkHudState state)
        {
            if (controls == null)
            {
                return;
            }

            controls.SelectedText.text = state.SelectedText;
            controls.ChainText.text = state.ChainText;
            controls.QueueText.text = state.QueueText;
            controls.FeedbackText.text = state.FeedbackText;
            controls.UnlinkButton.interactable = state.UnlinkEnabled;
            controls.StartWaveButton.interactable = state.StartWaveEnabled;
            SetButtonLabel(controls.StartWaveButton, state.StartWaveText);

            for (int index = 0; index < controls.TowerButtons.Count; index++)
            {
                controls.TowerButtons[index].interactable = state.TowerSelectionEnabled;
            }
        }

        public void Show()
        {
            if (controls != null)
            {
                controls.Root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (controls != null)
            {
                controls.Root.SetActive(false);
            }
        }

        public void Shutdown()
        {
            if (controls == null)
            {
                return;
            }

            for (int index = 0; index < controls.TowerButtons.Count; index++)
            {
                controls.TowerButtons[index].onClick.RemoveAllListeners();
            }

            controls.UnlinkButton.onClick.RemoveListener(HandleUnlinkRequested);
            controls.StartWaveButton.onClick.RemoveListener(HandleStartWaveRequested);
            if (Application.isPlaying)
            {
                Destroy(controls.Root);
            }
            else
            {
                DestroyImmediate(controls.Root);
            }
            controls = null;
            definitions.Clear();
        }

        private void HandleUnlinkRequested()
        {
            UnlinkRequested?.Invoke();
        }

        private void HandleStartWaveRequested()
        {
            StartWaveRequested?.Invoke();
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
