using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LyokhaVR
{
    /// <summary>Keep tracking live, but stop joystick motion during menus, hiding and endings.</summary>
    public sealed class VRSessionLocomotionGuard : MonoBehaviour
    {
        private GameSession session;
        private LocomotionProvider[] providers;
        private bool[] enabledByRig;
        private void Start()
        {
            session = FindAnyObjectByType<GameSession>();
            providers = GetComponentsInChildren<LocomotionProvider>(true);
            enabledByRig = new bool[providers.Length];
            for (var i = 0; i < providers.Length; i++) enabledByRig[i] = providers[i].enabled;
        }
        private void Update()
        {
            if (!session || providers == null) return;
            for (var i = 0; i < providers.Length; i++)
                if (providers[i]) providers[i].enabled = session.IsMovementAllowed && enabledByRig[i];
        }
    }
}
