namespace SanguoSLG.Core.Tests.Data;

using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using Xunit;

/// <summary>data/generals.json 무결성 — 코드 참조·중복·스킬 규칙(spec-general)을 지킨다.</summary>
public class GeneralDataTests
{
    private static readonly System.Collections.Generic.IReadOnlyList<General> All =
        new GeneralLoader().LoadFromDirectory(TestData.DataDirectory());

    [Fact]
    public void 장수_id와_이름은_중복이없다()
    {
        Assert.Equal(All.Count, All.Select(g => g.Id).Distinct().Count());
        Assert.Equal(All.Count, All.Select(g => g.Name).Distinct().Count());
    }

    [Fact]
    public void 명단_규모_중국100플러스_한국30_일본10()
    {
        Assert.True(All.Count(g => g.Id.Value < 200) >= 100);
        Assert.Equal(30, All.Count(g => g.Id.Value is > 200 and < 300));
        Assert.Equal(10, All.Count(g => g.Id.Value > 300));
    }

    [Fact]
    public void 모든_스킬코드는_실제_스킬데이터에_존재한다()
    {
        var actives = new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())
            .Select(a => a.Code).ToHashSet();
        var passives = new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())
            .Select(p => p.Code).ToHashSet();

        foreach (var g in All)
        {
            if (g.BattleActive is not null)
            {
                Assert.Contains(g.BattleActive, actives);
            }

            foreach (var s in g.Passives)
            {
                Assert.Contains(s.Code, passives);
                Assert.InRange(s.Tier, 1, 3);
            }
        }
    }

    [Fact]
    public void 전투스킬은_최대4개_액티브는_최대1개다()
    {
        foreach (var g in All)
        {
            var total = (g.BattleActive is null ? 0 : 1) + g.Passives.Count;
            Assert.InRange(total, 0, 4);
        }
    }

    [Fact]
    public void 모든_장수는_병종6분류_적성을_전부가진다()
    {
        foreach (var g in All)
        {
            Assert.Equal(6, g.Aptitudes.Count);
        }
    }

    [Fact]
    public void 모든_장수의_지역코드는_regions에_존재하고_출생년과_소개가_있다()
    {
        var regions = new RegionLoader().LoadFromDirectory(TestData.DataDirectory())
            .Select(r => r.Code).ToHashSet();

        foreach (var g in All)
        {
            Assert.Contains(g.Region, regions);
            Assert.NotEqual(0, g.Birth); // 음수 = 기원전
            Assert.False(string.IsNullOrWhiteSpace(g.Desc));
        }
    }

    [Fact]
    public void 지역코드는_중복이없고_권역은_세갈래다()
    {
        var regions = new RegionLoader().LoadFromDirectory(TestData.DataDirectory());
        Assert.Equal(regions.Count, regions.Select(r => r.Code).Distinct().Count());
        Assert.Equal(new[] { "china", "japan", "korea" },
            regions.Select(r => r.Realm).Distinct().OrderBy(x => x).ToArray());
    }
}
