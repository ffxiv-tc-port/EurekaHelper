using System.Numerics;
using System.Threading;

namespace EurekaHelper.System
{
    // 尋寶自動走向的純邏輯：旗標目標對應到哪個候選點、使用者在操作時延後送出。
    // 純計算、不碰 Dalamud，離線探針直接編譯本檔；只在框架執行緒呼叫，時間一律是呼叫端給的單調毫秒。
    public static class PotNavigation
    {
        // 旗標本身就是候選點時距離是 0；歷史挖到點離候選點實測都在 8 米內；純推算位置誤差數十米，不吸附。
        // 同一組候選點彼此至少相距 33 米（> 兩倍半徑），所以最多只有一點符合。
        public const float CandidateSnapRadius = PotTreasureCandidates.HistoryConfirmRadius;

        // 只找這一輪那一組：兩組之間有平面只差 14 米、高度差 33 米的點，跨組找會走到錯的樓層。
        // 回 true ＝ 最近的候選點在 CandidateSnapRadius 內；nearest／distance 在有候選點時一律填最近的那一點。
        public static bool TryResolveCandidate(ushort territoryId, PotCandidatePool pool, Vector2 flagTarget,
            out PotCandidate nearest, out float distance)
        {
            nearest = default;
            distance = float.MaxValue;
            foreach (var candidate in PotTreasureCandidates.GetPool(territoryId, pool))
            {
                var d = Vector2.Distance(candidate.Position2D, flagTarget);
                if (d < distance)
                {
                    nearest = candidate;
                    distance = d;
                }
            }

            return distance <= CandidateSnapRadius;
        }

        // vnavmesh 不記路線是誰送的：路線終點離本插件最後送的目的地（只比 X/Z）這麼近才算自己的。
        // 目的地不在網格上時 vnavmesh 會落到旁邊，與 BOCCHI OwnMoveDestinations 同值。
        public const float OwnPathMatchRadius = 20f;

        // 按清除時要停 vnavmesh 的哪些部分。sequence＝0（這一輪沒送過）一律不動。
        // 算路中分不出是誰的請求，本插件送過就取消；在走的路只在終點對得上（或讀不到終點）時才停。
        public static (bool CancelPathfind, bool StopPath) DecideOwnStop(int sequence, Vector3 destination,
            bool computing, bool running, Vector3? pathEnd)
        {
            if (sequence == 0)
                return (false, false);
            var stopPath = running && (pathEnd is not { } end
                || Vector2.Distance(new Vector2(end.X, end.Z), new Vector2(destination.X, destination.Z)) <= OwnPathMatchRadius);
            return (computing, stopPath);
        }
    }

    // 「使用者剛按過移動鍵」：按著時每幀記下，放開後 LatchMs 內仍算在操作。用旗標記「按過沒」，不拿 0 當「沒有」。
    public sealed class HandsOnLatch
    {
        public const long LatchMs = 1_000;

        private bool _seen;
        private long _lastAt;

        public void Note(bool pressed, long now)
        {
            if (!pressed)
                return;
            _seen = true;
            _lastAt = now;
        }

        public bool Active(long now) => _seen && now - _lastAt <= LatchMs;
    }

    // 使用者在操作時，這次提示的自動走向先記著；停手後只送一次、送當下最新的目的地。
    public sealed class AutoMoveDeferral
    {
        public bool Pending { get; private set; }

        // true ＝ 現在就送；false ＝ 記成待送。
        public bool OnRequest(bool handsOn)
        {
            if (handsOn)
            {
                Pending = true;
                return false;
            }

            Pending = false;
            return true;
        }

        // 每幀呼叫：有待送而且使用者已停手就回 true（並清掉待送）。
        public bool Tick(bool handsOn)
        {
            if (!Pending || handsOn)
                return false;
            Pending = false;
            return true;
        }

        public void Reset() => Pending = false;
    }

    // IPC「EurekaHelper.OccultPotNavTarget」背後的快照：自動走向每送出一次就換一份（序號 +1、不重用），0 ＝ 目前沒有。
    // 只在框架執行緒寫；Read 給任何執行緒，整份換參考，讀到的三個值一定屬於同一次送出。
    public sealed class PotNavTargetPublisher
    {
        private sealed class Snapshot
        {
            public int Sequence;
            public Vector3 Destination;
            public bool HasHeight;
        }

        private int _sequence;
        private Snapshot _current;

        // hasHeight＝false：送的是 /vnav moveflag，Destination 只有 X／Z（Y 是 0），vnavmesh 自己找地面。
        public int Publish(Vector3 destination, bool hasHeight)
        {
            _sequence = _sequence == int.MaxValue ? 1 : _sequence + 1;
            Volatile.Write(ref _current, new Snapshot { Sequence = _sequence, Destination = destination, HasHeight = hasHeight });
            return _sequence;
        }

        public void Clear() => Volatile.Write(ref _current, null);

        public (int Sequence, Vector3 Destination, bool HasHeight) Read()
        {
            var s = Volatile.Read(ref _current);
            return s == null ? (0, default, false) : (s.Sequence, s.Destination, s.HasHeight);
        }
    }
}
