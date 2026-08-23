using UnityEngine;

namespace TowerDefense3D.Mobile
{
    /// <summary>
    /// Applies the Android-first application frame pacing policy.
    /// </summary>
    public sealed class FramePacingSystem
    {
        public const int TargetFrameRate = 60;

        public void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
