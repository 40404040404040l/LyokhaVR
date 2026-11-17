using UnityEngine;

namespace LyokhaVR
{
    /// <summary>Allows the same UI command to be selected by a physical XR ray.</summary>
    public sealed class HudButtonCollider : MonoBehaviour
    {
        public BoxCollider Box;
        void LateUpdate()
        {
            if (Box == null) return;
            var r = ((RectTransform)transform).rect;
            Box.center = new Vector3(r.center.x, r.center.y, 0);
            Box.size = new Vector3(Mathf.Max(1,r.width), Mathf.Max(1,r.height), 6);
            var canvas = GetComponentInParent<Canvas>();
            Box.enabled = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
        }
    }
}
