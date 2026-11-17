using System;
using System.Collections.Generic;

namespace LyokhaVR
{
    public sealed class ItemInstance
    {
        public string Id { get; }
        public ItemKind Kind { get; }
        public string Name { get; }
        public ItemCategory Category { get; }
        public int Slots { get; }
        public int Price { get; }
        public bool IsStolen => Kind == ItemKind.Phone;
        internal ItemInstance(string id, ItemDefinition definition)
        {
            Id = id; Kind = definition.Kind; Name = definition.Name; Category = definition.Category;
            Slots = Math.Max(1, definition.Slots); Price = Math.Max(0, definition.Price);
        }
    }

    public sealed class GameState
    {
        internal readonly List<ItemInstance> InventoryItems = new List<ItemInstance>();
        internal readonly Dictionary<string, ItemInstance> Ground = new Dictionary<string, ItemInstance>();
        internal readonly Dictionary<ItemKind, int> Prices = new Dictionary<ItemKind, int>();
        internal readonly Dictionary<string, int> Searches = new Dictionary<string, int>();
        public IReadOnlyList<ItemInstance> Inventory => InventoryItems;
        public IReadOnlyDictionary<string, ItemInstance> GroundItems => Ground;
        public IReadOnlyDictionary<ItemKind, int> KnownPrices => Prices;
        public IReadOnlyDictionary<string, int> SearchCounts => Searches;
        public int Cash { get; internal set; }
        public int Health { get; internal set; }
        public int Capacity { get; internal set; }
        public int TotalMinutes { get; internal set; }
        public int MinuteOfDay => TotalMinutes % (24 * 60);
        public int DayNumber => TotalMinutes / (24 * 60) + 1;
        public bool IsNight { get; internal set; }
        public float BatterySeconds { get; internal set; }
        public bool FlashlightOn { get; internal set; }
        public int ArrestCount { get; internal set; }
        public int TruckCount { get; internal set; }
        public int LastTruckMinute { get; internal set; } = -1;
        public bool WalletFound { get; internal set; }
        public bool WalletResolved { get; internal set; }
        public bool IsInsideContainer { get; internal set; }
        public Ending Ending { get; internal set; }
        public bool IsFinished => Ending != Ending.None;
        public int UsedSlots
        {
            get { var slots = 0; foreach (var item in InventoryItems) slots += item.Slots; return slots; }
        }
        public int FreeSlots => Capacity - UsedSlots;
        public bool HasStolenPhone
        {
            get { foreach (var item in InventoryItems) if (item.IsStolen) return true; return false; }
        }
    }

    public sealed class ActionResult
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; }
        public List<ItemInstance> Items { get; } = new List<ItemInstance>();
        public int CashDelta { get; internal set; }
        public int HealthDelta { get; internal set; }
        public int MinutesAdvanced { get; internal set; }
        public Ending Ending { get; internal set; }
        public bool RaisesPoliceAlert { get; internal set; }
        public HazardKind? Hazard { get; internal set; }
        public bool TruckArrived { get; internal set; }
    }
}
