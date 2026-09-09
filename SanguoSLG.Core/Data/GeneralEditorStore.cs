namespace SanguoSLG.Core.Data;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class GeneralEditorStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly string[] TroopKeys =
    [
        "infantry",
        "archer",
        "cavalry",
        "elephant",
        "siege",
        "naval",
    ];

    private static readonly HashSet<string> Grades = ["F", "D", "C", "B", "A", "A+", "S", "SS", "SSS"];

    public static IReadOnlyList<GeneralEditorRecord> LoadGenerals(string json)
        => JsonSerializer.Deserialize<List<GeneralEditorRecord>>(json, ReadOptions)
            ?? throw new InvalidDataException("장수 데이터를 읽을 수 없습니다.");

    public static IReadOnlyList<GeneralPortraitRecord> LoadPortraits(string json)
        => JsonSerializer.Deserialize<List<GeneralPortraitRecord>>(json, ReadOptions)
            ?? throw new InvalidDataException("장수 초상 메타데이터를 읽을 수 없습니다.");

    public static GeneralEditorValidationResult Validate(
        IReadOnlyList<GeneralEditorRecord> generals,
        IReadOnlySet<string> activeSkillCodes,
        IReadOnlySet<string> passiveSkillCodes,
        IReadOnlySet<string> adminSkillCodes)
    {
        var errors = new List<string>();
        var ids = new HashSet<int>();

        foreach (var general in generals)
        {
            if (!ids.Add(general.Id))
            {
                errors.Add($"중복 장수 ID: {general.Id}");
            }

            if (string.IsNullOrWhiteSpace(general.Name))
            {
                errors.Add($"{general.Id}: 이름이 비어 있습니다.");
            }

            ValidateRange(errors, general.Id, "무력", general.Might);
            ValidateRange(errors, general.Id, "지력", general.Intellect);
            ValidateRange(errors, general.Id, "정치", general.Politics);

            foreach (var troop in TroopKeys)
            {
                if (!general.Aptitudes.TryGetValue(troop, out var grade) || !Grades.Contains(grade))
                {
                    errors.Add($"{general.Id}: {troop} 적성이 올바르지 않습니다.");
                }
            }

            if (!string.IsNullOrWhiteSpace(general.BattleActive) && !activeSkillCodes.Contains(general.BattleActive))
            {
                errors.Add($"{general.Id}: 미등록 전투 액티브 {general.BattleActive}");
            }

            ValidateSkills(errors, general.Id, "전투 패시브", general.BattlePassives, passiveSkillCodes, 4);
            ValidateSkills(errors, general.Id, "내정 패시브", general.AdminPassives, adminSkillCodes, 4);

            var battleSkillCount = (string.IsNullOrWhiteSpace(general.BattleActive) ? 0 : 1) + general.BattlePassives.Count;
            if (battleSkillCount > 4)
            {
                errors.Add($"{general.Id}: 전투 스킬은 액티브 포함 최대 4개입니다.");
            }
        }

        return errors.Count == 0 ? GeneralEditorValidationResult.Success : new GeneralEditorValidationResult(errors);
    }

    public static GeneralEditorValidationResult ValidatePortraits(
        IReadOnlyList<GeneralPortraitRecord> portraits,
        IReadOnlySet<int> generalIds,
        Func<string, bool>? portraitExists = null)
    {
        var errors = new List<string>();
        var ids = new HashSet<int>();

        foreach (var portrait in portraits)
        {
            if (!generalIds.Contains(portrait.GeneralId))
            {
                errors.Add($"미등록 장수 초상 ID: {portrait.GeneralId}");
            }

            if (!ids.Add(portrait.GeneralId))
            {
                errors.Add($"중복 장수 초상 ID: {portrait.GeneralId}");
            }

            if (Path.IsPathRooted(portrait.PortraitPath))
            {
                errors.Add($"{portrait.GeneralId}: 초상 경로는 저장소 상대 경로여야 합니다.");
            }

            var ext = Path.GetExtension(portrait.PortraitPath);
            if (!string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{portrait.GeneralId}: 초상은 png만 지원합니다.");
            }

            if (portraitExists is not null && !portraitExists(portrait.PortraitPath))
            {
                errors.Add($"{portrait.GeneralId}: 초상 파일을 찾을 수 없습니다.");
            }

            ValidateUnitInterval(errors, portrait.GeneralId, "face_center_x", portrait.FaceCenterX);
            ValidateUnitInterval(errors, portrait.GeneralId, "face_center_y", portrait.FaceCenterY);
            if (portrait.FaceZoom is < 1 or > 4 || double.IsNaN(portrait.FaceZoom))
            {
                errors.Add($"{portrait.GeneralId}: face_zoom은 1~4여야 합니다.");
            }
        }

        return errors.Count == 0 ? GeneralEditorValidationResult.Success : new GeneralEditorValidationResult(errors);
    }

    public static string ReplaceGeneral(string originalJson, GeneralEditorRecord edited)
    {
        var root = JsonNode.Parse(originalJson) as JsonArray
            ?? throw new InvalidDataException("generals.json 루트는 배열이어야 합니다.");
        var replaced = false;

        foreach (var node in root.OfType<JsonObject>())
        {
            if (node["id"]?.GetValue<int>() != edited.Id)
            {
                continue;
            }

            ApplyGeneral(node, edited);
            replaced = true;
            break;
        }

        if (!replaced)
        {
            throw new InvalidDataException($"장수 ID {edited.Id}를 찾을 수 없습니다.");
        }

        return root.ToJsonString(WriteOptions);
    }

    public static string ReplacePortrait(string originalJson, GeneralPortraitRecord edited)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(originalJson) ? "[]" : originalJson) as JsonArray
            ?? throw new InvalidDataException("general-portraits.json 루트는 배열이어야 합니다.");
        JsonObject? target = null;

        foreach (var node in root.OfType<JsonObject>())
        {
            if (node["general_id"]?.GetValue<int>() == edited.GeneralId)
            {
                target = node;
                break;
            }
        }

        if (target is null)
        {
            target = new JsonObject();
            root.Add(target);
        }

        ApplyPortrait(target, edited);
        return root.ToJsonString(WriteOptions);
    }

    public static void SaveValidated(
        string generalsPath,
        string portraitsPath,
        IReadOnlyList<GeneralEditorRecord> generals,
        IReadOnlyList<GeneralPortraitRecord> portraits,
        IReadOnlySet<string> activeSkillCodes,
        IReadOnlySet<string> passiveSkillCodes,
        IReadOnlySet<string> adminSkillCodes,
        string backupDirectory)
    {
        var generalValidation = Validate(generals, activeSkillCodes, passiveSkillCodes, adminSkillCodes);
        var portraitValidation = ValidatePortraits(portraits, generals.Select(g => g.Id).ToHashSet(),
            p => File.Exists(Path.Combine(Path.GetDirectoryName(generalsPath) ?? "", "..", p)));
        var errors = generalValidation.Errors.Concat(portraitValidation.Errors).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        Directory.CreateDirectory(backupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var generalBackup = Path.Combine(backupDirectory, $"generals-{stamp}.json");
        var portraitBackup = Path.Combine(backupDirectory, $"general-portraits-{stamp}.json");
        var originalGenerals = File.ReadAllText(generalsPath);
        var originalPortraits = File.Exists(portraitsPath) ? File.ReadAllText(portraitsPath) : "[]";
        File.WriteAllText(generalBackup, originalGenerals);
        File.WriteAllText(portraitBackup, originalPortraits);

        var tmpGenerals = generalsPath + ".tmp";
        var tmpPortraits = portraitsPath + ".tmp";
        try
        {
            File.WriteAllText(tmpGenerals, JsonSerializer.Serialize(generals, WriteOptions));
            File.WriteAllText(tmpPortraits, JsonSerializer.Serialize(portraits, WriteOptions));
            File.Move(tmpGenerals, generalsPath, overwrite: true);
            File.Move(tmpPortraits, portraitsPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmpGenerals))
            {
                File.Delete(tmpGenerals);
            }

            if (File.Exists(tmpPortraits))
            {
                File.Delete(tmpPortraits);
            }

            File.WriteAllText(generalsPath, originalGenerals);
            File.WriteAllText(portraitsPath, originalPortraits);
            throw;
        }
    }

    private static void ApplyGeneral(JsonObject node, GeneralEditorRecord edited)
    {
        node["aptitudes"] = JsonSerializer.SerializeToNode(edited.Aptitudes, WriteOptions);
        node["might"] = edited.Might;
        node["intellect"] = edited.Intellect;
        node["politics"] = edited.Politics;
        node["battle_active"] = string.IsNullOrWhiteSpace(edited.BattleActive) ? null : edited.BattleActive;
        node["battle_passives"] = JsonSerializer.SerializeToNode(edited.BattlePassives, WriteOptions);
        node["admin_passives"] = JsonSerializer.SerializeToNode(edited.AdminPassives, WriteOptions);
    }

    private static void ApplyPortrait(JsonObject node, GeneralPortraitRecord edited)
    {
        node["general_id"] = edited.GeneralId;
        node["portrait_path"] = edited.PortraitPath;
        node["face_center_x"] = edited.FaceCenterX;
        node["face_center_y"] = edited.FaceCenterY;
        node["face_zoom"] = edited.FaceZoom;
    }

    private static void ValidateRange(List<string> errors, int id, string label, int value)
    {
        if (value is < 1 or > 100)
        {
            errors.Add($"{id}: {label}은 1~100이어야 합니다.");
        }
    }

    private static void ValidateSkills(
        List<string> errors,
        int id,
        string label,
        IReadOnlyList<GeneralEditorSkill> skills,
        IReadOnlySet<string> validCodes,
        int maxCount)
    {
        if (skills.Count > maxCount)
        {
            errors.Add($"{id}: {label}은 최대 {maxCount}개입니다.");
        }

        foreach (var group in skills.GroupBy(s => s.Code).Where(g => g.Count() > 1))
        {
            errors.Add($"{id}: {label} 중복 코드 {group.Key}");
        }

        foreach (var skill in skills)
        {
            if (!validCodes.Contains(skill.Code))
            {
                errors.Add($"{id}: 미등록 {label} {skill.Code}");
            }

            if (skill.Tier is < 1 or > 3)
            {
                errors.Add($"{id}: {label} {skill.Code} 티어는 1~3이어야 합니다.");
            }
        }
    }

    private static void ValidateUnitInterval(List<string> errors, int id, string label, double value)
    {
        if (value is < 0 or > 1 || double.IsNaN(value))
        {
            errors.Add($"{id}: {label}은 0~1이어야 합니다.");
        }
    }
}

