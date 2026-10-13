namespace SanguoSLG.Core.Tests.Data;

using System.Text.Json;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class EventUnitLoaderTests
{
    [Fact]
    public void 대호데이터는_일반생산병종과_분리하여_읽는다()
    {
        var definition = Assert.Single(new EventUnitLoader().LoadFromDirectory(TestData.DataDirectory()));
        Assert.Equal("great_tiger", definition.Code);
        Assert.Contains(TerrainType.Forest, definition.SpawnTerrains);
        Assert.DoesNotContain(new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()),
            troop => troop.Code == definition.Code);
        var invalid = definition with { SpawnMaxDistance = definition.SpawnMinDistance - 1 };
        Assert.Throws<InvalidDataException>(() => new EventUnitLoader().LoadFromJson(
            JsonSerializer.Serialize(new[] { invalid }, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })));
    }

    [Fact]
    public void 이벤트예약과_호부는_저장복원되고_구저장은_빈목록이다()
    {
        var state = new GameState(15, 190, [], [], [],
            EventEncounters: [new("trace:1", "great_tiger", new(1), new(1), 8, new(10), 14, new(1))],
            FactionTreasures: [new("trace:1:reward", "tiger_tally", "호부", new(1), 14, "trace:1")]);
        var restored = SaveService.Deserialize(SaveService.Serialize(state));
        Assert.Equal(state.Encounters, restored.Encounters);
        Assert.Equal(state.Treasures, restored.Treasures);
        var old = SaveService.Deserialize("{\"Day\":1,\"StartYear\":190,\"Factions\":[],\"Cities\":[],\"Generals\":[]}");
        Assert.Empty(old.Encounters);
        Assert.Empty(old.Treasures);
    }
}
