using System;
using NUnit.Framework;
using UnityEngine;

namespace LyokhaVR.Tests
{
    public sealed class PoliceSearchTests
    {
        [Test]
        public void FirstWarningAfter180SecondsAllowsFull30Seconds()
        {
            var cycle = new PoliceSearchCycle(3);
            cycle.Tick(179.5); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Waiting));
            cycle.Tick(.5); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Hiding));
            Assert.That(cycle.Remaining, Is.EqualTo(30));
            cycle.Tick(29.5); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Hiding));
            Assert.Throws<InvalidOperationException>(() => cycle.Resolve(0));
            cycle.Tick(.5); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Checking));
        }

        [Test]
        public void RandomDrawIsIndependentOfPlayerAndMatchingZoneLoses()
        {
            const int seed = 42; int selected = new System.Random(seed).Next(3);
            for (int occupied = -1; occupied < 3; occupied++)
            {
                var cycle = new PoliceSearchCycle(3, 180, 30, seed); cycle.Tick(210);
                Assert.That(cycle.Resolve(occupied), Is.EqualTo(occupied < 0 || occupied == selected));
                Assert.That(cycle.SearchedZone, Is.EqualTo(selected));
                Assert.That(cycle.CompletedSearches, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => cycle.Resolve(occupied));
            }
        }

        [Test]
        public void SurvivingKeepsStartToStartCadenceAndAllZonesCanBeDrawn()
        {
            var cycle = new PoliceSearchCycle(3, 180, 30, 37);
            var oracle = new System.Random(37); var seen = new bool[3];
            cycle.Tick(180);
            for (int round = 0; round < 100; round++)
            {
                int selected = oracle.Next(3);
                cycle.Tick(30); Assert.That(cycle.Resolve((selected + 1) % 3), Is.False); seen[cycle.SearchedZone] = true;
                Assert.That(cycle.Remaining, Is.EqualTo(150));
                cycle.Tick(149); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Waiting));
                cycle.Tick(1); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Hiding));
            }
            Assert.That(seen, Is.All.True);
        }

        [Test]
        public void InvalidDeltaCannotSkipCountdownAndCaughtCycleStops()
        {
            var cycle = new PoliceSearchCycle(3); cycle.Tick(-1); cycle.Tick(double.NaN); cycle.Tick(double.PositiveInfinity);
            Assert.That(cycle.Remaining, Is.EqualTo(180));
            cycle.Tick(210); Assert.That(cycle.Resolve(-1), Is.True);
            cycle.Tick(10000); Assert.That(cycle.Phase, Is.EqualTo(PoliceSearchPhase.Caught));
            Assert.That(cycle.CompletedSearches, Is.EqualTo(1));
        }

        [Test]
        public void FoundByPoliceEndsImmediatelyWithoutFineOrTimeSkipAndBlocksActions()
        {
            var config = GameConfig.CreateDefault();
            try
            {
                var rules = new GameRules(config); var id = rules.SpawnItem(ItemKind.Scrap).Items[0].Id;
                rules.PickUp(id); rules.SellItem(id);
                var held = rules.SpawnItem(ItemKind.Bottle).Items[0].Id; rules.PickUp(held);
                int cash = rules.State.Cash, time = rules.State.TotalMinutes;
                rules.SetFlashlight(true);
                Ending observed = Ending.None; rules.Changed += r => observed = r.Ending;
                Assert.That(rules.CaughtByPolice().Ending, Is.EqualTo(Ending.Police));
                Assert.That(observed, Is.EqualTo(Ending.Police)); Assert.That(rules.State.FlashlightOn, Is.False);
                Assert.That(rules.State.Cash, Is.EqualTo(cash)); Assert.That(rules.State.TotalMinutes, Is.EqualTo(time));
                Assert.That(rules.SellItem(held).Success, Is.False); Assert.That(rules.Drop(held).Success, Is.False);
                Assert.That(rules.CaughtByPolice().Success, Is.False); Assert.That(rules.State.ArrestCount, Is.EqualTo(1));
                rules.Restart(); Assert.That(rules.State.IsFinished, Is.False); Assert.That(rules.State.Inventory, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
