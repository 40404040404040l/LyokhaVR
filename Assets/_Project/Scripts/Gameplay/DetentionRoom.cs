using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LyokhaVR
{
    /// <summary>Post-game exploration and the start screen have no inventory or police simulation.</summary>
    public sealed class DetentionRoom : MonoBehaviour
    {
        public bool VRMode, IsMainMenu;
        public Transform PlayerRoot;
        public Camera View;
        public Shader OverlayTextShader;
        public DetentionUI UI { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool CanWalk => !IsMainMenu && !MenuOpen && !GameSceneFlow.IsTransitioning;
        InputAction menuAction;
        LocomotionProvider[] providers;
        bool[] providerDefaults;
        NearFarInteractor[] hands;
        Vector3 spawn;
        Quaternion spawnRotation;

        void Start()
        {
            spawn = PlayerRoot.position; spawnRotation = PlayerRoot.rotation;
            providers = PlayerRoot.GetComponentsInChildren<LocomotionProvider>(true);
            providerDefaults = providers.Select(p => p.enabled).ToArray();
            hands = PlayerRoot.GetComponentsInChildren<NearFarInteractor>(true);
            UI = gameObject.AddComponent<DetentionUI>(); UI.Initialize(this);
            menuAction = new InputAction("Detention menu", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            menuAction.performed += OnMenu; menuAction.Enable();
            SetMenu(IsMainMenu);
            if (!VRMode && !IsMainMenu) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (!IsMainMenu) gameObject.AddComponent<DetentionAmbience>();
        }
        void OnMenu(InputAction.CallbackContext context) { if (!GameSceneFlow.IsTransitioning) SetMenu(!MenuOpen); }
        public void SetMenu(bool open)
        {
            MenuOpen = IsMainMenu || open; UI.ShowMenu(MenuOpen);
            if (!VRMode) { Cursor.lockState = MenuOpen ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = MenuOpen; }
            foreach (var hand in hands) hand.enableFarCasting = MenuOpen;
            ApplyLocomotion();
        }
        void ApplyLocomotion()
        {
            for (int i = 0; i < providers.Length; i++) if (providers[i]) providers[i].enabled = CanWalk && providerDefaults[i];
        }
        void Update()
        {
            if (providers == null) return;
            ApplyLocomotion();
            if (!GameSceneFlow.IsTransitioning && PlayerRoot.position.y < -2)
            {
                var character = PlayerRoot.GetComponent<CharacterController>();
                if (character) character.enabled = false;
                PlayerRoot.SetPositionAndRotation(spawn, spawnRotation);
                if (character) character.enabled = true;
            }
        }
        public void Restart() => GameSceneFlow.Travel(GameSceneFlow.Courtyard(VRMode), VRMode);
        public void GoToMainMenu() => GameSceneFlow.Travel(GameSceneFlow.MainMenu(VRMode), VRMode);
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy() { menuAction?.Dispose(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
