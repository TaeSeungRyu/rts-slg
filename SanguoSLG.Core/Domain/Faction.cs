namespace SanguoSLG.Core.Domain;

/// <summary>
/// 세력. 군주와 국고(자금)를 가진 불변 값.
/// 상태 변경은 명시적 메서드를 통해서만 이뤄진다.
/// </summary>
public sealed record Faction(
    FactionId Id,
    string Name,
    GeneralId Ruler,
    int Gold)
{
    /// <summary>자금을 더한(또는 뺀) 새 세력을 반환한다.</summary>
    public Faction AddGold(int amount) => this with { Gold = Gold + amount };
}
