namespace SanguoSLG.Core.Domain;

/// <summary>세력 식별자. 원시 int 대신 강타입으로 혼동을 막는다.</summary>
public readonly record struct FactionId(int Value)
{
    public override string ToString() => $"F{Value}";
}
