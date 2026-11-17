using UnityEngine;

namespace LyokhaVR
{
    public sealed class DetentionAmbience : MonoBehaviour
    {
        public AudioSource Engine { get; private set; }
        AudioClip clip;
        void Start()
        {
            const int rate = 22050, seconds = 4; var data = new float[rate * seconds];
            var random = new System.Random(57); float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .025f);
                float engine = Mathf.Sin(t * Mathf.PI * 2 * 60) * .23f + Mathf.Sin(t * Mathf.PI * 2 * 120) * .08f;
                // The ends cross-fade to silence to avoid a discontinuity in the noise loop.
                float envelope = Mathf.Min(1, t * 10) * Mathf.Min(1, (seconds - t) * 10);
                data[i] = (engine + filtered * .3f) * envelope;
            }
            clip = AudioClip.Create("Stationary van engine and muffled street", data.Length, 1, rate, false); clip.SetData(data, 0);
            Engine = gameObject.AddComponent<AudioSource>(); Engine.clip = clip; Engine.loop = true;
            Engine.volume = .2f; Engine.spatialBlend = 0; Engine.Play();
        }
        void OnDestroy() { if (Engine) Engine.Stop(); if (clip) Destroy(clip); }
    }
}
