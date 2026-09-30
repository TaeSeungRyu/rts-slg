namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Simulation;

/// <summary>20개 캠페인 저장 슬롯의 파일 수명주기와 표시용 메타데이터를 관리한다.</summary>
public sealed class SaveSlotStore
{
    public const int SlotCount = 20;
    private readonly string _directory;
    private readonly string? _legacyPath;

    public SaveSlotStore(string directory, string? legacyPath = null)
    {
        _directory = Path.GetFullPath(directory);
        _legacyPath = string.IsNullOrWhiteSpace(legacyPath) ? null : Path.GetFullPath(legacyPath);
    }

    public IReadOnlyList<SaveSlotInfo> InspectAll()
        => Enumerable.Range(1, SlotCount).Select(Inspect).ToList();

    public SaveSlotInfo Inspect(int slot)
    {
        var path = ExistingPath(slot);
        if (path is null)
        {
            return new SaveSlotInfo(slot, Exists: false, Corrupt: false);
        }

        try
        {
            var state = SaveService.Load(path);
            return new SaveSlotInfo(slot, Exists: true, Corrupt: false, state.Day, state.StartYear,
                File.GetLastWriteTimeUtc(path));
        }
        catch (Exception error)
        {
            return new SaveSlotInfo(slot, Exists: true, Corrupt: true, Error: error.Message,
                SavedAtUtc: File.GetLastWriteTimeUtc(path));
        }
    }

    public void Save(int slot, GameState state)
    {
        ValidateSlot(slot);
        Directory.CreateDirectory(_directory);
        var path = PathFor(slot);
        var temp = Path.Combine(_directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, SaveService.Serialize(state));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    public GameState Load(int slot)
    {
        ValidateSlot(slot);
        var path = ExistingPath(slot)
            ?? throw new FileNotFoundException($"슬롯 {slot:00}은 빈 슬롯입니다.", PathFor(slot));
        return SaveService.Load(path);
    }

    public string PathFor(int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(_directory, $"sanguo-save-{slot:00}.json");
    }

    private string? ExistingPath(int slot)
    {
        var path = PathFor(slot);
        if (File.Exists(path))
        {
            return path;
        }

        return slot == 1 && _legacyPath is not null && File.Exists(_legacyPath) ? _legacyPath : null;
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 1 or > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"저장 슬롯은 1~{SlotCount}만 가능합니다.");
        }
    }
}

public sealed record SaveSlotInfo(
    int Slot,
    bool Exists,
    bool Corrupt,
    int? Day = null,
    int? StartYear = null,
    DateTime? SavedAtUtc = null,
    string? Error = null)
{
    public int? Year => Day is { } day && StartYear is { } start ? start + (day - 1) / 360 : null;
    public int? Month => Day is { } day ? ((day - 1) % 360) / 30 + 1 : null;
}
