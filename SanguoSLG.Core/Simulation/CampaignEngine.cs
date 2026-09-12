namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 캠페인 진행(2026-08-16 확정): **진행 버튼 1번 = 7일 고정.** 야전(AdvanceOrchestrator)이
/// 접적으로 멈춰도 7일이 찰 때까지 자동 재개해 이동+전투가 계속되고(일 단위 교전 반복),
/// 내정(WorldEngine)도 같은 7일을 흐른다 — 두 세계가 한 시계를 쓴다. 판단 기회는 주 단위라
/// 한 번의 명령이 무겁다(design-movement "캠페인 해석").
/// 입성 부대는 도시 대기 병력(GarrisonForce)으로 편입된다.
/// </summary>
public sealed class CampaignEngine
{
    /// <summary>진행 1번의 길이(일) — 7일 고정(2026-08-16 확정).</summary>
    public const int WeekDays = 7;
    private const int ProductionUnitIdBase = -1_000_000;

    private readonly AdvanceOrchestrator _field;
    private readonly WorldEngine _world;

    /// <summary>직전 <see cref="AdvanceWeek(GameState, out IReadOnlyList{AdvanceTurn})"/>의 내정/라이프사이클 사건(보고용).</summary>
    public IReadOnlyList<WorldEvent> LastWorldEvents => _campaignEvents.Concat(_world.LastEvents).ToList();
    private readonly List<WorldEvent> _campaignEvents = [];

    private readonly CampaignSiege? _siege;
    private readonly CityCapture? _capture;
    private readonly CityPlunder? _plunder;
    private readonly IReadOnlyDictionary<string, PassiveSkill> _passives;
    private readonly IReadOnlyDictionary<string, ActiveSkill> _actives;
    private readonly IRandomSource _random;
    private readonly int _cityResupplyRadius;
    private readonly int _buildSiteHp;
    private readonly int _buildSiteDamagePerTurn;

    public CampaignEngine(AdvanceOrchestrator field, WorldEngine world,
        CampaignSiege? siege = null, CityCapture? capture = null, IRandomSource? random = null,
        CityPlunder? plunder = null, int cityResupplyRadius = 0,
        int buildSiteHp = 0, int buildSiteDamagePerTurn = 0,
        IReadOnlyList<PassiveSkill>? passives = null, IReadOnlyList<ActiveSkill>? actives = null)
    {
        _field = field;
        _world = world;
        _siege = siege;
        _capture = capture;
        _plunder = plunder;
        _passives = (passives ?? []).ToDictionary(p => p.Code);
        _actives = (actives ?? []).ToDictionary(a => a.Code);
        _random = random ?? new SeededRandomSource(0);
        _cityResupplyRadius = cityResupplyRadius;
        _buildSiteHp = buildSiteHp;
        _buildSiteDamagePerTurn = buildSiteDamagePerTurn;
    }

    /// <summary>7일을 진행한 새 상태를 반환한다. 야전 진행 보고 목록은 <paramref name="turns"/>로.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns)
        => AdvanceWeek(state, out turns, out _, out _);

    /// <summary>7일 진행 + 공성 교환 보고(<paramref name="sieges"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges)
        => AdvanceWeek(state, out turns, out sieges, out _);

    /// <summary>7일 진행 + 공성 교환 + 함락 보고(<paramref name="captures"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges, out IReadOnlyList<CaptureReport> captures)
        => AdvanceWeek(state, out turns, out sieges, out captures, out _);

    /// <summary>7일 진행 + 공성·함락·약탈 보고(<paramref name="plunders"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges, out IReadOnlyList<CaptureReport> captures,
        out IReadOnlyList<PlunderReport> plunders)
        => AdvanceWeek(state, out turns, out sieges, out captures, out plunders, out _);

    /// <summary>야전 전멸 장수 처리 보고(<paramref name="casualties"/>)까지 — design-general-lifecycle §4b.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges, out IReadOnlyList<CaptureReport> captures,
        out IReadOnlyList<PlunderReport> plunders, out IReadOnlyList<CasualtyReport> casualties)
    {
        var reports = new List<AdvanceTurn>();
        var siegeReports = new List<SiegeExchange>();
        var captureReports = new List<CaptureReport>();
        var plunderReports = new List<PlunderReport>();
        var casualtyReports = new List<CasualtyReport>();
        _campaignEvents.Clear();
        var work = state;
        var armies = state.Armies.Where(u => u.Pool.Active > 0).ToList();

        var remaining = WeekDays;
        while (remaining > 0 && armies.Count > 0)
        {
            // 성 접적 정지용 장애물 — 매 진행마다 현재 소유로 갱신(함락으로 주인이 바뀌므로).
            var castles = work.Cities
                .OrderBy(c => c.Id.Value)
                .Select(c => new SiegeSite(c.Position, c.Owner))
                .ToList();
            var cityAt = work.Cities.ToDictionary(c => c.Position, c => c.Id);

            // 성 보급(2026-08-20): 이동 전, 아군 성 반경 안의 아군 야전 부대 군량을 성 비축에서 채운다
            //  — 성문 앞 대기·수비 부대가 굶지 않도록(보급부대와 같은 원리, 성이 고정 보급원).
            if (_cityResupplyRadius > 0)
            {
                (work, armies) = ResupplyFromCities(work, armies);
            }

            var productionUnits = ProductionCombatUnits(work);
            var productionUnitIds = productionUnits.Select(u => u.Id).ToHashSet();
            var turnInput = productionUnits.Count == 0 ? armies : armies.Concat(productionUnits).ToList();

            var turn = _field.Run(turnInput, maxDays: remaining, castles);
            // 생산 대상은 저장용 야전 부대에서 제거해도 공격 모션의 목표 위치는 보존한다.
            var attackedProduction = productionUnits.Where(u =>
                turn.Combat?.DamageTaken.GetValueOrDefault(u.Id) > 0).ToList();
            turn = turn with
            {
                ProductionAttackTargets = turn.Units
                    .Where(u => turn.Combat?.DamageDealt.GetValueOrDefault(u.Id) > 0)
                    .Select(u => (Unit: u, Target: attackedProduction
                        .Where(p => p.Field.Owner != u.Field.Owner
                            && p.Field.Position.Distance(u.Field.Position) <= u.Field.AttackRange)
                        .OrderBy(p => p.Field.Position.Distance(u.Field.Position)).FirstOrDefault()))
                    .Where(x => x.Target is not null)
                    .ToDictionary(x => x.Unit.Id, x => x.Target!.Field.Position),
            };
            var hitProductionIds = HitProductionUnits(turn, productionUnitIds);
            if (hitProductionIds.Count > 0)
            {
                turn = turn with
                {
                    LostProductionPositions = productionUnits
                        .Where(u => hitProductionIds.Contains(u.Id))
                        .ToDictionary(u => u.Id, u => u.Field.Position),
                };
                work = RemoveHitProductionOperations(work, hitProductionIds);
            }

            turn = StripProductionUnits(turn, productionUnitIds);
            reports.Add(turn);
            remaining -= System.Math.Max(1, turn.Movement.Days);

            // 야전 전멸 장수 처리(§4b): 이 조각에서 사라진 부대(입성 제외)의 선봉·부관 판정.
            // 교전 사망(피해 기록 있음)이면 최근접 적 부대의 세력이 포획 후보, 아니면 100% 탈출.
            var survivors = turn.Units.Select(u => u.Id).ToHashSet();
            var enteredNow = turn.EnteredCastle.Select(u => u.Id).ToHashSet();
            var movedPos = turn.Movement.Units.ToDictionary(f => f.Id, f => f.Position);
            foreach (var dead in armies
                .Where(u => !survivors.Contains(u.Id) && !enteredNow.Contains(u.Id))
                .OrderBy(u => u.Id.Value))
            {
                var at = movedPos.TryGetValue(dead.Id, out var mp) ? mp : dead.Field.Position;
                FactionId? captor = null;
                if (turn.Combat is { } cbt && cbt.DamageTaken.ContainsKey(dead.Id))
                {
                    captor = turn.Units
                        .Where(o => o.Field.Owner != dead.Field.Owner)
                        .OrderBy(o => o.Field.Position.Distance(at)).ThenBy(o => o.Id.Value)
                        .Select(o => (FactionId?)o.Field.Owner)
                        .FirstOrDefault();
                }

                work = FieldCasualties.ResolveUnit(work, dead, captor, at, _random, casualtyReports);
            }

            armies = turn.Units.ToList();

            work = ApplyEntered(work, turn.EnteredCastle, cityAt);

            // 공성 교환(design-combat "성 전투") — 접적으로 멈춘 공격 부대가 성벽·수비를 깎는다.
            if (_siege is not null)
            {
                var siegeState = work;
                var activeChargeDays = System.Math.Max(1, turn.Movement.Days);
                var result = _siege.Resolve(armies, siegeState.Cities, siegeState.Garrisons, CounterAptitude, DefenseBonus, siegeState.CityWounded);

                // 성 반격/방어 보정 = 태수 또는 대리 수성 지휘관 1명의 적성·스킬만 반영한다.
                int CounterAptitude(CityId cid)
                    => SiegeDefensePercents(siegeState, cid).CounterPercent;

                int DefenseBonus(CityId cid)
                {
                    var percents = SiegeDefensePercents(siegeState, cid);
                    return ApplyDefenseActiveBonus(siegeState, cid, percents.DefensePercent, activeChargeDays);
                }

                // 성 반격으로 전멸한 공성 부대의 장수 판정(§4b) — 포획 후보 = 그 성의 소유 세력.
                foreach (var dead in result.Armies.Where(u => u.Pool.Active <= 0).OrderBy(u => u.Id.Value))
                {
                    var ex = result.Exchanges.FirstOrDefault(e => e.Besiegers.Contains(dead.Id));
                    FactionId? captor = ex is null ? null : work.Cities.First(c => c.Id == ex.City).Owner;
                    work = FieldCasualties.ResolveUnit(work, dead, captor, dead.Field.Position, _random, casualtyReports);
                }

                armies = result.Armies.Where(u => u.Pool.Active > 0).ToList();
                var attackedCities = result.Exchanges.Select(e => e.City).ToHashSet();
                work = work with { Cities = result.Cities, GarrisonForces = result.Garrisons, CityWoundedForces = result.CityWounded };
                work = UpdateDefenseCharges(work, siegeState, attackedCities, activeChargeDays);
                work = RecoverCityWounded(work, attackedCities);
                // 어느 진행 조각의 공성인지 스탬프 — 표현 계층의 재생 타이밍용.
                siegeReports.AddRange(result.Exchanges.Select(e => e with { TurnIndex = reports.Count - 1 }));
            }

            // 약탈(design-administration "시설 파괴·약탈") — 포위군이 진행마다 시설 1개 파괴·노획.
            if (_plunder is not null)
            {
                var looted = _plunder.Resolve(armies, work.Cities);
                armies = looted.Armies.ToList();
                work = work with { Cities = looted.Cities };
                plunderReports.AddRange(looted.Reports);
            }

            // 공사장 피해(2026-08-27) — 공사 중 시설은 병력 1000짜리 무방비 목표. 아군·적군 가리지 않고
            // 인접(거리1) 부대가 매 진행 공격하고(공사는 반격 없음), 체력이 다 깎이면 건설이 취소된다.
            if (_buildSiteHp > 0 && _buildSiteDamagePerTurn > 0)
            {
                work = DamageConstructionSites(work, armies);
            }

            // 함락 처리(design-general-lifecycle §4) — 성벽0+수비0에 근접 공격군이 있으면 점거.
            if (_capture is not null)
            {
                work = _capture.ResolveAll(work with { FieldArmies = armies }, _random, out var caps);
                armies = work.Armies.Where(u => u.Pool.Active > 0).ToList();
                captureReports.AddRange(caps);
            }
        }

        var afterField = work with
        {
            FieldArmies = armies,
            GarrisonForces = work.Garrisons
                .Where(g => g.Troops > 0)
                .OrderBy(g => g.City.Value).ThenBy(g => g.TroopCode, System.StringComparer.Ordinal).ThenBy(g => g.Trainee)
                .ToList(),
        };

        turns = reports;
        sieges = siegeReports;
        captures = captureReports;
        plunders = plunderReports;
        casualties = casualtyReports;
        return _world.AdvanceDays(afterField, WeekDays);
    }

    private static List<CombatUnit> ProductionCombatUnits(GameState work)
        => work.ProductionOps
            .OrderBy(o => o.Id)
            .Select(o =>
            {
                var id = ProductionUnitId(o.Id);
                var combatPosition = o.Phase == ProductionPhase.Returning ? o.Position : o.Target;
                return new CombatUnit(
                    new FieldUnit(id, o.Owner, combatPosition,
                        Speed: 0, Detection: 0, AttackRange: 0, MovementDomain.Land, UnitMode.March,
                        Target: null, CommandOrder: ProductionUnitIdBase + o.Id),
                    new CombatStats(Troops: o.Troops, AtkStat: 0, DfStat: 1),
                    new TroopPool(o.Troops, 0),
                    UnitCombatState.Create(0),
                    Might: 1,
                    Intellect: 1,
                    MaxTroops: o.Troops,
                    TroopCode: o.TroopCode,
                    Training: o.TrainingLevel);
            })
            .ToList();

    private static UnitId ProductionUnitId(int operationId) => new(ProductionUnitIdBase + operationId);

    private static int ProductionOperationId(UnitId unitId) => unitId.Value - ProductionUnitIdBase;

    private static HashSet<UnitId> HitProductionUnits(AdvanceTurn turn, HashSet<UnitId> productionUnitIds)
    {
        var survivors = turn.Units.ToDictionary(u => u.Id);
        return productionUnitIds
            .Where(id => turn.Combat?.DamageTaken.GetValueOrDefault(id) > 0
                || !survivors.TryGetValue(id, out var unit)
                || unit.Pool.Active < ProductionOperation.FixedTroops)
            .ToHashSet();
    }

    private GameState RemoveHitProductionOperations(GameState work, HashSet<UnitId> hitProductionIds)
    {
        var hitOps = hitProductionIds.Select(ProductionOperationId).ToHashSet();
        foreach (var op in work.ProductionOps.Where(o => hitOps.Contains(o.Id)).OrderBy(o => o.Id))
        {
            _campaignEvents.Add(new WorldEvent(WorldEventKind.ProductionLost, op.Owner, op.General, op.City,
                op.Troops, op.Facility));
        }

        return work with { ProductionOperations = work.ProductionOps.Where(o => !hitOps.Contains(o.Id)).ToList() };
    }

    private static AdvanceTurn StripProductionUnits(AdvanceTurn turn, HashSet<UnitId> productionUnitIds)
    {
        if (productionUnitIds.Count == 0) { return turn; }
        var movement = turn.Movement with
        {
            Units = turn.Movement.Units.Where(u => !productionUnitIds.Contains(u.Id)).ToList(),
            Ticks = turn.Movement.Ticks
                .Select(t => t with
                {
                    Units = t.Units.Where(u => !productionUnitIds.Contains(u.Id)).ToList(),
                    Events = t.Events.Where(e => !productionUnitIds.Contains(e.Unit)
                        && (e.Other is null || !productionUnitIds.Contains(e.Other.Value))).ToList(),
                })
                .ToList(),
            Entered = turn.Movement.EnteredCastle.Where(id => !productionUnitIds.Contains(id)).ToList(),
        };

        return turn with
        {
            Units = turn.Units.Where(u => !productionUnitIds.Contains(u.Id)).ToList(),
            Movement = movement,
            Combat = StripProductionCombat(turn.Combat, productionUnitIds),
            Entered = turn.EnteredCastle.Where(u => !productionUnitIds.Contains(u.Id)).ToList(),
            FiredActives = turn.FiredActives.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            FiredStratagems = turn.FiredStratagems.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            StatusDamage = turn.StatusDamage.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            StratagemDamage = turn.StratagemDamage.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            StarvationLoss = turn.Starvation.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            ReinforcedTroops = turn.Reinforced.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
        };
    }

    private static CombatPhaseResult? StripProductionCombat(CombatPhaseResult? combat, HashSet<UnitId> productionUnitIds)
    {
        if (combat is null) { return null; }
        return combat with
        {
            DamageTaken = combat.DamageTaken.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            DamageDealt = combat.DamageDealt.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            Pools = combat.Pools.Where(kv => !productionUnitIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
        };
    }

    // 공사장 피해: 적대 세력 부대만 공사를 '유닛'으로 보고 공격한다(내 군대는 적 공사를, 적 군대는 내
    // 공사를 친다). 인접(거리1) 적 부대가 매 진행 피해를 입히고, 누적 피해가 체력을 넘으면 그 건설
    // 명령을 취소한다(예약 자원·인력은 환불하지 않는다). 결정론: 명령 순서 유지.
    private GameState DamageConstructionSites(GameState work, List<CombatUnit> armies)
    {
        var cmds = work.Commands.ToList();
        var changed = false;
        for (var i = cmds.Count - 1; i >= 0; i--)
        {
            var c = cmds[i];
            if (c.Kind != CommandKind.Build || c.Plot is not { } plot)
            {
                continue;
            }

            // 공사 세력 = 그 도시의 현재 소유. 도시가 사라졌으면(함락 등) 공사도 방치 — 피해 없음.
            var owner = work.Cities.FirstOrDefault(cc => cc.Id == c.City)?.Owner;
            if (owner is null)
            {
                continue;
            }

            var attackers = armies.Count(u => u.Field.Owner != owner && u.Field.Position.Distance(plot) <= 1);
            if (attackers == 0)
            {
                continue;
            }

            changed = true;
            var dmg = c.SiteDamage + (attackers * _buildSiteDamagePerTurn);
            if (dmg >= _buildSiteHp)
            {
                cmds.RemoveAt(i); // 공사장 파괴 → 건설 취소
            }
            else
            {
                cmds[i] = c with { SiteDamage = dmg };
            }
        }

        return changed ? work with { PendingCommands = cmds } : work;
    }

    // 성 보급: 아군 성 반경(_cityResupplyRadius) 안의 아군 야전 부대(군량 추적) 군량을 성 비축에서
    // 최대치까지 채운다. 성 비축 한도 안에서만. 결정론: 성 id 오름차순, 부대 id 오름차순.
    private (GameState, List<CombatUnit>) ResupplyFromCities(GameState work, List<CombatUnit> armies)
    {
        var byId = armies.ToDictionary(u => u.Id);
        var stock = work.Cities.ToDictionary(c => c.Id, c => c.Provisions);
        foreach (var city in work.Cities.OrderBy(c => c.Id.Value))
        {
            var have = stock[city.Id];
            if (have <= 0)
            {
                continue;
            }

            foreach (var unit in armies
                .Where(u => u.Field.Owner == city.Owner && u.TracksProvisions
                    && u.Field.Position.Distance(city.Position) <= _cityResupplyRadius)
                .OrderBy(u => u.Id.Value))
            {
                var cur = byId[unit.Id];
                var deficit = cur.MaxProvisions() - cur.Provisions;
                var give = System.Math.Min(deficit, have);
                if (give <= 0)
                {
                    continue;
                }

                byId[unit.Id] = cur with { Provisions = cur.Provisions + give };
                have -= give;
            }

            stock[city.Id] = have;
        }

        var cities = work.Cities.Select(c => c with { Provisions = stock[c.Id] }).ToList();
        return (work with { Cities = cities }, armies.Select(u => byId[u.Id]).ToList());
    }

    // 입성 부대 → 그 도시 대기 병력 편입(병종·훈련도 보존, 가중 평균) + 실린 장수 그 도시 주둔 복귀
    // + 예치: 노획 금·잔여 휴대 군량을 성 비축에 합산(design-administration "복귀 예치").
    private int ApplyDefenseActiveBonus(GameState state, CityId cityId, int defensePercent, int elapsedDays)
    {
        var leader = SiegeDefenseCommand.Select(state, state.Cities.First(c => c.Id == cityId));
        if (leader.General is not { } generalId)
        {
            return defensePercent;
        }

        var charge = state.SiegeDefenseCharges.FirstOrDefault(c => c.City == cityId && c.Leader == generalId)?.ChargeDays ?? 0;
        if (charge + elapsedDays < CityDefenseCharge.RequiredDays)
        {
            return defensePercent;
        }

        var general = state.Generals.FirstOrDefault(g => g.Id == generalId);
        if (general?.BattleActive is not { } code || !_actives.TryGetValue(code, out var active))
        {
            return defensePercent;
        }

        if (active.Type == ActiveType.Defense)
        {
            return defensePercent * 100 / BattleResolver.DamageTakenPercent(active, general.Might);
        }

        if (active.Type == ActiveType.Strike)
        {
            return defensePercent + System.Math.Max(0, active.DamageMultPercent - 100) / 2;
        }

        if (active.Type == ActiveType.Heal)
        {
            return defensePercent + System.Math.Max(0, active.HealPercent);
        }

        return defensePercent;
    }

    private static GameState RecoverCityWounded(GameState state, IReadOnlySet<CityId> besiegedCities)
    {
        if (state.CityWounded.Count == 0)
        {
            return state;
        }

        var garrisons = state.Garrisons.ToList();
        var wounded = state.CityWounded.ToList();
        for (var i = wounded.Count - 1; i >= 0; i--)
        {
            var w = wounded[i];
            if (besiegedCities.Contains(w.City) || w.Troops <= 0)
            {
                continue;
            }

            var recover = System.Math.Max(1, w.Troops / 10);
            recover = System.Math.Min(recover, w.Troops);
            var idx = garrisons.FindIndex(g => g.City == w.City && g.TroopCode == w.TroopCode && g.Trainee == w.Trainee);
            if (idx >= 0)
            {
                garrisons[idx] = garrisons[idx].Merge(recover, w.TrainingLevel);
            }
            else
            {
                garrisons.Add(new GarrisonForce(w.City, w.TroopCode, recover, w.TrainingLevel, w.Trainee));
            }

            wounded[i] = w with { Troops = w.Troops - recover };
            if (wounded[i].Troops <= 0)
            {
                wounded.RemoveAt(i);
            }
        }

        return state with { GarrisonForces = garrisons, CityWoundedForces = wounded };
    }

    private static GameState UpdateDefenseCharges(GameState current, GameState beforeSiege, IReadOnlySet<CityId> attackedCities, int elapsedDays)
    {
        var next = new List<CityDefenseCharge>();
        foreach (var city in current.Cities.OrderBy(c => c.Id.Value))
        {
            if (!attackedCities.Contains(city.Id))
            {
                continue;
            }

            var leader = SiegeDefenseCommand.Select(beforeSiege, city).General;
            var previous = beforeSiege.SiegeDefenseCharges.FirstOrDefault(c => c.City == city.Id && c.Leader == leader)?.ChargeDays ?? 0;
            next.Add(new CityDefenseCharge(city.Id, leader, System.Math.Min(CityDefenseCharge.RequiredDays, previous + elapsedDays)));
        }

        return current with { DefenseCharges = next };
    }

    private (int CounterPercent, int DefensePercent, SiegeDefenseLeader Leader) SiegeDefensePercents(GameState state, CityId cityId)
    {
        var city = state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null)
        {
            return (100, 100, new SiegeDefenseLeader(cityId, null, false, AptitudeGrade.F, 0, 0, ""));
        }

        var leader = SiegeDefenseCommand.Select(state, city);
        if (leader.General is not { } generalId)
        {
            return (100, 100, leader);
        }

        var general = state.Generals.FirstOrDefault(g => g.Id == generalId);
        if (general is null)
        {
            return (100, 100, leader);
        }

        var held = new List<(PassiveSkill Skill, int Tier)>();
        foreach (var generalSkill in general.Passives)
        {
            if (_passives.TryGetValue(generalSkill.Code, out var skill))
            {
                held.Add((skill, generalSkill.Tier));
            }
        }
        var context = new CombatContext(InCastle: true, InField: false, IncomingMelee: true, IncomingRanged: true);
        var (passiveAtk, passiveDf) = PassiveBucketEvaluator.Evaluate(held, context);
        var aptitude = general.AptitudeFor(TroopClass.Defense).Percent();
        var counter = StatScale.Percent(general.Might) * aptitude / 100 * passiveAtk / 100;
        var defense = aptitude * passiveDf / 100;
        return (System.Math.Max(25, counter), System.Math.Max(25, defense), leader);
    }

    private static GameState ApplyEntered(GameState work, IReadOnlyList<CombatUnit> entered,
        IReadOnlyDictionary<Spatial.HexCoord, CityId> cityAt)
    {
        if (entered.Count == 0)
        {
            return work;
        }

        var garrisons = work.Garrisons.ToList();
        var postings = work.Assignments.ToList();
        var cities = work.Cities.ToList();
        foreach (var unit in entered)
        {
            if (unit.Field.Target is not { } pos || !cityAt.TryGetValue(pos, out var cityId))
            {
                continue;
            }

            var gold = unit.CarryingGold;
            var provisions = unit.TracksProvisions ? unit.Provisions : 0;
            if (gold > 0 || provisions > 0)
            {
                var cIdx = cities.FindIndex(c => c.Id == cityId);
                if (cIdx >= 0)
                {
                    cities[cIdx] = cities[cIdx] with
                    {
                        Gold = cities[cIdx].Gold + gold,
                        Provisions = cities[cIdx].Provisions + provisions,
                    };
                }
            }

            var incoming = (unit.IsSupply || unit.IsTransport) && unit.Cargo.Count > 0
                ? unit.Cargo.Select(c => (c.TroopCode, c.Troops, Training: c.TrainingLevel))
                : unit.TroopCode.Length > 0
                    ? [(unit.TroopCode, unit.Pool.Active, Training: unit.Training)]
                    : [];
            foreach (var (code, troops, training) in incoming)
            {
                if (troops <= 0)
                {
                    continue;
                }

                // 훈련도 50 미만이면 신병 풀로(방어적 — 현 규칙상 야전 부대는 50 이상).
                var trainee = training < 50;
                var idx = garrisons.FindIndex(g => g.City == cityId && g.TroopCode == code && g.Trainee == trainee);
                if (idx >= 0)
                {
                    garrisons[idx] = garrisons[idx].Merge(troops, training);
                }
                else
                {
                    garrisons.Add(new GarrisonForce(cityId, code, troops, training, trainee));
                }
            }

            foreach (var generalId in new[] { unit.VanguardId, unit.AdjutantId }.OfType<GeneralId>())
            {
                var pIdx = postings.FindIndex(p => p.General == generalId);
                if (pIdx >= 0)
                {
                    postings[pIdx] = postings[pIdx] with { Location = cityId };
                }
            }
        }

        return work with { GarrisonForces = garrisons, Postings = postings, Cities = cities };
    }
}
