using System;
using System.Reflection;
using MelonLoader;

namespace FFUIOverhaul.Workers
{
    /// <summary>
    /// Essential Provisions' Workplace Mastery, read through EP's public
    /// WorkInfoApi (soft-dep, bound once by reflection). Shared by the picker
    /// column and the worker-slot label so both show the same numbers.
    ///
    /// All values are percentage points (12f == +12%) and 0 / empty when EP is
    /// absent, its toggle is off, or the villager has no tenure.
    /// </summary>
    internal static class EpMastery
    {
        private static bool _resolved;
        private static Func<Villager, int, string[]>? _topNames;
        private static Func<Villager, int, float[]>? _topPercents;
        private static Func<Villager, float>? _current;
        private static MelonPreferences_Entry<bool>? _toggle;

        /// <summary>EP is loaded with the mastery API AND its Workplace Mastery
        /// toggle is on.</summary>
        public static bool Active
        {
            get
            {
                Resolve();
                if (_topNames == null || _topPercents == null) return false;
                if (_toggle == null) _toggle = MelonPreferences.GetEntry<bool>("EssentialProvisions", "EnableWorkplaceMastery");
                return _toggle != null && _toggle.Value;
            }
        }

        /// <summary>The villager's highest-mastery jobs as occupation enum names,
        /// best first, zero-mastery jobs left out.</summary>
        public static string[] TopNames(Villager v, int count)
        {
            if (v == null || _topNames == null) return new string[0];
            try { return _topNames(v, count) ?? new string[0]; } catch { return new string[0]; }
        }

        /// <summary>Parallel to <see cref="TopNames"/>.</summary>
        public static float[] TopPercents(Villager v, int count)
        {
            if (v == null || _topPercents == null) return new float[0];
            try { return _topPercents(v, count) ?? new float[0]; } catch { return new float[0]; }
        }

        /// <summary>Mastery in the villager's current job.</summary>
        public static float Current(Villager v)
        {
            if (v == null || _current == null) return 0f;
            try { return _current(v); } catch { return 0f; }
        }

        /// <summary>Mastery in one job, by occupation enum name.</summary>
        public static float For(Villager v, string occupation)
        {
            var names = TopNames(v, 64);
            var pcts = TopPercents(v, 64);
            for (int i = 0; i < names.Length && i < pcts.Length; i++)
                if (names[i] == occupation) return pcts[i];
            return 0f;
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            Type? api = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { api = a.GetType("EssentialProvisions.WorkInfoApi"); } catch { continue; }
                if (api != null) break;
            }
            if (api == null)
            {
                FFUIOverhaulMod.Log.Msg("[WorkerPicker] Essential Provisions not found — mastery displays off.");
                return;
            }

            const BindingFlags F = BindingFlags.Public | BindingFlags.Static;
            try
            {
                var top = new[] { typeof(Villager), typeof(int) };
                var one = new[] { typeof(Villager) };
                var mn = api.GetMethod("GetTopMasteryJobNames", F, null, top, null);
                var mp = api.GetMethod("GetTopMasteryJobPercents", F, null, top, null);
                var mc = api.GetMethod("GetMasteryBonusPercent", F, null, one, null);
                if (mn != null) _topNames = (Func<Villager, int, string[]>)Delegate.CreateDelegate(typeof(Func<Villager, int, string[]>), mn);
                if (mp != null) _topPercents = (Func<Villager, int, float[]>)Delegate.CreateDelegate(typeof(Func<Villager, int, float[]>), mp);
                if (mc != null) _current = (Func<Villager, float>)Delegate.CreateDelegate(typeof(Func<Villager, float>), mc);
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[WorkerPicker] EP mastery bind failed: " + e.Message); }

            FFUIOverhaulMod.Log.Msg(_topNames != null && _topPercents != null
                ? "[WorkerPicker] EP mastery API bound — mastery displays available."
                : "[WorkerPicker] EP found but its mastery API is missing (older EP?) — mastery displays off.");
        }
    }
}
