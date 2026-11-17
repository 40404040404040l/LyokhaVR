using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace LyokhaVR
{
    /// <summary>One authored physical object. Grip never creates loot or awards money.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class PhysicalRummageItem : MonoBehaviour, IXRSelectFilter
    {
        public bool Valuable;
        public ItemKind Kind = ItemKind.Scrap;
        public PhysicalRummageDemo Demo;
        public Rigidbody Body { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public string ItemId { get; set; }
        public bool Stored { get; private set; }
        public int CollisionCount { get; private set; }
        public int GrabCount { get; private set; }
        public Vector3 LastReleaseVelocity { get; private set; }
        bool measureRelease;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Grab = GetComponent<XRGrabInteractable>();
            if (!Grab) Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            Grab.selectMode = InteractableSelectMode.Multiple;
            Grab.useDynamicAttach = true;
            Grab.reinitializeDynamicAttachEverySingleGrab = true;
            Grab.throwOnDetach = true;
            Grab.throwSmoothingDuration = .12f;
            Grab.throwVelocityScale = 1;
            Grab.throwAngularVelocityScale = 1;
            Grab.selectFilters.Add(this);
            Grab.selectEntered.AddListener(OnGrab);
            Grab.selectExited.AddListener(OnRelease);
            Grab.activated.AddListener(OnActivate);
        }

        void Start()
        {
            foreach (var transformer in GetComponents<XRGeneralGrabTransformer>())
                transformer.allowTwoHandedScaling = false;
        }

        public bool canProcess => isActiveAndEnabled;
        public bool Process(IXRSelectInteractor hand, IXRSelectInteractable item)
        {
            if (Stored || (Demo && Demo.Rules != null && Demo.Rules.State.IsFinished)) return false;
            // Keep an existing grip when a fast motion briefly separates physics and tracking.
            if (Grab.interactorsSelecting.Contains(hand)) return true;
            if (Demo && Demo.Session && Demo.Session.MenuOpen) return false;
            var nearest = GetComponent<Collider>().ClosestPoint(hand.transform.position);
            if (Vector3.Distance(hand.transform.position, nearest) > .22f) return false;
            return !Valuable || (Demo && Demo.IsExposed(this));
        }

        void OnGrab(SelectEnterEventArgs args) { GrabCount++; }
        void OnRelease(SelectExitEventArgs args)
        {
            if (Grab.interactorsSelecting.Count == 0) measureRelease = true;
        }
        void LateUpdate()
        {
            if (!measureRelease) return;
            LastReleaseVelocity = Body.linearVelocity;
            measureRelease = false;
        }
        void OnActivate(ActivateEventArgs args) { if (Demo) Demo.TryStore(this); }
        void OnCollisionEnter(Collision collision) { CollisionCount++; }
        public void MarkStored() { Stored = true; gameObject.SetActive(false); }
        public void RestoreToWorld(Vector3 position)
        {
            Stored = false; transform.position = position; gameObject.SetActive(true);
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
        }
    }
}
