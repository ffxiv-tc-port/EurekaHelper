using System.Collections.Generic;
using System.Numerics;
using static EurekaHelper.XIV.MutationWindow;

namespace EurekaHelper.XIV.Zones
{
    // Static location/level/weather-window data for Hydatos's "變異怪物" (mutant field mobs) -
    // see MutantMonster's doc comment. Sourced from a community wiki's variant-monster page;
    // column order per row is (FairSkies, Showers, Gloom, Thunderstorms, Snow), matching
    // EurekaHydatos.Weathers.
    public static class EurekaHydatosMutants
    {
        public static readonly List<MutantMonster> Monsters = new()
        {
            MutantMonster.ForHydatos(50, "優雷卡死亡凝視", 827, 515, new Vector2(20.3f, 16.1f), MutationOutcome.Adapted, Any, Any, Any, Any, Any),
            MutantMonster.ForHydatos(50, "瓦爾南加", 827, 515, new Vector2(21.3f, 16.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(51, "豐水軟糊怪", 827, 515, new Vector2(16.5f, 19.5f), MutationOutcome.Adapted, Any, Any, Any, Any, Any),
            MutantMonster.ForHydatos(51, "研究所長須豹", 827, 515, new Vector2(15.9f, 19.7f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(52, "無魂搜尋者", 827, 515, new Vector2(17.0f, 25.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(52, "瓦爾沙蠶", 827, 515, new Vector2(14.5f, 15.8f), MutationOutcome.Mutated, Any, None, None, None, None),
            MutantMonster.ForHydatos(53, "雷暴元精", 827, 515, new Vector2(11.0f, 23.0f), MutationOutcome.Adapted, None, None, None, Any, None),
            MutantMonster.ForHydatos(53, "豐水榴彈怪", 827, 515, new Vector2(26.3f, 22.6f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(54, "築巢公雛鳥", 827, 515, new Vector2(17.4f, 27.5f), MutationOutcome.Adapted, Any, Any, Any, Any, Any),
            MutantMonster.ForHydatos(54, "研究所溝鼠", 827, 515, new Vector2(13.6f, 26.7f), MutationOutcome.Mutated, None, None, None, Any, None),
            MutantMonster.ForHydatos(55, "融雪元精", 827, 515, new Vector2(11.0f, 27.0f), MutationOutcome.Adapted, None, Any, None, None, None),
            MutantMonster.ForHydatos(55, "孤獨象魔", 827, 515, new Vector2(9.0f, 24.0f), MutationOutcome.Mutated, Any, None, None, None, None),
            MutantMonster.ForHydatos(55, "豐水奇納哈爾鳥妖", 827, 515, new Vector2(11.0f, 28.0f), MutationOutcome.Mutated, None, None, None, None, Any),
            MutantMonster.ForHydatos(56, "雪暴元精", 827, 515, new Vector2(11.4f, 21.5f), MutationOutcome.Adapted, None, None, None, None, Any),
            MutantMonster.ForHydatos(56, "瓦爾火尾飛蜥", 827, 515, new Vector2(13.0f, 18.6f), MutationOutcome.Mutated, Any, None, None, None, None),
            MutantMonster.ForHydatos(56, "瓦爾羚羊", 827, 515, new Vector2(16.5f, 26.3f), MutationOutcome.Mutated, None, None, None, None, Any),
            MutantMonster.ForHydatos(57, "艾歐晶片", 827, 515, new Vector2(6.8f, 18.0f), MutationOutcome.Adapted, Any, Any, Any, Any, Any),
            MutantMonster.ForHydatos(57, "湖畔蝦蛄", 827, 515, new Vector2(5.8f, 19.0f), MutationOutcome.Mutated, Day, Day, Day, Day, Day),
            MutantMonster.ForHydatos(57, "暗黑騎手", 827, 515, new Vector2(16.5f, 26.3f), MutationOutcome.Mutated, None, None, None, Night, None),
            MutantMonster.ForHydatos(58, "暗黑騎手", 827, 515, new Vector2(5.4f, 20.1f), MutationOutcome.Adapted, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(58, "生鏽恐慌裝甲", 827, 515, new Vector2(4.0f, 17.0f), MutationOutcome.Mutated, Day, Day, Day, Day, Day),
            MutantMonster.ForHydatos(58, "自走人偶013BL", 827, 515, new Vector2(4.0f, 14.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(59, "豐水幽靈", 827, 515, new Vector2(5.0f, 29.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(59, "修驗天狗", 827, 515, new Vector2(7.3f, 29.8f), MutationOutcome.Mutated, Day, Day, Day, Day, Day),
            MutantMonster.ForHydatos(59, "虛無薇薇爾飛龍", 827, 515, new Vector2(4.0f, 25.4f), MutationOutcome.Mutated, None, Any, None, None, None),
            MutantMonster.ForHydatos(60, "滾滾葉小妖", 827, 515, new Vector2(23.0f, 15.5f), MutationOutcome.Mutated, Day, Day, Day, Day, Day),
            MutantMonster.ForHydatos(60, "雪暴元精", 827, 515, new Vector2(24.8f, 17.5f), MutationOutcome.Adapted, None, None, None, None, Any),
            MutantMonster.ForHydatos(60, "瓦爾螳螂", 827, 515, new Vector2(24.8f, 17.5f), MutationOutcome.Mutated, None, Any, None, None, None),
            MutantMonster.ForHydatos(61, "豐水爆殼怪", 827, 515, new Vector2(26.5f, 20.7f), MutationOutcome.Adapted, Any, Any, Any, Any, Any),
            MutantMonster.ForHydatos(61, "瓦爾鼴鼠", 827, 515, new Vector2(29.0f, 15.4f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(61, "雷暴元精", 827, 515, new Vector2(26.5f, 20.3f), MutationOutcome.Mutated, None, None, None, Any, None),
            MutantMonster.ForHydatos(62, "武士腐屍", 827, 515, new Vector2(37.0f, 28.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(62, "未知食人魔", 827, 515, new Vector2(29.0f, 26.0f), MutationOutcome.Mutated, Any, None, None, None, None),
            MutantMonster.ForHydatos(62, "豐水巨猿", 827, 515, new Vector2(34.3f, 27.2f), MutationOutcome.Mutated, Day, Day, Day, Day, Day),
            MutantMonster.ForHydatos(63, "築巢祖", 827, 515, new Vector2(35.7f, 21.3f), MutationOutcome.Adapted, Any, Any, Any, Any, Any),
            MutantMonster.ForHydatos(63, "研究所黑豺", 827, 515, new Vector2(34.8f, 21.2f), MutationOutcome.Mutated, None, None, None, Any, None),
            MutantMonster.ForHydatos(63, "融雪元精", 827, 515, new Vector2(35.0f, 18.0f), MutationOutcome.Mutated, None, Any, None, None, None),
            MutantMonster.ForHydatos(64, "雪暴元精", 827, 515, new Vector2(33.0f, 15.0f), MutationOutcome.Adapted, None, None, None, None, Any),
            MutantMonster.ForHydatos(64, "豐水毒蜥蜴", 827, 515, new Vector2(33.0f, 15.0f), MutationOutcome.Mutated, Day, Day, Day, Day, Day),
            MutantMonster.ForHydatos(64, "暗黑石像鬼", 827, 515, new Vector2(35.0f, 15.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(65, "武士腐屍", 827, 515, new Vector2(30.0f, 21.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night),
            MutantMonster.ForHydatos(65, "豐水瘤牛", 827, 515, new Vector2(34.5f, 18.5f), MutationOutcome.Mutated, None, None, None, None, Any),
            MutantMonster.ForHydatos(65, "虛無雙足飛龍", 827, 515, new Vector2(31.5f, 18.5f), MutationOutcome.Mutated, Night, Night, Night, Night, Night),
        };
    }
}
