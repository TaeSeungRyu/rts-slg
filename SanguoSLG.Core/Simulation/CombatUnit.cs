namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 진행 루프가 다루는 한 부대의 전체 상태(이동 + 전투). 이동은 <see cref="Field"/>, 산출된 유효
/// 능력치는 <see cref="Stats"/>(패시브까지 반영·지형 보정 제외, 병력은 정산 시 <see cref="Pool"/>로 덮음),
/// 병력 구성은 <see cref="Pool"/>, 발동 지속 상태는 <see cref="State"/>. 지형 공방 보정은 전투 시점에
/// 오케스트레이터가 이동 후 위치·<see cref="Class"/>로 얹는다. LootGold는 약탈 노획 금(무제한 휴대,
/// 아군 성 입성 시 예치 — design-administration "시설 파괴·약탈"). 불변 값.
/// </summary>
public sealed record CombatUnit(
    FieldUnit Field,
    CombatStats Stats,
    TroopPool Pool,
    UnitCombatState State,
    int Might = 60,
    int Intellect = 60,
    int MaxTroops = 0,
    TroopClass Class = TroopClass.Infantry,
    int Provisions = -1,
    int ProvisionsCapacity = 300,
    bool IsSupply = false,
    int Training = 50,
    string TroopCode = "",
    GeneralId? VanguardId = null,
    GeneralId? AdjutantId = null,
    IReadOnlyList<SupplyComponent>? SupplyCargo = null,
    UnitId? ReinforceTarget = null,
    int LootGold = 0)
{
    public UnitId Id => Field.Id;

    /// <summary>보급부대의 병종별 구성(일반 부대는 빈 목록). design-unit-state 1단계-보급.</summary>
    public IReadOnlyList<SupplyComponent> Cargo => SupplyCargo ?? [];

    /// <summary>군량을 추적하는 부대인가(−1 = 미추적 = 무한 보급 가정 — 전술 하베스트·단발 전투용).</summary>
    public bool TracksProvisions => Provisions >= 0;

    /// <summary>이 부대의 최대 휴대 군량(적재능력 × 병력 비례, 보급부대는 ×배수). design-unit-state 1단계-보급.</summary>
    public int MaxProvisions(int supplyMultiplier = 5)
        => ProvisionsCapacity * Pool.Active / 10000 * (IsSupply ? supplyMultiplier : 1);
}
