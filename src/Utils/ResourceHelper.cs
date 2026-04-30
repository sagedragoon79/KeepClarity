using System.Collections.Generic;
using System.Reflection;

namespace FFUIOverhaul.Utils
{
    public enum ResourceCategory
    {
        Food,
        RawMaterial,
        Produced,
        Usable,
        Livestock
    }

    public static class ResourceHelper
    {
        private static readonly Dictionary<string, ResourceCategory> KnownCategories = new()
        {
            // Food & Consumables
            { "ItemBerries", ResourceCategory.Food },
            { "ItemMeat", ResourceCategory.Food },
            { "ItemFish", ResourceCategory.Food },
            { "ItemSmokedMeat", ResourceCategory.Food },
            { "ItemSmokedFish", ResourceCategory.Food },
            { "ItemPreserves", ResourceCategory.Food },
            { "ItemPreservedVegetables", ResourceCategory.Food },
            { "ItemGreens", ResourceCategory.Food },
            { "ItemRootVegetables", ResourceCategory.Food },
            { "ItemBread", ResourceCategory.Food },
            { "ItemMushrooms", ResourceCategory.Food },
            { "ItemFruit", ResourceCategory.Food },
            { "ItemNuts", ResourceCategory.Food },
            { "ItemEggs", ResourceCategory.Food },
            { "ItemBeans", ResourceCategory.Food },
            { "ItemMilk", ResourceCategory.Food },
            { "ItemCheese", ResourceCategory.Food },
            { "ItemPastries", ResourceCategory.Food },
            { "ItemMedicine", ResourceCategory.Food },

            // Raw Materials
            { "ItemLogs", ResourceCategory.RawMaterial },
            { "ItemMedicinalRoots", ResourceCategory.RawMaterial },
            { "ItemHerbs", ResourceCategory.RawMaterial },
            { "ItemWillow", ResourceCategory.RawMaterial },
            { "ItemStone", ResourceCategory.RawMaterial },
            { "ItemGrain", ResourceCategory.RawMaterial },
            { "ItemWater", ResourceCategory.RawMaterial },
            { "ItemIronOre", ResourceCategory.RawMaterial },
            { "ItemGoldOre", ResourceCategory.RawMaterial },
            { "ItemCoal", ResourceCategory.RawMaterial },
            { "ItemFlax", ResourceCategory.RawMaterial },
            { "ItemCite", ResourceCategory.RawMaterial }, // Clay
            { "ItemClay", ResourceCategory.RawMaterial },
            { "ItemHoney", ResourceCategory.RawMaterial },
            { "ItemWax", ResourceCategory.RawMaterial },
            { "ItemSand", ResourceCategory.RawMaterial },
            { "ItemHay", ResourceCategory.RawMaterial },

            // Produced / Refined
            { "ItemFirewood", ResourceCategory.Produced },
            { "ItemWoodPlanks", ResourceCategory.Produced },
            { "ItemPelts", ResourceCategory.Produced },
            { "ItemTallow", ResourceCategory.Produced },
            { "ItemFlour", ResourceCategory.Produced },
            { "ItemIron", ResourceCategory.Produced },
            { "ItemBrick", ResourceCategory.Produced },
            { "ItemGoldIngot", ResourceCategory.Produced },
            { "ItemPaper", ResourceCategory.Produced },

            // Usable / Equipment
            { "ItemHeavyTools", ResourceCategory.Usable },
            { "ItemClothing", ResourceCategory.Usable },
            { "ItemShoes", ResourceCategory.Usable },
            { "ItemCandles", ResourceCategory.Usable },
            { "ItemSoap", ResourceCategory.Usable },
            { "ItemBeer", ResourceCategory.Usable },
            { "ItemGlass", ResourceCategory.Usable },
            { "ItemLinen", ResourceCategory.Usable },
            { "ItemLeather", ResourceCategory.Usable },
            { "ItemWeapons", ResourceCategory.Usable },
            { "ItemArmor", ResourceCategory.Usable },
        };

        public static ResourceCategory GetCategory(string itemId)
        {
            return KnownCategories.TryGetValue(itemId, out var cat)
                ? cat
                : ResourceCategory.RawMaterial;
        }

        public static UnityEngine.Color GetCategoryColor(ResourceCategory cat)
        {
            return cat switch
            {
                ResourceCategory.Food        => new UnityEngine.Color(0.63f, 0.72f, 0.41f, 1f),
                ResourceCategory.RawMaterial  => new UnityEngine.Color(0.63f, 0.53f, 0.38f, 1f),
                ResourceCategory.Produced     => new UnityEngine.Color(0.50f, 0.63f, 0.75f, 1f),
                ResourceCategory.Usable       => new UnityEngine.Color(0.75f, 0.63f, 0.44f, 1f),
                ResourceCategory.Livestock    => new UnityEngine.Color(0.56f, 0.69f, 0.50f, 1f),
                _                             => new UnityEngine.Color(0.63f, 0.53f, 0.38f, 1f),
            };
        }

        public static UnityEngine.Color CriticalColor => new UnityEngine.Color(0.88f, 0.31f, 0.25f, 1f);
    }
}
