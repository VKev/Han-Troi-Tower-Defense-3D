using UnityEngine;

namespace TowerDefense3D.Mobile
{
    /// <summary>
    /// Applies the Android-first frame pacing request for the current scene.
    /// Device profiling remains a separate acceptance boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileFrameRatePolicy : MonoBehaviour
    {
        private const int TargetFrameRate = 60;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
