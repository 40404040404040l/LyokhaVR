using System;

namespace LyokhaVR
{
    public enum PoliceSearchPhase { Waiting, Hiding, Checking, Caught }
    public enum EmergencySiren { Police, Ambulance, Fire, Traffic }

    /// <summary>Real-second event clock. A search draws one zone, independently of player position.</summary>
    public sealed class PoliceSearchCycle
    {
        readonly Random random;
        readonly int zoneCount;
        readonly double interval, hideSeconds;
        readonly double maxInterval;
        readonly bool randomEvents;
        double elapsed, nextWarning;
        public PoliceSearchPhase Phase { get; private set; }
        public int SearchedZone { get; private set; } = -1;
        public int CompletedSearches { get; private set; }
        public EmergencySiren CurrentSiren { get; private set; }
        public double Remaining => Phase == PoliceSearchPhase.Caught ? 0 : Math.Max(0, (Phase == PoliceSearchPhase.Waiting ? nextWarning : nextWarning + hideSeconds) - elapsed);

        public PoliceSearchCycle(int zones, double intervalSeconds = 180, double hidingSeconds = 30, int seed = 1)
        {
            if (zones < 2) throw new ArgumentOutOfRangeException(nameof(zones));
            if (!Valid(intervalSeconds) || !Valid(hidingSeconds) || hidingSeconds <= 0 || intervalSeconds <= hidingSeconds)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            zoneCount = zones; interval = intervalSeconds; hideSeconds = hidingSeconds;
            nextWarning = interval; random = new Random(seed);
        }

        public PoliceSearchCycle(int zones, double minimumSeconds, double maximumSeconds, double hidingSeconds, int seed)
            : this(zones, minimumSeconds, hidingSeconds, seed)
        {
            if (!Valid(maximumSeconds) || maximumSeconds < minimumSeconds) throw new ArgumentOutOfRangeException(nameof(maximumSeconds));
            maxInterval = maximumSeconds; randomEvents = true; nextWarning = RollInterval();
        }
        double RollInterval() => interval + random.NextDouble() * (maxInterval - interval);

        public void Tick(double seconds)
        {
            if (!Valid(seconds) || seconds <= 0 || Phase == PoliceSearchPhase.Caught || Phase == PoliceSearchPhase.Checking) return;
            elapsed += seconds;
            if (Phase == PoliceSearchPhase.Waiting && elapsed >= nextWarning)
            {
                Phase = PoliceSearchPhase.Hiding;
                CurrentSiren = randomEvents ? (EmergencySiren)random.Next(4) : EmergencySiren.Police;
            }
            if (Phase == PoliceSearchPhase.Hiding && elapsed >= nextWarning + hideSeconds) Phase = PoliceSearchPhase.Checking;
        }

        public bool Resolve(int occupiedZone)
        {
            if (Phase != PoliceSearchPhase.Checking) throw new InvalidOperationException("The hiding deadline has not expired.");
            SearchedZone = CurrentSiren == EmergencySiren.Police ? random.Next(zoneCount) : -1;
            CompletedSearches++;
            bool caught = CurrentSiren == EmergencySiren.Police && (occupiedZone < 0 || occupiedZone >= zoneCount || occupiedZone == SearchedZone);
            Phase = caught ? PoliceSearchPhase.Caught : PoliceSearchPhase.Waiting;
            if (randomEvents) nextWarning = elapsed + RollInterval(); else nextWarning += interval;
            return caught;
        }

        static bool Valid(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
