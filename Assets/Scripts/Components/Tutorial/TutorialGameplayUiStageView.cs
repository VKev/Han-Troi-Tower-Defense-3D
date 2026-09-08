using TowerDefense3D.Tutorials;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TutorialGameplayUiStageView : MonoBehaviour
    {
        private WaveHudView waveHud;
        private TowerNetworkHudView towerHud;
        private LevelStatusHudView levelStatus;
        private PauseHudView pauseHud;
        private LevelSkipCheatView skipCheat;

        public void Initialize()
        {
            waveHud = GetComponentInChildren<WaveHudView>(true);
            towerHud = GetComponentInChildren<TowerNetworkHudView>(true);
            levelStatus = GetComponentInChildren<LevelStatusHudView>(true);
            pauseHud = GetComponentInChildren<PauseHudView>(true);
            skipCheat = GetComponentInChildren<LevelSkipCheatView>(true);
        }

        public void SetMode(TutorialGameplayUiMode mode)
        {
            switch (mode)
            {
                case TutorialGameplayUiMode.PreviewOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, false);
                    SetTowerHudVisible(false);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.FrogOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, false);
                    SetTowerHudVisible(false);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.StartWaveOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialStartWaveOnly();
                    SetVisible(levelStatus, false);
                    SetTowerHudVisible(false);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                default:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    SetVisible(towerHud, true);
                    towerHud?.SetTutorialControlsVisible(true);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, true);
                    break;
            }
        }

        private static void SetVisible(Component component, bool visible)
        {
            if (component != null) component.gameObject.SetActive(visible);
        }

        private void SetTowerHudVisible(bool visible)
        {
            if (towerHud == null)
            {
                return;
            }

            // WaveHudView and TowerNetworkHudView share the same authored root in the current
            // prefab. Hiding that GameObject also hid the next-wave preview during the tutorial.
            bool sharesRootWithWave = waveHud != null
                && ReferenceEquals(towerHud.gameObject, waveHud.gameObject);
            if (sharesRootWithWave)
            {
                towerHud.gameObject.SetActive(true);
                towerHud.SetTutorialControlsVisible(visible);
                return;
            }

            SetVisible(towerHud, visible);
            towerHud.SetTutorialControlsVisible(visible);
        }

    }
}
