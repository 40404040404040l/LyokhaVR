using UnityEngine;

namespace LyokhaVR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PoliceHidingZone : MonoBehaviour
    {
        public string DisplayName;
        public BoxCollider Volume;
        public CourtyardSession Session;
        public TMPro.TextMeshPro Sign;

        // Test the player's feet, not hands or held objects. This also works for
        // stationary characters and room-scale VR without relying on trigger callbacks.
        public bool Contains(Vector3 feet)
        {
            if (!isActiveAndEnabled || !Volume || !Volume.enabled) return false;
            var local = Volume.transform.InverseTransformPoint(feet) - Volume.center;
            var half = Volume.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
        void Reset() { Volume = GetComponent<BoxCollider>(); Volume.isTrigger = true; }
        void LateUpdate()
        {
            if (!Session || !Sign) return;
            float distance = Vector3.Distance(Session.View.transform.position, Sign.transform.position);
            Sign.enabled = distance > 2.5f && distance < 35 && !Session.MenuOpen && !Session.Rules.State.IsFinished;
            var forward = Vector3.ProjectOnPlane(Sign.transform.position - Session.View.transform.position, Vector3.up);
            if (forward.sqrMagnitude > .001f) Sign.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        void OnDrawGizmos()
        {
            var volume = Volume ? Volume : GetComponent<BoxCollider>();
            Gizmos.color = new Color(0, .9f, .8f, .35f);
            Gizmos.matrix = volume.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(volume.center, volume.size);
        }
    }
}
