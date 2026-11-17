using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LyokhaVR
{
    /// <summary>Static actions use grip select. Loose items use grip to inspect and trigger to stash.</summary>
    [DisallowMultipleComponent]
    public sealed class VRWorldAction : MonoBehaviour
    {
        public bool Grabbable { get; private set; }
        public int SuccessfulInvocations { get; private set; }
        WorldAction action;
        XRBaseInteractable interactable;
        float nextAllowedTime;
        int lastInvocationFrame = -1;

        public void Configure(WorldAction target, bool grabbable)
        {
            if (target == action && interactable != null && Grabbable == grabbable) return;
            Unsubscribe();
            action = target;
            Grabbable = grabbable;
            if (grabbable)
            {
                var body = GetComponent<Rigidbody>();
                if (body == null) body = gameObject.AddComponent<Rigidbody>();
                body.mass = Mathf.Clamp(body.mass, 0.05f, 2f);
                var grab = GetComponent<XRGrabInteractable>();
                if (grab == null) grab = gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;
                grab.useDynamicAttach = true;
                interactable = grab;
            }
            else
            {
                var simple = GetComponent<XRSimpleInteractable>();
                if (simple == null) simple = gameObject.AddComponent<XRSimpleInteractable>();
                interactable = simple;
            }
            interactable.selectMode = InteractableSelectMode.Single;
            interactable.selectEntered.AddListener(OnSelect);
            interactable.activated.AddListener(OnActivate);
            interactable.hoverEntered.AddListener(OnHover);
            interactable.hoverExited.AddListener(OnHoverExit);
        }

        void OnSelect(SelectEnterEventArgs args)
        {
            var session = FindAnyObjectByType<GameSession>();
            if (session) session.SetAim(action, Grabbable);
            if (!Grabbable) InvokeAction();
        }

        void OnHover(HoverEnterEventArgs args)
        {
            var session = FindAnyObjectByType<GameSession>();
            if (session) session.SetAim(action, Grabbable && interactable.isSelected);
        }
        void OnHoverExit(HoverExitEventArgs args)
        {
            var session = FindAnyObjectByType<GameSession>();
            if (session && !interactable.isSelected) session.SetAim(null, false);
        }

        void OnActivate(ActivateEventArgs args)
        {
            if (Grabbable && interactable != null && interactable.isSelected) InvokeAction();
        }

        void InvokeAction()
        {
            if (action == null || !action.Available || lastInvocationFrame == Time.frameCount || Time.unscaledTime < nextAllowedTime) return;
            var rig = VRIntegration.Instance;
            if (rig == null || rig.ViewCamera == null) return;
            // Collider surface distance permits large bins while preventing remote searches across a courtyard.
            var collider = GetComponent<Collider>();
            var point = collider != null ? collider.ClosestPoint(rig.ViewCamera.transform.position) : transform.position;
            if (Vector3.Distance(point, rig.ViewCamera.transform.position) > rig.MaximumReach) return;
            lastInvocationFrame = Time.frameCount;
            nextAllowedTime = Time.unscaledTime + 0.25f;
            SuccessfulInvocations++;
            action.Activate();
        }

        void Unsubscribe()
        {
            if (interactable == null) return;
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.activated.RemoveListener(OnActivate);
            interactable.hoverEntered.RemoveListener(OnHover);
            interactable.hoverExited.RemoveListener(OnHoverExit);
        }

        void OnDestroy() => Unsubscribe();
    }
}
