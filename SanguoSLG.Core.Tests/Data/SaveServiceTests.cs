namespace SanguoSLG.Core.Tests.Data;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>세이브 왕복 — GameState → JSON → GameState 동일성(장수 적성·도시·부대·명령·포로 포함).</summary>
public class SaveServiceTests
{
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    [Fact]
    public void 저장_불러오기_왕복하면_핵심_상태가_보존된다()
    {
        var t = Troops.First(x => x.Code == "swordsman");
        var unit = new CombatUnit(
            new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(2, 3), t.MovementPerDay, t.Detection,
                t.RangeUnit, MovementDomain.Land, UnitMode.Attack, new HexCoord(5, 0), 1, t.RangeCastle,
                new[] { new HexCoord(3, 1) }),
            CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, 5000),
            new TroopPool(5000, 200), UnitCombatState.Create(60), 70, 60, 5000, t.Class,
            TroopCode: "swordsman", VanguardId: new GeneralId(1));

        var g = new General(new GeneralId(1), "관우",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Cavalry] = AptitudeGrade.S, [TroopClass.Infantry] = AptitudeGrade.A },
            Might: 97, Intellect: 75, Politics: 62, Region: "hedong");

        var city = new City(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, CastleSize.Large,
            Gold: 5000, Population: 200_000, Ore: 8000, Governor: new GeneralId(1), Strategist: new GeneralId(1),
            Wall: 6000);

        var state = new GameState(45, 190,
            new List<Faction> { new(new FactionId(1), "위", new GeneralId(1), 1000, "#0af") },
            new List<City> { city }, new List<General> { g },
            Postings: new List<GeneralPosting> { new(new GeneralId(1), new FactionId(1), null) },
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 8000, 55, Trainee: true) },
            FieldArmies: new List<CombatUnit> { unit },
            Captives: new List<Prisoner> { new(new GeneralId(1), new FactionId(1), new FactionId(2)) },
            FactionAlliances: new List<FactionAlliance> { FactionAlliance.Create(new FactionId(1), new FactionId(2), 40, 100) },
            MarketPricePercent: 130,
            FacilityPlacements: new List<FacilityPlacement> { new(new CityId(1), new HexCoord(1, 0), "paddy", FacilityHealth.Level2) });

        var round = SaveService.Deserialize(SaveService.Serialize(state));

        Assert.Equal(45, round.Day);
        Assert.Equal(130, round.MarketPricePercent);
        // 장수·적성(enum 키 딕셔너리).
        var rg = round.Generals.Single();
        Assert.Equal("관우", rg.Name);
        Assert.Equal(AptitudeGrade.S, rg.AptitudeFor(TroopClass.Cavalry));
        Assert.Equal(AptitudeGrade.A, rg.AptitudeFor(TroopClass.Infantry));
        // 도시(태수·군사·성벽).
        var rc = round.Cities.Single();
        Assert.Equal(5000, rc.Gold);
        Assert.Equal(new GeneralId(1), rc.Governor);
        Assert.Equal(new GeneralId(1), rc.Strategist);
        Assert.Equal(6000, rc.Wall);
        // 대기 병력·야전 부대(경유지 포함)·포로.
        Assert.Equal(8000, round.Garrisons.Single().Troops);
        Assert.True(round.Garrisons.Single().Trainee);
        var ru = round.Armies.Single();
        Assert.Equal(5000, ru.Pool.Active);
        Assert.Equal(new HexCoord(2, 3), ru.Field.Position);
        Assert.Equal(new HexCoord(5, 0), ru.Field.Target);
        Assert.Equal(new HexCoord(3, 1), ru.Field.Waypoints!.Single());
        Assert.Single(round.Prisoners);
        Assert.True(round.AreAllied(new FactionId(1), new FactionId(2)));
        // 시설 배치 타일(건설 위치)도 왕복 보존.
        var rp = Assert.Single(round.Placements);
        Assert.Equal(new HexCoord(1, 0), rp.Plot);
        Assert.Equal("paddy", rp.Code);
        Assert.Equal(FacilityHealth.Level2, rp.HitPoints);
    }
}
