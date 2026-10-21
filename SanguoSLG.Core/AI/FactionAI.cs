namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 세력 AI의 주간 판단. 정찰한 적 성에 대해서만 공세 묶음을 편성하고, 현장 대응과
/// 후방 수송을 별도 계획한다. 자원 예약은 각 배치 서비스의 불변 상태 결과에 반영된다.
/// </summary>
public sealed class FactionAI
{
    private readonly CommandService _commands;
    private readonly DeployService _deployer;
    private readonly AiConfig _config;
    private readonly HexMap? _map;
    private readonly BalanceConfig _economy;

    public FactionAI(CommandService commands, DeployService deployer, AiConfig? config = null,
        HexMap? map = null, BalanceConfig? economy = null)
    {
        _commands = commands;
        _deployer = deployer;
        _config = config ?? new AiConfig();
        _map = map;
        _economy = economy ?? new BalanceConfig(MonthlyTaxPerCity: 100);
    }

    /// <summary>이 세력의 한 주 명령·출전을 반영한 새 상태를 반환한다.</summary>
    public GameState PlanWeek(GameState state, FactionId faction)
    {
        state = PlanAppointments(state, faction);
        var existingArmyIds = state.Armies.Select(unit => unit.Id).ToHashSet();
        state = state with { FieldArmies = state.Armies.Select(unit =>
            unit.Field.Owner == faction && unit.AiOpeningAttack && !unit.IsWaitingDeployment
                ? unit with { AiOpeningAttack = false,
                    Field = unit.Field with { Mode = unit.Class == TroopClass.Siege
                        || unit.AiSiegeAssemblyTarget is not null
                        ? UnitMode.Attack : UnitMode.Advance,
                        AssignedUnitTarget = null, PursuitTarget = null } }
                : unit).ToList() };
        state = CommitPressureAssault(state, faction);
        state = WithdrawPressure(state, faction);
        state = RecoverIdleExpeditions(state, faction);
        state = Retarget(state, faction);
        state = TargetUnlockRuins(state, faction);
        state = RecruitUnlockedHeroes(state, faction);
        state = new AiFieldResponsePlanner(_deployer, _config, _map, _economy).Plan(state, faction);
        state = PlanOffenseScouting(state, faction);
        state = new AiOffensePlanner(_deployer, _config, _commands, _map, _economy).Plan(state, faction);
        state = new AiRearSupportPlanner(_deployer, _map).Plan(state, faction);
        state = PlanGeneralResearch(state, faction);
        state = PlanStratagemResearch(state, faction);

        state = PlanExploration(state, faction);
        state = PlanCityOfficers(state, faction);
        state = PlanCityMaintenance(state, faction);
        state = PlanCityBaseline(state, faction);
        return state with { FieldArmies = state.Armies.Select(unit =>
            unit.Field.Owner == faction && !existingArmyIds.Contains(unit.Id)
                && unit.Field.Mode == UnitMode.Attack
                && state.Cities.Any(city => city.Position == unit.Field.Target)
                ? unit with { AiOpeningAttack = true } : unit).ToList() };
    }

    private GameState PlanAppointments(GameState state, FactionId faction)
    {
        foreach (var city in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList())
        {
            var residents = state.GeneralsAt(city.Id)
                .Where(id => state.PostingOf(id)?.Faction == faction && !state.IsGeneralInField(id))
                .Select(id => state.Generals.FirstOrDefault(g => g.Id == id))
                .Where(g => g is not null)
                .Select(g => g!)
                .OrderBy(g => g.Id.Value)
                .ToList();
            if (residents.Count == 0)
            {
                continue;
            }

            if (city.Governor is null || residents.All(g => g.Id != city.Governor))
            {
                // A vacancy is filled once; a stable per-city seed prevents weekly reshuffling.
                var seed = unchecked(state.Day * 397 ^ faction.Value * 31 ^ city.Id.Value);
                var governor = residents[new SeededRandomSource(seed).Next(0, residents.Count)];
                var result = _commands.Issue(state, new CommandRequest(city.Id, CommandKind.AppointGovernor, governor.Id));
                if (result.Ok) state = result.State;
            }

            var strategist = residents.OrderByDescending(g => g.Intellect).ThenBy(g => g.Id.Value).First();
            if (city.Strategist != strategist.Id)
            {
                var result = _commands.Issue(state, new CommandRequest(city.Id, CommandKind.AppointStrategist, strategist.Id));
                if (result.Ok) state = result.State;
            }
        }
        return state;
    }

    private GameState PlanOffenseScouting(GameState state, FactionId faction)
    {
        if (state.Commands.Any(command => command.Kind == CommandKind.CityStratagem
            && command.Facility == "scout" && state.Cities.Any(city => city.Id == command.City
                && city.Owner == faction)))
            return state;

        var options = from city in state.Cities
            where city.Owner == faction && !city.IsPort
                && state.Garrisons.Where(g => g.City == city.Id && !g.Trainee)
                    .Sum(g => g.Troops) >= 30_000
                && state.Garrisons.Any(g => g.City == city.Id && !g.Trainee
                    && g.Troops >= 1000 && _deployer.Troops.TryGetValue(g.TroopCode, out var troop)
                    && troop.Class == TroopClass.Siege)
            from target in state.Cities
            where !target.IsPort && state.AreHostile(faction, target.Owner)
                && !state.IsScouted(faction, target.Id)
                && city.Position.Distance(target.Position) <= 15
            from officer in state.GeneralsAt(city.Id)
            where !state.IsGeneralBusy(officer)
            select new { City = city, Target = target,
                Officer = state.Generals.First(g => g.Id == officer) };
        foreach (var option in options.OrderBy(x => x.City.Position.Distance(x.Target.Position))
            .ThenByDescending(x => x.Officer.Intellect)
            .ThenBy(x => x.City.Id.Value).ThenBy(x => x.Target.Id.Value))
        {
            var result = _commands.Issue(state, new CommandRequest(option.City.Id,
                CommandKind.CityStratagem, option.Officer.Id, Facility: "scout",
                TargetCity: option.Target.Id));
            if (result.Ok) return result.State;
        }
        return state;
    }

    private GameState PlanCityOfficers(GameState state, FactionId faction)
    {
        foreach (var cityId in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).Select(c => c.Id))
        {
            var city = state.Cities.First(c => c.Id == cityId);
            var residentCount = state.GeneralsAt(cityId).Count(id =>
                state.PostingOf(id)?.Faction == faction && !state.IsGeneralInField(id));
            var recruitmentRate = AiCityGrowthPolicy.RecruitmentRate(residentCount);
            var candidates = state.GeneralsAt(cityId)
                .Where(id => state.PostingOf(id)?.Faction == faction && !state.IsGeneralBusy(id))
                .Select(id => state.Generals.FirstOrDefault(g => g.Id == id))
                .Where(g => g is not null)
                .Select(g => g!)
                .Where(g => !state.Cities.Any(c => c.SecurityOfficer == g.Id || c.DomesticOfficer == g.Id
                    || c.RecruitmentOfficer == g.Id || c.TrainingOfficer == g.Id))
                .ToList();

            void Assign(CommandKind kind, Func<General, int> score, string troopCode = "")
            {
                var officer = candidates.OrderByDescending(score).ThenBy(g => g.Id.Value).FirstOrDefault();
                if (officer is null) return;
                var result = _commands.Issue(state, new CommandRequest(cityId, kind, officer.Id,
                    Value: kind == CommandKind.AppointRecruitmentOfficer ? recruitmentRate : 0,
                    TroopCode: troopCode));
                if (!result.Ok) return;
                state = result.State;
                candidates.Remove(officer);
            }

            if (city.SecurityOfficer is null)
                Assign(CommandKind.AppointSecurityOfficer, g => g.Might);
            if (city.DomesticOfficer is null)
                Assign(CommandKind.AppointDomesticOfficer, g => g.Politics);
            if (city.RecruitmentOfficer is null)
            {
                var elite = candidates.SelectMany(g => _deployer.Troops.Values
                        .Where(t => t.Class != TroopClass.Naval && g.AptitudeFor(t.Class) >= AptitudeGrade.S
                            && FactionTroopUnlock.Check(state, faction, t.Code).Allowed)
                        .Select(t => (General: g, Troop: t)))
                    .OrderByDescending(x => x.General.AptitudeFor(x.Troop.Class))
                    .ThenByDescending(x => x.General.Might)
                    .ThenBy(x => x.Troop.Code, StringComparer.Ordinal)
                    .FirstOrDefault();
                Assign(CommandKind.AppointRecruitmentOfficer,
                    g => elite.General is not null && g.Id == elite.General.Id ? int.MaxValue : g.Might,
                    elite.Troop?.Code ?? _config.Troop);
            }
            if (state.Cities.First(c => c.Id == cityId).AutoRecruitRate != recruitmentRate)
            {
                state = state with { Cities = state.Cities.Select(c => c.Id == cityId
                    ? c with { AutoRecruitRate = recruitmentRate } : c).ToList() };
            }
        }
        return state;
    }

    private GameState PlanCityBaseline(GameState state, FactionId faction)
    {
        var garrisons = state.Garrisons.ToList();
        var cities = new List<City>();
        foreach (var city in state.Cities)
        {
            if (city.Owner != faction || city.AiLastGrowthDay == state.Day)
            {
                cities.Add(city);
                continue;
            }
            var residents = state.GeneralsAt(city.Id)
                .Where(id => !state.IsGeneralInField(id))
                .Select(id => state.Generals.First(g => g.Id == id)).ToList();
            var tier = AiCityGrowthPolicy.RecruitmentRate(residents.Count);
            var might = residents.Count == 0 ? 50 : residents.Max(g => g.Might);
            var politics = residents.Count == 0 ? 50 : residents.Max(g => g.Politics);
            var gold = city.Gold;
            var provisions = city.Provisions;
            var security = city.Security;
            var wall = city.Wall;
            bool HasOfficer(GeneralId? id) => id is { } general
                && state.Generals.Any(g => g.Id == general)
                && (state.Assignments.Count == 0 || state.PostingOf(general) is { } posting
                    && posting.Location == city.Id && posting.Faction == faction);
            if (!HasOfficer(city.SecurityOfficer) || security < 70)
                security = Math.Min(100, security + tier + 3);
            if (!HasOfficer(city.DomesticOfficer))
            {
                gold += PortAdministration.Scale(city, (50 + politics) * tier / 2);
                provisions += PortAdministration.Scale(city, (150 + politics * 2) * tier / 2);
            }
            if (!HasOfficer(city.RecruitmentOfficer))
            {
                var code = string.IsNullOrWhiteSpace(city.AutoRecruitTroopCode)
                    ? _config.Troop : city.AutoRecruitTroopCode;
                var amount = Math.Max(100, might * 5 * tier / 2);
                amount = PortAdministration.Scale(city, amount);
                var cost = _deployer.Balance.AutoRecruitGoldCost(code, amount);
                if (amount > 0 && cost > 0 && gold >= cost
                    && FactionTroopUnlock.Check(state, faction, code).Allowed)
                {
                    gold -= cost;
                    var index = garrisons.FindIndex(g => g.City == city.Id
                        && g.TroopCode == code && !g.Trainee);
                    if (index >= 0) garrisons[index] = garrisons[index].Merge(amount,
                        _deployer.Balance.AutoRecruitTroopTrainingLevel);
                    else garrisons.Add(new GarrisonForce(city.Id, code, amount,
                        _deployer.Balance.AutoRecruitTroopTrainingLevel));
                    security = Math.Max(0, security + _deployer.Balance.AutoRecruitSecurityDeltaForRate(tier));
                }
            }
            if (!state.Commands.Any(command => command.City == city.Id
                    && command.Kind == CommandKind.Repair && command.TroopCode == FactionResearch.WallCode)
                && !WallRepairRules.IsThreatened(state, city))
            {
                var maxWall = CastleWall.Max(city.Castle, _economy, city.WallLevel);
                if (wall * 4 < maxWall * 3)
                {
                    var restore = Math.Min(maxWall - wall, Math.Max(1, maxWall * tier / 20));
                    var cost = Math.Max(1, restore / _deployer.Balance.WallRepairGoldDivisor);
                    if (gold >= cost + _config.MaintenanceReserveGold)
                    {
                        gold -= cost;
                        wall += restore;
                    }
                }
            }
            cities.Add(city with { Gold = gold, Provisions = provisions,
                Security = security, Wall = wall, AiLastGrowthDay = state.Day });
        }
        return state with { Cities = cities, GarrisonForces = garrisons };
    }

    private GameState PlanCityMaintenance(GameState state, FactionId faction)
    {
        foreach (var city in state.Cities.Where(c => c.Owner == faction)
            .OrderBy(c => c.Id.Value).ToList())
        {
            var residentCount = state.GeneralsAt(city.Id).Count(id =>
                state.PostingOf(id)?.Faction == faction && !state.IsGeneralInField(id));
            var pending = state.Commands.Where(command => command.City == city.Id
                && command.Kind == CommandKind.Repair).ToList();
            var remainingSlots = AiCityGrowthPolicy.MaintenanceSlots(residentCount) - pending.Count;
            if (remainingSlots <= 0) continue;
            var facilities = new[]
            {
                (city.MineDestroyed, "mine"), (city.RanchDestroyed, "ranch"),
                (city.ElephantGardenDestroyed, "elephant_garden"),
                (city.RuinedPaddies > 0, "paddy"), (city.RuinedFarms > 0, "farm"),
                (city.RuinedVillages > 0, "village"), (city.WorkshopRuined, "workshop"),
            };
            var maxWall = CastleWall.Max(city.Castle, _economy, city.WallLevel);
            var repairs = new List<(string Facility, string TroopCode)>();
            if (city.Wall * 4 < maxWall * 3
                && !pending.Any(command => command.TroopCode == FactionResearch.WallCode))
                repairs.Add(("", FactionResearch.WallCode));
            if (!city.IsPort)
            {
                repairs.AddRange(facilities.Where(item => item.Item1
                    && !pending.Any(command => command.Facility == item.Item2))
                    .Select(item => (item.Item2, "")));
            }
            foreach (var (facility, troopCode) in repairs)
            {
                if (remainingSlots <= 0) break;
                if (state.Cities.First(c => c.Id == city.Id).Gold < _config.MaintenanceReserveGold)
                    break;
                var officer = state.GeneralsAt(city.Id)
                    .Where(id => state.PostingOf(id)?.Faction == faction
                        && !state.IsGeneralBusy(id) && !state.IsGeneralInField(id))
                    .Select(id => state.Generals.First(g => g.Id == id))
                    .OrderBy(g => IsDutyOfficer(state, g.Id) ? 1 : 0)
                    .ThenByDescending(g => g.Politics).ThenBy(g => g.Id.Value).FirstOrDefault();
                if (officer is null) break;
                var result = _commands.Issue(state, new CommandRequest(city.Id,
                    CommandKind.Repair, officer.Id, Facility: facility, TroopCode: troopCode));
                if (result.Ok)
                {
                    state = result.State;
                    remainingSlots--;
                }
            }
        }
        return state;
    }

    private static bool IsDutyOfficer(GameState state, GeneralId id)
        => state.Cities.Any(city => city.SecurityOfficer == id || city.DomesticOfficer == id
            || city.RecruitmentOfficer == id || city.TrainingOfficer == id);

    // 탐색은 공세·연구가 장수를 예약한 뒤, 방어 여력과 금 예비가 남는 성에서만 수행한다.
    private GameState PlanExploration(GameState state, FactionId faction)
    {
        foreach (var city in state.Cities.Where(c => c.Owner == faction && !c.IsPort)
                     .OrderBy(c => c.Id.Value))
        {
            if (city.Gold < _config.ExplorationReserveGold
                || state.Commands.Any(c => c.City == city.Id && c.Kind == CommandKind.Explore)
                || state.Cities.Where(c => state.AreHostile(faction, c.Owner))
                    .Any(enemy => enemy.Position.Distance(city.Position) <= _config.ExplorationSafeDistance))
                continue;

            var free = state.GeneralsAt(city.Id)
                .Where(id => !state.IsGeneralBusy(id))
                .Select(id => state.Generals.First(g => g.Id == id))
                .OrderBy(g => IsDutyOfficer(state, g.Id))
                .ThenByDescending(g => g.Intellect).ThenBy(g => g.Id.Value).ToList();
            if (free.Count <= _config.KeepGeneralsHome)
                continue;

            var result = _commands.Issue(state,
                new CommandRequest(city.Id, CommandKind.Explore, free[0].Id));
            if (result.Ok) state = result.State;
        }
        return state;
    }

    private static GameState TargetUnlockRuins(GameState state, FactionId faction)
    {
        if (state.Ruins.Count == 0) return state;
        var armies = state.Armies.ToList();
        foreach (var ruin in state.Ruins.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var status = state.RuinStatus.FirstOrDefault(s => s.RuinId == ruin.Id);
            if (status is null) continue;
            var candidates = armies.Where(a => a.Field.Owner == faction && a.CanInitiateCombat
                    && a.Field.Mode == UnitMode.Standby && a.Field.Target is null
                    && (ruin.Naval ? a.Field.Domain == MovementDomain.DeepWater : a.Field.Domain != MovementDomain.DeepWater))
                .OrderBy(a => a.Field.Position.Distance(ruin.Position)).ThenBy(a => a.Id.Value).ToList();
            if (!RuinAiPolicy.ShouldAttack(ruin, status, faction, state.Day, candidates.Sum(a => a.Pool.Active))) continue;
            var committed = 0;
            foreach (var army in candidates)
            {
                var index = armies.FindIndex(a => a.Id == army.Id);
                armies[index] = army with { Field = army.Field with { Mode = UnitMode.Attack, Target = ruin.Position,
                    Waypoints = null, ContinuousTarget = null, ContinuousWaypoints = null,
                    AssignedUnitTarget = null, PursuitTarget = null } };
                committed += army.Pool.Active;
                if (committed >= status.Defenders) break;
            }
        }
        return state with { FieldArmies = armies };
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

        var code = AiResearchPriority.General(state, faction)
            .FirstOrDefault(candidate => cities.Sum(city => city.Gold) >=
                GeneralResearchRules.Cost(candidate, state.ResearchOf(faction, candidate) + 1,
                    _commands.Balance) + _config.GeneralResearchReserveGold);
        if (code is null)
        {
            return state;
        }

        var level = state.ResearchOf(faction, code);
        var cost = GeneralResearchRules.Cost(code, level + 1, _commands.Balance);
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
                    .OrderBy(g => IsDutyOfficer(state, g.Id))
                    .ThenByDescending(g => g.Intellect)
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

    private GameState PlanStratagemResearch(GameState state, FactionId faction)
    {
        if (_config.GeneralResearchReserveGold < 0
            || state.Commands.Any(c => c.Kind == CommandKind.Research
                && FactionResearch.IsStratagemResearch(c.TroopCode)
                && state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == faction))
        {
            return state;
        }

        var cities = state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList();
        var code = AiResearchPriority.Stratagem(state, faction)
            .FirstOrDefault(candidate => cities.Sum(city => city.Gold) >=
                StratagemResearchRules.Cost(state.ResearchOf(faction, candidate) + 1)
                + _config.GeneralResearchReserveGold);
        if (cities.Count == 0 || code is null)
        {
            return state;
        }

        var cost = StratagemResearchRules.Cost(state.ResearchOf(faction, code) + 1);
        if (cities.Sum(c => c.Gold) < cost + _config.GeneralResearchReserveGold)
        {
            return state;
        }

        var candidate = cities
            .Select(city => new
            {
                City = city,
                Officers = state.GeneralsAt(city.Id)
                    .Where(id => !state.IsGeneralBusy(id))
                    .Select(id => state.Generals.First(general => general.Id == id))
                    .OrderBy(general => IsDutyOfficer(state, general.Id))
                    .ThenByDescending(general => general.Intellect)
                    .ThenBy(general => general.Id.Value)
                    .ToList(),
            })
            .Where(item => item.Officers.Count > _config.KeepGeneralsHome)
            .OrderByDescending(item => item.Officers[0].Intellect)
            .ThenBy(item => item.City.Id.Value)
            .FirstOrDefault();
        if (candidate is null)
        {
            return state;
        }

        var funding = cities.Where(city => city.Gold > 0)
            .Select(city => new ResearchFundingShare(city.Id, city.Gold))
            .ToList();
        var result = _commands.Issue(state, new CommandRequest(candidate.City.Id, CommandKind.Research,
            candidate.Officers[0].Id, TroopCode: code, ResearchFunding: funding));
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
                .OrderBy(g => IsDutyOfficer(state, g.Id))
                .ThenByDescending(ArmyGroupAptitude)
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
                .OrderBy(g => IsDutyOfficer(state, g.Id))
                .ThenByDescending(g => g.AptitudeFor(TroopClass.Supply))
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
            if (hero is null || hero.Type != HeroUnlockType.Faction
                || hero.Faction != faction || !hero.AiCanRecruit)
            {
                continue;
            }
            if (RecruitmentPointBank.Balance(state, faction) < hero.RecruitPointCost)
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

    // 야전 공격 부대를 가장 가까운 적 성 또는 표적 가능한 야전 건축물로 재조준한다.
    // 정찰대는 통행형 비전 시설이므로 AI가 공격 대상으로 삼지 않는다.
    private static GameState Retarget(GameState state, FactionId faction)
    {
        var armies = state.Armies.Select(u =>
        {
            if (u.Field.Owner != faction || u.Field.Mode != UnitMode.Attack)
            {
                return u;
            }

            if (u.Field.AssignedUnitTarget is { } assigned)
            {
                var quarry = state.Armies.FirstOrDefault(enemy => enemy.Id == assigned
                    && enemy.Pool.Active > 0 && state.AreHostile(faction, enemy.Field.Owner));
                var home = state.Cities.Where(city => city.Owner == faction)
                    .OrderBy(city => city.Id == u.OriginCity ? 0 : 1)
                    .ThenBy(city => city.Position.Distance(u.Field.Position))
                    .FirstOrDefault();
                var retreat = quarry is null || u.MaxTroops > 0 && u.Pool.Active * 100 < u.MaxTroops * 60;
                if (!retreat && home is not null && u.Provisions >= 0)
                {
                    var travelDays = (u.Field.Position.Distance(home.Position)
                        + Math.Max(1, u.Field.Speed) - 1) / Math.Max(1, u.Field.Speed);
                    var daily = Math.Max(1, (u.Pool.Active + 999) / 1000);
                    retreat = u.Provisions < daily * (travelDays + 3);
                }
                if (retreat)
                    return home is null ? u with { Field = u.Field with
                    {
                        Mode = UnitMode.Standby, Target = null, AssignedUnitTarget = null,
                        PursuitTarget = null, ContinuousTarget = null,
                        Waypoints = null, ContinuousWaypoints = null,
                    } } : u with { Field = u.Field with
                    {
                        Mode = UnitMode.March, Target = home.Position,
                        ReturnCity = home.Id, AssignedUnitTarget = null, PursuitTarget = null,
                        ContinuousTarget = null, Waypoints = null, ContinuousWaypoints = null,
                    } };
                var position = quarry!.RenewalPosition
                    ?? RenewalHexSpace.Center(quarry.Field.Position);
                return u with { Field = u.Field with { Target = quarry.Field.Position,
                    ContinuousTarget = position, Waypoints = null, ContinuousWaypoints = null } };
            }

            var current = u.Field.Target;
            if (current is { } objective && (
                state.Cities.Any(city => state.AreHostile(faction, city.Owner)
                    && CastleFootprint.TilesFor(city).Contains(objective))
                || state.Buildings.Any(building => state.AreHostile(faction, building.Owner)
                    && !building.IsExpired(state.Day) && building.Position == objective)
                || state.Ruins.Any(ruin => ruin.Position == objective)
                || state.Armies.Any(enemy => state.AreHostile(faction, enemy.Field.Owner) && enemy.Pool.Active > 0
                    && enemy.Field.Position == objective)))
                return u;

            var target = NearestEnemyObjective(state, faction, u.Field.Position);
            var alliedObjective = current is { } goal && (state.Cities.Any(city => state.AreAllied(faction, city.Owner)
                    && CastleFootprint.TilesFor(city).Contains(goal))
                || state.Buildings.Any(building => state.AreAllied(faction, building.Owner)
                    && building.Position == goal)
                || state.Armies.Any(ally => state.AreAllied(faction, ally.Field.Owner)
                    && ally.Field.Position == goal));
            return target is { } dest ? u with { Field = u.Field with
            {
                Target = dest, Waypoints = null, ContinuousTarget = null,
                ContinuousWaypoints = null, AssignedUnitTarget = null, PursuitTarget = null,
            } } : !alliedObjective ? u : u with { Field = u.Field with
            {
                Mode = UnitMode.Standby, Target = null, Waypoints = null,
                ContinuousTarget = null, ContinuousWaypoints = null,
                AssignedUnitTarget = null, PursuitTarget = null,
            } };
        }).ToList();
        return state with { FieldArmies = armies };
    }

    private static GameState CommitPressureAssault(GameState state, FactionId faction)
    {
        return state with { FieldArmies = state.Armies.Select(unit =>
        {
            if (unit.Field.Owner != faction || unit.IsSupply || unit.Class == TroopClass.Siege
                || unit.AiPressureUntilDay <= state.Day
                || unit.AiSiegeAssemblyTarget is not { } targetId) return unit;
            var target = state.Cities.FirstOrDefault(city => city.Id == targetId);
            if (target is null || target.Wall > 0 || !state.AreHostile(faction, target.Owner)
                || !state.Armies.Any(siege => siege.Field.Owner == faction
                    && siege.Class == TroopClass.Siege && siege.Pool.Active > 0
                    && siege.Field.Target == target.Position
                    && siege.Field.Position.Distance(target.Position) <= 3)) return unit;
            return unit with { Field = unit.Field with { Mode = UnitMode.Attack,
                Target = target.Position, ContinuousTarget = null, Waypoints = null,
                ContinuousWaypoints = null, AssignedUnitTarget = null, PursuitTarget = null } };
        }).ToList() };
    }

    private static GameState WithdrawPressure(GameState state, FactionId faction)
    {
        return state with { FieldArmies = state.Armies.Select(unit =>
        {
            if (unit.Field.Owner != faction || unit.AiPressureUntilDay <= 0
                || state.Day < unit.AiPressureUntilDay || unit.IsWaitingDeployment)
                return unit;
            var home = state.Cities.Where(city => city.Owner == faction)
                .OrderBy(city => city.Id == unit.OriginCity ? 0 : 1)
                .ThenBy(city => city.Position.Distance(unit.Field.Position))
                .ThenBy(city => city.Id.Value).FirstOrDefault();
            if (home is null) return unit with { AiPressureUntilDay = 0 };
            return unit with { AiPressureUntilDay = 0, AiSiegeAssemblyTarget = null,
                Field = unit.Field with { Mode = UnitMode.March, Target = home.Position,
                    ReturnCity = home.Id, ContinuousTarget = null, Waypoints = null,
                    ContinuousWaypoints = null, AssignedUnitTarget = null,
                    PursuitTarget = null } };
        }).ToList() };
    }

    private static GameState RecoverIdleExpeditions(GameState state, FactionId faction)
    {
        var armies = state.Armies.Select(unit =>
        {
            if (unit.Field.Owner != faction || unit.Field.Mode != UnitMode.Standby
                || unit.OriginCity is null || unit.IsWaitingDeployment
                || unit.AiPressureUntilDay > state.Day
                || unit.IsSupply && unit.Field.FollowUnitId is not null
                || unit.IsTransport || unit.IsSummon || unit.IsConstructing
                || state.Buildings.Any(building => building.GarrisonUnit == unit.Id))
                return unit;
            var home = state.Cities.Where(city => city.Owner == faction)
                .OrderBy(city => city.Id == unit.OriginCity ? 0 : 1)
                .ThenBy(city => city.Position.Distance(unit.Field.Position))
                .ThenBy(city => city.Id.Value).FirstOrDefault();
            if (home is null) return unit;
            return unit with { Field = unit.Field with { Mode = UnitMode.March,
                Target = home.Position, ReturnCity = home.Id, ContinuousTarget = null,
                Waypoints = null, ContinuousWaypoints = null, AssignedUnitTarget = null,
                PursuitTarget = null } };
        }).ToList();
        return state with { FieldArmies = armies };
    }

    private static HexCoord? NearestEnemyCity(GameState state, FactionId self, HexCoord from)
        => state.Cities.Where(c => state.AreHostile(self, c.Owner))
            .OrderBy(c => c.Position.Distance(from)).ThenBy(c => c.Id.Value)
            .Select(c => (HexCoord?)c.Position)
            .FirstOrDefault();

    private static (City City, int GarrisonTroops)? NearestEnemyCityInfo(GameState state, FactionId self, HexCoord from)
    {
        var city = state.Cities.Where(c => state.AreHostile(self, c.Owner))
            .OrderBy(c => c.Position.Distance(from))
            .ThenBy(c => c.Id.Value)
            .FirstOrDefault();
        if (city is null)
        {
            return null;
        }

        return (city, state.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops));
    }

    private static HexCoord? NearestEnemyObjective(GameState state, FactionId self, HexCoord from)
    {
        var cityTargets = state.Cities
            .Where(city => state.AreHostile(self, city.Owner))
            .Select(city => (Position: city.Position, Kind: 0, Id: city.Id.Value));
        var buildingTargets = state.Buildings
            .Where(building => state.AreHostile(self, building.Owner)
                && !building.IsExpired(state.Day)
                && !string.Equals(building.DefinitionCode, "scout_post", StringComparison.Ordinal))
            .Select(building => (Position: building.Position, Kind: 1, Id: building.Id.Value));
        return cityTargets.Concat(buildingTargets)
            .OrderBy(x => x.Position.Distance(from)).ThenBy(x => x.Kind).ThenBy(x => x.Id)
            .Select(x => (HexCoord?)x.Position).FirstOrDefault();
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
