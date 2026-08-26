using System.Collections.Generic;
using System.Numerics;
using static EurekaHelper.XIV.MutationWindow;

namespace EurekaHelper.XIV.Zones
{
    // Static location/level/weather-window data for Pagos's "變異怪物" (mutant field mobs) - see
    // MutantMonster's doc comment. Sourced from a community wiki's variant-monster page; column
    // order per row is (FairSkies, Fog, HeatWaves, Thunder, Snow, Blizzards), matching
    // EurekaPagos.Weathers (in a different column order than the array itself, but same weather
    // set). "虛無冰雪龍" has a null Position - the wiki lists its location as "地圖內各關隘處"
    // (roams multiple checkpoints across the zone) rather than a fixed coordinate.
    public static class EurekaPagosMutants
    {
        public static readonly List<MutantMonster> Monsters = new()
        {
            MutantMonster.ForPagos(20, "野樹靈", 763, 467, new Vector2(7.0f, 24.7f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(20, "恆冰深瞳", 763, 467, new Vector2(8.0f, 25.8f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(21, "瓦爾嬰猴", 763, 467, new Vector2(12.6f, 23.4f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPagos(21, "北境浮蝶", 763, 467, new Vector2(13.0f, 27.5f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(22, "雪地蛞蝓", 763, 467, new Vector2(16.0f, 27.4f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(22, "瓦爾禍蛛蠍", 763, 467, new Vector2(14.0f, 22.4f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(23, "僵屍布羅賓雅克", 763, 467, new Vector2(8.0f, 24.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(23, "瓦爾鼴鼠", 763, 467, new Vector2(15.7f, 28.7f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPagos(24, "雪地海月水母", 763, 467, new Vector2(17.2f, 29.8f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(24, "恆冰白狼", 763, 467, new Vector2(17.6f, 25.8f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(25, "珍卷惡魔", 763, 467, new Vector2(19.5f, 26.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(25, "恆冰跳蜥", 763, 467, new Vector2(22.0f, 26.4f), MutationOutcome.Mutated, None, None, None, Any, None, None),
            MutantMonster.ForPagos(25, "雷暴元精", 763, 467, new Vector2(21.0f, 29.0f), MutationOutcome.Mutated, None, None, None, Any, None, None),
            MutantMonster.ForPagos(26, "瓦爾螳螂", 763, 467, new Vector2(25.0f, 27.0f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(26, "融雪元精", 763, 467, new Vector2(26.0f, 26.1f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPagos(26, "北境蜂鳥", 763, 467, new Vector2(26.4f, 29.2f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPagos(27, "恆冰巨熊", 763, 467, new Vector2(28.2f, 26.0f), MutationOutcome.Mutated, None, None, None, None, Any, Any),
            MutantMonster.ForPagos(27, "冰霜明膠怪", 763, 467, new Vector2(27.7f, 29.2f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(27, "餘燼元精", 763, 467, new Vector2(28.0f, 27.0f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPagos(28, "雪暴元精", 763, 467, new Vector2(29.7f, 26.6f), MutationOutcome.Adapted, None, None, None, None, Any, Any),
            MutantMonster.ForPagos(28, "優雷卡風巨魔", 763, 467, new Vector2(32.0f, 29.4f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPagos(28, "珊瑚烏菊石", 763, 467, new Vector2(29.7f, 26.6f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(29, "虛無雙足飛龍", 763, 467, new Vector2(33.3f, 23.3f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(29, "恆冰牛羚", 763, 467, new Vector2(32.0f, 24.0f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(29, "餘光閃爍瑪塔蛇頸龜", 763, 467, new Vector2(31.2f, 25.2f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPagos(30, "死魂", 763, 467, new Vector2(31.0f, 25.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(30, "恆冰巨鱷", 763, 467, new Vector2(31.0f, 21.5f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(30, "寒冰鏡騎士", 763, 467, new Vector2(29.0f, 25.0f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPagos(31, "凋零山克芹尼", 763, 467, new Vector2(16.3f, 21.4f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPagos(31, "瓦爾守護者", 763, 467, new Vector2(17.0f, 18.4f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(31, "餘燼元精", 763, 467, new Vector2(17.0f, 18.4f), MutationOutcome.Adapted, None, None, Any, None, None, None),
            MutantMonster.ForPagos(32, "迪戈泰塔斯", 763, 467, new Vector2(11.5f, 14.0f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPagos(32, "恆冰駿鵰", 763, 467, new Vector2(11.2f, 14.1f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(32, "雪暴元精", 763, 467, new Vector2(10.0f, 13.1f), MutationOutcome.Mutated, None, None, None, None, Any, Any),
            MutantMonster.ForPagos(33, "雷暴元精", 763, 467, new Vector2(10.0f, 13.4f), MutationOutcome.Mutated, None, None, None, Any, None, None),
            MutantMonster.ForPagos(33, "瓦爾守衛", 763, 467, new Vector2(10.0f, 13.4f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(33, "恆冰半人馬", 763, 467, new Vector2(71.0f, 17.0f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPagos(34, "鬼靈", 763, 467, new Vector2(9.0f, 16.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(34, "烏洛里石守衛", 763, 467, new Vector2(10.6f, 20.6f), MutationOutcome.Mutated, None, None, None, None, Any, Any),
            MutantMonster.ForPagos(34, "冰霜龍鳥", 763, 467, new Vector2(6.0f, 14.2f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPagos(35, "山谷曼提克", 763, 467, new Vector2(6.0f, 17.7f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPagos(35, "瓦爾屍生花", 763, 467, new Vector2(7.5f, 17.7f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPagos(35, "餘燼元精", 763, 467, new Vector2(7.5f, 17.7f), MutationOutcome.Adapted, None, None, Any, None, None, None),
            MutantMonster.ForPagos(36, "暴雪古菩猩猩", 763, 467, new Vector2(23.0f, 18.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(36, "塵世巨蟒", 763, 467, new Vector2(19.5f, 20.0f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPagos(36, "瓦爾腐屍", 763, 467, new Vector2(34.6f, 18.5f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(37, "雷暴元精", 763, 467, new Vector2(29.0f, 19.0f), MutationOutcome.Adapted, None, None, None, Any, None, None),
            MutantMonster.ForPagos(37, "風暴鰩", 763, 467, new Vector2(29.0f, 19.0f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPagos(37, "虛無希里科塔", 763, 467, new Vector2(30.0f, 16.0f), MutationOutcome.Mutated, None, None, None, None, None, Any),
            MutantMonster.ForPagos(38, "融雪元精", 763, 467, new Vector2(23.0f, 17.0f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPagos(38, "瓦爾尤彌爾", 763, 467, new Vector2(25.0f, 24.0f), MutationOutcome.Mutated, None, Day, None, None, None, None),
            MutantMonster.ForPagos(38, "恆冰阿努比斯", 763, 467, new Vector2(32.0f, 20.0f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPagos(39, "餘燼元精", 763, 467, new Vector2(26.0f, 20.0f), MutationOutcome.Adapted, None, None, Any, None, None, None),
            MutantMonster.ForPagos(39, "大安菲瑟龍", 763, 467, new Vector2(26.0f, 20.0f), MutationOutcome.Mutated, None, None, None, Any, None, None),
            MutantMonster.ForPagos(39, "脫逃暴龍", 763, 467, new Vector2(36.0f, 16.7f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPagos(40, "瓦爾獅鷲", 763, 467, new Vector2(22.0f, 13.0f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPagos(40, "恆冰奇美拉", 763, 467, new Vector2(35.5f, 14.7f), MutationOutcome.Mutated, None, None, None, None, None, Any),
            MutantMonster.ForPagos(40, "雪暴元精", 763, 467, new Vector2(36.0f, 16.7f), MutationOutcome.Adapted, None, None, None, None, Any, Any),
            MutantMonster.ForPagos(40, "虛無冰雪龍", 763, 467, null, MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
        };
    }
}
