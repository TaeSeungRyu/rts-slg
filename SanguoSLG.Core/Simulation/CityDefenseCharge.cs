namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>도시별 수성 액티브 충전 상태(Phase 11C). 적 공성이 이어진 날만 누적된다.</summary>
public sealed record CityDefenseCharge(CityId City, GeneralId? Leader, int ChargeDays)
{
    public const int RequiredDays = 5;
    public bool Ready => ChargeDays >= RequiredDays;
}
