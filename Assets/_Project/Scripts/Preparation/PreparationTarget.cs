using UnityEngine;

namespace LyokhaVR.Preparation
{
    public sealed class PreparationTarget : MonoBehaviour
    {
        public int InteractionCount { get; private set; }
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Interact()
        {
            InteractionCount++;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(BaseColor, InteractionCount % 2 == 1
                ? new Color(0.15f, 0.85f, 0.45f)
                : new Color(0.95f, 0.55f, 0.15f));
            GetComponent<Renderer>().SetPropertyBlock(properties);
            Debug.Log($"[Preparation] Interaction confirmed: {InteractionCount}");
        }
    }
}
