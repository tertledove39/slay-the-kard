using System.Collections.Generic;

public static class HistoricalBattleNames
{
    private static readonly Dictionary<string, string> Names = new()
    {
        { "brest_fortress_assault", "布列斯特要塞突击" }, { "raseiniai_armored_clash", "拉塞尼艾装甲遭遇战" },
        { "bialystok_minsk_encirclement", "比亚韦斯托克-明斯克合围" }, { "smolensk_encirclement_1941", "斯摩棱斯克合围战" },
        { "luga_line_breakthrough", "卢加防线突破战" }, { "uman_pocket", "乌曼合围战" },
        { "kiev_encirclement_1941", "基辅合围战" }, { "leningrad_blockade_begins", "列宁格勒封锁" },
        { "sea_of_azov_pocket", "亚速海合围战" }, { "vyazma_pocket", "维亚济马合围战" },
        { "bryansk_pocket", "布良斯克合围战" }, { "moscow_clin_offensive", "克林方向攻势" },
        { "rzhev_winter_defense", "勒热夫冬季防御" }, { "lyuban_encirclement", "柳班合围战" },
        { "kerch_bustard_hunt", "刻赤猎鸨行动" }, { "second_kharkov_counterstroke", "第二次哈尔科夫反击" },
        { "sevastopol_final_assault", "塞瓦斯托波尔总攻" }, { "case_blue_voronezh", "蓝色方案：沃罗涅日" },
        { "don_bend_advance", "顿河大弯曲部战斗" }, { "caucasus_eidelweiss", "高加索雪绒花行动" },
        { "kalach_bridgehead", "卡拉奇桥头堡" }, { "stalingrad_city_center", "斯大林格勒市中心" },
        { "stalingrad_factory_district", "斯大林格勒工厂区" }, { "winter_storm_relief", "冬季风暴行动" },
        { "third_kharkov_counteroffensive", "第三次哈尔科夫反攻" }, { "kursk_northern_pincer", "库尔斯克北翼钳攻" },
        { "kursk_southern_pincer", "库尔斯克南翼钳攻" }, { "prokhorovka_armored_battle", "普罗霍罗夫卡装甲战" },
        { "mius_front_defense", "米乌斯河防线" }, { "belgorod_kharkov_retreat", "别尔哥罗德-哈尔科夫撤退" },
        { "dnieper_east_wall", "第聂伯河东方壁垒" }, { "bukrin_bridgehead_counterattack", "布克林桥头堡反击" },
        { "zhitomir_counterattack", "日托米尔反击战" }, { "korsun_pocket_relief", "科尔孙解围行动" },
        { "nikopol_bridgehead", "尼科波尔桥头堡" }, { "kamenets_podolsky_breakout", "卡缅涅茨-波多利斯基突围" },
        { "vitebsk_fortress", "维捷布斯克要塞" }, { "bobruisk_pocket", "博布鲁伊斯克合围战" },
        { "minsk_pocket_1944", "明斯克合围战" }, { "brody_pocket", "布罗德合围战" },
        { "warsaw_praga_counterattack", "华沙-布拉格反击" }, { "jassy_kishinev_defense", "雅西-基什尼奥夫防御" },
        { "baltic_riga_corridor", "波罗的海里加走廊" }, { "gumbinnen_counterattack", "贡宾嫩反击战" },
        { "courland_pocket", "库尔兰包围圈" }, { "vistula_oder_baranuv", "维斯瓦-奥得：巴拉努夫" },
        { "konigsberg_fortress", "柯尼斯堡要塞" }, { "operation_solstice", "日光行动" },
        { "seelow_heights", "泽洛高地" }, { "berlin_final_battle", "柏林最终战" },
    };

    public static string Get(string id) => Names.TryGetValue(id, out var name) ? name : id;
}
