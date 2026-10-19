using System;
using System.Collections.Generic;
using System.Numerics;

namespace EurekaHelper.System
{
    // 新月島南部（TerritoryType 1252）魔法甕財寶的候選點表與篩選。純計算、不碰 Dalamud，
    // 離線重播測試直接編譯本檔；只在框架執行緒的聊天事件裡被呼叫。
    // 座標來源：上游 BOCCHI（OhKannaDuh/BOCCHI，aa4f6efce）SouthHorn.GetPotChestData()
    // 與 GetRerollPotChestData()。
    public enum PotCandidatePool
    {
        Primary = 0,
        SecondChance = 1,
    }

    // Number 是該組內 1 起算的編號；FateId 0 ＝ 第二處財寶的偏遠點（不屬於任何一場）。
    public readonly record struct PotCandidate(int Number, ushort FateId, Vector3 Position)
    {
        public Vector2 Position2D => new(Position.X, Position.Z);
    }

    public readonly record struct PotHint(Vector2 Origin, float AngleDegrees, string TierText);

    // Confirmed ＝ 只剩這一點符合，或以前在這裡挖到過（TreasureHuntHistory）。
    public readonly record struct PotCandidateState(PotCandidate Candidate, bool Confirmed);

    public static class PotTreasureCandidates
    {
        public const ushort SouthHornTerritoryId = 1252;
        public const ushort NorthPotFateId = 1976;
        public const ushort SouthPotFateId = 1977;

        // 距離帶與 Matches 優雷卡也共用：兩地實測交界都在 25/100/200 米附近（約 ±6 米），加上原點誤差，
        // 比對兩側各放 DistanceSlack。很遠沒有上限，用 float.MaxValue 與既有尋寶紀錄存的值相同。
        private static readonly Dictionary<string, (float Min, float Max)> TierBands = new()
        {
            ["很近"] = (0f, 25f),
            ["不遠"] = (25f, 100f),
            ["稍遠"] = (100f, 200f),
            ["很遠"] = (200f, float.MaxValue),
        };

        public static bool TryGetTierBand(string tierText, out (float Min, float Max) band) =>
            TierBands.TryGetValue(tierText, out band);

        private const float DistanceSlack = 12f;
        private const float FanHalfAngleDegrees = 22.5f;

        // 提示的原點是「訊息到達時」的玩家位置，移動中可能差幾米；近距離時這幾米會吃掉很大的角度。
        private const float OriginSlack = 5f;

        // 第一則提示離某場魔法甕 F.A.T.E. 中心多近才算「剛打完那一場」。任何候選點離兩個中心都至少
        // 155 米，所以在候選點旁開始的第二處財寶不會被誤認成新的一輪。
        private const float FateInferenceRadius = 100f;

        // 歷史挖到點離候選點多近才算證實。候選點彼此至少相距 33 米，一筆紀錄不會同時證實兩點。
        public const float HistoryConfirmRadius = 10f;

        private static readonly Vector2 NorthPotFateCenter = new(200f, -215f);
        private static readonly Vector2 SouthPotFateCenter = new(-481f, 528f);

        private static readonly PotCandidate[] PrimaryPads =
        [
            new(1, 1976, new Vector3(571.5841f, 51.451305f, -813.1642f)),
            new(2, 1976, new Vector3(662.4388f, 120.0f, 161.1339f)),
            new(3, 1976, new Vector3(606.4641f, 108.07402f, 184.8517f)),
            new(4, 1976, new Vector3(-312.2778f, 103.19944f, -35.25348f)),
            new(5, 1976, new Vector3(587.7039f, 78.8956f, -545.8168f)),
            new(6, 1976, new Vector3(891.2597f, 120.0f, -20.672f)),
            new(7, 1976, new Vector3(878.1131f, 108.28959f, -91.1057f)),
            new(8, 1976, new Vector3(803.6609f, 95.99998f, -354.1809f)),
            new(9, 1976, new Vector3(341.4413f, 95.99999f, 194.7507f)),
            new(10, 1976, new Vector3(570.2421f, 64.66201f, 272.1734f)),
            new(11, 1976, new Vector3(-216.372f, 5.4469404f, -510.1361f)),
            new(12, 1976, new Vector3(684.4223f, 96.10129f, -165.4811f)),
            new(13, 1976, new Vector3(-188.1745f, 2.999999f, -717.2005f)),
            new(14, 1976, new Vector3(-476.3011f, 101.44228f, -86.69939f)),
            new(15, 1976, new Vector3(80.19762f, 101.27949f, 391.2263f)),
            new(16, 1976, new Vector3(-534.6993f, 2.999998f, -651.6244f)),
            new(17, 1976, new Vector3(-165.2374f, 95.33837f, 437.4505f)),
            new(18, 1976, new Vector3(330.8659f, 6.7168036f, -654.5339f)),
            new(19, 1976, new Vector3(-333.3444f, 2.9999998f, -861.1722f)),
            new(20, 1976, new Vector3(-313.2906f, 108.10962f, 70.76207f)),
            new(21, 1976, new Vector3(-459.1735f, 93.57443f, 5.054043f)),
            new(22, 1976, new Vector3(-54.69518f, 99.40573f, 405.0261f)),
            new(23, 1976, new Vector3(-382.4396f, 109.30187f, -378.3482f)),
            new(24, 1976, new Vector3(263.2559f, 100.38499f, 326.6834f)),
            new(25, 1976, new Vector3(224.7233f, 68.7328f, 518.668f)),
            new(26, 1976, new Vector3(19.73968f, 26.045855f, -420.977f)),
            new(27, 1976, new Vector3(705.2716f, 68.143616f, 358.6714f)),
            new(28, 1976, new Vector3(-660.5336f, 98.0f, -216.7666f)),
            new(29, 1976, new Vector3(-324.2736f, 121.0f, 203.2017f)),
            new(30, 1976, new Vector3(-386.5904f, -0.13994062f, -461.0976f)),
            new(1, 1977, new Vector3(-195.4419f, 110.15342f, -287.8911f)),
            new(2, 1977, new Vector3(74.73397f, 110.494316f, -394.1289f)),
            new(3, 1977, new Vector3(-386.437f, 98.60658f, -221.7847f)),
            new(4, 1977, new Vector3(-554.6146f, 99.01769f, -309.1231f)),
            new(5, 1977, new Vector3(107.0611f, 105.699875f, 146.7059f)),
            new(6, 1977, new Vector3(825.9521f, 70.0f, 772.4054f)),
            new(7, 1977, new Vector3(-836.7586f, 106.999985f, 597.2944f)),
            new(8, 1977, new Vector3(67.45271f, 69.477974f, 745.8658f)),
            new(9, 1977, new Vector3(45.0f, 111.56f, -225.0f)),
            new(10, 1977, new Vector3(301.8741f, 103.784424f, 70.59854f)),
            new(11, 1977, new Vector3(-38.97946f, 102.073296f, -175.4589f)),
            new(12, 1977, new Vector3(-60.72729f, 69.687035f, 828.4997f)),
            new(13, 1977, new Vector3(17.60418f, 65.93209f, 674.6207f)),
            new(14, 1977, new Vector3(393.2685f, 57.545956f, 844.6924f)),
            new(15, 1977, new Vector3(393.0191f, 104.0f, -124.1651f)),
            new(16, 1977, new Vector3(-798.7886f, 84.22545f, -4.822005f)),
            new(17, 1977, new Vector3(440.8355f, 70.3f, 876.4097f)),
            new(18, 1977, new Vector3(-734.1434f, 170.99998f, 683.7238f)),
            new(19, 1977, new Vector3(423.3505f, 70.3f, 578.9013f)),
            new(20, 1977, new Vector3(200.1241f, 56.0f, 624.2285f)),
            new(21, 1977, new Vector3(-603.3457f, 139.0f, 858.6771f)),
            new(22, 1977, new Vector3(-829.598f, 62.66814f, 66.82948f)),
            new(23, 1977, new Vector3(-645.3027f, 135.69208f, -73.54771f)),
            new(24, 1977, new Vector3(-836.1612f, 107.0f, 770.2822f)),
            new(25, 1977, new Vector3(-676.6202f, 128.57442f, 1.531581f)),
            new(26, 1977, new Vector3(-713.6796f, 203.0f, 710.08f)),
            new(27, 1977, new Vector3(781.2514f, 70.0f, 560.0701f)),
            new(28, 1977, new Vector3(-746.1318f, 172.00023f, 828.8809f)),
            new(29, 1977, new Vector3(-730.5441f, 107.694275f, -371.4776f)),
            new(30, 1977, new Vector3(-810.8279f, 114.053925f, -226.8324f)),
        ];

        private static readonly PotCandidate[] SecondChancePads =
        [
            new(1, 0, new Vector3(-676.4631f, 5.0f, -769.7955f)),
            new(2, 0, new Vector3(-823.9183f, 140.00032f, 677.6934f)),
            new(3, 0, new Vector3(-886.4718f, 107.0f, 712.4964f)),
            new(4, 0, new Vector3(-625.7809f, 171.0f, 810.8691f)),
            new(5, 0, new Vector3(-813.9943f, 5.0f, -663.3634f)),
            new(6, 0, new Vector3(-842.8967f, 75.76903f, -125.0559f)),
            new(7, 0, new Vector3(-680.0345f, 201.0f, 739.9117f)),
            new(8, 0, new Vector3(-793.0552f, 5.0f, -777.3126f)),
            new(9, 0, new Vector3(-708.6777f, 171.0f, 669.5714f)),
            new(10, 0, new Vector3(-718.0424f, 5.0f, -633.8791f)),
            new(11, 0, new Vector3(-868.8489f, 67.5054f, -59.44909f)),
            new(12, 0, new Vector3(-803.5182f, 3.0f, -602.7497f)),
            new(13, 0, new Vector3(-732.2048f, 139.0f, 828.8491f)),
            new(14, 0, new Vector3(-659.1158f, 12.198493f, -508.7968f)),
            new(15, 0, new Vector3(-785.997f, 162.39513f, 790.5948f)),
            new(16, 0, new Vector3(-840.8771f, 107.26465f, -250.273f)),
            new(17, 0, new Vector3(-708.687f, 141.16982f, -139.3283f)),
            new(18, 0, new Vector3(-796.66f, 114.15647f, -228.9318f)),
            new(19, 0, new Vector3(-776.6315f, 5.0f, -486.978f)),
            new(20, 0, new Vector3(-758.8058f, 127.66496f, -183.164f)),
        ];

        public static bool HasTable(ushort territoryId) => territoryId == SouthHornTerritoryId;

        public static IReadOnlyList<PotCandidate> GetPool(ushort territoryId, PotCandidatePool pool)
        {
            if (!HasTable(territoryId))
                return Array.Empty<PotCandidate>();

            return pool == PotCandidatePool.SecondChance ? SecondChancePads : PrimaryPads;
        }

        // null ＝ 判斷不了是哪一場。只拿來排序，不拿來刪候選。
        public static ushort? InferFateId(ushort territoryId, Vector2 firstHintOrigin)
        {
            if (!HasTable(territoryId))
                return null;

            var north = Vector2.Distance(firstHintOrigin, NorthPotFateCenter);
            var south = Vector2.Distance(firstHintOrigin, SouthPotFateCenter);
            if (north <= FateInferenceRadius && north <= south)
                return NorthPotFateId;
            if (south <= FateInferenceRadius)
                return SouthPotFateId;
            return null;
        }

        public static bool Matches(Vector2 candidate, PotHint hint)
        {
            var offset = candidate - hint.Origin;
            var distance = offset.Length();

            // 不認得的距離文字只用方向篩：不知道界線就不拿它排除。
            if (TierBands.TryGetValue(hint.TierText, out var band) &&
                (distance < band.Min - DistanceSlack || distance > band.Max + DistanceSlack))
                return false;

            if (distance <= OriginSlack)
                return true;

            var bearing = MathF.Atan2(offset.X, -offset.Y) * 180f / MathF.PI;
            if (bearing < 0f)
                bearing += 360f;

            var diff = MathF.Abs(bearing - hint.AngleDegrees) % 360f;
            if (diff > 180f)
                diff = 360f - diff;

            var tolerance = FanHalfAngleDegrees + MathF.Asin(OriginSlack / distance) * 180f / MathF.PI;
            return diff <= tolerance;
        }

        public static List<PotCandidate> Filter(IReadOnlyList<PotCandidate> pool, IReadOnlyList<PotHint> hints)
        {
            var result = new List<PotCandidate>();
            foreach (var candidate in pool)
            {
                var ok = true;
                foreach (var hint in hints)
                {
                    if (!Matches(candidate.Position2D, hint))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    result.Add(candidate);
            }

            return result;
        }

        // 選離玩家（最新一則提示的原點）最近的；判斷得出是哪一場時，那一場的點優先。
        // 走到最近的點再用一次聖靈藥，不是答案就會被下一則提示刷掉，剩下的範圍跟著縮小。
        public static PotCandidate? PickTarget(IReadOnlyList<PotCandidate> matching, Vector2 from, ushort? preferredFateId)
        {
            PotCandidate? best = null;
            var bestRank = int.MaxValue;
            var bestDistance = float.MaxValue;
            foreach (var candidate in matching)
            {
                var rank = preferredFateId.HasValue && candidate.FateId == preferredFateId.Value ? 0 : 1;
                var distance = Vector2.Distance(candidate.Position2D, from);
                if (rank < bestRank || (rank == bestRank && distance < bestDistance))
                {
                    best = candidate;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static bool IsConfirmedByHistory(PotCandidate candidate, IEnumerable<Vector2> foundPositions)
        {
            foreach (var found in foundPositions)
            {
                if (Vector2.Distance(candidate.Position2D, found) <= HistoryConfirmRadius)
                    return true;
            }

            return false;
        }
    }
}
