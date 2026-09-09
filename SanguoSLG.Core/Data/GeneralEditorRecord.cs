namespace SanguoSLG.Core.Data;

public sealed record GeneralEditorRecord(
    int Id,
    string Name,
    Dictionary<string, string> Aptitudes,
    int Might,
    int Intellect,
    int Politics,
    string? BattleActive,
    List<GeneralEditorSkill> BattlePassives,
    List<GeneralEditorSkill> AdminPassives,
    int Birth,
    int UnlockYear,
    string Region,
    string Desc);

