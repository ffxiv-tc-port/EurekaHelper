using System.Collections.Generic;
using System.Numerics;
using static EurekaHelper.XIV.MutationWindow;

namespace EurekaHelper.XIV.Zones
{
    // Static location/level/weather-window data for Pyros's "變異怪物" (mutant field mobs) - see
    // MutantMonster's doc comment. Sourced from a community wiki's variant-monster page; column
    // order per row is (FairSkies, HeatWaves, Thunder, Blizzards, UmbralWind, Snow), matching
    // EurekaPyros.Weathers. "虛無炎龍" has a null Position - the wiki lists its location as
    // "地圖內各關隘處" (roams multiple checkpoints across the zone) rather than a fixed
    // coordinate.
    public static class EurekaPyrosMutants
    {
        public static readonly List<MutantMonster> Monsters = new()
        {
            MutantMonster.ForPyros(30, "徒步花苗", 795, 484, new Vector2(17.4f, 27.5f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(35, "搏鬥魔石精", 795, 484, new Vector2(16.5f, 26.3f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPyros(36, "瓦爾寒冰陷阱草", 795, 484, new Vector2(14.8f, 27.1f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(36, "北境獅鷲", 795, 484, new Vector2(19.0f, 28.0f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPyros(37, "長臂猿", 795, 484, new Vector2(23.7f, 25.6f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(37, "湧火蛞蝓", 795, 484, new Vector2(14.4f, 28.3f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(38, "卡魔人", 795, 484, new Vector2(24.6f, 24.7f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(38, "食葉蟲", 795, 484, new Vector2(12.5f, 28.7f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPyros(39, "虛無巨蠅", 795, 484, new Vector2(26.7f, 25.4f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(39, "湧火格雷姆林", 795, 484, new Vector2(12.0f, 30.0f), MutationOutcome.Mutated, None, Day, None, None, None, None),
            MutantMonster.ForPyros(40, "自走人偶守護者", 795, 484, new Vector2(29.2f, 27.7f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(40, "巨螺蝓", 795, 484, new Vector2(30.5f, 28.6f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPyros(40, "迷途哈奧卡", 795, 484, new Vector2(28.6f, 30.7f), MutationOutcome.Mutated, None, None, None, None, Any, None),
            MutantMonster.ForPyros(41, "湧火海石龜", 795, 484, new Vector2(31.3f, 29.4f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(41, "火焰之翼", 795, 484, new Vector2(24.5f, 34.3f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPyros(41, "滴水石像鬼", 795, 484, new Vector2(25.0f, 35.0f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(42, "瓦爾長毛象", 795, 484, new Vector2(29.0f, 33.0f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(42, "海棲馬", 795, 484, new Vector2(25.8f, 36.6f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPyros(42, "瓦爾幽靈", 795, 484, new Vector2(26.0f, 36.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(43, "短劍劍齒虎", 795, 484, new Vector2(22.5f, 36.0f), MutationOutcome.Mutated, None, None, None, Any, None, None),
            MutantMonster.ForPyros(43, "湧火巨水蛇", 795, 484, new Vector2(19.6f, 34.0f), MutationOutcome.Adapted, Any, None, None, None, None, None),
            MutantMonster.ForPyros(43, "餘燼元精", 795, 484, new Vector2(18.6f, 31.4f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPyros(44, "暗黑行吟詩人", 795, 484, new Vector2(19.0f, 31.0f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(44, "瓦爾雪人", 795, 484, new Vector2(21.0f, 31.5f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(44, "赤目墨水瓶", 795, 484, new Vector2(18.6f, 31.4f), MutationOutcome.Mutated, None, None, None, None, Any, None),
            MutantMonster.ForPyros(45, "鋒螯陸蟹", 795, 484, new Vector2(24.3f, 16.9f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPyros(45, "湧火龍蝦", 795, 484, new Vector2(24.3f, 16.9f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(45, "北境鰩", 795, 484, new Vector2(12.0f, 17.5f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPyros(46, "湧火爆彈魚", 795, 484, new Vector2(10.0f, 17.0f), MutationOutcome.Adapted, None, Any, None, None, None, None),
            MutantMonster.ForPyros(46, "湧火軟糊怪", 795, 484, new Vector2(11.0f, 15.6f), MutationOutcome.Mutated, None, None, None, Any, None, None),
            MutantMonster.ForPyros(46, "雷暴元精", 795, 484, new Vector2(12.0f, 17.5f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPyros(47, "湧火帕爾忒諾珀", 795, 484, new Vector2(17.0f, 10.3f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(48, "湧火南加", 795, 484, new Vector2(12.0f, 15.0f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(47, "湧火狼獾", 795, 484, new Vector2(14.0f, 11.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(48, "丁格犬", 795, 484, new Vector2(15.0f, 9.0f), MutationOutcome.Mutated, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPyros(48, "純白", 795, 484, new Vector2(7.0f, 24.7f), MutationOutcome.Adapted, None, None, None, None, Any, None),
            MutantMonster.ForPyros(49, "湧火黏液怪", 795, 484, new Vector2(16.2f, 7.5f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(49, "北境灰熊", 795, 484, new Vector2(17.0f, 10.0f), MutationOutcome.Mutated, Any, None, None, None, None, None),
            MutantMonster.ForPyros(50, "暗黑帕爾忒諾珀", 795, 484, new Vector2(17.0f, 10.0f), MutationOutcome.Adapted, None, None, Night, None, None, None),
            MutantMonster.ForPyros(50, "湧火鷹蜂", 795, 484, new Vector2(25.2f, 12.3f), MutationOutcome.Mutated, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(50, "瓦爾皮拉魚", 795, 484, new Vector2(24.0f, 17.3f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(50, "達菲妮", 795, 484, new Vector2(21.4f, 7.2f), MutationOutcome.Adapted, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(51, "達菲妮", 795, 484, new Vector2(7.0f, 24.7f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(51, "湧火樹木巨像", 795, 484, new Vector2(26.7f, 9.1f), MutationOutcome.Adapted, None, None, None, None, None, Any),
            MutantMonster.ForPyros(51, "湧火天仙子", 795, 484, new Vector2(30.7f, 8.7f), MutationOutcome.Adapted, Day, Day, Day, Day, Day, Day),
            MutantMonster.ForPyros(52, "瓦爾犀鳥", 795, 484, new Vector2(28.0f, 18.0f), MutationOutcome.Mutated, None, Any, None, None, None, None),
            MutantMonster.ForPyros(53, "瓦爾彌諾陶洛斯", 795, 484, new Vector2(7.0f, 24.7f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPyros(52, "雪暴元精", 795, 484, new Vector2(27.0f, 18.0f), MutationOutcome.Adapted, None, None, None, Any, None, Any),
            MutantMonster.ForPyros(53, "無魂代理人", 795, 484, new Vector2(35.0f, 15.0f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(53, "複製僧伽", 795, 484, new Vector2(30.0f, 17.4f), MutationOutcome.Mutated, Night, Night, Night, Night, Night, Night),
            MutantMonster.ForPyros(53, "劍角龍", 795, 484, new Vector2(36.5f, 14.5f), MutationOutcome.Adapted, Any, None, None, None, None, None),
            MutantMonster.ForPyros(54, "雷暴元精", 795, 484, new Vector2(35.0f, 16.0f), MutationOutcome.Mutated, None, None, Any, None, None, None),
            MutantMonster.ForPyros(54, "瘤牛", 795, 484, new Vector2(35.0f, 16.0f), MutationOutcome.Mutated, None, None, None, None, None, Any),
            MutantMonster.ForPyros(54, "虛無遠古之龍", 795, 484, new Vector2(38.0f, 14.6f), MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
            MutantMonster.ForPyros(55, "颱風元精", 795, 484, new Vector2(15.9f, 36.4f), MutationOutcome.Adapted, None, None, None, None, Any, None),
            MutantMonster.ForPyros(55, "瓦爾巨猿", 795, 484, new Vector2(35.6f, 18.5f), MutationOutcome.Mutated, None, None, None, None, Any, None),
            MutantMonster.ForPyros(55, "無魂尋路人", 795, 484, new Vector2(37.0f, 14.4f), MutationOutcome.Mutated, None, None, Night, None, None, None),
            MutantMonster.ForPyros(55, "虛無炎龍", 795, 484, null, MutationOutcome.Adapted, Any, Any, Any, Any, Any, Any),
        };
    }
}
