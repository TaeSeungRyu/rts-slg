namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>공격 세력이 현 소유 세력의 특정 도시에 선전 포고한 기록.</summary>
public sealed record CityWarDeclaration(FactionId Attacker, CityId City, FactionId Defender, int Day)
{
    public const int DurationDays = 100;
    public int ExpiresDay => Day + DurationDays;
    public bool ActiveOn(int day) => day < ExpiresDay;
}
