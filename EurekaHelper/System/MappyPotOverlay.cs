using System;
using System.Collections.Generic;
using System.Numerics;

namespace EurekaHelper.System
{
    // 魔法甕尋寶候選點推給 Mappy：可能有＝淡色空心圈（pot.possible）、已確認＝實心點（pot.confirmed），同一個顏色。
    // 只在框架執行緒用：SetCandidates／ClearCandidates 在聊天事件與換區事件，Tick 在 Framework.Update。
    // 內容沒變不重送、不評估的幀零配置；沒有內容也沒推過東西時連 Mappy 都不問。TTL 兜底：沒來得及清時最多 TtlMs 後自己消失。
    public sealed class MappyPotOverlay
    {
        public const string PossibleKey = "pot.possible";
        public const string ConfirmedKey = "pot.confirmed";
        public const int TtlMs = 30_000;
        public const long RefreshMs = TtlMs / 3;
        // 深紫紅：與 BOCCHI 魔法甕出生／等待點（粉紅 E377C2）同色系但暗得多。不用遊戲畫面上那個金黃：
        // Mappy 上 BOCCHI 的幸運胡蘿蔔與 Mappy 自己的好友標記都是金黃，在羊皮紙底色上也最淡。
        public const uint Rgb = 0x80004C;
        public const int FlagsConfirmed = 0;
        public const int FlagsPossible = 1;

        private sealed class Layer
        {
            public readonly string Key;
            public readonly int Flags;
            public Vector3[] Points = Array.Empty<Vector3>();
            public string[] Labels = Array.Empty<string>();
            public bool Pushed;

            public Layer(string key, int flags)
            {
                Key = key;
                Flags = flags;
            }
        }

        private readonly MappyOverlayClient _client;
        private readonly Action<string> _log;
        private readonly Layer _possible = new(PossibleKey, FlagsPossible);
        private readonly Layer _confirmed = new(ConfirmedKey, FlagsConfirmed);

        private uint _territoryId;
        private int _version;
        private int _pushedVersion = -1;
        private long _refreshAt;

        public MappyPotOverlay(MappyOverlayClient client, Action<string> log)
        {
            _client = client;
            _log = log;
        }

        public MappyOverlayClient Client => _client;

        public bool HasContent => _possible.Points.Length > 0 || _confirmed.Points.Length > 0;

        private bool AnyPushed => _possible.Pushed || _confirmed.Pushed;

        public void SetCandidates(ushort territoryId, IReadOnlyList<PotCandidateState> states, PotCandidate? flagTarget)
        {
            if (territoryId == 0 || states.Count == 0)
            {
                ClearCandidates();
                return;
            }

            var confirmedCount = 0;
            foreach (var state in states)
            {
                if (state.Confirmed)
                    confirmedCount++;
            }

            var possiblePoints = new Vector3[states.Count - confirmedCount];
            var possibleLabels = new string[possiblePoints.Length];
            var confirmedPoints = new Vector3[confirmedCount];
            var confirmedLabels = new string[confirmedCount];
            int p = 0, c = 0;
            foreach (var state in states)
            {
                var isTarget = flagTarget is { } target && target == state.Candidate;
                var label = Label(state.Candidate, state.Confirmed, isTarget);
                if (state.Confirmed)
                {
                    confirmedPoints[c] = state.Candidate.Position;
                    confirmedLabels[c++] = label;
                }
                else
                {
                    possiblePoints[p] = state.Candidate.Position;
                    possibleLabels[p++] = label;
                }
            }

            var changed = _territoryId != territoryId;
            changed |= Replace(_possible, possiblePoints, possibleLabels);
            changed |= Replace(_confirmed, confirmedPoints, confirmedLabels);
            _territoryId = territoryId;
            if (changed)
                _version++;
        }

        public void ClearCandidates()
        {
            if (!HasContent)
                return;

            Replace(_possible, Array.Empty<Vector3>(), Array.Empty<string>());
            Replace(_confirmed, Array.Empty<Vector3>(), Array.Empty<string>());
            _version++;
        }

        // 每幀呼叫。enabled false（設定關）時把推過的整個來源清掉。
        public void Tick(long now, bool enabled)
        {
            if (!enabled)
            {
                if (AnyPushed && _client.IsAvailable)
                    _client.ClearSource(now);
                Forget();
                return;
            }

            if (!HasContent && !AnyPushed)
                return;

            // Mappy 不在了（含呼叫失敗後的重問等待期），它那邊的點也跟著不在：回來時全部重推。
            if (!_client.Poll(now))
            {
                Forget();
                return;
            }

            if (_pushedVersion == _version && now < _refreshAt)
                return;

            var isNewContent = _pushedVersion != _version;
            if (!Push(_possible, now) || !Push(_confirmed, now))
                return;

            _pushedVersion = _version;
            _refreshAt = now + RefreshMs;
            if (isNewContent)
            {
                _log(HasContent
                    ? $"[TreasureHuntManager] Mappy：顯示尋寶候選點 可能 {_possible.Points.Length} 處、已確認 {_confirmed.Points.Length} 處（地區 {_territoryId}）"
                    : "[TreasureHuntManager] Mappy：已移除尋寶候選點");
            }
        }

        // 插件卸載：推過東西就整個來源清掉。
        public void Shutdown(long now)
        {
            if (AnyPushed && _client.IsAvailable)
                _client.ClearSource(now);
            Forget();
        }

        // false ＝ 呼叫失敗（Mappy 不能用了），這一輪到此為止。
        private bool Push(Layer layer, long now)
        {
            if (layer.Points.Length > 0)
            {
                if (!_client.SetPoints(layer.Key, _territoryId, layer.Points, Rgb, layer.Flags, layer.Labels, TtlMs, now))
                    return false;
                layer.Pushed = true;
                return true;
            }

            if (!layer.Pushed)
                return true;

            if (!_client.Remove(layer.Key, now))
                return false;
            layer.Pushed = false;
            return true;
        }

        private void Forget()
        {
            _possible.Pushed = false;
            _confirmed.Pushed = false;
            _pushedVersion = -1;
        }

        // 內容逐點逐字相同就保留舊陣列、回 false。
        private static bool Replace(Layer layer, Vector3[] points, string[] labels)
        {
            if (layer.Points.AsSpan().SequenceEqual(points) && layer.Labels.AsSpan().SequenceEqual(labels))
                return false;

            layer.Points = points;
            layer.Labels = labels;
            return true;
        }

        public static string Label(PotCandidate candidate, bool confirmed, bool isFlagTarget)
        {
            string text;
            if (candidate.FateId == 0)
            {
                text = Loc.Format(confirmed ? "Second treasure #{0}" : "Second treasure spot #{0}", candidate.Number);
            }
            else
            {
                var fate = candidate.FateId switch
                {
                    PotTreasureCandidates.NorthPotFateId => Loc.Text("Persistent Pots"),
                    PotTreasureCandidates.SouthPotFateId => Loc.Text("Pleading Pots"),
                    _ => null,
                };
                text = fate == null
                    ? Loc.Format(confirmed ? "Treasure #{0}" : "Treasure spot #{0}", candidate.Number)
                    : Loc.Format(confirmed ? "Treasure {0} #{1}" : "Treasure spot {0} #{1}", fate, candidate.Number);
            }

            return isFlagTarget ? text + Loc.Text(" (flag target)") : text;
        }
    }
}
