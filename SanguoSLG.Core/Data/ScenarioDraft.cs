namespace SanguoSLG.Core.Data;

using System.Text.Json;

public sealed record ScenarioFactionEntry(int Id, string Name, int RulerGeneralId, string Color,
    bool Playable = true, bool Hidden = false, int ArrivalYear = 0, int ArrivalSiteId = 0);

public sealed record ScenarioGarrisonEntry(string TroopCode, int Troops);

public sealed record ScenarioShipEntry(string ShipCode, int Count);

public sealed record ScenarioCityEntry(int SiteId, int OwnerFactionId, int Gold, int Provisions,
    int Wall, int Security, IReadOnlyList<ScenarioGarrisonEntry> Garrisons,
    IReadOnlyList<ScenarioShipEntry> Ships);

public sealed record ScenarioGeneralEntry(int GeneralId, int FactionId, int? SiteId);

public sealed record ScenarioDraft(int Version, string Id, string Title, string Description,
    int StartYear, int StartMonth, int StartDay, string MapId, string MapRevision,
    IReadOnlyList<ScenarioFactionEntry> Factions, IReadOnlyList<ScenarioCityEntry> Cities,
    IReadOnlyList<ScenarioGeneralEntry> Generals)
{
    public IReadOnlyList<string> Validate(ScenarioMapPreset map, IReadOnlySet<int> knownGenerals)
    {
        var errors = new List<string>();
        if (Version != 1 || string.IsNullOrWhiteSpace(Id) || Id.Length > 64
            || Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(MapId) || MapRevision?.Length != 64
            || StartYear < 1 || StartMonth is < 1 or > 12 || StartDay is < 1 or > 30)
            errors.Add("시나리오 기본 정보 또는 지도 버전이 올바르지 않습니다.");
        if (MapRevision != ScenarioMapArchive.RevisionOf(map))
            errors.Add("시나리오가 참조하는 지도 버전과 실제 지도가 일치하지 않습니다.");
        var factionIds = Factions.Select(f => f.Id).ToHashSet();
        var siteById = map.Sites.Where(s => s.Kind is "city" or "port" or "gate")
            .ToDictionary(s => s.Id);
        var cityById = Cities.GroupBy(c => c.SiteId).ToDictionary(g => g.Key, g => g.First());
        if (Factions.Any(f => f.Id <= 0 || string.IsNullOrWhiteSpace(f.Name)
                || !knownGenerals.Contains(f.RulerGeneralId))
            || factionIds.Count != Factions.Count)
            errors.Add("세력 ID, 군주 또는 이름이 올바르지 않습니다.");
        foreach (var faction in Factions.Where(f => f.Hidden))
        {
            if (faction.Playable || faction.ArrivalYear < StartYear
                || !siteById.ContainsKey(faction.ArrivalSiteId)
                || Cities.Any(city => city.OwnerFactionId == faction.Id)
                || Generals.Any(general => general.FactionId == faction.Id && general.SiteId is not null)
                || !Generals.Any(general => general.FactionId == faction.Id
                    && general.GeneralId == faction.RulerGeneralId))
                errors.Add($"{faction.Name}: 숨은세력은 시작할 수 없으며, 출현 연도·거점·군주를 지정하고 시작 배치를 비워야 합니다.");
        }
        if (Cities.Count != siteById.Count || cityById.Count != Cities.Count
            || cityById.Keys.Any(id => !siteById.ContainsKey(id))
            || siteById.Keys.Any(id => !cityById.ContainsKey(id)))
            errors.Add("지도 거점과 시나리오 거점 목록이 일치하지 않습니다.");
        foreach (var city in Cities)
        {
            if (!siteById.TryGetValue(city.SiteId, out var site)) continue;
            if (city.OwnerFactionId != 0 && !factionIds.Contains(city.OwnerFactionId))
                errors.Add($"{site.Name}: 존재하지 않는 세력이 소유합니다.");
            if (city.Gold < 0 || city.Provisions < 0 || city.Wall < 0
                || city.Security is < 0 or > 100 || city.Garrisons.Any(g =>
                    string.IsNullOrWhiteSpace(g.TroopCode) || g.Troops <= 0)
                || city.Ships.Any(s => string.IsNullOrWhiteSpace(s.ShipCode) || s.Count <= 0))
                errors.Add($"{site.Name}: 자원·병력 수치가 올바르지 않습니다.");
            if (site.Kind != "port" && city.Ships.Count > 0)
                errors.Add($"{site.Name}: 항구가 아닌 거점에는 선박을 둘 수 없습니다.");
            if (city.OwnerFactionId == 0 && (city.Garrisons.Count > 0 || city.Ships.Count > 0))
                errors.Add($"{site.Name}: 중립 거점에 특정 세력의 병력·선박이 남아 있습니다.");
        }
        var postedIds = new HashSet<int>();
        foreach (var general in Generals)
        {
            if (!knownGenerals.Contains(general.GeneralId) || !postedIds.Add(general.GeneralId)
                || !factionIds.Contains(general.FactionId))
                errors.Add($"장수 {general.GeneralId}: 배치 또는 세력이 올바르지 않습니다.");
            if (general.SiteId is not { } cityId) continue;
            if (!cityById.TryGetValue(cityId, out var city))
                errors.Add($"장수 {general.GeneralId}: 삭제된 거점 {cityId}에 배치되어 있습니다.");
            else if (city.OwnerFactionId != general.FactionId)
                errors.Add($"장수 {general.GeneralId}: 소속 세력과 주둔 거점이 다릅니다.");
        }
        foreach (var faction in Factions.Where(f => f.Playable))
        {
            if (!Generals.Any(g => g.GeneralId == faction.RulerGeneralId
                    && g.FactionId == faction.Id && g.SiteId is not null)
                || !Cities.Any(c => c.OwnerFactionId == faction.Id))
                errors.Add($"{faction.Name}: 시작 거점에 군주를 배치해야 합니다.");
        }
        return errors;
    }
}

public static class ScenarioDraftStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static ScenarioDraft Load(string path)
        => JsonSerializer.Deserialize<ScenarioDraft>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("시나리오 파일을 읽을 수 없습니다.");

    public static void Save(string path, ScenarioDraft draft, ScenarioMapPreset map,
        IReadOnlySet<int> knownGenerals)
    {
        map.Validate();
        var errors = draft.Validate(map, knownGenerals);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(draft, Options));
            var saved = Load(temporary);
            if (saved.Id != draft.Id || saved.MapRevision != draft.MapRevision)
                throw new InvalidDataException("시나리오 저장 후 검증에 실패했습니다.");
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
