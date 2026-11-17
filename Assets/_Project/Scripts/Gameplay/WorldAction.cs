using System;
using UnityEngine;

namespace LyokhaVR
{
    /// <summary>One interaction contract shared by desktop rays, XR hands and UI.</summary>
    public sealed class WorldAction : MonoBehaviour
    {
        public string Id;
        public string Label;
        public bool Available { get; set; } = true;
        public event Action<WorldAction> Triggered;
        private int lastFrame = -1;
        public void Activate()
        {
            if (!Available || !isActiveAndEnabled || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Triggered?.Invoke(this);
        }
    }
}
