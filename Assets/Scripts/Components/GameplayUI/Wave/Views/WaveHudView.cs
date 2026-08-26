using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class WaveHudView : MonoBehaviour, IWaveHudView
    {
        [SerializeField] private Button startWaveButton;
        [SerializeField] private Text previewText;
        private bool isInitialized;

        public event Action StartWaveRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            startWaveButton.onClick.AddListener(HandleStartWaveRequested);
            isInitialized = true;
        }

        public void Render(WaveHudState state)
        {
            startWaveButton.interactable = state.StartWaveEnabled;
            startWaveButton.GetComponentInChildren<Text>(true).text = state.WaveText;
            previewText.text = state.PreviewText;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            startWaveButton.onClick.RemoveListener(HandleStartWaveRequested);
            isInitialized = false;
        }

        private void HandleStartWaveRequested()
        {
            StartWaveRequested?.Invoke();
        }
    }
}
