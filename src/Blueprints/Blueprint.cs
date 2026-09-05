using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// A captured layout: buildings stored as offsets from a stamp origin, so a
    /// blueprint is position- and save-independent by construction. Rotation is
    /// stored in 90° quarter-turns because FF snaps buildings to 90°
    /// (MiscUtilities.SnapAngleToClosest(y, 90f)) — a float euler would invite
    /// round-trip drift for no gain.
    ///
    /// SERIALIZATION: plain public fields and List&lt;T&gt; of [Serializable]
    /// types, so Unity's JsonUtility can round-trip this with no JSON library.
    /// These classes MUST be public, not internal: JsonUtility silently drops
    /// fields whose element type isn't public, which produced blueprint files
    /// containing only the header and no entries at all.
    /// That is why recipes are a list of triples rather than the dictionaries the
    /// game uses — JsonUtility cannot serialize Dictionary. They convert back to
    /// dictionaries at stamp time, which is what ConstructionData wants.
    /// </summary>
    [Serializable]
    public class Blueprint
    {
        public const int CurrentSchema = 1;

        public int schema = CurrentSchema;
        public string name = "";
        public string created = "";
        public List<BlueprintEntry> entries = new List<BlueprintEntry>();

        /// <summary>Footprint of the capture in world units.</summary>
        public Vector2 SizeXZ()
        {
            if (entries.Count == 0) return Vector2.zero;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var e in entries)
            {
                if (e.dx < minX) minX = e.dx;
                if (e.dx > maxX) maxX = e.dx;
                if (e.dz < minZ) minZ = e.dz;
                if (e.dz > maxZ) maxZ = e.dz;
            }
            return new Vector2(maxX - minX, maxZ - minZ);
        }

        /// <summary>A one-line description for the panel list.</summary>
        public string Summary()
        {
            var size = SizeXZ();
            return $"{entries.Count} building(s) · {size.x:0}×{size.y:0}";
        }
    }

    /// <summary>One building in a blueprint.</summary>
    [Serializable]
    public class BlueprintEntry
    {
        /// <summary>BuildingData identifier (Building.buildingDataRecordName).</summary>
        public string id = "";

        /// <summary>Offset from the stamp origin, world units, on the XZ plane.</summary>
        public float dx;
        public float dz;

        /// <summary>Rotation in quarter-turns (0-3), i.e. yaw / 90.</summary>
        public int rot90;

        public BlueprintSettings settings = new BlueprintSettings();

        public override string ToString() => $"{id} @({dx:0.##},{dz:0.##}) rot{rot90 * 90}°";
    }

    /// <summary>
    /// Per-building settings worth preserving. This mirrors what FF itself
    /// persists in Building.Save — and every field here has a matching field on
    /// ConstructionData, so a stamp passes them IN at construct time rather than
    /// re-applying them to the Building afterwards.
    /// </summary>
    [Serializable]
    public class BlueprintSettings
    {
        public bool workEnabled = true;
        public int workers = -1;        // -1 = leave at the building's default
        public string priority = "";    // Resource.Priority name; "" = leave default
        public List<RecipePref> recipes = new List<RecipePref>();

        public bool HasAny =>
            workers >= 0 || !workEnabled || priority.Length > 0 || recipes.Count > 0;
    }

    /// <summary>One recipe's state, keyed by ManufactureDefinition.guid —
    /// the same key ConstructionData's dictionaries use.</summary>
    [Serializable]
    public class RecipePref
    {
        public string guid = "";
        public bool enabled = true;
        public int batch = -1;   // -1 = not captured
    }
}
