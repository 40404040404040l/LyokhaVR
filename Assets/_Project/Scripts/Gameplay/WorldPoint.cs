using UnityEngine;

namespace LyokhaVR
{
    public enum PointKind { Search, GlassBuyer, MetalBuyer, ElectronicsBuyer, FoodShop, WalletOwner, SafeHide, RiskyHide, Container, Wait, Instructions }

    [RequireComponent(typeof(WorldAction))]
    public sealed class WorldPoint : MonoBehaviour
    {
        public PointKind Kind;
        public int Tier;
        public Transform LootOrigin;
        public string Description;
        public WorldAction Action => GetComponent<WorldAction>();
    }
}
