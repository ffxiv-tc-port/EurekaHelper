using System;
using System.Numerics;
using Dalamud.Plugin.Ipc.Exceptions;

namespace EurekaHelper.System
{
    // 本檔契約：只在框架執行緒呼叫（狀態欄位不加鎖）；端點全部以委派注入，本身不碰 DalamudApi。
    // 帶擁有者端點可用就用它送 3D 自動走向，清除／卸載時 Path.StopIfOwner 只停本插件送的那一筆（含排隊、算路中）；
    // 擲 IpcError（舊版 vnavmesh、端點沒註冊、型別對不上）就退回舊端點，RetryAfterMs 內不再試，退回時寫一行 Information（每次載入一次）。
    // 停的方式跟最後一次送的方式一致：舊端點或 /vnav moveflag 送的路沒有擁有者，StopIfOwner 停不到，回 Legacy 讓呼叫端走原本的判斷。
    internal sealed class VnavOwnedRouting
    {
        internal const string Owner = "EurekaHelper";
        internal const long RetryAfterMs = 30_000;

        internal enum StopKind
        {
            OwnedStopped,
            OwnedNothing,
            Legacy,
        }

        private readonly Func<Vector3, bool, string, bool> pathfindAndMoveToOwned;
        private readonly Func<Vector3, bool, bool> pathfindAndMoveToLegacy;
        private readonly Func<string, bool> stopIfOwner;
        private readonly Func<bool> providerLoaded;
        private readonly Action<string> logInfo;
        private readonly Func<long> nowMs;

        private bool ownedUnavailable;
        private long retryAtMs;
        private bool lastSendLegacy;
        private bool fallbackLogged;

        internal VnavOwnedRouting(
            Func<Vector3, bool, string, bool> pathfindAndMoveToOwned,
            Func<Vector3, bool, bool> pathfindAndMoveToLegacy,
            Func<string, bool> stopIfOwner,
            Func<bool> providerLoaded,
            Action<string> logInfo,
            Func<long> nowMs)
        {
            this.pathfindAndMoveToOwned = pathfindAndMoveToOwned;
            this.pathfindAndMoveToLegacy = pathfindAndMoveToLegacy;
            this.stopIfOwner = stopIfOwner;
            this.providerLoaded = providerLoaded;
            this.logInfo = logInfo;
            this.nowMs = nowMs;
        }

        internal bool OwnedUnavailable => ownedUnavailable;
        internal bool LastSendLegacy => lastSendLegacy;

        /// <summary>SimpleMove.PathfindAndMoveTo（帶擁有者版優先）。舊端點擲的例外與提供端自己擲的例外（非 IpcError）照樣往上冒。</summary>
        internal bool PathfindAndMoveTo(Vector3 dest, bool fly)
        {
            if (TryOwned())
            {
                try
                {
                    var started = pathfindAndMoveToOwned(dest, fly, Owner);
                    lastSendLegacy = false;
                    return started;
                }
                catch (IpcError ex)
                {
                    MarkUnavailable("SimpleMove.PathfindAndMoveToOwned", ex);
                }
            }

            lastSendLegacy = true;
            return pathfindAndMoveToLegacy(dest, fly);
        }

        /// <summary>本插件改用沒有擁有者的方式送了路（/vnav moveflag）：之後的停止回 Legacy。</summary>
        internal void NoteUnownedSend() => lastSendLegacy = true;

        /// <summary>停本插件最後一次送的路。回 Legacy＝停不到（沒有擁有者或新端點失效），呼叫端照原本的判斷停。</summary>
        internal StopKind StopOwn()
        {
            if (!lastSendLegacy && TryOwned())
            {
                try
                {
                    return stopIfOwner(Owner) ? StopKind.OwnedStopped : StopKind.OwnedNothing;
                }
                catch (IpcError ex)
                {
                    MarkUnavailable("Path.StopIfOwner", ex);
                }
            }

            return StopKind.Legacy;
        }

        private bool TryOwned()
        {
            if (!ownedUnavailable)
                return true;
            if (nowMs() < retryAtMs)
                return false;
            ownedUnavailable = false;
            return true;
        }

        private void MarkUnavailable(string endpoint, IpcError ex)
        {
            ownedUnavailable = true;
            retryAtMs = nowMs() + RetryAfterMs;
            // vnavmesh 根本沒載入時舊端點也一樣失敗，這行說的「舊版」不成立，留到它載入後再記。
            if (fallbackLogged || !providerLoaded())
                return;
            fallbackLogged = true;
            logInfo($"[TreasureHuntManager] 這版 vnavmesh 沒有帶擁有者的導航端點（vnavmesh.{endpoint}：{ex.GetType().Name}），尋寶自動走向改用舊端點：按清除時正在算的路不分是誰的都會取消。更新 vnavmesh 後會自動改回只停自己的。（每次載入只記這一行）");
        }
    }
}
