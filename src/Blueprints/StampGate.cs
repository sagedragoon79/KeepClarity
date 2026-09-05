using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>Why a blueprint entry can't be stamped right now.</summary>
    internal enum StampBlock
    {
        None,
        MissingBuilding,   // mod/DLC not installed on this save
        Locked,            // tech or prerequisite building not yet met
        Maxed,             // already at the game's cap (monuments, etc.)
    }

    /// <summary>
    /// Decides which blueprint entries may actually be placed.
    ///
    /// Building availability in FF is NOT a tech-tree effect (only crops and
    /// policies use GE_Unlock*). It is a prerequisite system on BuildingData:
    /// `prerequisiteIdentifiers` holds either "Tech_&lt;node&gt;" or another
    /// building's identifier, and BuildManager.Start compiles those into
    /// bitmask groups. The evaluated result lands in a private
    /// Dictionary&lt;string,bool&gt; on UIToolbarBuildingButtonManager, which is
    /// what greys out toolbar buttons — so reading that dictionary answers
    /// "can the player build this yet?" exactly as the game answers it, with no
    /// re-implementation of the prerequisite solver.
    ///
    /// A MISSING KEY MEANS UNLOCKED: buildings with no prerequisites never get a
    /// callback, so they never get an entry. Treating absence as "locked" would
    /// block most of the game.
    /// </summary>
    internal static class StampGate
    {
        private static FieldInfo? _prereqField;
        private static Component? _buttonManager;
        private static bool _resolved;
        private static bool _warnedNoDict;

        public static void Reset()
        {
            _resolved = false;
            _prereqField = null;
            _buttonManager = null;
            _warnedNoDict = false;
        }

        /// <summary>Can this building id be stamped? Also reports why not.</summary>
        public static StampBlock Check(string id, out int existingCount)
        {
            existingCount = 0;
            if (string.IsNullOrEmpty(id)) return StampBlock.MissingBuilding;

            // Free-build custom games bypass prerequisites entirely.
            bool freeBuild = false;
            try { freeBuild = SettingsManager.cstmEnableFreeBuildings; } catch { }

            BuildingData? bd = null;
            try { bd = GlobalAssets.buildingSetupData?.GetBuildingData(id); } catch { }
            if (bd == null) return StampBlock.MissingBuilding;

            var bm = UnitySingleton<GameManager>.Instance?.buildManager;
            if (bm != null)
            {
                try
                {
                    if (bm.BuildingIsMaxed(id, out existingCount, true)) return StampBlock.Maxed;
                }
                catch { }
            }

            if (freeBuild) return StampBlock.None;

            var met = PrereqsMet();
            if (met == null) return StampBlock.None;   // can't tell — don't block

            // Absent key == no prerequisites == unlocked.
            if (met.TryGetValue(id, out bool ok) && !ok) return StampBlock.Locked;
            return StampBlock.None;
        }

        public static string Describe(StampBlock b) => b switch
        {
            StampBlock.MissingBuilding => "not installed",
            StampBlock.Locked => "locked",
            StampBlock.Maxed => "at max count",
            _ => "",
        };

        private static Dictionary<string, bool>? PrereqsMet()
        {
            if (!_resolved)
            {
                _resolved = true;
                try
                {
                    // Same path BuildManager.Start uses to wire its callbacks.
                    var gm = UnitySingleton<GameManager>.Instance;
                    var window = gm?.uiManager?.windowManager?.buildingsWindow;
                    _buttonManager = window != null
                        ? window.GetComponent<UIToolbarBuildingButtonManager>()
                        : null;

                    _prereqField = typeof(UIToolbarBuildingButtonManager).GetField(
                        "buildingNameToPrereqsMet",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                }
                catch (Exception e)
                {
                    FFUIOverhaulMod.Log.Warning("[Gate] prerequisite lookup unavailable: " + e.Message);
                }
            }

            if (_buttonManager == null || _prereqField == null)
            {
                if (!_warnedNoDict)
                {
                    _warnedNoDict = true;
                    FFUIOverhaulMod.Log.Warning(
                        "[Gate] can't read building prerequisites — locked buildings will not be " +
                        "filtered out of stamps (everything else still works).");
                }
                return null;
            }

            try { return _prereqField.GetValue(_buttonManager) as Dictionary<string, bool>; }
            catch { return null; }
        }
    }
}
