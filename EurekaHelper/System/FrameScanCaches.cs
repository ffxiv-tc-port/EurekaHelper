using System;
using System.Collections.Generic;

namespace EurekaHelper.System
{
    // 優雷卡每幀掃描用的兩個快取。只在框架執行緒使用；只存數字 ID 與位元組副本，不存任何物件包裝或原生指標。

    // 怪物名字：鍵是 NameId，命中還要求原始名字位元組完全相同 ⇒ 不依賴「同 NameId 必同名」，位元組一變就重新解析。
    internal sealed class MonsterNameCache
    {
        private readonly Dictionary<uint, (byte[] Raw, string Name)> _byNameId = new();

        public int Count => _byNameId.Count;

        // 與 SeString.Parse(ReadOnlySpan<byte>) 相同的截斷：只看第一個 NUL 之前。
        public static ReadOnlySpan<byte> TrimAtNul(ReadOnlySpan<byte> raw)
        {
            var nul = raw.IndexOf((byte)0);
            return nul < 0 ? raw : raw[..nul];
        }

        public bool TryGet(uint nameId, ReadOnlySpan<byte> raw, out string name)
        {
            if (_byNameId.TryGetValue(nameId, out var hit) && raw.SequenceEqual(hit.Raw))
            {
                name = hit.Name;
                return true;
            }
            name = null!;
            return false;
        }

        public string Store(uint nameId, ReadOnlySpan<byte> raw, string name)
        {
            _byNameId[nameId] = (raw.ToArray(), name);
            return name;
        }
    }

    internal enum MutationStatusKind : byte
    {
        Other,
        Adapted,
        Mutated,
    }

    // 狀態分類：StatusId → 分類。同一個 StatusId 的名字來自同一列遊戲資料，所以第一次見到才取名字比對。
    internal sealed class StatusKindCache
    {
        private readonly Dictionary<uint, MutationStatusKind> _byStatusId = new();

        public int Count => _byStatusId.Count;

        public bool TryGet(uint statusId, out MutationStatusKind kind) => _byStatusId.TryGetValue(statusId, out kind);

        public MutationStatusKind Store(uint statusId, MutationStatusKind kind)
        {
            _byStatusId[statusId] = kind;
            return kind;
        }
    }
}
