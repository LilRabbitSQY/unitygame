using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinalDefense.Combat;

internal static class CombatDataTests
{
    private static readonly string[] Names =
    {
        "卷王驾到", "开源学霸", "历年真题", "通宵王者", "千手观音", "蕉绿贩卖机", "菜菜捞捞", "美式加茶",
        "退课申请", "季老心态", "摸摸咸鱼", "热带风味", "高考战魂", "赛博外援", "宝藏网课", "红榜讲师"
    };

    internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

    private sealed class SourceUnit
    {
        internal string name;
        internal readonly Dictionary<string, string[]> fields = new Dictionary<string, string[]>();
        internal readonly Dictionary<string, string> skill = new Dictionary<string, string>();
    }

    internal static UnitDefinition[] LoadUnits(string fileName)
    {
        var files = Directory.GetFiles("Assets", fileName + ".json", SearchOption.AllDirectories)
            .Where(path => path.Replace('\\', '/').Contains("/Resources/Battle/")).ToArray();
        BattleLogicTests.Equal(1, files.Length, "Exactly one production Resources/Battle/" + fileName + ".json");
        using var document = JsonDocument.Parse(File.ReadAllText(files[0]));
        var element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Object)
        {
            var arrays = element.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.Array).ToArray();
            BattleLogicTests.Equal(1, arrays.Length, "Data wrapper contains one unit array");
            element = arrays[0].Value;
        }
        return JsonSerializer.Deserialize<UnitDefinition[]>(element.GetRawText(), JsonOptions);
    }

    private static List<SourceUnit> ReadSource(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText("Docs/Battle/Source/" + name + ".json"));
        var units = new List<SourceUnit>();
        SourceUnit current = null;
        foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
        {
            var cells = row.GetProperty("cells").EnumerateArray().ToDictionary(
                cell => cell.GetProperty("column").GetInt32(), cell => cell.GetProperty("text").GetString().Trim());
            cells.TryGetValue(3, out string label);
            if (Names.Contains(label))
            {
                current = new SourceUnit { name = label };
                units.Add(current);
            }
            if (current == null) continue;
            if (label != null && cells.ContainsKey(4))
                current.fields[label] = new[] { cells[4], cells.GetValueOrDefault(5, ""), cells.GetValueOrDefault(6, "") };
            if (cells.TryGetValue(8, out string skillLabel) && cells.TryGetValue(9, out string value))
                current.skill[skillLabel] = value;
        }
        return units;
    }

    private static float Number(string text)
    {
        var match = Regex.Match(text, @"^-?\d+(?:\.\d+)?");
        return match.Success ? float.Parse(match.Value, CultureInfo.InvariantCulture) : 0;
    }

    private static void ArrayValues(string label, int[] actual, SourceUnit source, bool tower)
    {
        int levels = tower ? 3 : 1;
        BattleLogicTests.True(actual != null && actual.Length >= levels, source.name + " has " + levels + " authored " + label + " values");
        for (int index = 0; index < levels; index++)
            BattleLogicTests.Equal((int)Number(source.fields[label][index]), actual[index], source.name + " " + label + " level " + (index + 1));
    }

    private static void VerifyUnit(UnitDefinition actual, SourceUnit source, bool tower)
    {
        BattleLogicTests.True(!string.IsNullOrWhiteSpace(actual.id), source.name + " stable id exists");
        ArrayValues("生命值", actual.hp, source, tower);
        ArrayValues("攻击力", actual.attack, source, tower);
        ArrayValues("防御力", actual.defense, source, tower);
        var frames = source.fields["攻击速度"][0];
        if (Regex.IsMatch(frames, @"^\d"))
            BattleLogicTests.Equal((int)Number(frames), actual.attackFrames, source.name + " attack frames");
        BattleLogicTests.True(actual.projectile == (source.fields["攻击弹道"][0] == "有"), source.name + " projectile flag");
        BattleLogicTests.True(actual.skill != null, source.name + " has a skill definition");
        BattleLogicTests.True(!string.IsNullOrWhiteSpace(actual.skill.id), source.name + " skill id exists");
        BattleLogicTests.True(actual.skill.name == source.skill["技能名"], source.name + " authored skill name");
        if (tower)
        {
            BattleLogicTests.Equal((int)Number(source.fields["部署COST"][0]), actual.deployCost, source.name + " deployment cost");
            BattleLogicTests.True(actual.highGround == (source.fields["部署位置"][0] == "高台"), source.name + " deployment terrain");
            BattleLogicTests.Equal((int)Number(source.fields["阻挡数"][0]), actual.block, source.name + " blocking capacity");
            BattleLogicTests.Near(Number(source.skill["初始技力"]), actual.skill.initialSp, source.name + " initial SP");
            BattleLogicTests.Near(Number(source.skill["所需技力"]), actual.skill.spCost, source.name + " required SP");
            BattleLogicTests.Near(Number(source.skill["持续时间"]), actual.skill.duration, source.name + " duration (instant skills = 0)");
            ArrayValues("升级COST", actual.upgradeCost, source, true);
        }
        else
        {
            BattleLogicTests.Equal((int)Number(source.fields["攻入保护点削减点数"][0]), actual.goalDamage, source.name + " protection damage");
            BattleLogicTests.Near(Number(source.fields["移动速度"][0]), actual.moveSpeed, source.name + " grid cells per second");
            BattleLogicTests.Near(Number(source.skill["施放间隔"]) / 30, actual.skill.interval, source.name + " skill frames converted to seconds");
        }
    }

    internal static void Run()
    {
        foreach (bool tower in new[] { true, false })
        {
            string kind = tower ? "Towers" : "Enemies";
            UnitDefinition[] actual = null;
            List<SourceUnit> expected = null;
            BattleLogicTests.Run(kind + " source and production catalog contain 16 unique units", () =>
            {
                actual = LoadUnits(kind);
                expected = ReadSource(tower ? "04-towers" : "05-enemies");
                BattleLogicTests.Equal(16, expected.Count, "Exported source unit count");
                BattleLogicTests.Equal(16, actual.Length, "Production unit count");
                BattleLogicTests.Equal(16, actual.Select(unit => unit.id).Distinct().Count(), "Unique unit IDs");
                BattleLogicTests.Equal(16, actual.Select(unit => unit.skill.id).Distinct().Count(), "Unique skill IDs");
            });
            if (actual == null || expected == null) continue;
            foreach (var source in expected)
                BattleLogicTests.Run(kind + " exact source values: " + source.name, () =>
                {
                    var matches = actual.Where(unit => unit.name == source.name).ToArray();
                    BattleLogicTests.Equal(1, matches.Length, "Exactly one unit matches authored name");
                    VerifyUnit(matches[0], source, tower);
                });
        }
    }
}
