using UnityEngine;
using UnityEngine.XR;

namespace LyokhaVR
{
    /// <summary>The simulator is editor-only; physical players receive native tracked devices.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class VRSimulatorAvailability : MonoBehaviour
    {
        public GameObject Simulator;
        void Awake()
        {
            if (Simulator == null) return;
#if UNITY_EDITOR
            // A native session may not yet be active during Awake (for example, before
            // the headset is worn). Use the configured Editor launch mode as well,
            // otherwise simulated devices can replace the real headset's input.
            var nativeSettings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(UnityEditor.BuildTargetGroup.Standalone);
            bool nativeStartup = nativeSettings != null && nativeSettings.InitManagerOnStart;
            Simulator.SetActive(!nativeStartup && !XRSettings.isDeviceActive);
#else
            Simulator.SetActive(false);
#endif
        }
    }
}
