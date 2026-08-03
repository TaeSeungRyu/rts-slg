namespace SanguoSLG.Core.Domain;

/// <summary>부대 식별자.</summary>
public readonly record struct UnitId(int Value)
{
    public override string ToString() => $"U{Value}";
}
