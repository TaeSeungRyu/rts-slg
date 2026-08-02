namespace SanguoSLG.Core.Domain;

/// <summary>
/// 무장. 오6능력치(통솔·무력·지력·정치·매력)를 가진 불변 값.
/// 스켈레톤 단계에서는 데이터를 담는 그릇 수준이다.
/// </summary>
public sealed record General(
    GeneralId Id,
    string Name,
    int Leadership,
    int Might,
    int Intellect,
    int Politics,
    int Charisma);
