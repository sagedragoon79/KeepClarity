using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Aggregates the effective in-season windows of every forageable type present
    /// on the current map. Season windows live on each spawned instance's
    /// SeasonalComponentBase (applied per-biome at map gen as unions of the four
    /// calendar quarters), so we sample the live instances rather than any static
    /// table — which also means windows modified by other mods (e.g. Tended Wilds
    /// relocation) are reflected automatically. Cultivated forager-shack garden
    /// instances are skipped: their seasons are the shack's business, not the map's.
    ///
    /// Shared by the ForageCalendarBar (visual) and ForageCalendarPatch (readout).
    /// Recomputed at most once per in-game day.
    /// </summary>
    internal static class ForageSeasonData
    {
        internal sealed class Row
        {
            public int TypeIndex;            // ForagingManager.ForagedItemType 0..7
            public string Name = "?";
            public Sprite? Icon;
            public int SeasonMask;           // bit0 Spring, bit1 Summer, bit2 Autumn, bit3 Winter
            public bool InSeasonNow;
        }

        // The four calendar quarters every SeasonalComponentBase window is built
        // from (ForagingManager.CreateForagingSourcesInArea uses these exact
        // constants; TimeManager.GetSeason agrees). Winter wraps the year end.
        private static readonly int[] QuarterStart = { 78, 171, 265, 355 }; // Spring, Summer, Autumn, Winter
        private static readonly int[] QuarterEnd   = { 170, 264, 354, 77 }; // Winter wraps

        private const int TypeCount = 8; // Mushrooms, Roots, Nuts, Herbs, Eggs, Willow, Greens, Berries

        private static List<Row>? _rows;
        private static int _cachedDay = -1;
        private static bool _loggedError;

        /// <summary>Day-of-year the current cache was built for (-1 = none). The bar
        /// compares this to know when to rebuild its row visuals.</summary>
        public static int CacheDay => _cachedDay;

        public static void ResetState()
        {
            _rows = null;
            _cachedDay = -1;
        }

        public static List<Row> GetRows()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null || gm.resourceManager == null || gm.timeManager == null)
                return _rows ?? new List<Row>();

            int day = gm.timeManager.currentDate.dayOfYear;
            if (_rows != null && day == _cachedDay) return _rows;

            try
            {
                _rows = BuildRows(gm);
                _cachedDay = day;
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    FFUIOverhaulMod.Log.Warning("[ForageCalendar] season aggregation failed: " + e.Message);
                }
                _rows ??= new List<Row>();
            }
            return _rows;
        }

        /// <summary>One line for the date/weather popup, e.g.
        /// "In season: Herbs, Greens, Berries" — or null when nothing to show
        /// (no forageables found yet / aggregation unavailable).</summary>
        public static string? GetInSeasonLine()
        {
            var rows = GetRows();
            if (rows.Count == 0) return null;
            var names = new List<string>();
            foreach (var r in rows)
                if (r.InSeasonNow) names.Add(r.Name);
            return "In season: " + (names.Count == 0 ? "none" : string.Join(", ", names));
        }

        private static List<Row> BuildRows(GameManager gm)
        {
            var masks = new int[TypeCount];
            var present = new bool[TypeCount];
            var items = ForagingManager.foragedItemsRO; // static type→Item map, index = ForagedItemType

            foreach (var res in gm.resourceManager.forageableResourceInstancesRO)
            {
                if (res == null || res.isCultivated) continue;
                int idx = TypeIndexOf(res, items);
                if (idx < 0 || idx >= TypeCount) continue;
                present[idx] = true;

                var seasonal = res.GetComponent<SeasonalComponentBase>();
                if (seasonal == null)
                {
                    // No component = never gated off (FF calls OnInSeason unconditionally).
                    masks[idx] |= 15;
                    continue;
                }
                foreach (var window in seasonal.seasons)
                    masks[idx] |= QuarterBits(window.first, window.second);
            }

            gm.timeManager.GetSeason(out var season, out _);
            int nowBit = (int)season - 1; // Season enum: None=0, Spring=1 → bit 0

            var rows = new List<Row>();
            for (int i = 0; i < TypeCount; i++)
            {
                if (!present[i]) continue;
                Item? item = (items != null && i < items.Count) ? items[i] : null;
                rows.Add(new Row
                {
                    TypeIndex = i,
                    Name = LocalizedItemName(item),
                    Icon = item != null ? item.icon : null,
                    SeasonMask = masks[i],
                    InSeasonNow = nowBit >= 0 && (masks[i] & (1 << nowBit)) != 0,
                });
            }
            return rows;
        }

        private static int TypeIndexOf(ForageableResource res, System.Collections.ObjectModel.ReadOnlyCollection<Item>? items)
        {
            if (items == null) return -1;
            var resItems = res.resourceItems;
            if (resItems == null) return -1;
            foreach (var it in resItems)
            {
                int i = items.IndexOf(it);
                if (i >= 0) return i;
            }
            return -1;
        }

        /// <summary>Which calendar quarters a [start,end] day window (wrap-aware)
        /// overlaps. Vanilla windows ARE quarters so this is exact; anything a mod
        /// set to an odd range maps to every quarter it touches.</summary>
        private static int QuarterBits(int start, int end)
        {
            int bits = 0;
            for (int q = 0; q < 4; q++)
            {
                int qs = QuarterStart[q], qe = QuarterEnd[q];
                if (MiscUtilities.IsWithinValidDay(start, end, qs)
                    || MiscUtilities.IsWithinValidDay(start, end, qe)
                    || MiscUtilities.IsWithinValidDay(qs, qe, start))
                    bits |= 1 << q;
            }
            return bits;
        }

        private static string LocalizedItemName(Item? item)
        {
            if (item == null) return "?";
            try
            {
                if (I2.Loc.LocalizationManager.TryGetTranslation(item.name, out string t)
                    && !string.IsNullOrEmpty(t))
                    return t;
            }
            catch { /* fall back to the raw key */ }
            return item.name;
        }
    }
}
