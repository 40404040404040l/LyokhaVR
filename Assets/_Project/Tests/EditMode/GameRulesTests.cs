using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LyokhaVR.Tests
{
    public sealed class GameRulesTests
    {
        private GameConfig config;
        private GameRules rules;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
            config.SearchHazardChance = 0;
            rules = new GameRules(config, 1234);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        private ItemInstance Give(ItemKind kind)
        {
            var item = rules.SpawnItem(kind).Items.Single();
            Assert.That(rules.PickUp(item.Id).Success, Is.True);
            return item;
        }

        [Test]
        public void InventoryCapacityIsSixSlotsAndLargeScrapConsumesTwo()
        {
            for (var i = 0; i < 3; i++) Give(ItemKind.LargeScrap);
            Assert.That(rules.State.UsedSlots, Is.EqualTo(6));
            var excess = rules.SpawnItem(ItemKind.Bottle).Items.Single();
            Assert.That(rules.PickUp(excess.Id).Success, Is.False);
            Assert.That(rules.State.GroundItems.ContainsKey(excess.Id), Is.True);
            Assert.That(rules.State.FreeSlots, Is.Zero);
            Assert.That(rules.Drop(rules.State.Inventory[0].Id).Success, Is.True);
            Assert.That(rules.PickUp(excess.Id).Success, Is.True);
            Assert.That(rules.State.UsedSlots, Is.EqualTo(5));
        }

        [Test]
        public void SameItemCannotBePickedUpTwiceOrSoldTwice()
        {
            var bottle = Give(ItemKind.Bottle);
            Assert.That(rules.PickUp(bottle.Id).Success, Is.False);
            Assert.That(rules.Sell(ItemCategory.Glass).CashDelta, Is.EqualTo(15));
            Assert.That(rules.Sell(ItemCategory.Glass).Success, Is.False);
            Assert.That(rules.State.Cash, Is.EqualTo(15));
            Assert.That(rules.Drop(bottle.Id).Success, Is.False);
        }

        [Test]
        public void SellingOnlyAcceptsTheRequestedCategoryAndDiscoversActualPrices()
        {
            var bottle = Give(ItemKind.Bottle);
            var scrap = Give(ItemKind.Scrap);
            Give(ItemKind.Medkit);
            Assert.That(rules.State.KnownPrices, Is.Empty);
            var sold = rules.Sell(ItemCategory.Glass);
            Assert.That(sold.Items.Select(x => x.Id), Is.EqualTo(new[] { bottle.Id }));
            Assert.That(rules.State.KnownPrices[ItemKind.Bottle], Is.EqualTo(bottle.Price));
            Assert.That(rules.State.KnownPrices.ContainsKey(ItemKind.Scrap), Is.False);
            Assert.That(rules.State.Inventory.Any(x => x.Id == scrap.Id), Is.True);
            Assert.That(rules.Sell(ItemCategory.Special).Success, Is.False);
        }

        [Test]
        public void FailedTradeDoesNotAdvanceTime()
        {
            var before = rules.State.TotalMinutes;
            Assert.That(rules.Sell(ItemCategory.Metal).Success, Is.False);
            Assert.That(rules.State.TotalMinutes, Is.EqualTo(before));
        }

        [Test]
        public void SelectedSaleLeavesOtherSameCategoryItemsAndRejectsDuplicate()
        {
            var selected=Give(ItemKind.Scrap);var retained=Give(ItemKind.Can);
            var result=rules.SellItem(selected.Id);
            Assert.That(result.CashDelta,Is.EqualTo(selected.Price));
            Assert.That(rules.State.Inventory.Single().Id,Is.EqualTo(retained.Id));
            Assert.That(rules.SellItem(selected.Id).Success,Is.False);
            Assert.That(rules.State.Cash,Is.EqualTo(selected.Price));
            Assert.That(rules.SellItem(Give(ItemKind.Medkit).Id).Success,Is.False);
        }

        [Test]
        public void FineOnlyArrestContinuesPreservesInventoryAndNeverMakesCashNegative()
        {
            var sold=Give(ItemKind.LargeScrap);rules.SellItem(sold.Id);var retained=Give(ItemKind.Can);
            var time=rules.State.TotalMinutes;
            Assert.That(rules.Arrest(true).CashDelta,Is.EqualTo(-35));
            rules.Arrest(true);rules.Arrest(true);rules.Arrest(true);
            Assert.That(rules.State.Cash,Is.Zero);
            Assert.That(rules.State.Ending,Is.EqualTo(Ending.None));
            Assert.That(rules.State.Inventory.Single().Id,Is.EqualTo(retained.Id));
            Assert.That(rules.State.TotalMinutes,Is.EqualTo(time));
        }

        [Test]
        public void SearchProducesOneToThreeUniqueFindsAndExhaustionDoesNotDeleteThem()
        {
            var first = rules.Search("near-bin", 0);
            Assert.That(first.Success, Is.True);
            Assert.That(first.Items.Count, Is.InRange(1, 3));
            Assert.That(first.Items.Select(x => x.Id).Distinct().Count(), Is.EqualTo(first.Items.Count));
            var time = rules.State.TotalMinutes;
            Assert.That(rules.Search("near-bin", 0).Success, Is.False);
            Assert.That(rules.State.GroundItems.Count, Is.EqualTo(first.Items.Count));
            Assert.That(rules.State.TotalMinutes, Is.EqualTo(time));
        }

        [Test]
        public void SearchSequenceIsReproducibleForTheSameSeed()
        {
            var other = new GameRules(config, 1234);
            for (var i = 0; i < 30; i++)
            {
                var a = rules.Search("point-" + i, i % 3);
                var b = other.Search("point-" + i, i % 3);
                Assert.That(a.Items.Select(x => x.Kind), Is.EqualTo(b.Items.Select(x => x.Kind)));
                Assert.That(a.Items.Select(x => x.Id), Is.EqualTo(b.Items.Select(x => x.Id)));
                Assert.That(a.HealthDelta, Is.EqualTo(b.HealthDelta));
            }
        }

        [Test]
        public void DistantSearchHasHigherAverageResaleValueThanStartingArea()
        {
            var near = new GameRules(config, 92);
            var far = new GameRules(config, 92);
            var nearValue = 0;
            var farValue = 0;
            for (var i = 0; i < 200; i++)
            {
                nearValue += near.Search("p" + i, 0).Items.Sum(x => x.Price);
                farValue += far.Search("p" + i, 2).Items.Sum(x => x.Price);
            }
            Assert.That(farValue, Is.GreaterThan(nearValue * 2));
        }

        [Test]
        public void TruckClearsPendingLootResetsSearchAndRetainsInventory()
        {
            var kept = Give(ItemKind.Bottle);
            var pending = rules.Search("bin", 1).Items;
            var arrival = rules.GarbageTruck();
            Assert.That(arrival.TruckArrived, Is.True);
            Assert.That(rules.State.TruckCount, Is.EqualTo(1));
            Assert.That(rules.State.LastTruckMinute, Is.EqualTo(rules.State.TotalMinutes));
            Assert.That(rules.State.GroundItems, Is.Empty);
            Assert.That(arrival.Items.Select(x => x.Id), Is.EquivalentTo(pending.Select(x => x.Id)));
            Assert.That(rules.State.Inventory.Single().Id, Is.EqualTo(kept.Id));
            Assert.That(rules.Search("bin", 1).Success, Is.True);
        }

        [TestCase(HazardKind.Glass, 1)]
        [TestCase(HazardKind.Needle, 2)]
        [TestCase(HazardKind.Rat, 1)]
        public void HazardsApplyConfiguredDamage(HazardKind hazard, int expected)
        {
            var result = rules.ApplyHazard(hazard);
            Assert.That(rules.State.Health, Is.EqualTo(5 - expected));
            Assert.That(result.HealthDelta, Is.EqualTo(-expected));
            Assert.That(result.Hazard, Is.EqualTo(hazard));
        }

        [Test]
        public void RiskySearchCanApplyRealDamageButStartingSearchIsSafe()
        {
            config.SearchHazardChance = 1;
            var start = rules.Search("start", 0);
            Assert.That(start.HealthDelta, Is.Zero);
            var risky = rules.Search("far", 2);
            Assert.That(risky.HealthDelta, Is.LessThan(0));
            Assert.That(rules.State.Health, Is.LessThan(5));
        }

        [Test]
        public void ZeroHealthProducesHealthEndingAndFurtherActionsAreBlocked()
        {
            for (var i = 0; i < 10; i++) rules.ApplyHazard(HazardKind.Needle);
            Assert.That(rules.State.Health, Is.Zero);
            Assert.That(rules.State.Ending, Is.EqualTo(Ending.Health));
            var time = rules.State.TotalMinutes;
            Assert.That(rules.Search("another", 0).Success, Is.False);
            Assert.That(rules.Arrest().Success, Is.False);
            Assert.That(rules.AdvanceTime(100).Success, Is.False);
            Assert.That(rules.State.TotalMinutes, Is.EqualTo(time));
        }

        [Test]
        public void MedkitPreservesFullHealthKitAndThenHealsConfiguredAmount()
        {
            var kit = Give(ItemKind.Medkit);
            Assert.That(rules.Use(kit.Id).Success, Is.False);
            Assert.That(rules.State.Inventory.Count, Is.EqualTo(1));
            rules.ApplyHazard(HazardKind.Needle);
            Assert.That(rules.Use(kit.Id).HealthDelta, Is.EqualTo(1));
            Assert.That(rules.State.Health, Is.EqualTo(4));
            Assert.That(rules.State.Inventory, Is.Empty);
        }

        [Test]
        public void FullHealingOptionCapsAtMaximum()
        {
            config.MedkitFullyHeals = true;
            rules.ApplyHazard(HazardKind.Needle);
            rules.ApplyHazard(HazardKind.Glass);
            var kit = Give(ItemKind.Medkit);
            Assert.That(rules.Use(kit.Id).HealthDelta, Is.EqualTo(3));
            Assert.That(rules.State.Health, Is.EqualTo(5));
        }

        [Test]
        public void FlashlightDrainsOnlyWhenOnStopsAtZeroAndBatteryRestoresIt()
        {
            rules.TickFlashlight(10000);
            Assert.That(rules.State.BatterySeconds, Is.EqualTo(120));
            rules.SetFlashlight(true);
            rules.TickFlashlight(30);
            Assert.That(rules.State.BatterySeconds, Is.EqualTo(90));
            rules.TickFlashlight(10000);
            Assert.That(rules.State.BatterySeconds, Is.Zero);
            Assert.That(rules.State.FlashlightOn, Is.False);
            Assert.That(rules.SetFlashlight(true).Success, Is.False);
            var battery = Give(ItemKind.Battery);
            rules.Use(battery.Id);
            Assert.That(rules.State.BatterySeconds, Is.EqualTo(120));
            Assert.That(rules.SetFlashlight(true).Success, Is.True);
        }

        [Test]
        public void InvalidFlashlightDeltaCannotIncreaseOrCorruptCharge()
        {
            rules.SetFlashlight(true);
            rules.TickFlashlight(-10);
            rules.TickFlashlight(float.NaN);
            rules.TickFlashlight(float.PositiveInfinity);
            Assert.That(rules.State.BatterySeconds, Is.EqualTo(120));
        }

        [Test]
        public void TimeProgressesThroughNightAndNextDayByActions()
        {
            Assert.That(rules.State.IsNight, Is.False);
            rules.AdvanceTime(12 * 60);
            Assert.That(rules.State.IsNight, Is.True);
            Assert.That(rules.State.MinuteOfDay, Is.EqualTo(20 * 60));
            rules.AdvanceTime(10 * 60);
            Assert.That(rules.State.IsNight, Is.False);
            Assert.That(rules.State.DayNumber, Is.EqualTo(2));
            Assert.That(rules.State.MinuteOfDay, Is.EqualTo(6 * 60));
            Assert.That(rules.AdvanceTime(-10).Success, Is.False);
        }

        [Test]
        public void FirstArrestConfiscatesPhoneAdvancesHalfDayAndDoesNotFinish()
        {
            Give(ItemKind.LargeScrap);
            rules.Sell(ItemCategory.Metal);
            Give(ItemKind.Phone);
            var before = rules.State.TotalMinutes;
            var arrested = rules.Arrest();
            Assert.That(rules.State.HasStolenPhone, Is.False);
            Assert.That(arrested.Items.Single().Kind, Is.EqualTo(ItemKind.Phone));
            Assert.That(rules.State.TotalMinutes - before, Is.EqualTo(720));
            Assert.That(rules.State.Cash, Is.EqualTo(50));
            Assert.That(rules.State.Ending, Is.EqualTo(Ending.None));
            Assert.That(rules.State.ArrestCount, Is.EqualTo(1));
            Assert.That(rules.Arrest().Ending, Is.EqualTo(Ending.Police));
        }

        [Test]
        public void PhoneAlertsOnlyWhenPickedUpAndHeld()
        {
            var phone = rules.SpawnItem(ItemKind.Phone);
            Assert.That(phone.RaisesPoliceAlert, Is.False);
            Assert.That(rules.State.HasStolenPhone, Is.False);
            var id = phone.Items.Single().Id;
            Assert.That(rules.PickUp(id).RaisesPoliceAlert, Is.True);
            Assert.That(rules.State.HasStolenPhone, Is.True);
            rules.Drop(id);
            Assert.That(rules.State.HasStolenPhone, Is.False);
        }

        [Test]
        public void ArrestPenaltyCannotMakeCashNegativeAndThresholdIsConfigurable()
        {
            config.ArrestsForPoliceEnding = 1;
            var result = rules.Arrest();
            Assert.That(rules.State.Cash, Is.Zero);
            Assert.That(result.Ending, Is.EqualTo(Ending.Police));
        }

        [Test]
        public void WalletRequiresPossessionAndReturningItProducesJobEnding()
        {
            var wallet = rules.SpawnItem(ItemKind.Wallet).Items.Single();
            Assert.That(rules.ResolveWallet(true).Success, Is.False);
            rules.PickUp(wallet.Id);
            Assert.That(rules.SpawnItem(ItemKind.Wallet).Success, Is.False);
            Assert.That(rules.ResolveWallet(true).Ending, Is.EqualTo(Ending.Job));
            Assert.That(rules.State.WalletResolved, Is.True);
            Assert.That(rules.State.Inventory, Is.Empty);
        }

        [Test]
        public void KeepingWalletPaysOnceAndPreventsLaterReturn()
        {
            Give(ItemKind.Wallet);
            Assert.That(rules.ResolveWallet(false).CashDelta, Is.EqualTo(100));
            Assert.That(rules.State.IsFinished, Is.False);
            Assert.That(rules.ResolveWallet(false).Success, Is.False);
            Assert.That(rules.ResolveWallet(true).Success, Is.False);
            Assert.That(rules.State.Cash, Is.EqualTo(100));
        }

        [Test]
        public void UncollectedWalletCanBeRecreatedAfterTruckButCannotBeDuplicated()
        {
            Assert.That(rules.SpawnItem(ItemKind.Wallet).Success, Is.True);
            Assert.That(rules.SpawnItem(ItemKind.Wallet).Success, Is.False);
            rules.GarbageTruck();
            Assert.That(rules.SpawnItem(ItemKind.Wallet).Success, Is.True);
        }

        [Test]
        public void BuyingFoodRequiresGoalAndSpendsMoneyOnlyOnce()
        {
            Assert.That(rules.BuyFood().Success, Is.False);
            for (var i = 0; i < 3; i++) Give(ItemKind.LargeScrap);
            rules.Sell(ItemCategory.Metal);
            Assert.That(rules.State.Cash, Is.EqualTo(255));
            Assert.That(rules.State.IsFinished, Is.False, "Reaching money goal alone must not end the game.");
            Assert.That(rules.BuyFood().Ending, Is.EqualTo(Ending.Food));
            Assert.That(rules.State.Cash, Is.EqualTo(5));
            Assert.That(rules.BuyFood().Success, Is.False);
        }

        [Test]
        public void SafeExitBeforeTruckAvoidsContainerEndingAndStayingProducesIt()
        {
            rules.EnterContainer();
            Assert.That(rules.State.IsInsideContainer, Is.True);
            rules.ExitContainer();
            Assert.That(rules.GarbageTruck().Ending, Is.EqualTo(Ending.None));
            rules.EnterContainer();
            Assert.That(rules.GarbageTruck().Ending, Is.EqualTo(Ending.Container));
        }

        [Test]
        public void OriginalImmediateContainerEndingCanBeConfigured()
        {
            config.ContainerEndsImmediately = true;
            Assert.That(rules.EnterContainer().Ending, Is.EqualTo(Ending.Container));
        }

        [Test]
        public void RestartClearsAllSessionStateAndRestoresSeededSearches()
        {
            var initial = rules.Search("first", 0).Items.Select(x => x.Kind).ToArray();
            Give(ItemKind.Bottle);
            rules.Sell(ItemCategory.Glass);
            Give(ItemKind.Wallet);
            rules.ResolveWallet(false);
            rules.GarbageTruck();
            rules.Arrest();
            rules.Arrest();
            rules.Restart(1234);
            Assert.That(rules.State.Cash, Is.Zero);
            Assert.That(rules.State.Health, Is.EqualTo(5));
            Assert.That(rules.State.ArrestCount, Is.Zero);
            Assert.That(rules.State.TruckCount, Is.Zero);
            Assert.That(rules.State.Ending, Is.EqualTo(Ending.None));
            Assert.That(rules.State.Inventory, Is.Empty);
            Assert.That(rules.State.GroundItems, Is.Empty);
            Assert.That(rules.State.KnownPrices, Is.Empty);
            Assert.That(rules.State.SearchCounts, Is.Empty);
            Assert.That(rules.State.WalletFound, Is.False);
            Assert.That(rules.State.WalletResolved, Is.False);
            Assert.That(rules.State.BatterySeconds, Is.EqualTo(120));
            Assert.That(rules.Search("first", 0).Items.Select(x => x.Kind), Is.EqualTo(initial));
        }

        [Test]
        public void FinishingExtinguishesFlashlightAndNotifiesObserverWithEnding()
        {
            Ending observed = Ending.None;
            rules.Changed += result => observed = result.Ending;
            rules.SetFlashlight(true);
            Give(ItemKind.Wallet);
            rules.ResolveWallet(true);
            Assert.That(rules.State.FlashlightOn, Is.False);
            Assert.That(observed, Is.EqualTo(Ending.Job));
        }
    }
}
