using System.Text.Json.Serialization;

namespace IdleMMO.Core;

public enum ItemTier { Common, Uncommon, Rare, Epic, Legendary }

public enum EquipSlot { Weapon, Armor, Helmet, Boots, Belt, Ring }

public sealed class Item
{
    public const int MaxEnhancement = 10;
    /// <summary>Enhancement level required before an item can be promoted to the next tier.</summary>
    public const int PromoteAtEnhancement = 5;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public EquipSlot Slot { get; set; }
    public ItemTier Tier { get; set; }
    public int ItemLevel { get; set; } = 1;
    public int EnhancementLevel { get; set; }
    public Attributes BaseBonus { get; set; } = new();
    public int BaseDefense { get; set; }

    public Item() { }

    public Item(string name, EquipSlot slot, ItemTier tier, int itemLevel, Attributes baseBonus, int baseDefense = 0)
    {
        Name = name; Slot = slot; Tier = tier; ItemLevel = itemLevel; BaseBonus = baseBonus; BaseDefense = baseDefense;
    }

    public static double TierMultiplier(ItemTier t) => t switch
    {
        ItemTier.Common => 1.0,
        ItemTier.Uncommon => 1.5,
        ItemTier.Rare => 2.2,
        ItemTier.Epic => 3.2,
        ItemTier.Legendary => 4.5,
        _ => 1.0
    };

    [JsonIgnore] private double Multiplier => TierMultiplier(Tier) * (1.0 + 0.1 * EnhancementLevel);

    [JsonIgnore] public Attributes EffectiveBonus => BaseBonus.Scale(Multiplier);
    [JsonIgnore] public int EffectiveDefense => (int)Math.Round(BaseDefense * Multiplier);

    /// <summary>Single number used by auto-equip / salvage decisions.</summary>
    [JsonIgnore] public int PowerScore => EffectiveBonus.Total * 2 + EffectiveDefense;

    [JsonIgnore] public long EnhanceCost => (long)(40 * ItemLevel * (1 + (int)Tier) * Math.Pow(1.45, EnhancementLevel));
    [JsonIgnore] public long PromoteCost => 400L * ItemLevel * (1 + (int)Tier) * (1 + (int)Tier);
    [JsonIgnore] public long SalvageValue => 5L * ItemLevel * (1 + (int)Tier) * (1 + EnhancementLevel);

    [JsonIgnore] public bool CanEnhance => EnhancementLevel < MaxEnhancement;
    [JsonIgnore] public bool CanPromote => Tier < ItemTier.Legendary && EnhancementLevel >= PromoteAtEnhancement;

    public override string ToString() =>
        $"[{Tier}] {Name}{(EnhancementLevel > 0 ? $" +{EnhancementLevel}" : "")} ({Slot}, iLvl {ItemLevel}, power {PowerScore})";
}
