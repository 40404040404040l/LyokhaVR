using System;

namespace LyokhaVR
{
    /// <summary>Authoritative gameplay rules; presentation, physics, navigation and audio live outside this class.</summary>
    public sealed class GameRules
    {
        private readonly GameConfig config;
        private Random random;
        private int nextItem;
        double hungerElapsed, minuteFraction;
        public double HungerRemaining => Math.Max(0, config.HungerIntervalSeconds - hungerElapsed);
        public GameState State { get; private set; }
        public GameConfig Config => config;
        public event Action<ActionResult> Changed;

        public GameRules(GameConfig config, int seed = 1)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.config = config;
            Reset(seed);
        }

        public ActionResult Restart(int seed = 1)
        {
            Reset(seed);
            return Finish(new ActionResult { Success = true, Message = "Новый день. Осмотрись и собери деньги на еду." });
        }

        private void Reset(int seed)
        {
            random = new Random(seed);
            nextItem = 0;
            hungerElapsed = minuteFraction = 0;
            State = new GameState
            {
                Capacity = Math.Max(1, config.InventorySlots), Health = Math.Max(1, config.MaxHealth),
                TotalMinutes = Math.Max(0, config.StartMinute),
                BatterySeconds = Math.Min(Math.Max(0, config.InitialBatterySeconds), Math.Max(0, config.BatteryCapacitySeconds))
            };
            UpdateNight();
        }

        // Scene-authored unique finds (wallet, batteries and medical supplies) also use the same pickup path.
        public ActionResult SpawnItem(ItemKind kind) => SpawnGroundItem(kind);

        public ActionResult SpawnGroundItem(ItemKind kind)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (kind == ItemKind.Wallet && State.WalletFound) return Failed("Кошелёк уже найден.");
            var item = CreateItem(kind);
            State.Ground.Add(item.Id, item);
            if (kind == ItemKind.Wallet) State.WalletFound = true;
            var result = Success("Найдено: " + item.Name);
            result.Items.Add(item);
            return Finish(result);
        }

        public ActionResult Search(string pointId, int tier)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (string.IsNullOrWhiteSpace(pointId)) return Failed("Неизвестная точка поиска.");
            State.Searches.TryGetValue(pointId, out var count);
            if (count >= Math.Max(1, config.SearchesPerPoint)) return Failed("Здесь уже всё осмотрено. После мусоровоза появится новый мусор.");
            State.Searches[pointId] = count + 1;
            tier = Math.Max(0, Math.Min(2, tier));
            var minimum = Math.Max(1, Math.Min(3, config.MinimumLoot));
            var maximum = Math.Max(minimum, Math.Min(3, config.MaximumLoot));
            var result = Success("Выбери находки. Места в карманах на всё не хватит.");
            var quantity = random.Next(minimum, maximum + 1);
            for (var i = 0; i < quantity; i++)
            {
                var kind = RollLoot(tier);
                var item = CreateItem(kind);
                State.Ground.Add(item.Id, item);
                result.Items.Add(item);
            }
            Advance(config.SearchMinutes, result);
            if (tier > 0 && random.NextDouble() < Math.Max(0, Math.Min(1, config.SearchHazardChance)))
            {
                var hazard = (HazardKind)random.Next(3);
                Damage(hazard, result);
            }
            return Finish(result);
        }

        private ItemKind RollLoot(int tier)
        {
            var roll = random.Next(100);
            if (roll < 8) return ItemKind.Battery;
            if (roll < 14) return ItemKind.Medkit;
            if (tier == 0) return roll < 65 ? ItemKind.Bottle : ItemKind.Can;
            if (tier == 1) return roll < 48 ? ItemKind.Can : roll < 88 ? ItemKind.Scrap : ItemKind.LargeScrap;
            return roll < 40 ? ItemKind.Scrap : roll < 85 ? ItemKind.LargeScrap : ItemKind.Phone;
        }

        public ActionResult PickUp(string itemId)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (string.IsNullOrEmpty(itemId) || !State.Ground.TryGetValue(itemId, out var item)) return Failed("Предмет уже забрали.");
            if (item.Slots > State.FreeSlots) return Failed("Нет места: нужно ячеек — " + item.Slots + ". Можно выбросить менее ценную находку.");
            State.Ground.Remove(itemId);
            State.InventoryItems.Add(item);
            var result = Success("В кармане: " + item.Name);
            result.Items.Add(item);
            result.RaisesPoliceAlert = item.IsStolen;
            return Finish(result);
        }

        public ActionResult Drop(string itemId)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            var item = FindInventory(itemId);
            if (item == null) return Failed("Предмета нет в кармане.");
            State.InventoryItems.Remove(item);
            State.Ground.Add(item.Id, item);
            var result = Success("Оставлено: " + item.Name);
            result.Items.Add(item);
            return Finish(result);
        }

        public ActionResult Sell(ItemCategory category)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            var result = Success("Продано.");
            for (var i = State.InventoryItems.Count - 1; i >= 0; i--)
            {
                var item = State.InventoryItems[i];
                if (item.Category != category || item.Price <= 0 || category == ItemCategory.Special) continue;
                State.InventoryItems.RemoveAt(i);
                State.Prices[item.Kind] = item.Price;
                result.CashDelta += item.Price;
                result.Items.Add(item);
            }
            if (result.Items.Count == 0) return Failed("Нет подходящих находок для этой точки приёма.");
            State.Cash += result.CashDelta;
            Advance(config.TradeMinutes, result);
            result.Message = "Получено " + result.CashDelta + " ₽. Цена проданных находок теперь известна.";
            return Finish(result);
        }

        public ActionResult SellItem(string itemId)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            var item = FindInventory(itemId);
            if (item == null) return Failed("Предмета нет в кармане.");
            if (item.Category == ItemCategory.Special || item.Price <= 0) return Failed("Этот предмет не принимают.");
            State.InventoryItems.Remove(item);
            State.Prices[item.Kind] = item.Price;
            State.Cash += item.Price;
            var result = Success("Продано: " + item.Name + " за " + item.Price + " руб.");
            result.Items.Add(item); result.CashDelta = item.Price;
            Advance(config.TradeMinutes, result);
            return Finish(result);
        }

        public ActionResult Use(string itemId)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            var item = FindInventory(itemId);
            if (item == null) return Failed("Предмета нет в кармане.");
            var result = Success("Использовано: " + item.Name);
            if (item.Kind == ItemKind.Medkit)
            {
                if (State.Health >= Math.Max(1, config.MaxHealth)) return Failed("Здоровье уже полное.");
                var healing = config.MedkitFullyHeals ? Math.Max(1, config.MaxHealth) : Math.Max(1, config.MedkitHealing);
                var old = State.Health;
                State.Health = Math.Min(Math.Max(1, config.MaxHealth), State.Health + healing);
                result.HealthDelta = State.Health - old;
            }
            else if (item.Kind == ItemKind.Battery)
            {
                if (State.BatterySeconds >= Math.Max(0, config.BatteryCapacitySeconds)) return Failed("Батарея фонарика полная.");
                State.BatterySeconds = Math.Min(Math.Max(0, config.BatteryCapacitySeconds), State.BatterySeconds + Math.Max(0, config.BatteryRefillSeconds));
            }
            else return Failed(item.Kind == ItemKind.Wallet ? "В кошельке есть визитка. Вернуть владельцу или оставить деньги?" : "Эту находку можно продать.");
            State.InventoryItems.Remove(item);
            result.Items.Add(item);
            return Finish(result);
        }

        public ActionResult SetFlashlight(bool on)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (on && State.BatterySeconds <= 0) return Failed("Фонарик разрядился. Нужна батарейка.");
            State.FlashlightOn = on;
            return Finish(Success(on ? "Фонарик включён." : "Фонарик выключен."));
        }

        public ActionResult TickFlashlight(float seconds)
        {
            if (State.IsFinished || !State.FlashlightOn || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return new ActionResult { Success = false, Message = string.Empty, Ending = State.Ending };
            State.BatterySeconds = Math.Max(0, State.BatterySeconds - seconds);
            if (State.BatterySeconds > 0) return new ActionResult { Success = true, Message = string.Empty, Ending = State.Ending };
            State.FlashlightOn = false;
            return Finish(Success("Батарейка села. Найди новую."));
        }

        public ActionResult ApplyHazard(HazardKind hazard)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            var result = Success(string.Empty);
            Damage(hazard, result);
            return Finish(result);
        }

        private void Damage(HazardKind hazard, ActionResult result)
        {
            var damage = hazard == HazardKind.Glass ? config.GlassDamage : hazard == HazardKind.Needle ? config.NeedleDamage : config.RatDamage;
            var old = State.Health;
            State.Health = Math.Max(0, State.Health - Math.Max(0, damage));
            result.HealthDelta += State.Health - old;
            result.Hazard = hazard;
            result.Message = hazard == HazardKind.Glass ? "Порез об острое стекло!" : hazard == HazardKind.Needle ? "Укол о выброшенный шприц!" : "Крыса укусила за руку!";
            if (State.Health == 0) End(Ending.Health);
        }

        public ActionResult AdvanceTime(int minutes)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (minutes < 0) return Failed("Время нельзя повернуть назад.");
            var result = Success("Прошло " + minutes + " мин.");
            Advance(minutes, result);
            return Finish(result);
        }

        public ActionResult CaughtByPolice()
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            State.ArrestCount++;
            End(Ending.Police);
            return Finish(Success("Полиция тебя нашла. Игра окончена."));
        }

        public ActionResult Arrest(bool fineOnly = false)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            State.ArrestCount++;
            if (fineOnly)
            {
                var fine = Math.Min(State.Cash, Math.Max(0, config.ArrestCashPenalty));
                State.Cash -= fine;
                var continued = Success("Задержание. Штраф " + fine + " руб. Можно продолжать поиск.");
                continued.CashDelta = -fine;
                return Finish(continued);
            }
            var result = Success("Полиция задержала Лёху. Прошло полдня; краденый телефон изъят.");
            for (var i = State.InventoryItems.Count - 1; i >= 0; i--)
                if (State.InventoryItems[i].IsStolen)
                { result.Items.Add(State.InventoryItems[i]); State.InventoryItems.RemoveAt(i); }
            result.CashDelta = -Math.Min(State.Cash, Math.Max(0, config.ArrestCashPenalty));
            State.Cash += result.CashDelta;
            State.IsInsideContainer = false;
            Advance(config.ArrestMinutes, result);
            if (State.ArrestCount >= Math.Max(1, config.ArrestsForPoliceEnding)) End(Ending.Police);
            return Finish(result);
        }

        public ActionResult ResolveWallet(bool returnToOwner)
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (State.WalletResolved) return Failed("Судьба кошелька уже решена.");
            ItemInstance wallet = null;
            foreach (var item in State.InventoryItems) if (item.Kind == ItemKind.Wallet) { wallet = item; break; }
            if (wallet == null) return Failed("Сначала подбери кошелёк.");
            State.InventoryItems.Remove(wallet);
            State.WalletResolved = true;
            var result = Success(returnToOwner ? "Владелец благодарит Лёху и предлагает работу и ночлег." : "Деньги из кошелька остались у Лёхи. Визитка потеряна.");
            result.Items.Add(wallet);
            Advance(config.TradeMinutes, result);
            if (returnToOwner) End(Ending.Job);
            else { result.CashDelta = Math.Max(0, config.WalletCash); State.Cash += result.CashDelta; }
            return Finish(result);
        }

        public ActionResult BuyFood()
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (config.ContinuousSurvival)
            {
                if (State.Health >= config.MaxHealth) return Failed("Ты уже сыт: здоровье полное.");
                int price = Math.Max(1, config.FoodPrice);
                if (State.Cash < price) return Failed("На еду нужно " + price + " руб.");
                int before = State.Health;
                State.Cash -= price; State.Health = Math.Min(config.MaxHealth, State.Health + Math.Max(1, config.FoodHealing));
                return Finish(new ActionResult { Success = true, CashDelta = -price, HealthDelta = State.Health - before, Message = "Еда съедена. Здоровье +" + (State.Health - before) + "." });
            }
            if (State.Cash < Math.Max(1, config.CashGoal)) return Failed("На еду нужно " + Math.Max(1, config.CashGoal) + " ₽. Пока не хватает " + (Math.Max(1, config.CashGoal) - State.Cash) + " ₽.");
            var result = Success("Еда куплена. Сегодня Лёха не останется голодным.");
            result.CashDelta = -Math.Max(1, config.CashGoal);
            State.Cash += result.CashDelta;
            Advance(config.TradeMinutes, result);
            End(Ending.Food);
            return Finish(result);
        }

        public ActionResult BuyBattery()
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (State.BatterySeconds >= config.BatteryCapacitySeconds) return Failed("Фонарик уже полностью заряжен.");
            int price = Math.Max(1, config.ShopBatteryPrice);
            if (State.Cash < price) return Failed("На батарейку нужно " + price + " руб.");
            var battery = CreateItem(ItemKind.Battery);
            if (State.FreeSlots < battery.Slots) return Failed("Для батарейки нужна свободная ячейка.");
            State.Cash -= price; State.InventoryItems.Add(battery);
            var result = Success("Батарейка в кармане. Выбери её и нажми «Использовать».");
            result.CashDelta = -price; result.Items.Add(battery); return Finish(result);
        }

        public ActionResult TickSurvival(double seconds)
        {
            if (!config.ContinuousSurvival || State.IsFinished || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
                return new ActionResult { Ending = State.Ending };
            // One real-time clock owns day/night. Trading and inventory actions do not skip time.
            minuteFraction += seconds * 1440 / Math.Max(1, config.DayLengthSeconds);
            int minutes = (int)Math.Min(int.MaxValue - State.TotalMinutes, Math.Floor(minuteFraction));
            minuteFraction -= minutes; State.TotalMinutes += minutes; UpdateNight();
            hungerElapsed += seconds;
            int ticks = (int)Math.Min(State.Health, Math.Floor(hungerElapsed / Math.Max(1, config.HungerIntervalSeconds)));
            if (ticks <= 0) return new ActionResult { Success = true };
            hungerElapsed -= ticks * Math.Max(1, config.HungerIntervalSeconds); State.Health -= ticks;
            if (State.Health == 0) End(Ending.Health);
            return Finish(new ActionResult { Success = true, HealthDelta = -ticks, Message = State.Health == 0 ? "Сил больше нет. Лёха не смог продержаться без еды." : "Голод отнимает силы. Купи еду в киоске: здоровье −" + ticks + "." });
        }

        public ActionResult EnterContainer()
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (State.IsInsideContainer) return Failed("Лёха уже в контейнере.");
            State.IsInsideContainer = true;
            var result = Success("Лёха спрятался в баке. Если услышишь мусоровоз — немедленно выходи!");
            if (config.ContainerEndsImmediately) End(Ending.Container);
            return Finish(result);
        }

        public ActionResult ExitContainer()
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            if (!State.IsInsideContainer) return Failed("Лёха не в контейнере.");
            State.IsInsideContainer = false;
            return Finish(Success("Лёха выбрался из бака."));
        }

        public ActionResult GarbageTruck()
        {
            if (State.IsFinished) return Failed("Игра завершена.");
            var result = Success("Мусоровоз забрал мусор. Точки поиска обновились.");
            State.TruckCount++;
            State.LastTruckMinute = State.TotalMinutes;
            result.TruckArrived = true;
            foreach (var item in State.Ground.Values) result.Items.Add(item);
            State.Ground.Clear();
            State.Searches.Clear();
            // An uncollected unique wallet remains available from its authored location after a truck pass.
            var hasWallet = false;
            foreach (var item in State.InventoryItems) hasWallet |= item.Kind == ItemKind.Wallet;
            if (!hasWallet && !State.WalletResolved) State.WalletFound = false;
            if (State.IsInsideContainer && config.TruckKillsHiddenPlayer) End(Ending.Container);
            return Finish(result);
        }

        private ItemInstance CreateItem(ItemKind kind) => new ItemInstance("item-" + (++nextItem), config.Find(kind));
        private ItemInstance FindInventory(string id)
        { foreach (var item in State.InventoryItems) if (item.Id == id) return item; return null; }
        private void Advance(int minutes, ActionResult result)
        {
            if (config.ContinuousSurvival) return;
            var safe = Math.Max(0, Math.Min(minutes, int.MaxValue - State.TotalMinutes));
            State.TotalMinutes += safe;
            result.MinutesAdvanced += safe;
            UpdateNight();
        }
        private void UpdateNight()
        {
            var start = Math.Max(0, Math.Min(1439, config.NightStartsAt));
            var end = Math.Max(0, Math.Min(1439, config.NightEndsAt));
            State.IsNight = start >= end ? State.MinuteOfDay >= start || State.MinuteOfDay < end : State.MinuteOfDay >= start && State.MinuteOfDay < end;
        }
        private void End(Ending ending) { State.Ending = ending; State.FlashlightOn = false; }
        private static ActionResult Success(string message) => new ActionResult { Success = true, Message = message };
        private ActionResult Failed(string message) => Finish(new ActionResult { Success = false, Message = message });
        private ActionResult Finish(ActionResult result)
        { result.Ending = State.Ending; Changed?.Invoke(result); return result; }
    }
}
