namespace SanguoSLG.Core.Domain;

public readonly record struct FieldBuildingId(int Value)
{
    public override string ToString() => $"FB{Value}";
}
