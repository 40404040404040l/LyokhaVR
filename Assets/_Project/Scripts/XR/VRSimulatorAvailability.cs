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
            // Do not overwrite a real headset if the project is later opened on a VR workstation.
            Simulator.SetActive(!XRSettings.isDeviceActive);
#else
            Simulator.SetActive(false);
#endif
        }
    }
}
