namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;

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
