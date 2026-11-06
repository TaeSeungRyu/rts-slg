namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Domain;

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

    /// <summary>이어하기 화면은 실제 파일만 표시하며, 과거에 만든 20번 초과 슬롯도 읽는다.</summary>
    public IReadOnlyList<SaveSlotInfo> InspectSaved()
    {
        var slots = new HashSet<int>();
        if (Directory.Exists(_directory))
        {
            foreach (var path in Directory.EnumerateFiles(_directory, "sanguo-save-*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(name["sanguo-save-".Length..], out var slot) && slot > 0)
                    slots.Add(slot);
            }
        }
        if (_legacyPath is not null && File.Exists(_legacyPath)) slots.Add(1);
        return slots.Select(Inspect).Where(info => info.Exists)
            .OrderByDescending(info => info.SavedAtUtc).ThenBy(info => info.Slot).ToList();
    }

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
            var faction = state.Factions.FirstOrDefault(f => f.Id.Value == state.HumanFactionId);
            var ruler = faction is null ? null : state.Generals.FirstOrDefault(g => g.Id == faction.Ruler);
            City[] ownedCities = faction is null ? [] : state.Cities.Where(c => c.Owner == faction.Id).ToArray();
            int? troops = faction is null || state.GarrisonForces is null || state.FieldArmies is null
                ? null : state.GarrisonForces.Where(g => ownedCities.Any(c => c.Id == g.City)).Sum(g => g.Troops)
                    + state.FieldArmies.Where(a => a.Field.Owner == faction.Id).Sum(a => a.Pool.Active);
            return new SaveSlotInfo(slot, Exists: true, Corrupt: false, state.Day, state.StartYear,
                File.GetLastWriteTimeUtc(path), ScenarioTitle: state.ScenarioTitle,
                FactionName: faction?.Name, RulerId: ruler?.Id.Value, RulerName: ruler?.Name,
                RulerPortraitPath: ruler?.PortraitPath, OwnedSiteCount: faction is null ? null : ownedCities.Length,
                OfficerCount: faction is null || state.Postings is null ? null : state.GeneralsOf(faction.Id).Count(),
                TotalTroops: troops);
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
        ValidatePositiveSlot(slot);
        var path = ExistingPath(slot)
            ?? throw new FileNotFoundException($"슬롯 {slot:00}은 빈 슬롯입니다.", PathFor(slot));
        return SaveService.Load(path);
    }

    public string PathFor(int slot)
    {
        ValidatePositiveSlot(slot);
        return Path.Combine(_directory, $"sanguo-save-{slot:00}.json");
    }

    public void Delete(int slot)
    {
        ValidatePositiveSlot(slot);
        var path = ExistingPath(slot);
        if (path is not null) File.Delete(path);
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

    private static void ValidatePositiveSlot(int slot)
    {
        if (slot < 1) throw new ArgumentOutOfRangeException(nameof(slot));
    }
}

public sealed record SaveSlotInfo(
    int Slot,
    bool Exists,
    bool Corrupt,
    int? Day = null,
    int? StartYear = null,
    DateTime? SavedAtUtc = null,
    string? Error = null,
    string? ScenarioTitle = null,
    string? FactionName = null,
    int? RulerId = null,
    string? RulerName = null,
    string? RulerPortraitPath = null,
    int? OwnedSiteCount = null,
    int? OfficerCount = null,
    int? TotalTroops = null)
{
    public int? Year => Day is { } day && StartYear is { } start ? start + (day - 1) / 360 : null;
    public int? Month => Day is { } day ? ((day - 1) % 360) / 30 + 1 : null;
}
