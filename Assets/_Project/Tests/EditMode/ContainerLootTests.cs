using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LyokhaVR.Tests
{
    public sealed class ContainerLootTests
    {
        [Test]
        public void BatchesVaryInCountAndValueAndAreReproducibleWithSeed()
        {
            var a = new Random(314159); var b = new Random(314159);
            var counts = new HashSet<int>(); var kinds = new HashSet<ItemKind>(); var values = new HashSet<int>();
            for (int i = 0; i < 1000; i++)
            {
                var batch = ContainerLoot.RollBatch(a);
                Assert.That(batch, Is.EqualTo(ContainerLoot.RollBatch(b)));
                Assert.That(batch.Length, Is.InRange(1, 4)); counts.Add(batch.Length);
                foreach (var kind in batch) kinds.Add(kind);
                values.Add(batch.Sum(k => k == ItemKind.Bottle ? 15 : k == ItemKind.Can ? 20 : k == ItemKind.Scrap ? 40 : k == ItemKind.LargeScrap ? 85 : 0));
            }
            Assert.That(counts.Count, Is.EqualTo(4)); Assert.That(kinds.SetEquals(new[]{ItemKind.Bottle,ItemKind.Can,ItemKind.Scrap,ItemKind.LargeScrap,ItemKind.Battery}), Is.True);
            Assert.That(values.Max() - values.Min(), Is.GreaterThan(100));
        }
        [Test]
        public void RefillRequiresFullMinuteOfContinuousEmptinessAndResetsWhenFindReturned()
        {
            var clock = new ContainerRefillClock();
            Assert.That(clock.Tick(1000, true), Is.False);
            Assert.That(clock.Tick(.1, false), Is.False);
            Assert.That(clock.Tick(59, false), Is.False);
            Assert.That(clock.Tick(.9, false), Is.False);
            Assert.That(clock.Tick(.1, false), Is.True);
            clock.Reset(); clock.Tick(0, false); clock.Tick(45, false);
            Assert.That(clock.Tick(1, true), Is.False); Assert.That(clock.Waiting, Is.False);
            clock.Tick(0, false); Assert.That(clock.Remaining, Is.EqualTo(60));
            clock.Tick(double.NaN, false); clock.Tick(double.PositiveInfinity, false); clock.Tick(-10, false);
            Assert.That(clock.Remaining, Is.EqualTo(60));
            var other = new ContainerRefillClock(); other.Tick(0, false); other.Tick(30, false);
            Assert.That(clock.Remaining, Is.EqualTo(60)); Assert.That(other.Remaining, Is.EqualTo(30));
        }
    }
}
