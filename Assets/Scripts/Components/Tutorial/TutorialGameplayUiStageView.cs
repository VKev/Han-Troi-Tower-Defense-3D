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
                case TutorialGameplayUiMode.GeneratorOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorOnly();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.GeneratorLinkOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorPlaced();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.GeneratorPlacedReady:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorPlaced();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, true);
                    break;
                case TutorialGameplayUiMode.SinkPlacementOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialSinkPlacement();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.SecondGeneratorPlacementOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialSecondGeneratorPlacement();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.GeneratorAndSinkReady:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorAndSinkReady();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, true);
                    break;
                case TutorialGameplayUiMode.EnemyDetailOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorPlaced();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.FirePlacementOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialFirePlacement();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.GeneratorUnlinkOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorUnlinkOnly();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.FireLinkFromGeneratorOnly:
                case TutorialGameplayUiMode.FireLinkToSinkOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialFireLink();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.GeneratorSinkAndElementsReady:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialGeneratorSinkAndElementsReady();
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, true);
                    break;
                case TutorialGameplayUiMode.LevelTwoUpgrade:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    towerHud?.SetTutorialControlsVisible(true);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.LevelTwoEnemyDetailOnly:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(true);
                    SetVisible(levelStatus, true);
                    SetTowerHudVisible(true);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                case TutorialGameplayUiMode.LevelTwoWaterPlacement:
                    SetLevelTwoFullTowerHud();
                    break;
                case TutorialGameplayUiMode.LevelTwoFirePlacement:
                    SetLevelTwoFullTowerHud();
                    break;
                case TutorialGameplayUiMode.LevelTwoSinkPlacement:
                    SetLevelTwoFullTowerHud();
                    break;
                case TutorialGameplayUiMode.LevelTwoGeneratorPlacement:
                    SetLevelTwoFullTowerHud();
                    break;
                case TutorialGameplayUiMode.LevelTwoLinking:
                    SetLevelTwoFullTowerHud();
                    break;
                case TutorialGameplayUiMode.BurnStatusFrozen:
                    // The board is frozen mid-wave, so the HUD stays as the player left it apart
                    // from the skip cheat, which would end the wave the beat is talking about.
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    SetTowerHudVisible(true);
                    SetVisible(pauseHud, true);
                    SetVisible(skipCheat, false);
                    break;
                default:
                    waveHud?.gameObject.SetActive(true);
                    waveHud?.SetTutorialPreviewOnly(false);
                    SetVisible(levelStatus, true);
                    SetTowerHudVisible(true);
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

            bool sharesRootWithWave = waveHud != null
                && towerHud.gameObject == waveHud.gameObject;
            if (sharesRootWithWave)
            {
                towerHud.gameObject.SetActive(true);
            }
            else
            {
                towerHud.gameObject.SetActive(visible);
            }

            towerHud.SetTutorialControlsVisible(visible);
        }

        private void SetLevelTwoFullTowerHud()
        {
            waveHud?.gameObject.SetActive(true);
            waveHud?.SetTutorialPreviewOnly(true);
            SetVisible(levelStatus, true);
            SetTowerHudVisible(true);
            SetVisible(pauseHud, true);
            SetVisible(skipCheat, false);
        }

    }
}
