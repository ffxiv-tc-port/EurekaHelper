using System.Collections.Generic;

namespace EurekaHelper.System
{
    // 台服 7.25 起遊戲文字的中間點由「·」(U+00B7) 改成「・」(U+30FB)，7.25 前存下的怪物名仍是舊寫法。
    // 以怪物名為鍵的資料（AggroRanges.json、SeenMonsters.json）與即時名稱一律經過這裡，以 U+30FB 為準。
    internal static class MonsterNameKey
    {
        private const char LegacyMiddleDot = (char)0x00B7;
        private const char MiddleDot = (char)0x30FB;

        // 不含舊寫法時回傳同一個字串、不配置，每幀掃描可以直接呼叫。
        public static string Normalize(string name) =>
            HasLegacyDot(name) ? name.Replace(LegacyMiddleDot, MiddleDot) : name;

        private static bool HasLegacyDot(string name) => name != null && name.Contains(LegacyMiddleDot);

        // 兩種寫法並存時併成一個鍵：先出現的清單在前，另一份逐筆補上。沒有舊寫法的鍵時原字典不動。
        public static (int Renamed, int Merged) NormalizeKeys(ref Dictionary<string, List<AggroRangeConfig>> ranges)
        {
            if (ranges == null)
                return (0, 0);

            var renamed = 0;
            foreach (var key in ranges.Keys)
            {
                if (HasLegacyDot(key))
                    renamed++;
            }

            if (renamed == 0)
                return (0, 0);

            var result = new Dictionary<string, List<AggroRangeConfig>>(ranges.Count);
            var merged = 0;
            foreach (var (key, list) in ranges)
            {
                var name = Normalize(key);
                if (!result.TryGetValue(name, out var existing))
                {
                    result[name] = list;
                    continue;
                }

                merged++;
                if (list == null)
                    continue;

                if (existing == null)
                    result[name] = existing = new List<AggroRangeConfig>();

                foreach (var config in list)
                    MergeInto(existing, config);
            }

            ranges = result;
            return (renamed, merged);
        }

        // 形狀參數全同、NpcId 不衝突＝同一筆（只補上缺的 NpcId）；其餘一律保留，不丟使用者量過的條目。
        private static void MergeInto(List<AggroRangeConfig> target, AggroRangeConfig config)
        {
            if (config == null)
                return;

            foreach (var existing in target)
            {
                if (existing == null || !SameShape(existing, config))
                    continue;

                if (existing.NpcId != 0 && config.NpcId != 0 && existing.NpcId != config.NpcId)
                    continue;

                if (existing.NpcId == 0)
                    existing.NpcId = config.NpcId;
                return;
            }

            target.Add(config);
        }

        private static bool SameShape(AggroRangeConfig a, AggroRangeConfig b) =>
            a.Type == b.Type && a.Shape == b.Shape && a.Radius == b.Radius &&
            a.ConeHalfAngleDegrees == b.ConeHalfAngleDegrees && a.Color == b.Color && a.Thickness == b.Thickness;

        // 集合語意：兩種寫法並存時只留新寫法一筆。沒有舊寫法時原集合不動。
        public static int NormalizeNames(ref HashSet<string> names)
        {
            if (names == null)
                return 0;

            var renamed = 0;
            foreach (var name in names)
            {
                if (HasLegacyDot(name))
                    renamed++;
            }

            if (renamed == 0)
                return 0;

            var result = new HashSet<string>(names.Count, names.Comparer);
            foreach (var name in names)
                result.Add(Normalize(name));

            names = result;
            return renamed;
        }
    }
}
