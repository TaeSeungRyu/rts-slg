namespace SanguoSLG.Core.Data;

using System.Text.Json;
using System.Text.Json.Serialization;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>기존 캠페인 저장과 섞이지 않는 Phase 18D 전용 저장 계약.</summary>
public sealed class RenewalMovementSaveService
{
    public const int CurrentSchemaVersion = 1;
    public const string Mode = "renewal_movement";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize(RenewalAdvanceState state) => JsonSerializer.Serialize(
        new RenewalMovementSaveEnvelope(CurrentSchemaVersion, Mode, state), Options);

    public RenewalAdvanceState Deserialize(string json)
    {
        var envelope = JsonSerializer.Deserialize<RenewalMovementSaveEnvelope>(json, Options)
            ?? throw new InvalidDataException("신규 이동 저장 데이터가 비어 있습니다.");
        if (!string.Equals(envelope.Mode, Mode, StringComparison.Ordinal))
        {
            throw new InvalidDataException("기존 캠페인 저장은 신규 이동 검수장에 불러올 수 없습니다.");
        }
        if (envelope.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"지원하지 않는 신규 이동 저장 버전입니다: {envelope.SchemaVersion}");
        }

        return envelope.State ?? throw new InvalidDataException("신규 이동 상태가 없습니다.");
    }

    public void Save(string path, RenewalAdvanceState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(state));
    }

    public RenewalAdvanceState Load(string path) => Deserialize(File.ReadAllText(path));

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new UnitIdJsonConverter());
        return options;
    }

    private sealed record RenewalMovementSaveEnvelope(
        int SchemaVersion,
        string Mode,
        RenewalAdvanceState? State);

    private sealed class UnitIdJsonConverter : JsonConverter<UnitId>
    {
        public override UnitId Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options) => new(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, UnitId value,
            JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);

        public override UnitId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options) => int.TryParse(reader.GetString(), out var value)
                ? new UnitId(value)
                : throw new JsonException("부대 ID 키가 올바르지 않습니다.");

        public override void WriteAsPropertyName(Utf8JsonWriter writer, UnitId value,
            JsonSerializerOptions options) => writer.WritePropertyName(value.Value.ToString());
    }
}
