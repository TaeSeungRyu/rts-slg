namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class SaveSlotStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sanguo-slot-tests", Guid.NewGuid().ToString("N"));

    private static GameState State(int day) => new(day, 190, [], [], []);

    [Fact]
    public void 스무개_슬롯은_처음에_모두_비어있다()
    {
        var slots = new SaveSlotStore(_directory).InspectAll();

        Assert.Equal(20, slots.Count);
        Assert.All(slots, slot => Assert.False(slot.Exists));
    }

    [Fact]
    public void 신규저장과_덮어쓰기후_새로운_스토어에서도_왕복된다()
    {
        var store = new SaveSlotStore(_directory);
        store.Save(7, State(8));
        store.Save(7, State(367));

        var restarted = new SaveSlotStore(_directory);
        var info = restarted.Inspect(7);
        var loaded = restarted.Load(7);

        Assert.True(info.Exists);
        Assert.False(info.Corrupt);
        Assert.Equal(191, info.Year);
        Assert.Equal(1, info.Month);
        Assert.Equal(367, loaded.Day);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void 빈슬롯은_거부하고_손상슬롯은_표시하며_기존상태를_건드리지않는다()
    {
        var store = new SaveSlotStore(_directory);
        var current = State(77);
        Assert.Throws<FileNotFoundException>(() => store.Load(3));

        Directory.CreateDirectory(_directory);
        File.WriteAllText(store.PathFor(4), "{broken-json");
        var info = store.Inspect(4);
        Assert.True(info.Exists);
        Assert.True(info.Corrupt);
        Assert.ThrowsAny<Exception>(() => store.Load(4));
        Assert.Equal(77, current.Day);
    }

    [Fact]
    public void 메인화면_슬롯에는_저장된_시나리오_제목이_표시된다()
    {
        var store = new SaveSlotStore(_directory);
        store.Save(2, State(41) with { ScenarioTitle = "영웅호걸" });

        Assert.Equal("영웅호걸", store.Inspect(2).ScenarioTitle);
        Assert.Equal("영웅호걸", store.Load(2).ScenarioTitle);
    }

    [Fact]
    public void 이전_단일세이브는_첫번째_슬롯에서_읽힌다()
    {
        Directory.CreateDirectory(_directory);
        var legacy = Path.Combine(_directory, "sanguo-save.json");
        SaveService.Save(State(31), legacy);

        var store = new SaveSlotStore(Path.Combine(_directory, "slots"), legacy);

        Assert.True(store.Inspect(1).Exists);
        Assert.Equal(31, store.Load(1).Day);
        Assert.False(store.Inspect(2).Exists);
    }

    [Fact]
    public void 이어하기는_실제_파일만_최근순으로_보이고_20번_초과_기록도_읽는다()
    {
        var store = new SaveSlotStore(_directory);
        store.Save(2, State(3));
        Directory.CreateDirectory(_directory);
        SaveService.Save(State(9), store.PathFor(27));
        File.SetLastWriteTimeUtc(store.PathFor(2), new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(store.PathFor(27), new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal([27, 2], store.InspectSaved().Select(info => info.Slot));
        Assert.Equal(9, store.Load(27).Day);
        Assert.Equal(20, store.InspectAll().Count);
    }

    [Fact]
    public void 삭제는_선택한_저장_파일만_제거한다()
    {
        var store = new SaveSlotStore(_directory);
        store.Save(1, State(1));
        store.Save(3, State(3));

        store.Delete(3);

        Assert.Equal([1], store.InspectSaved().Select(info => info.Slot));
        Assert.Equal(1, store.Load(1).Day);
    }

    [Fact]
    public void 카드_메타데이터는_저장된_인물과_거점_장수_병력에서_계산한다()
    {
        var faction = new Faction(new FactionId(5), "황건군", new GeneralId(31), 0, "gold");
        var general = new General(new GeneralId(31), "장각", new Dictionary<TroopClass, AptitudeGrade>(), 60, 80, 70);
        var city = new City(new CityId(7), "낙양", new HexCoord(0, 0), faction.Id, 0);
        var state = State(31) with
        {
            Factions = [faction], Generals = [general], Cities = [city], HumanFactionId = 5,
            Postings = [new GeneralPosting(general.Id, faction.Id, city.Id)],
            GarrisonForces = [new GarrisonForce(city.Id, "swordsman", 1200, 50)],
            FieldArmies = []
        };
        var store = new SaveSlotStore(_directory);
        store.Save(4, state);

        var card = store.Inspect(4);
        Assert.Equal("황건군", card.FactionName);
        Assert.Equal("장각", card.RulerName);
        Assert.Equal(31, card.RulerId);
        Assert.Equal(1, card.OwnedSiteCount);
        Assert.Equal(1, card.OfficerCount);
        Assert.Equal(1200, card.TotalTroops);
    }

    public void Dispose()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sanguo-slot-tests"));
        var target = Path.GetFullPath(_directory);
        if (target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }
}
