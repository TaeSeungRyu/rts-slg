namespace SanguoSLG.Core.Data;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed record ScenarioMapVersion(string Id, string Title, string Revision, ScenarioMapPreset Preset);

public sealed class ScenarioMapArchive(string mapDirectory, string scenarioDirectory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _mapDirectory = Path.GetFullPath(mapDirectory);
    private readonly string _scenarioDirectory = Path.GetFullPath(scenarioDirectory);

    public static string RevisionOf(ScenarioMapPreset preset)
    {
        preset.Validate();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(preset, Options)))).ToLowerInvariant();
    }

    public ScenarioMapVersion SaveVersion(string id, string title, ScenarioMapPreset preset)
    {
        ValidateId(id);
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidDataException("지도 이름이 필요합니다.");
        var version = new ScenarioMapVersion(id, title, RevisionOf(preset), preset.Snapshot());
        var path = PathFor(id, version.Revision);
        if (File.Exists(path))
        {
            var saved = Load(id, version.Revision);
            if (!saved.Preset.Rows.SequenceEqual(preset.Rows)
                || !saved.Preset.Sites.SequenceEqual(preset.Sites))
                throw new InvalidDataException("같은 지도 버전에 다른 내용이 저장되어 있습니다.");
            return saved;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(version, Options));
            File.Move(temporary, path);
            return Load(id, version.Revision);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public ScenarioMapVersion Load(string id, string revision)
    {
        ValidateId(id);
        ValidateRevision(revision);
        var version = JsonSerializer.Deserialize<ScenarioMapVersion>(File.ReadAllText(PathFor(id, revision)), Options)
            ?? throw new InvalidDataException("지도 버전을 읽을 수 없습니다.");
        if (version.Id != id || version.Revision != revision
            || RevisionOf(version.Preset) != revision)
            throw new InvalidDataException("지도 ID 또는 내용 해시가 일치하지 않습니다.");
        return version;
    }

    public IReadOnlyList<ScenarioMapVersion> ListVersions()
    {
        if (!Directory.Exists(_mapDirectory)) return [];
        return Directory.EnumerateFiles(_mapDirectory, "*.json", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .Select(path =>
            {
                var id = Path.GetFileName(Path.GetDirectoryName(path)!);
                var revision = Path.GetFileNameWithoutExtension(path);
                return Load(id, revision);
            }).OrderBy(version => version.Title).ThenBy(version => version.Revision).ToArray();
    }

    public IReadOnlyList<string> ReferencingScenarios(string id, string revision)
    {
        ValidateId(id);
        ValidateRevision(revision);
        if (!Directory.Exists(_scenarioDirectory)) return [];
        return Directory.EnumerateFiles(_scenarioDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(ScenarioDraftStore.Load)
            .Where(draft => draft.MapId == id && draft.MapRevision == revision)
            .Select(draft => draft.Title).OrderBy(title => title).ToArray();
    }

    public string PathFor(string id, string revision)
    {
        ValidateId(id);
        ValidateRevision(revision);
        return Path.Combine(_mapDirectory, id, revision + ".json");
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64
            || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new InvalidDataException("지도 ID는 영문·숫자·하이픈만 사용할 수 있습니다.");
    }

    private static void ValidateRevision(string revision)
    {
        if (revision.Length != 64 || revision.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("지도 버전 해시가 올바르지 않습니다.");
    }
}
