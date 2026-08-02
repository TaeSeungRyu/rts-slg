namespace SanguoSLG.Core.Domain;

/// <summary>무장 식별자.</summary>
public readonly record struct GeneralId(int Value)
{
    public override string ToString() => $"G{Value}";
}
