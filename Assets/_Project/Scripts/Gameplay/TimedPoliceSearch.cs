using System.Linq;
using UnityEngine;

namespace LyokhaVR
{
    [DefaultExecutionOrder(200)]
    public sealed class TimedPoliceSearch : MonoBehaviour
    {
        public CourtyardSession Session;
        public PoliceHidingZone[] Zones;
        public float IntervalSeconds = 120, MaximumIntervalSeconds = 300, HidingSeconds = 15;
        public SirenLibrary Sounds;
        public int RandomSeed;
        public PoliceSearchCycle Cycle { get; private set; }
        public int OccupiedZone { get; private set; } = -1;
        public int WarningsPlayed { get; private set; }
        public string LastOutcome { get; private set; } = "";
        public float OutcomeUntil { get; private set; }
        public AudioSource WarningAudio { get; private set; }
        public PoliceSearchHUD HUD { get; private set; }

        void Start()
        {
            if (!Session || Zones == null || Zones.Length < 2 || Zones.Any(z => !z || !z.Volume))
            { Debug.LogError("Police search needs a session and at least two authored hiding zones.", this); enabled = false; return; }
            if (RandomSeed == 0) RandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            Cycle = new PoliceSearchCycle(Zones.Length, IntervalSeconds, MaximumIntervalSeconds, HidingSeconds, RandomSeed);
            WarningAudio = gameObject.AddComponent<AudioSource>();
            WarningAudio.playOnAwake = false; WarningAudio.spatialBlend = 0; WarningAudio.volume = .55f;
            WarningAudio.loop = true;
            if (!Sounds) Sounds = SirenLibrary.Load();
            HUD = gameObject.AddComponent<PoliceSearchHUD>(); HUD.Initialize(this);
            Session.PoliceMessage = "Слушай сирены: опасна полиция. На укрытие — 15 секунд.";
        }

        void LateUpdate()
        {
            if (Cycle == null || Session.Rules.State.IsFinished) return;
            // HMD x/z includes walking inside the real room; its height is not an occupancy condition.
            var feet = Session.Demo.VRMode
                ? new Vector3(Session.View.transform.position.x, Session.PlayerRoot.position.y + .1f, Session.View.transform.position.z)
                : Session.PlayerRoot.position + Vector3.up * .1f;
            OccupiedZone = System.Array.FindIndex(Zones, z => z.Contains(feet));
            var before = Cycle.Phase;
            Cycle.Tick(Time.deltaTime);
            if (before == PoliceSearchPhase.Waiting && Cycle.Phase != PoliceSearchPhase.Waiting)
            {
                WarningAudio.clip = Sounds.Get(Cycle.CurrentSiren); WarningAudio.Play(); WarningsPlayed++;
                Session.SetMenu(false);
                Session.Demo.SetMessage("Слышна сирена. Реши, нужно ли прятаться. На укрытие — 15 секунд.");
            }
            if (Cycle.Phase == PoliceSearchPhase.Checking)
            {
                bool caught = Cycle.Resolve(OccupiedZone);
                WarningAudio.Stop();
                var searched = Cycle.SearchedZone >= 0 ? Zones[Cycle.SearchedZone].DisplayName : "";
                LastOutcome = Cycle.CurrentSiren != EmergencySiren.Police
                    ? "Это были «" + SirenLibrary.Title(Cycle.CurrentSiren) + "». Машина проехала мимо."
                    : caught
                    ? OccupiedZone < 0 ? "Ты не успел спрятаться. Полиция тебя нашла." : "Проверили укрытие «" + searched + "». Тебя нашли."
                    : "Проверили укрытие «" + searched + "». Тебя не нашли. Полиция уехала.";
                OutcomeUntil = Time.time + 8;
                Session.PoliceMessage = LastOutcome;
                Session.Demo.SetMessage(LastOutcome);
                if (caught) Session.EndPoliceSearch(LastOutcome);
            }
            else if (Cycle.Phase == PoliceSearchPhase.Hiding)
                Session.PoliceMessage = "Слышна сирена. Осталось " + Mathf.CeilToInt((float)Cycle.Remaining) + " с. " +
                    (OccupiedZone < 0 ? "Ты вне укрытия!" : "Ты в укрытии: " + Zones[OccupiedZone].DisplayName);
        }

        void OnDestroy() { if (WarningAudio) WarningAudio.Stop(); }
    }
}
