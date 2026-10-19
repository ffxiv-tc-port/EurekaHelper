using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EurekaHelper.System
{
    public class InventoryManager : IDisposable
    {
        private readonly CancellationTokenSource CancellationTokenSource = new();

        private readonly InventoryType[] InventoriesToScan = new[]
        {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
            InventoryType.SaddleBag1,
            InventoryType.SaddleBag2,
            InventoryType.PremiumSaddleBag1,
            InventoryType.PremiumSaddleBag2,
            InventoryType.RetainerPage1,
            InventoryType.RetainerPage2,
            InventoryType.RetainerPage3,
            InventoryType.RetainerPage4,
            InventoryType.RetainerPage5,
            InventoryType.RetainerPage6,
            InventoryType.RetainerPage7,
        };

        public Dictionary<uint, List<InventoryCount>> ScannedItems = new()
        {
            [21801] = new(), // Protean Crystal
            [21802] = new(), // Pazuzu's feather
            [21803] = new(), // Anemos Crystal

            [23309] = new(), // Frosted Protean Crystal
            [22976] = new(), // Pagos Crystal
            [22975] = new(), // Louhi's Ice

            [24124] = new(), // Pyros Crystal
            [24123] = new(), // Penthesilea's Flame

            [24807] = new(), // Hydatos Crystal
            [24806] = new(), // Crystalline Scale
            [24808] = new(), // Eureka Fragment
        };

        public InventoryManager()
        {
            DalamudApi.Framework.RunOnTick(ScanInventories, TimeSpan.FromMilliseconds(500), cancellationToken: CancellationTokenSource.Token);
        }

        private unsafe void ScanInventories()
        {
            try
            {
                var inventoryManger = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();

                // 每半秒跑一次、不分場合：這裡不用 LINQ 與 lambda（每格一個閉包、每個已載入的格子兩個委派）。
                // 走訪途中只改清單內容、不動字典本身，所以直接列舉字典。
                foreach (var item in ScannedItems)
                {
                    var counts = item.Value;
                    foreach (var inventoryType in InventoriesToScan)
                    {
                        var inventory = inventoryManger->GetInventoryContainer(inventoryType);
                        if (inventory == null)
                            continue;

                        if (inventory->IsLoaded == false)
                            continue;

                        var totalCount = inventoryManger->GetItemCountInContainer(item.Key, inventoryType);
                        StoreCount(counts, inventoryType, totalCount);
                    }
                }
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, "Failed to scan for items.");
            }
            finally
            {
                DalamudApi.Framework.RunOnTick(ScanInventories, TimeSpan.FromMilliseconds(500), cancellationToken: CancellationTokenSource.Token);
            }
        }

        // 每種容器在清單裡至多一筆（只有這裡會加）：有就改數量，沒有才加在最後面。
        private static void StoreCount(List<InventoryCount> counts, InventoryType inventoryType, int count)
        {
            for (var i = 0; i < counts.Count; i++)
            {
                if (counts[i].InventoryType != inventoryType)
                    continue;

                counts[i].Count = count;
                return;
            }

            counts.Add(new(inventoryType, count));
        }

        public void Dispose()
        {
            CancellationTokenSource.Cancel();
        }
    }

    public class InventoryCount
    {
        public InventoryType InventoryType { get; set; }
        public int Count { get; set; }

        public InventoryCount(InventoryType inventoryType, int count)
        {
            InventoryType = inventoryType;
            Count = count;
        }
    }
}
