namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 세력 AI 최소판(12단계 "모집·출전·공성 판단"). 한 세력의 한 주 결정을 결정론적으로 내린다:
/// ① 야전 공격 부대를 가장 가까운 적 성으로 재조준(멈춘·무효 목표 복구) → ② 도시별(id순) 장수
/// 1명으로 — 대기 병력이 문턱 이상이고 도시에 장수가 남으면 최근접 적 성으로 대군 출전, 아니면
/// 여력만큼 모집. 관전 캠페인에서 수렴을 검증한 휴리스틱을 Core로 승격했다. 정식 확장(내정·연구·
/// 방어 판단)은 후속. 결정론: 세력·도시 id순·문턱값, 난수 없음(함락 판정만 상위 시드 난수).
/// </summary>
public sealed class FactionAI
{
    private readonly CommandService _commands;
    private readonly DeployService _deployer;
    private readonly AiConfig _config;

    public FactionAI(CommandService commands, DeployService deployer, AiConfig? config = null)
    {
        _commands = commands;
        _deployer = deployer;
        _config = config ?? new AiConfig();
    }

    /// <summary>이 세력의 한 주 명령·출전을 반영한 새 상태를 반환한다.</summary>
    public GameState PlanWeek(GameState state, FactionId faction)
    {
        state = Retarget(state, faction);
        state = RecruitUnlockedHeroes(state, faction);
        state = PlanSupplyDeploys(state, faction);
        state = PlanArmyGroupDeploys(state, faction);
        state = PlanGeneralResearch(state, faction);

        foreach (var city in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList())
        {
            var free = state.GeneralsAt(city.Id)
                .Where(g => !state.IsGeneralBusy(g))
                .OrderBy(g => g.Value)
                .ToList();
            if (free.Count == 0)
            {
                continue;
            }

            var gid = free[0];
            var garrison = state.Garrisons
                .Where(g => g.City == city.Id && g.TroopCode == _config.Troop)
                .Sum(g => g.Troops);

            if (garrison >= _config.DeployTarget && free.Count > _config.KeepGeneralsHome)
            {
                var target = NearestEnemyCity(state, faction, city.Position);
                if (target is { } dest)
                {
                    var result = _deployer.Deploy(state, new DeployRequest(
                        city.Id, _config.Troop, System.Math.Min(garrison, _config.DeploySize),
                        gid, Mode: UnitMode.Attack, Target: dest));
                    if (result.Ok)
                    {
                        state = result.State;
                    }
                }
            }
        }

        state = ExploreWithIdleOfficers(state, faction);
        return state;
    }

    private GameState PlanGeneralResearch(GameState state, FactionId faction)
    {
        if (_config.GeneralResearchReserveGold < 0
            || state.Commands.Any(c => c.Kind == CommandKind.Research
                && FactionResearch.IsGeneralResearch(c.TroopCode)
                && state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == faction))
        {
            return state;
        }

        var cities = state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList();
        if (cities.Count == 0)
        {
            return state;
        }

        var priority = new List<string>();
        if (cities.Average(c => c.Security) <= GeneralResearchRules.DefaultLowSecurityThreshold)
        {
            priority.Add(FactionResearch.PublicOrderCode);
        }
        if (state.CityWounded.Any(w => cities.Any(c => c.Id == w.City) && w.Troops > 0))
        {
            priority.Add(FactionResearch.MedicineCode);
        }
        priority.AddRange(
        [
            FactionResearch.CommerceCode,
            FactionResearch.AgricultureCode,
            FactionResearch.ConscriptionCode,
            FactionResearch.TrainingCode,
            FactionResearch.PublicOrderCode,
            FactionResearch.MedicineCode,
        ]);
        var code = priority.Distinct()
            .Where(c => state.ResearchOf(faction, c) < GeneralResearchRules.MaxLevel)
            .OrderBy(c => state.ResearchOf(faction, c))
            .ThenBy(c => priority.IndexOf(c))
            .FirstOrDefault();
        if (code is null)
        {
            return state;
        }

        var level = state.ResearchOf(faction, code);
        var cost = GeneralResearchRules.Cost(level + 1, _commands.Balance);
        if (cities.Sum(c => c.Gold) < cost + _config.GeneralResearchReserveGold)
        {
            return state;
        }

        var candidates = cities
            .Select(c => new
            {
                City = c,
                Officers = state.GeneralsAt(c.Id)
                    .Where(g => !state.IsGeneralBusy(g))
                    .Select(id => state.Generals.First(g => g.Id == id))
                    .OrderByDescending(g => g.Intellect)
                    .ThenBy(g => g.Id.Value)
                    .ToList(),
            })
            .Where(x => x.Officers.Count > _config.KeepGeneralsHome)
            .OrderByDescending(x => x.Officers[0].Intellect)
            .ThenBy(x => x.City.Id.Value)
            .ToList();
        if (candidates.Count == 0)
        {
            return state;
        }

        var funding = cities.Where(c => c.Gold > 0)
            .Select(c => new ResearchFundingShare(c.Id, c.Gold))
            .ToList();
        var selected = candidates[0];
        var result = _commands.Issue(state, new CommandRequest(selected.City.Id, CommandKind.Research,
            selected.Officers[0].Id, TroopCode: code, ResearchFunding: funding));
        return result.Ok ? result.State : state;
    }

    private GameState PlanArmyGroupDeploys(GameState state, FactionId faction)
    {
        if (_config.ArmyGroupDeployTarget <= 0 || _config.ArmyGroupMinFreeGenerals <= 0)
        {
            return state;
        }

        foreach (var city in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList())
        {
            if (state.Armies.Any(u => u.IsArmyGroup && u.OriginCity == city.Id))
            {
                continue;
            }

            var free = state.GeneralsAt(city.Id)
                .Where(g => !state.IsGeneralBusy(g))
                .Select(id => state.Generals.First(g => g.Id == id))
                .OrderByDescending(ArmyGroupAptitude)
                .ThenByDescending(g => g.Might)
                .ThenBy(g => g.Id.Value)
                .ToList();
            if (free.Count < _config.ArmyGroupMinFreeGenerals)
            {
                continue;
            }

            var target = NearestEnemyCityInfo(state, faction, city.Position);
            if (target is null)
            {
                continue;
            }

            var ownMainTroops = state.Garrisons
                .Where(g => g.City == city.Id && g.TroopCode == _config.Troop && !g.Trainee)
                .Sum(g => g.Troops);
            if (target.Value.GarrisonTroops <= ownMainTroops)
            {
                continue;
            }

            var eligible = state.Garrisons
                .Where(g => g.City == city.Id && g.Troops > 0 && !g.Trainee)
                .Join(_deployer.Troops.Values, g => g.TroopCode, t => t.Code, (g, t) => (Garrison: g, Troop: t))
                .Where(x => x.Troop.Class is TroopClass.Infantry or TroopClass.Archer or TroopClass.Siege)
                .OrderBy(x => x.Troop.Code, System.StringComparer.Ordinal)
                .ToList();
            if (eligible.Sum(x => x.Garrison.Troops) < _config.ArmyGroupDeployTarget)
            {
                continue;
            }

            var vanguard = free.FirstOrDefault(g => !HasStandaloneEliteAptitude(g));
            if (vanguard is null)
            {
                continue;
            }

            var adjutant = free.FirstOrDefault(g => g.Id != vanguard.Id);
            var maxTroops = CommandEfficiency.ArmyGroupDeployLimit(
                state.ResearchOf(city.Owner, FactionResearch.ArmyGroupCode), _deployer.Balance);
            var lines = HalfArmyGroupLines(eligible, maxTroops);
            var result = _deployer.DeployArmyGroup(state, new ArmyGroupDeployRequest(
                city.Id, lines, vanguard.Id, adjutant?.Id, UnitMode.Attack, target.Value.City.Position));
            if (result.Ok)
            {
                state = result.State;
            }
        }

        return state;
    }

    private GameState PlanSupplyDeploys(GameState state, FactionId faction)
    {
        if (_config.SupplyDeployTarget <= 0 || _config.SupplyDeploySize <= 0)
        {
            return state;
        }

        foreach (var city in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList())
        {
            var free = state.GeneralsAt(city.Id)
                .Where(g => !state.IsGeneralBusy(g))
                .Select(id => state.Generals.First(g => g.Id == id))
                .OrderByDescending(g => g.AptitudeFor(TroopClass.Supply))
                .ThenBy(g => g.Id.Value)
                .ToList();
            if (free.Count <= _config.KeepGeneralsHome)
            {
                continue;
            }

            var garrison = state.Garrisons
                .Where(g => g.City == city.Id && g.TroopCode == _config.Troop && !g.Trainee)
                .Sum(g => g.Troops);
            if (garrison < _config.SupplyDeployTarget)
            {
                continue;
            }

            var target = NearestEnemyCity(state, faction, city.Position);
            if (target is not { } dest)
            {
                continue;
            }

            var vanguard = free[0];
            var result = _deployer.DeploySupply(state, new SupplyDeployRequest(
                city.Id,
                [new SupplyLine(_config.Troop, System.Math.Min(garrison, _config.SupplyDeploySize))],
                vanguard.Id,
                UnitMode.Attack,
                dest));
            if (result.Ok)
            {
                state = result.State;
            }
        }

        return state;
    }

    private GameState ExploreWithIdleOfficers(GameState state, FactionId faction)
    {
        if (!state.Factions.Any(f => f.Id == faction))
        {
            return state;
        }

        foreach (var city in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList())
        {
            if (state.Commands.Any(c => c.City == city.Id && c.Kind == CommandKind.Explore))
            {
                continue;
            }

            var free = state.GeneralsAt(city.Id)
                .Where(g => !state.IsGeneralBusy(g))
                .OrderBy(g => g.Value)
                .ToList();
            if (free.Count <= _config.KeepGeneralsHome)
            {
                continue;
            }

            var result = _commands.Issue(state, new CommandRequest(city.Id, CommandKind.Explore, free[0]));
            if (result.Ok)
            {
                state = result.State;
            }
        }

        return state;
    }

    private GameState RecruitUnlockedHeroes(GameState state, FactionId faction)
    {
        state = new HeroUnlockService().Evaluate(state);
        foreach (var heroState in state.HeroStates
            .Where(s => s.CanRecruit && s.EligibleFaction == faction)
            .OrderBy(s => state.HeroUnlocks.FirstOrDefault(h => h.General == s.General)?.RecruitGold ?? int.MaxValue)
            .ThenBy(s => s.General.Value)
            .ToList())
        {
            var hero = state.HeroUnlocks.FirstOrDefault(h => h.General == heroState.General);
            if (hero is null || !hero.AiCanRecruit)
            {
                continue;
            }

            var city = state.Cities
                .Where(c => c.Owner == faction && c.Gold >= hero.RecruitGold)
                .OrderByDescending(c => c.Gold)
                .ThenBy(c => c.Id.Value)
                .FirstOrDefault();
            if (city is null)
            {
                continue;
            }

            var actor = city.Governor
                ?? state.Assignments.FirstOrDefault(p => p.Location == city.Id && p.Faction == faction)?.General
                ?? state.Factions.FirstOrDefault(f => f.Id == faction)?.Ruler;
            if (actor is null)
            {
                continue;
            }

            var result = _commands.Issue(state,
                new CommandRequest(city.Id, CommandKind.RecruitHero, actor.Value, TargetGeneral: hero.General));
            if (result.Ok)
            {
                state = result.State;
            }
        }

        return state;
    }

    // 야전 공격 부대를 가장 가까운 적 성으로 재조준(멈춘 부대·무효 목표 복구).
    private static GameState Retarget(GameState state, FactionId faction)
    {
        var armies = state.Armies.Select(u =>
        {
            if (u.Field.Owner != faction || u.Field.Mode != UnitMode.Attack)
            {
                return u;
            }

            var target = NearestEnemyCity(state, faction, u.Field.Position);
            return target is { } dest ? u with { Field = u.Field with { Target = dest } } : u;
        }).ToList();
        return state with { FieldArmies = armies };
    }

    private static HexCoord? NearestEnemyCity(GameState state, FactionId self, HexCoord from)
        => state.Cities.Where(c => c.Owner != self)
            .OrderBy(c => c.Position.Distance(from)).ThenBy(c => c.Id.Value)
            .Select(c => (HexCoord?)c.Position)
            .FirstOrDefault();

    private static (City City, int GarrisonTroops)? NearestEnemyCityInfo(GameState state, FactionId self, HexCoord from)
    {
        var city = state.Cities.Where(c => c.Owner != self)
            .OrderBy(c => c.Position.Distance(from))
            .ThenBy(c => c.Id.Value)
            .FirstOrDefault();
        if (city is null)
        {
            return null;
        }

        return (city, state.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops));
    }

    private static AptitudeGrade ArmyGroupAptitude(General general)
        => AptitudeGrades.AverageFloor(
            general.AptitudeFor(TroopClass.Infantry),
            general.AptitudeFor(TroopClass.Archer),
            general.AptitudeFor(TroopClass.Siege));

    private static bool HasStandaloneEliteAptitude(General general)
        => general.AptitudeFor(TroopClass.Cavalry) >= AptitudeGrade.S
            || general.AptitudeFor(TroopClass.Elephant) >= AptitudeGrade.S
            || general.AptitudeFor(TroopClass.Naval) >= AptitudeGrade.S;

    private static IReadOnlyList<SupplyLine> HalfArmyGroupLines(
        IReadOnlyList<(GarrisonForce Garrison, TroopTemplate Troop)> eligible,
        int maxTroops)
    {
        var lines = eligible
            .Select(x => new SupplyLine(x.Garrison.TroopCode, x.Garrison.Troops / 2))
            .Where(l => l.Troops > 0)
            .ToList();
        var total = lines.Sum(l => l.Troops);
        if (total <= maxTroops)
        {
            return lines;
        }

        var overflow = total - maxTroops;
        for (var i = lines.Count - 1; i >= 0 && overflow > 0; i--)
        {
            var cut = System.Math.Min(overflow, System.Math.Max(0, lines[i].Troops - 5000));
            if (cut <= 0)
            {
                continue;
            }

            lines[i] = lines[i] with { Troops = lines[i].Troops - cut };
            overflow -= cut;
        }

        return lines;
    }
}
