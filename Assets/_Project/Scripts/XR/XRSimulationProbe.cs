#if UNITY_EDITOR && ENABLE_VR
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace LyokhaVR
{
    /// <summary>
    /// Editor-only test driver. Queues official simulated device states into the normal
    /// Input System event update, so XRI sees performed/completed action edges reliably.
    /// It never calls gameplay Activate or XRInteractionManager.SelectEnter directly.
    /// Add only in Play Mode and remove after the probe; never save it into a scene.
    /// </summary>
    [DefaultExecutionOrder(-29990)]
    public sealed class XRSimulationProbe : MonoBehaviour
    {
        public Vector3 HeadPosition;
        public Quaternion HeadRotation = Quaternion.identity;
        public Vector3 LeftPosition;
        public Quaternion LeftRotation = Quaternion.identity;
        public Vector3 RightPosition;
        public Quaternion RightRotation = Quaternion.identity;
        public Vector2 LeftMove;
        public Vector2 RightMove;
        public bool LeftGrip;
        public bool RightGrip;
        public bool RightTrigger;
        public bool LeftTrigger;
        public bool LeftMenuButton, RightFlashlightButton;
        public int AppliedFrames;
        XRSimulatedHMD head;
        XRSimulatedController left;
        XRSimulatedController right;
        XRInteractionSimulator simulator;
        bool restoreSimulator;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        void OnEnable()
        {
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            head = InputSystem.devices.OfType<XRSimulatedHMD>().FirstOrDefault();
            left = InputSystem.devices.OfType<XRSimulatedController>().FirstOrDefault(d => d.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand));
            right = InputSystem.devices.OfType<XRSimulatedController>().FirstOrDefault(d => d.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand));
            if (head == null || left == null || right == null)
            {
                Debug.LogError("[XR probe] Start the official simulator and wait for its three devices first.");
                enabled = false;
                return;
            }
            HeadPosition = head.centerEyePosition.ReadValue();
            HeadRotation = head.centerEyeRotation.ReadValue();
            LeftPosition = left.devicePosition.ReadValue();
            LeftRotation = left.deviceRotation.ReadValue();
            RightPosition = right.devicePosition.ReadValue();
            RightRotation = right.deviceRotation.ReadValue();
            simulator = FindAnyObjectByType<XRInteractionSimulator>();
            restoreSimulator = simulator != null && simulator.enabled;
            // Its lifecycle manager stays enabled, retaining the official devices and usages.
            if (simulator != null) simulator.enabled = false;
            InputSystem.onBeforeUpdate += ApplyBeforeInputUpdate;
        }

        void ApplyBeforeInputUpdate()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (head == null || !head.added || left == null || !left.added || right == null || !right.added) return;
            var h = new XRSimulatedHMDState
            {
                devicePosition = HeadPosition, centerEyePosition = HeadPosition,
                leftEyePosition = HeadPosition + HeadRotation * Vector3.left * 0.032f,
                rightEyePosition = HeadPosition + HeadRotation * Vector3.right * 0.032f,
                deviceRotation = HeadRotation, centerEyeRotation = HeadRotation,
                leftEyeRotation = HeadRotation, rightEyeRotation = HeadRotation,
                isTracked = true, trackingState = 3
            };
            var l = ControllerState(LeftPosition, LeftRotation, LeftMove, LeftGrip, LeftTrigger, LeftMenuButton);
            var r = ControllerState(RightPosition, RightRotation, RightMove, RightGrip, RightTrigger, RightFlashlightButton);
            InputSystem.QueueStateEvent(head, h);
            InputSystem.QueueStateEvent(left, l);
            InputSystem.QueueStateEvent(right, r);
            AppliedFrames++;
        }

        static XRSimulatedControllerState ControllerState(Vector3 position, Quaternion rotation, Vector2 move, bool grip, bool trigger, bool primary)
        {
            return new XRSimulatedControllerState
            {
                devicePosition = position, deviceRotation = rotation,
                isTracked = true, trackingState = 3,
                primary2DAxis = move, grip = grip ? 1f : 0f, trigger = trigger ? 1f : 0f
            }.WithButton(ControllerButton.GripButton, grip).WithButton(ControllerButton.TriggerButton, trigger).WithButton(ControllerButton.PrimaryButton, primary);
        }

        void OnDisable()
        {
            InputSystem.onBeforeUpdate -= ApplyBeforeInputUpdate;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            if (restoreSimulator && simulator != null) simulator.enabled = true;
        }
    }
}
#endif
