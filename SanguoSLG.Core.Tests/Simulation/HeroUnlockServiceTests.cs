namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class HeroUnlockServiceTests
{
    [Fact]
    public void 세력형_위인은_소속_세력의_조건이_맞으면_해금된다()
    {
        var hero = new HeroUnlockDefinition(
            new GeneralId(6),
            HeroUnlockType.Faction,
            new FactionId(2),
            Conditions:
            [
                new HeroUnlockCondition("owned_cities", 2),
                new HeroUnlockCondition("city_security_at_least", 80),
            ],
            RecruitGold: 1000);
        var state = State([hero],
        [
            City(1, new FactionId(2), "jingzhou", 90),
            City(2, new FactionId(2), "yizhou", 60),
        ]);

        var next = new HeroUnlockService().Evaluate(state);

        var unlocked = Assert.Single(next.HeroStates);
        Assert.Equal(HeroUnlockStatus.Unlocked, unlocked.Status);
        Assert.Equal(new FactionId(2), unlocked.EligibleFaction);
    }

    [Fact]
    public void 도시형_위인은_지역을_점령한_세력에게_해금된다()
    {
        var hero = new HeroUnlockDefinition(
            new GeneralId(11),
            HeroUnlockType.Region,
            HomeRegions: [ "jingzhou" ],
            Conditions:
            [
                new HeroUnlockCondition("owned_region_cities", 1, Region: "jingzhou"),
                new HeroUnlockCondition("research_level", 4, TroopCode: "archer"),
            ]);
        var state = State([hero], [City(1, new FactionId(1), "jingzhou", 70)])
            with { ResearchTracks = [new FactionResearch(new FactionId(1), "archer", 4)] };

        var next = new HeroUnlockService().Evaluate(state);

        var unlocked = Assert.Single(next.HeroStates);
        Assert.Equal(HeroUnlockStatus.Unlocked, unlocked.Status);
        Assert.Equal(new FactionId(1), unlocked.EligibleFaction);
    }

    [Fact]
    public void 멸망한_세력의_잠긴_세력형_위인은_유랑_상태가_된다()
    {
        var lockedHero = new HeroUnlockDefinition(new GeneralId(6), HeroUnlockType.Faction, new FactionId(2));
        var recruitedHero = new HeroUnlockDefinition(new GeneralId(9), HeroUnlockType.Faction, new FactionId(2));
        var regionHero = new HeroUnlockDefinition(new GeneralId(11), HeroUnlockType.Region, HomeRegions: [ "jingzhou" ]);
        var state = State([lockedHero, recruitedHero, regionHero], [City(1, new FactionId(1), "jingzhou", 90)])
            with
            {
                HeroUnlockStates =
                [
                    new HeroUnlockState(new GeneralId(6), HeroUnlockStatus.Locked),
                    new HeroUnlockState(new GeneralId(9), HeroUnlockStatus.Recruited, new FactionId(2)),
                    new HeroUnlockState(new GeneralId(11), HeroUnlockStatus.Locked),
                ],
            };

        var next = new HeroUnlockService().MarkFactionEliminated(state, new FactionId(2));

        Assert.Equal(HeroUnlockStatus.Wanderer, next.HeroStates.Single(s => s.General == new GeneralId(6)).Status);
        Assert.Equal(HeroUnlockStatus.Recruited, next.HeroStates.Single(s => s.General == new GeneralId(9)).Status);
        Assert.Equal(HeroUnlockStatus.Locked, next.HeroStates.Single(s => s.General == new GeneralId(11)).Status);
    }

    private static GameState State(IReadOnlyList<HeroUnlockDefinition> heroes, IReadOnlyList<City> cities)
        => new(
            10,
            190,
            [new Faction(new FactionId(1), "위", new GeneralId(1), 1000, "#2d5fd0"), new Faction(new FactionId(2), "촉", new GeneralId(2), 1000, "#2c8c46")],
            cities,
            [new General(new GeneralId(6), "제갈량", new Dictionary<TroopClass, AptitudeGrade>(), 38, 100, 95)],
            HeroUnlockDefinitions: heroes,
            HeroUnlockStates: heroes.Select(h => new HeroUnlockState(h.General, HeroUnlockStatus.Locked)).ToList());

    private static City City(int id, FactionId owner, string region, int security)
        => new(new CityId(id), $"도시{id}", new HexCoord(id, 0), owner, 1000, CastleSize.Small,
            Security: security, Region: region);
}
