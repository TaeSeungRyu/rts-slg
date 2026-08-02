namespace SanguoSLG.Core.Domain;

/// <summary>도시 식별자.</summary>
public readonly record struct CityId(int Value)
{
    public override string ToString() => $"C{Value}";
}
