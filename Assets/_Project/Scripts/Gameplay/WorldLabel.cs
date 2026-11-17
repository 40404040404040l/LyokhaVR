using UnityEngine;

namespace LyokhaVR
{
    public sealed class WorldLabel : MonoBehaviour
    {
        public float ViewDistance = 24;
        private Renderer textRenderer;
        private void Awake() { textRenderer = GetComponent<Renderer>(); }
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (!camera) return;
            var direction = transform.position - camera.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
            if (textRenderer) textRenderer.enabled = direction.sqrMagnitude < ViewDistance * ViewDistance;
        }
    }
}
