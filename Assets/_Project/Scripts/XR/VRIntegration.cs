using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace LyokhaVR
{
    /// <summary>Registers the same gameplay actions for tracked controllers and desktop.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class VRIntegration : MonoBehaviour
    {
        public static VRIntegration Instance { get; private set; }
        public static event Action<Camera> RigReady;
        public Camera ViewCamera;
        public Transform PlayerRoot;
        public float MaximumReach = 3.3f;

        void Awake()
        {
            Instance = this;
            if (ViewCamera == null) ViewCamera = GetComponentInChildren<Camera>();
            if (PlayerRoot == null) PlayerRoot = transform;
            // Starter Assets masks omit custom physics layers. Loose finds use layer 6
            // so that they do not block the static navigation grid.
            foreach (var caster in GetComponentsInChildren<CurveInteractionCaster>(true))
                caster.raycastMask = (int)caster.raycastMask | (1 << 6);
            foreach (var caster in GetComponentsInChildren<SphereInteractionCaster>(true))
                caster.physicsLayerMask = (int)caster.physicsLayerMask | (1 << 6);
            foreach (var action in FindObjectsByType<WorldAction>(FindObjectsInactive.Exclude))
                Register(action, action.GetComponent<Rigidbody>() != null);
        }

        void Start() => RigReady?.Invoke(ViewCamera);

        public void Configure(Camera camera, Transform root)
        {
            ViewCamera = camera;
            PlayerRoot = root;
        }

        public static void Register(WorldAction action, bool grabbable = false)
        {
            if (action == null || Instance == null) return;
            // uGUI buttons already dispatch through XRUIInputModule. A second grip listener
            // would double-fire the same command when the tracked UI ray clicks them.
            if (action.GetComponent<UnityEngine.UI.Button>() != null) return;
            var adapter = action.GetComponent<VRWorldAction>();
            if (adapter == null) adapter = action.gameObject.AddComponent<VRWorldAction>();
            adapter.Configure(action, grabbable);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            RigReady = null;
        }
    }
}
