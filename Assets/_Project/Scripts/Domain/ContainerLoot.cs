using System;

namespace LyokhaVR
{
    /// <summary>Independent weighted rolls: no distance tiers or guaranteed equal-value sessions.</summary>
    public static class ContainerLoot
    {
        public static ItemKind RollKind(Random random)
        {
            int roll = random.Next(100);
            return roll < 42 ? ItemKind.Bottle : roll < 72 ? ItemKind.Can :
                roll < 94 ? ItemKind.Scrap : ItemKind.LargeScrap;
        }

        public static ItemKind[] RollBatch(Random random)
        {
            var batch = new ItemKind[random.Next(1, 5)];
            for (int i = 0; i < batch.Length; i++) batch[i] = random.Next(100) < 10 ? ItemKind.Battery : RollKind(random);
            return batch;
        }
    }

    public sealed class ContainerRefillClock
    {
        public double Delay { get; }
        public double Remaining { get; private set; }
        public bool Waiting { get; private set; }
        public ContainerRefillClock(double delay = 60)
        {
            if (double.IsNaN(delay) || double.IsInfinity(delay) || delay <= 0)
                throw new ArgumentOutOfRangeException(nameof(delay));
            Delay = delay; Remaining = delay;
        }
        public bool Tick(double seconds, bool hasUsefulFinds)
        {
            if (hasUsefulFinds) { Waiting = false; Remaining = Delay; return false; }
            if (!Waiting) { Waiting = true; Remaining = Delay; return false; }
            if (!double.IsNaN(seconds) && !double.IsInfinity(seconds) && seconds > 0)
                Remaining = Math.Max(0, Remaining - seconds);
            return Remaining <= 0;
        }
        public void Reset() { Waiting = false; Remaining = Delay; }
    }
}
