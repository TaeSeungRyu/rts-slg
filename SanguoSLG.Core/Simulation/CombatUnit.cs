namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 진행 루프가 다루는 한 부대의 전체 상태(이동 + 전투). 이동은 <see cref="Field"/>, 산출된 유효
/// 능력치는 <see cref="Stats"/>(패시브까지 반영·지형 보정 제외, 병력은 정산 시 <see cref="Pool"/>로 덮음),
/// 병력 구성은 <see cref="Pool"/>, 발동 지속 상태는 <see cref="State"/>. 지형 공방 보정은 전투 시점에
/// 오케스트레이터가 이동 후 위치·<see cref="Class"/>로 얹는다. 불변 값.
/// </summary>
public sealed record CombatUnit(
    FieldUnit Field,
    CombatStats Stats,
    TroopPool Pool,
    UnitCombatState State,
    int Might = 60,
    int Intellect = 60,
    int MaxTroops = 0,
    TroopClass Class = TroopClass.Infantry)
{
    public UnitId Id => Field.Id;
}
