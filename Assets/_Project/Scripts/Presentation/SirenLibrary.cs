using UnityEngine;

namespace LyokhaVR
{
    [CreateAssetMenu(menuName = "LyokhaVR/Siren Library")]
    public sealed class SirenLibrary : ScriptableObject
    {
        public AudioClip[] Clips = new AudioClip[4];
        public AudioClip Get(EmergencySiren kind) => Clips[(int)kind];
        public static SirenLibrary Load() => Resources.Load<SirenLibrary>("SirenLibrary");
        public static string Title(EmergencySiren kind) => kind == EmergencySiren.Police ? "Полиция" :
            kind == EmergencySiren.Ambulance ? "Скорая" : kind == EmergencySiren.Fire ? "Пожарные" : "ДПС";
    }
}
