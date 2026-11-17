using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LyokhaVR
{
    /// <summary>Spawns real rigidbodies and refills only after all useful objects leave the volume.</summary>
    public sealed class RandomContainerLoot : MonoBehaviour
    {
        [Serializable]
        public struct JunkPlacement
        {
            public PhysicalRummageItem Prefab;
            public Vector3 Position;
            public Quaternion Rotation;
        }
        public PhysicalRummageItem FindPrefab;
        public JunkPlacement[] Junk;
        public float RefillSeconds = 60;
        public CourtyardSession Session { get; private set; }
        public ContainerRefillClock Clock { get; private set; }
        public int Generation { get; private set; }
        public ItemKind[] LastBatch { get; private set; }
        public int UsefulCount => Session.FindObjects.Count(ContainsUseful);
        System.Random random;
        NearFarInteractor[] hands;
        DesktopMotor desktop;
        static readonly Vector3[] Slots = {
            new Vector3(-.28f,.445f,-.18f), new Vector3(.28f,.445f,-.18f),
            new Vector3(-.28f,.445f,.18f), new Vector3(.28f,.445f,.18f)
        };

        public void Initialize(CourtyardSession session, int seed)
        {
            Session = session; random = new System.Random(seed);
            Clock = new ContainerRefillClock(RefillSeconds);
            hands = session.PlayerRoot.GetComponentsInChildren<NearFarInteractor>(true);
            desktop = session.PlayerRoot.GetComponent<DesktopMotor>();
            Fill();
        }

        public bool Contains(Vector3 position)
        {
            var p = transform.InverseTransformPoint(position);
            return Mathf.Abs(p.x) < .70f && Mathf.Abs(p.z) < .44f && p.y > .35f && p.y < 1.25f;
        }
        bool ContainsUseful(PhysicalRummageItem item) => item && item.Valuable && !item.Stored &&
            item.gameObject.activeInHierarchy && Contains(item.Body.worldCenterOfMass);

        void Update()
        {
            if (Session == null || Session.Rules.State.IsFinished) return;
            if (Clock.Tick(Time.deltaTime, UsefulCount > 0) && !OccupiedForRefill()) Fill();
        }

        bool OccupiedForRefill()
        {
            // Never materialize a new stack through a hand or an object being held in the bin.
            if (Contains(Session.View.transform.position) || Contains(Session.PlayerRoot.position + Vector3.up * .6f)) return true;
            if (hands.Any(h => h && Contains(h.transform.position))) return true;
            if (desktop && desktop.CarriedBody && Contains(desktop.CarriedBody.worldCenterOfMass)) return true;
            return FindObjectsByType<PhysicalRummageItem>().Any(i => Contains(i.Body.worldCenterOfMass) && i.Grab && i.Grab.isSelected);
        }

        void Fill()
        {
            // Replace only rubbish still inside. Thrown objects and every useful item survive.
            foreach (var item in FindObjectsByType<PhysicalRummageItem>().Where(i => !i.Valuable && i.Demo == Session.Demo && Contains(i.Body.worldCenterOfMass)))
            {
                item.gameObject.SetActive(false); Destroy(item.gameObject);
            }
            LastBatch = ContainerLoot.RollBatch(random);
            var slots = Slots.ToArray();
            for (int i = slots.Length - 1; i > 0; i--)
            { int j = random.Next(i + 1); (slots[i], slots[j]) = (slots[j], slots[i]); }
            for (int i = 0; i < LastBatch.Length; i++)
            {
                var position = transform.TransformPoint(slots[i]);
                var item = Instantiate(FindPrefab, position, transform.rotation, transform);
                item.Body.position = position; item.Body.rotation = transform.rotation;
                item.Kind = LastBatch[i]; Session.RegisterFind(item);
                if (!Session.Demo.Find) Session.Demo.Find = item;
            }
            foreach (var placement in Junk)
            {
                if (random.NextDouble() < .22) continue;
                var position = transform.TransformPoint(placement.Position);
                var rotation = transform.rotation * placement.Rotation;
                var item = Instantiate(placement.Prefab, position, rotation, transform);
                item.name = placement.Prefab.name;
                item.Body.position = position; item.Body.rotation = rotation;
                item.Demo = Session.Demo; item.Valuable = false;
            }
            Generation++; Clock.Reset();
        }
    }
}
