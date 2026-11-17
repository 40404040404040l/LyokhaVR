using System;
using UnityEngine;

namespace LyokhaVR
{
    public enum ItemCategory { Glass, Metal, Electronics, Special }
    public enum ItemKind { Bottle, Can, Scrap, LargeScrap, Phone, Wallet, Medkit, Battery }
    public enum Ending { None, Food, Job, Police, Container, Health }
    public enum HazardKind { Glass, Needle, Rat }

    [Serializable]
    public sealed class ItemDefinition
    {
        public ItemKind Kind;
        public string Name;
        public ItemCategory Category;
        public int Slots = 1;
        public int Price;

        public ItemDefinition(ItemKind kind, string name, ItemCategory category, int slots, int price)
        { Kind = kind; Name = name; Category = category; Slots = slots; Price = price; }
    }

    [CreateAssetMenu(menuName = "LyokhaVR/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Economy and survival")]
        public int CashGoal = 250;
        public int InventorySlots = 6;
        public int MaxHealth = 5;
        public int MedkitHealing = 1;
        public bool MedkitFullyHeals;
        public int WalletCash = 100;
        public int ArrestCashPenalty = 35;
        public int ArrestsForPoliceEnding = 2;
        [Header("Courtyard survival")]
        public bool ContinuousSurvival;
        public float HungerIntervalSeconds = 90;
        public float DayLengthSeconds = 720;
        public int FoodPrice = 30, FoodHealing = 3, ShopBatteryPrice = 20;
        [Header("Action based time")]
        public int StartMinute = 8 * 60;
        public int SearchMinutes = 45;
        public int TradeMinutes = 20;
        public int ArrestMinutes = 12 * 60;
        public int NightStartsAt = 20 * 60;
        public int NightEndsAt = 6 * 60;
        [Header("Search and hazards")]
        public int SearchesPerPoint = 1;
        public int MinimumLoot = 1;
        public int MaximumLoot = 3;
        [Range(0, 1)] public float SearchHazardChance = .18f;
        public int GlassDamage = 1;
        public int NeedleDamage = 2;
        public int RatDamage = 1;
        [Header("Flashlight")]
        public float InitialBatterySeconds = 120;
        public float BatteryRefillSeconds = 120;
        public float BatteryCapacitySeconds = 300;
        [Header("Container")]
        public bool ContainerEndsImmediately;
        public bool TruckKillsHiddenPlayer = true;
        public ItemDefinition[] Catalog = DefaultCatalog();

        public static GameConfig CreateDefault() => CreateInstance<GameConfig>();

        public ItemDefinition Find(ItemKind kind)
        {
            if (Catalog != null)
                foreach (var item in Catalog)
                    if (item != null && item.Kind == kind) return item;
            foreach (var item in DefaultCatalog()) if (item.Kind == kind) return item;
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        private static ItemDefinition[] DefaultCatalog() => new[]
        {
            new ItemDefinition(ItemKind.Bottle, "Стеклянная бутылка", ItemCategory.Glass, 1, 15),
            new ItemDefinition(ItemKind.Can, "Алюминиевая банка", ItemCategory.Metal, 1, 20),
            new ItemDefinition(ItemKind.Scrap, "Металлолом", ItemCategory.Metal, 1, 40),
            new ItemDefinition(ItemKind.LargeScrap, "Крупный металлолом", ItemCategory.Metal, 2, 85),
            new ItemDefinition(ItemKind.Phone, "Краденый телефон", ItemCategory.Electronics, 1, 160),
            new ItemDefinition(ItemKind.Wallet, "Кошелёк с визиткой", ItemCategory.Special, 1, 0),
            new ItemDefinition(ItemKind.Medkit, "Аптечка", ItemCategory.Special, 1, 0),
            new ItemDefinition(ItemKind.Battery, "Батарейка", ItemCategory.Special, 1, 0)
        };
    }
}
