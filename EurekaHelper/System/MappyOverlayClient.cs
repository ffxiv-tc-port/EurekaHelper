using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace EurekaHelper.System
{
    public enum MappyOverlayStatus
    {
        NotChecked,
        Connected,
        NotFound,
    }

    // Mappy.Overlay.* 的呼叫端。契約（名字與型別逐字照 Mappy，不可改）：ApiVersion Func<int>（1 起）；
    // SetPoints(string source, string key, uint territoryId, Vector3[] points, uint rgb, int flags, string[] labels, int ttlMs) -> bool；
    // Remove(string source, string key) -> bool；ClearSource(string source) -> bool。
    // 只在框架執行緒呼叫，絕不擲出。一律傳陣列：型別不合時 CallGate 會靜默走一趟 JSON 來回。
    public sealed class MappyOverlayClient
    {
        public const string Source = "EurekaHelper";
        public const long ProbeIntervalMs = 5000;
        public const int SupportedApiVersion = 1;

        private readonly ICallGateSubscriber<int> _apiVersion;
        private readonly ICallGateSubscriber<string, string, uint, Vector3[], uint, int, string[], int, bool> _setPoints;
        private readonly ICallGateSubscriber<string, string, bool> _remove;
        private readonly ICallGateSubscriber<string, bool> _clearSource;
        private readonly Action<string> _log;

        private long _nextProbeAt;
        private bool _failureReported;
        private bool _rejectReported;

        public MappyOverlayClient(IDalamudPluginInterface pluginInterface, Action<string> log)
        {
            _log = log;
            try
            {
                _apiVersion = pluginInterface.GetIpcSubscriber<int>("Mappy.Overlay.ApiVersion");
                _setPoints = pluginInterface.GetIpcSubscriber<string, string, uint, Vector3[], uint, int, string[], int, bool>("Mappy.Overlay.SetPoints");
                _remove = pluginInterface.GetIpcSubscriber<string, string, bool>("Mappy.Overlay.Remove");
                _clearSource = pluginInterface.GetIpcSubscriber<string, bool>("Mappy.Overlay.ClearSource");
            }
            catch (Exception ex)
            {
                Report(ex, "取得 Mappy.Overlay 呼叫端");
            }
        }

        public bool IsAvailable { get; private set; }

        // 至少問過一次 ApiVersion：UI 用來分辨「沒找到」與「還沒找過」。
        public bool HasProbed { get; private set; }

        public MappyOverlayStatus Status =>
            IsAvailable ? MappyOverlayStatus.Connected : HasProbed ? MappyOverlayStatus.NotFound : MappyOverlayStatus.NotChecked;

        // 不能用時每 ProbeIntervalMs 問一次 ApiVersion。回傳現在能不能用。
        public bool Poll(long now)
        {
            if (IsAvailable)
                return true;

            if (now < _nextProbeAt || _apiVersion == null)
                return false;

            _nextProbeAt = now + ProbeIntervalMs;
            HasProbed = true;
            int version;
            try
            {
                version = _apiVersion.InvokeFunc();
            }
            catch (IpcNotReadyError)
            {
                return false;
            }
            catch (Exception ex)
            {
                Report(ex, "Mappy.Overlay.ApiVersion");
                return false;
            }

            if (version < SupportedApiVersion)
            {
                if (!_failureReported)
                {
                    _failureReported = true;
                    _log($"[TreasureHuntManager] Mappy.Overlay.ApiVersion 回 {version}（要 {SupportedApiVersion} 以上）：不在 Mappy 顯示尋寶候選點，每 {ProbeIntervalMs / 1000} 秒重問");
                }

                return false;
            }

            IsAvailable = true;
            _rejectReported = false;
            _log($"[TreasureHuntManager] 找到 Mappy 疊加層（ApiVersion {version}）：開始在 Mappy 顯示尋寶候選點");
            return true;
        }

        // false ＝ 呼叫失敗（已轉成不能用）；Mappy 回 false（拒收）仍算送到，這次連線只記一行。
        public bool SetPoints(string key, uint territoryId, Vector3[] points, uint rgb, int flags, string[] labels, int ttlMs, long now)
        {
            if (!IsAvailable || _setPoints == null)
                return false;

            try
            {
                if (!_setPoints.InvokeFunc(Source, key, territoryId, points, rgb, flags, labels, ttlMs) && !_rejectReported)
                {
                    _rejectReported = true;
                    _log($"[TreasureHuntManager] Mappy 拒收「{key}」（{points.Length} 點，地區 {territoryId}）；Mappy 的 log 有原因，這次連線不再記");
                }

                return true;
            }
            catch (Exception ex)
            {
                Lost(ex, "SetPoints", now);
                return false;
            }
        }

        public bool Remove(string key, long now)
        {
            if (!IsAvailable || _remove == null)
                return false;

            try
            {
                _remove.InvokeFunc(Source, key);
                return true;
            }
            catch (Exception ex)
            {
                Lost(ex, "Remove", now);
                return false;
            }
        }

        public bool ClearSource(long now)
        {
            if (!IsAvailable || _clearSource == null)
                return false;

            try
            {
                _clearSource.InvokeFunc(Source);
                return true;
            }
            catch (Exception ex)
            {
                Lost(ex, "ClearSource", now);
                return false;
            }
        }

        // 呼叫失敗就當 Mappy 不在了，照樣每 ProbeIntervalMs 重問；卸載（IpcNotReadyError）不寫 log，其他只寫第一次。
        private void Lost(Exception ex, string what, long now)
        {
            IsAvailable = false;
            _nextProbeAt = now + ProbeIntervalMs;
            if (ex is not IpcNotReadyError)
                Report(ex, "Mappy.Overlay." + what);
        }

        private void Report(Exception ex, string what)
        {
            if (_failureReported)
                return;

            _failureReported = true;
            _log($"[TreasureHuntManager] 呼叫 {what} 失敗（{ex.GetType().Name}：{ex.Message}）：先不在 Mappy 顯示尋寶候選點，每 {ProbeIntervalMs / 1000} 秒重試；之後的失敗不再記");
        }
    }
}
