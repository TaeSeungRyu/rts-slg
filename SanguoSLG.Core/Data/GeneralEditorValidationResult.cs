namespace SanguoSLG.Core.Data;

public sealed record GeneralEditorValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static GeneralEditorValidationResult Success { get; } = new([]);
}

