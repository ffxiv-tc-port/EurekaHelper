using System;

namespace EurekaHelper.System
{
    public enum PotHuntEndReason
    {
        Found,
        Cleared,
        TerritoryChanged,
        Logout,
        AutoMoveOff,
        Idle,
        MaxDuration,
        Unload,
        EmergencyStop,
    }

    public enum PotHuntHintResult
    {
        Ignored,
        Started,
        Continued,
        Capped,
    }

    // IPC「EurekaHelper.OccultPotHuntActive」背後的狀態機。純計算、不碰 Dalamud，離線探針直接編譯本檔。
    // 只在框架執行緒呼叫；別的執行緒只讀 ActiveUntilMs 的快照，再用 IsActiveAt 對時鐘判斷。
    // 進行中＝開著自動走向旗標、在有候選表的地區收到提示，之後沒滿 IdleTimeoutMs 沒有新提示、整輪沒滿 MaxDurationMs。
    // 時間一律是呼叫端給的單調毫秒（Environment.TickCount64）。
    public sealed class PotHuntActivity
    {
        public const long IdleTimeoutMs = 120_000;
        public const long MaxDurationMs = 300_000;

        // Capped ＝ 整輪滿 MaxDurationMs：之後的提示不再算進行中，直到 IdleTimeoutMs 沒有提示或整輪作廢。
        private enum State
        {
            None,
            Active,
            Capped,
        }

        private State _state;

        public bool IsActive => _state == State.Active;
        public bool HasRound => _state != State.None;
        public ushort TerritoryId { get; private set; }
        public long StartedAtMs { get; private set; }
        public long LastHintAtMs { get; private set; }
        public int HintCount { get; private set; }

        // 0 ＝ 不在進行中；否則在這個時間之前（不含）都算進行中。
        public long ActiveUntilMs => IsActive ? Math.Min(StartedAtMs + MaxDurationMs, LastHintAtMs + IdleTimeoutMs) : 0;

        public static bool IsActiveAt(long activeUntilMs, long nowMs) => activeUntilMs != 0 && nowMs < activeUntilMs;

        public PotHuntHintResult OnHint(long nowMs, ushort territoryId, bool autoMove)
        {
            if (!autoMove || !PotTreasureCandidates.HasTable(territoryId))
                return PotHuntHintResult.Ignored;

            // 同一區、閒置上限內的提示屬於同一輪；整輪滿上限後只延後解除，不再算進行中。
            if (_state != State.None && territoryId == TerritoryId && nowMs < LastHintAtMs + IdleTimeoutMs)
            {
                LastHintAtMs = nowMs;
                HintCount++;
                if (_state == State.Active && nowMs < StartedAtMs + MaxDurationMs)
                    return PotHuntHintResult.Continued;

                _state = State.Capped;
                return PotHuntHintResult.Capped;
            }

            _state = State.Active;
            TerritoryId = territoryId;
            StartedAtMs = nowMs;
            LastHintAtMs = nowMs;
            HintCount = 1;
            return PotHuntHintResult.Started;
        }

        // 每幀呼叫。回傳這一幀從進行中結束的原因，沒有結束回 null（Capped 解除不算結束）。
        public PotHuntEndReason? Tick(long nowMs, ushort territoryId, bool autoMove, bool loggedIn)
        {
            if (_state == State.None)
                return null;

            if (_state == State.Capped)
            {
                if (!loggedIn || territoryId != TerritoryId || nowMs >= LastHintAtMs + IdleTimeoutMs)
                    _state = State.None;
                return null;
            }

            PotHuntEndReason? reason = null;
            if (!loggedIn)
                reason = PotHuntEndReason.Logout;
            else if (territoryId != TerritoryId)
                reason = PotHuntEndReason.TerritoryChanged;
            else if (!autoMove)
                reason = PotHuntEndReason.AutoMoveOff;
            else if (nowMs >= LastHintAtMs + IdleTimeoutMs)
                reason = PotHuntEndReason.Idle;
            else if (nowMs >= StartedAtMs + MaxDurationMs)
                reason = PotHuntEndReason.MaxDuration;

            if (reason == PotHuntEndReason.MaxDuration)
                _state = State.Capped;
            else if (reason != null)
                _state = State.None;
            return reason;
        }

        // 挖到、清除、換區、登出、卸載、急停：整輪作廢（含 Capped）。回傳 true ＝ 原本在進行中。
        public bool End()
        {
            var wasActive = _state == State.Active;
            _state = State.None;
            return wasActive;
        }
    }
}
