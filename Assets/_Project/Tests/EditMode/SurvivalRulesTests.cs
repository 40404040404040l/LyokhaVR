using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace LyokhaVR.Tests
{
    public sealed class SurvivalRulesTests
    {
        GameConfig config; GameRules rules;
        [SetUp] public void Setup() { config=GameConfig.CreateDefault();config.ContinuousSurvival=true;config.StartMinute=240;rules=new GameRules(config); }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(config);
        void Earn() { var id=rules.SpawnGroundItem(ItemKind.LargeScrap).Items[0].Id;rules.PickUp(id);rules.SellItem(id); }
        [Test] public void NightStartRealTimeDayCycleAndHungerAreIndependentOfTransactions()
        {
            Assert.That(rules.State.IsNight,Is.True); Earn(); Assert.That(rules.State.TotalMinutes,Is.EqualTo(240));
            rules.TickSurvival(89);Assert.That(rules.State.Health,Is.EqualTo(5));
            rules.TickSurvival(1);Assert.That(rules.State.Health,Is.EqualTo(4));Assert.That(rules.State.MinuteOfDay,Is.EqualTo(420));Assert.That(rules.State.IsNight,Is.False);
            rules.TickSurvival(double.NaN);rules.TickSurvival(-1);rules.TickSurvival(double.PositiveInfinity);Assert.That(rules.State.Health,Is.EqualTo(4));
            Assert.That(rules.HungerRemaining,Is.EqualTo(90));
        }
        [Test] public void FoodHealsImmediatelyWithoutEndingAndRejectsFullHealthOrInsufficientMoney()
        {
            Assert.That(rules.BuyFood().Success,Is.False);Earn();int cash=rules.State.Cash;
            Assert.That(rules.BuyFood().Success,Is.False);Assert.That(rules.State.Cash,Is.EqualTo(cash));
            rules.TickSurvival(270);Assert.That(rules.State.Health,Is.EqualTo(2));
            Assert.That(rules.BuyFood().HealthDelta,Is.EqualTo(3));Assert.That(rules.State.Health,Is.EqualTo(5));
            Assert.That(rules.State.Cash,Is.EqualTo(cash-30));Assert.That(rules.State.Ending,Is.EqualTo(Ending.None));
            Assert.That(rules.BuyFood().Success,Is.False);rules.TickSurvival(90);rules.BuyFood();
            rules.TickSurvival(90);Assert.That(rules.BuyFood().Success,Is.False);Assert.That(rules.State.Cash,Is.EqualTo(25));
        }
        [Test] public void ShopBatteryUsesInventoryCapacityUniqueIdAndNormalUse()
        {
            Earn();var purchase=rules.BuyBattery();Assert.That(purchase.Success,Is.True);Assert.That(rules.State.Cash,Is.EqualTo(65));
            string id=purchase.Items[0].Id;Assert.That(rules.State.Inventory[0].Id,Is.EqualTo(id));
            for(int i=0;i<5;i++)rules.PickUp(rules.SpawnGroundItem(ItemKind.Can).Items[0].Id);
            Assert.That(rules.BuyBattery().Success,Is.False);Assert.That(rules.State.Cash,Is.EqualTo(65));
            rules.SetFlashlight(true);rules.TickFlashlight(90);Assert.That(rules.Use(id).Success,Is.True);
            Assert.That(rules.State.BatterySeconds,Is.EqualTo(150));Assert.That(rules.Use(id).Success,Is.False);
            var second=rules.BuyBattery();Assert.That(second.Items[0].Id,Is.Not.EqualTo(id));rules.Use(second.Items[0].Id);
            var last=rules.SpawnGroundItem(ItemKind.Battery).Items[0];rules.PickUp(last.Id);rules.Use(last.Id);
            Assert.That(rules.State.BatterySeconds,Is.EqualTo(300));Assert.That(rules.BuyBattery().Success,Is.False);
        }
        [Test] public void StarvationEndsWithoutArrestAndRestartResetsClocks()
        {
            Earn();Assert.That(rules.TickSurvival(450).Ending,Is.EqualTo(Ending.Health));
            Assert.That(rules.State.ArrestCount,Is.Zero);int minute=rules.State.TotalMinutes;
            rules.TickSurvival(100);Assert.That(rules.State.TotalMinutes,Is.EqualTo(minute));Assert.That(rules.BuyBattery().Success,Is.False);Assert.That(rules.BuyFood().Success,Is.False);
            rules.Restart();Assert.That(rules.State.Health,Is.EqualTo(5));Assert.That(rules.HungerRemaining,Is.EqualTo(90));Assert.That(rules.State.MinuteOfDay,Is.EqualTo(240));
        }
        [Test] public void TwelveRealMinutesReturnToNightWhenFoodKeepsPlayerAlive()
        {
            for(int i=0;i<5;i++)Earn();
            for(int i=0;i<8;i++){rules.TickSurvival(90);Assert.That(rules.BuyFood().Success,Is.True);}
            Assert.That(rules.State.MinuteOfDay,Is.EqualTo(240));Assert.That(rules.State.IsNight,Is.True);
            Assert.That(rules.State.IsFinished,Is.False);Assert.That(rules.State.Health,Is.EqualTo(5));
        }
        [Test] public void FourSirenTypesHaveRandomIntervalsAndOnlyPoliceCanCatch()
        {
            var seen=new HashSet<EmergencySiren>();var gaps=new HashSet<int>();
            for(int seed=1;seed<=100;seed++)
            {
                var cycle=new PoliceSearchCycle(3,120,300,15,seed);Assert.That(cycle.Remaining,Is.InRange(120,300));gaps.Add((int)cycle.Remaining);
                cycle.Tick(cycle.Remaining);Assert.That(cycle.Remaining,Is.EqualTo(15).Within(.0001));seen.Add(cycle.CurrentSiren);
                cycle.Tick(14);Assert.That(cycle.Phase,Is.EqualTo(PoliceSearchPhase.Hiding));cycle.Tick(1);
                bool police=cycle.CurrentSiren==EmergencySiren.Police;Assert.That(cycle.Resolve(-1),Is.EqualTo(police));
                if(!police) { Assert.That(cycle.SearchedZone,Is.EqualTo(-1));Assert.That(cycle.Remaining,Is.InRange(120,300)); }
            }
            Assert.That(seen.Count,Is.EqualTo(4));Assert.That(gaps.Count,Is.GreaterThan(20));
        }
        [Test] public void PoliceDrawIsIndependentOfOccupiedZoneAndFalseAlarmsAreSafeEverywhere()
        {
            for(int seed=1;seed<=30;seed++)
            {
                var oracle=new PoliceSearchCycle(3,120,300,15,seed);oracle.Tick(oracle.Remaining+15);oracle.Resolve(-1);
                for(int occupied=0;occupied<3;occupied++)
                {
                    var cycle=new PoliceSearchCycle(3,120,300,15,seed);cycle.Tick(cycle.Remaining+15);
                    Assert.That(cycle.Resolve(occupied),Is.EqualTo(cycle.CurrentSiren==EmergencySiren.Police&&occupied==oracle.SearchedZone));
                    Assert.That(cycle.SearchedZone,Is.EqualTo(oracle.SearchedZone));
                }
            }
        }
        [Test] public void BatteriesReallyOccurInContainerBatchesAtAboutTenPercent()
        {
            var random=new Random(1337);int batteries=0,total=0;
            for(int i=0;i<4000;i++)foreach(var kind in ContainerLoot.RollBatch(random)){total++;if(kind==ItemKind.Battery)batteries++;}
            Assert.That(batteries/(double)total,Is.InRange(.08,.12));
        }
    }
}
