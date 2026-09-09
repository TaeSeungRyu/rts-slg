namespace SanguoSLG.Core.Tests.Data;

using System.Text.Json.Nodes;
using SanguoSLG.Core.Data;
using Xunit;

public class GeneralEditorStoreTests
{
    [Fact]
    public void ReplaceGeneral_편집대상밖의필드를_보존한다()
    {
        const string json = """
        [
          {
            "id": 1,
            "name": "조조",
            "unknown_future_field": "keep",
            "aptitudes": { "infantry": "S", "archer": "A", "cavalry": "S", "elephant": "C", "siege": "B", "naval": "C" },
            "might": 72,
            "intellect": 91,
            "politics": 94,
            "battle_active": "peerless",
            "battle_passives": [ { "code": "momentum", "tier": 2 } ],
            "admin_passives": [ { "code": "tuntian", "tier": 3 } ],
            "birth": 155,
            "unlock_year": 190,
            "region": "yuzhou",
            "desc": "desc"
          }
        ]
        """;
        var edited = GeneralEditorStore.LoadGenerals(json).Single() with
        {
            Might = 80,
            BattleActive = null,
        };

        var saved = GeneralEditorStore.ReplaceGeneral(json, edited);
        var node = JsonNode.Parse(saved)![0]!;

        Assert.Equal(1, node["id"]!.GetValue<int>());
        Assert.Equal("조조", node["name"]!.GetValue<string>());
        Assert.Equal("keep", node["unknown_future_field"]!.GetValue<string>());
        Assert.Equal(80, node["might"]!.GetValue<int>());
        Assert.Null(node["battle_active"]);
    }

    [Fact]
    public void Validate_스킬과범위를_검증한다()
    {
        var record = new GeneralEditorRecord(
            1,
            "테스트",
            new()
            {
                ["infantry"] = "S",
                ["archer"] = "A",
                ["cavalry"] = "B",
                ["elephant"] = "C",
                ["siege"] = "D",
                ["naval"] = "F",
            },
            101,
            50,
            60,
            "missing",
            [new GeneralEditorSkill("dup", 1), new GeneralEditorSkill("dup", 2)],
            [new GeneralEditorSkill("admin", 4)],
            0,
            0,
            "yuzhou",
            "desc");

        var result = GeneralEditorStore.Validate([record], new HashSet<string>(), new HashSet<string> { "dup" }, new HashSet<string> { "admin" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("무력"));
        Assert.Contains(result.Errors, e => e.Contains("미등록 전투 액티브"));
        Assert.Contains(result.Errors, e => e.Contains("중복 코드"));
        Assert.Contains(result.Errors, e => e.Contains("티어"));
    }

    [Fact]
    public void ValidatePortraits_초상메타데이터를_검증한다()
    {
        var result = GeneralEditorStore.ValidatePortraits(
            [new GeneralPortraitRecord(1, "SanguoSLG.Game/assets/portraits/1.png", 0.5, 0.35, 1.2)],
            new HashSet<int> { 1 },
            _ => true);

        Assert.True(result.IsValid);
    }
}

