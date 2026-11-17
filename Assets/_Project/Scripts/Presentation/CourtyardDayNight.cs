using UnityEngine;
using UnityEngine.Rendering;

namespace LyokhaVR
{
    /// <summary>Lighting follows the survival clock; every new session starts at 04:00.</summary>
    public sealed class CourtyardDayNight : MonoBehaviour
    {
        public CourtyardSession Session;
        public Light Sun;
        public Light[] StreetLights;
        public Renderer[] StreetLampBulbs;
        public float Daylight { get; private set; }
        Material sky, originalSky;
        float[] nightIntensities;
        MaterialPropertyBlock bulbProperties;
        void Start()
        {
            nightIntensities = new float[StreetLights.Length];
            for (int i = 0; i < StreetLights.Length; i++)
                nightIntensities[i] = StreetLights[i] ? StreetLights[i].intensity : 0;
            bulbProperties = new MaterialPropertyBlock();
            originalSky = RenderSettings.skybox;
            if (originalSky) { sky = new Material(originalSky); RenderSettings.skybox = sky; }
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.sun = Sun;
            Apply();
        }
        void Update() { if (sky) Apply(); }
        void Apply()
        {
            float hour = Session.Rules.State.MinuteOfDay / 60f;
            float elevation = Mathf.Sin((hour - 6) * Mathf.PI / 12);
            Daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.12f, .35f, elevation));
            Sun.transform.rotation = Quaternion.Euler((hour - 6) * 15 + (elevation<0?180:0), -35, 0);
            Sun.intensity = Mathf.Lerp(.035f, 1.5f, Daylight);
            Sun.color = Color.Lerp(new Color(.45f,.57f,.9f), new Color(1,.94f,.84f), Daylight);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.16f,.21f,.3f),new Color(.55f,.67f,.8f),Daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.10f,.13f,.19f),new Color(.3f,.34f,.38f),Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.055f,.075f,.12f),new Color(.18f,.18f,.18f),Daylight);
            RenderSettings.reflectionIntensity = Mathf.Lerp(.025f,.65f,Daylight);
            if (sky)
            {
                sky.SetFloat("_Exposure",Mathf.Lerp(.1f,1.2f,Daylight));
                sky.SetColor("_SkyTint",Color.Lerp(new Color(.025f,.04f,.1f),new Color(.47f,.63f,.83f),Daylight));
                sky.SetColor("_GroundColor",Color.Lerp(new Color(.008f,.01f,.018f),new Color(.37f,.37f,.37f),Daylight));
            }
            for (int i = 0; i < StreetLights.Length; i++)
                if (StreetLights[i]) StreetLights[i].intensity = nightIntensities[i] * (1 - Daylight);
            bulbProperties.SetColor("_EmissionColor", new Color(1,.78f,.48f) * Mathf.Lerp(4,.08f,Daylight));
            if (StreetLampBulbs != null)
                foreach (var bulb in StreetLampBulbs) if (bulb) bulb.SetPropertyBlock(bulbProperties);
        }
        void OnDestroy() { if (sky) { if (RenderSettings.skybox == sky) RenderSettings.skybox = originalSky; Destroy(sky); } }
    }
}
