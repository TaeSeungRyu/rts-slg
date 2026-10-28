namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Domain;

public sealed class ScenarioMapPresetTests
{
    [Fact]
    public void ResizeKeepsMapShapeAndSiteIdentityWhileIncreasingCitySpacing()
    {
        var source = new ScenarioMapPreset(1, 8, 6,
            ["PPPPPPPP", "PPPPPPPP", "PPPPPPPP", "PPPPPPPP", "PPPPPPPP", "PPPPPPPP"],
            [new MapEditorSite(11, "장안", "city", "small", 1, 2),
                new MapEditorSite(29, "낙양", "city", "small", 5, 2)]);
        var resized = ScenarioMapPresetResizer.Resize(source, 12, 9);
        resized.Validate();
        Assert.Equal(12 * 9, resized.Width * resized.Height);
        Assert.Equal(source.Sites.Select(site => (site.Id, site.Name)),
            resized.Sites.Select(site => (site.Id, site.Name)));
        Assert.True(resized.ToAxial(resized.Sites[0].Column, resized.Sites[0].Row)
            .Distance(resized.ToAxial(resized.Sites[1].Column, resized.Sites[1].Row)) >
            source.ToAxial(source.Sites[0].Column, source.Sites[0].Row)
                .Distance(source.ToAxial(source.Sites[1].Column, source.Sites[1].Row)));
        Assert.Equal(8, source.Width);
    }

    [Fact]
    public void OffsetAxialRoundTripAndSnapshotAreStable()
    {
        var preset = new ScenarioMapPreset(1, 4, 3, ["PPPP", "PWWP", "PPPP"],
            [new MapEditorSite(1, "시험성", "city", "small", 3, 2)]);
        preset.Validate();
        for (var column = 0; column < preset.Width; column++)
        for (var row = 0; row < preset.Height; row++)
            Assert.Equal((column, row), preset.ToOffset(preset.ToAxial(column, row)));
        Assert.NotSame(preset.Rows, preset.Snapshot().Rows);
    }

    [Fact]
    public void StoreRoundTripsWithoutChangingOriginalAndRejectsOverlaps()
    {
        var path = Path.Combine(Path.GetTempPath(), "map-preset-" + Guid.NewGuid() + ".json");
        try
        {
            var preset = new ScenarioMapPreset(1, 2, 2, ["PP", "PW"],
                [new MapEditorSite(1, "성", "city", "small", 0, 0)]);
            ScenarioMapPresetStore.Save(path, preset);
            Assert.Equal(preset, ScenarioMapPresetStore.Load(path) with { Rows = preset.Rows, Sites = preset.Sites });
            Assert.Throws<InvalidDataException>(() => (preset with
            {
                Sites = [..preset.Sites, new MapEditorSite(2, "중복", "port", "small", 0, 0)]
            }).Validate());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AppliedMapIsIsolatedAndSavedCampaignKeepsItsSnapshot()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var baselineCount = source.Map.Count;
        var preset = new ScenarioMapPreset(1, 4, 3,
            ["PPPP", "PPWP", "PPPP"],
            [new MapEditorSite(1, "새 성", "city", "small", 0, 0),
                new MapEditorSite(100, "새 항", "port", "small", 2, 0, 1),
                new MapEditorSite(101, "새 논", "facility", "paddy", 0, 1, 0, 1)]);
        var applied = ScenarioMapPresetApplier.Apply(source, preset);
        Assert.Equal(baselineCount, source.Map.Count);
        Assert.Equal(12, applied.Map.Count);
        Assert.False(applied.Map.Contains(new HexCoord(3, -2)));
        Assert.Equal(2, applied.Cities.Count);
        var state = GameState.FromScenario(applied);
        Assert.Equal("새 성", state.Cities[0].Name);
        Assert.Single(state.Placements);
        var reloaded = SaveService.Deserialize(SaveService.Serialize(state));
        Assert.Equal(preset.Rows, reloaded.MapPresetSnapshot!.Rows);
        Assert.Equal(preset.Sites, reloaded.MapPresetSnapshot.Sites);
        Assert.NotEqual("새 성", source.Cities[0].Name);
    }

    [Fact]
    public void ExistingOfficerPostingFollowsCityNameWhenMapIdsChange()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var original = source.Cities.First(city => city.Name == "장안");
        var posting = source.PostingList.First(item => item.Location == original.Id);
        var preset = new ScenarioMapPreset(1, 6, 5,
            Enumerable.Repeat("PPPPPP", 5).ToArray(),
            [new MapEditorSite(100, "장안", "city", "large", 3, 2)]);
        var applied = ScenarioMapPresetApplier.Apply(source, preset);
        Assert.Equal(new CityId(100), applied.PostingList.First(item => item.General == posting.General).Location);
        Assert.Equal(CastleWall.Max(CastleSize.Large, source.Balance, original.WallLevel), applied.Cities[0].Wall);
    }

    [Fact]
    public void RotatedFootprintsMatchAppliedCitiesAndRejectBlockedTiles()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var preset = new ScenarioMapPreset(1, 8, 7, Enumerable.Repeat("PPPPPPPP", 7).ToArray(),
            [new MapEditorSite(100, "방향성", "city", "medium", 3, 3, 2)]);
        var applied = ScenarioMapPresetApplier.Apply(source, preset);
        var city = Assert.Single(applied.Cities);
        Assert.Equal(2, city.Direction);
        var footprint = CastleFootprint.TilesFor(city).ToArray();
        Assert.Equal(3, footprint.Length);
        Assert.Contains(city.Position + CastleFootprint.Rotate(CastleFootprint.OffsetsFor(CastleSize.Medium).ElementAt(1), 2), footprint);
        var blocked = preset.Rows.ToArray();
        var offset = preset.ToOffset(footprint[1]);
        blocked[offset.Row] = blocked[offset.Row][..offset.Column] + "W" + blocked[offset.Row][(offset.Column + 1)..];
        Assert.Throws<InvalidDataException>(() => (preset with { Rows = blocked }).Validate());
    }

    [Fact]
    public void AdditionalTerrainAndWaterfallArePresentInNewCampaign()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var preset = new ScenarioMapPreset(1, 8, 5,
            ["PSTXCINQ", "PELVPPPP", "PPPPPPPP", "PPPPPPPP", "PPPPPPPP"],
            [new MapEditorSite(1, "성", "city", "small", 0, 3),
                new MapEditorSite(2, "폭포 절벽", "landmark", "waterfall_cliff", 3, 2)]);
        var campaign = ScenarioMapPresetApplier.Apply(source, preset);
        Assert.Equal(TerrainType.Rocks, campaign.Map.TerrainAt(preset.ToAxial(1, 0)));
        Assert.Equal(TerrainType.Cliff, campaign.Map.TerrainAt(preset.ToAxial(4, 0)));
        Assert.Contains(campaign.Features, feature => feature.Type == FeatureType.WaterfallCliff);
        Assert.Throws<InvalidDataException>(() => (preset with
        {
            Sites = [..preset.Sites, new MapEditorSite(3, "중복", "city", "small", 4, 3)]
        }).Validate());
    }

    [Fact]
    public void EditedLandAndNavalRuinsReachTheNewScenario()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var preset = new ScenarioMapPreset(1, 6, 3,
            ["PPPPPP", "PPPPWP", "PPPPPP"],
            [new MapEditorSite(1, "성", "city", "small", 0, 0),
                new MapEditorSite(2, "극병 시험 유적", "ruin", "geukbyeong", 2, 0),
                new MapEditorSite(3, "왜선 시험 유적", "ruin", "waeseon", 4, 1)]);
        var applied = ScenarioMapPresetApplier.Apply(source, preset);
        Assert.Contains(applied.RuinList, ruin => ruin.Id == "map_ruin_2"
            && ruin.Name == "극병 시험 유적" && ruin.MaxDefenders == 15_000 && !ruin.Naval);
        Assert.Contains(applied.RuinList, ruin => ruin.Id == "map_ruin_3"
            && ruin.Name == "왜선 시험 유적" && ruin.MaxDefenders == 5_000 && ruin.Naval);
        Assert.Throws<InvalidDataException>(() => (preset with
        {
            Sites = [..preset.Sites.Where(site => site.Id != 3),
                new MapEditorSite(3, "잘못된 해상 유적", "ruin", "waeseon", 4, 2)]
        }).Validate());
    }
}
