namespace SanguoSLG.Sandbox;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 관전 캠페인 — 실제 시나리오로 각 세력이 **최소 휴리스틱**(대기 병력이 쌓이면 가장 가까운 적 성으로
/// 출전, 아니면 여력만큼 모집)으로 스스로 싸우는 것을 주간 로그로 지켜본다. 모집→출전→행군→공성→
/// 함락→충성/포로가 한 흐름으로 도는지 눈으로 체감하는 도구이자 12단계 세력 AI의 전신
/// (정식 AI는 Core.AI + 테스트로). 여기 결정 로직은 결정론(세력·도시 id순·문턱값, 난수 없음).
/// </summary>
internal static class SpectatorCampaign
{
    private const string Troop = "swordsman";
    private const int DeployTarget = 8000;   // 대기 병력이 이 이상이면 출전(결정적 규모)
    private const int DeploySize = 10000;    // 한 번에 편성하는 병력(일반 부대 상한)
    private const int MinOre = 300;          // 광석이 이 이상일 때만 모집

    public static void Run(string dataDir, int weeks, int seed)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(dataDir);
        var cmdBalance = new CommandBalanceLoader().LoadFromDirectory(dataDir);
        var troops = new TroopTypeLoader().LoadFromDirectory(dataDir);
        var actives = new ActiveSkillLoader().LoadFromDirectory(dataDir);
        var passives = new PassiveSkillLoader().LoadFromDirectory(dataDir);
        var adminSkills = new AdminSkillLoader().LoadFromDirectory(dataDir);

        var commander = new CommandService(cmdBalance, troops);
        var deployer = new DeployService(cmdBalance, troops, actives, passives);
        var world = new WorldEngine(scenario.Balance, cmdBalance, adminSkills);
        var orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(scenario.Map, scenario.Features, scenario.Cities)),
            new CombatPhaseResolver(
                new BattleResolver(scenario.Balance.MultiTargetSecondaryPercent),
                scenario.Balance.WoundedPercent));
        var engine = new CampaignEngine(orchestrator, world,
            new CampaignSiege(new BattleResolver(scenario.Balance.MultiTargetSecondaryPercent), troops),
            new CityCapture(), new SeededRandomSource(seed));

        var state = GameState.FromScenario(scenario);

        Console.WriteLine("=== 관전 캠페인 (최소 휴리스틱 자율 전쟁) ===");
        Console.WriteLine($"seed={seed}  weeks={weeks}  출전 문턱 {DeployTarget}");
        foreach (var f in state.Factions.OrderBy(f => f.Id.Value))
        {
            var names = state.GeneralsOf(f.Id).Select(id => state.Generals.First(g => g.Id == id).Name);
            Console.WriteLine($"  {f.Name}: 도시 {state.CityCount(f.Id)}, 장수 {string.Join("·", names)}");
        }

        Console.WriteLine();
        for (var w = 1; w <= weeks; w++)
        {
            state = Decide(state, commander, deployer);
            state = engine.AdvanceWeek(state, out _, out var sieges, out var captures);
            PrintWeek(w, state, sieges, captures);

            var alive = state.Factions.Where(f => state.CityCount(f.Id) > 0).ToList();
            if (alive.Count <= 1)
            {
                Console.WriteLine($"\n=== 종료: {(alive.Count == 1 ? alive[0].Name + " 통일" : "전멸")} (주 {w}) ===");
                Summary(state);
                return;
            }
        }

        Console.WriteLine($"\n=== {weeks}주 종료(미결착) ===");
        Summary(state);
    }

    private static void Summary(GameState state)
    {
        foreach (var f in state.Factions.OrderBy(f => f.Id.Value))
        {
            var cities = state.Cities.Where(c => c.Owner == f.Id).Select(c => c.Name);
            var held = state.PrisonersHeldBy(f.Id).Count();
            Console.WriteLine($"  {f.Name}: 도시 [{string.Join(",", cities)}] · 억류 포로 {held}");
        }

        if (state.Prisoners.Count > 0)
        {
            var names = state.Prisoners.Select(p => state.Generals.First(g => g.Id == p.General).Name);
            Console.WriteLine($"  포로: {string.Join(", ", names)}");
        }
    }

    // 최소 휴리스틱. 각 세력: ① 멈춘 야전 공격 부대는 가장 가까운 적 성으로 재조준(목표가 함락돼
    // 무효화되면 다시 향한다), ② 각 도시(id순) 장수 1명으로 한 행동 — 대기 병력이 문턱 이상이고
    // **도시에 장수가 2명 이상 남을 때만** 출전(1명은 모집용으로 남긴다), 아니면 여력만큼 모집.
    private static GameState Decide(GameState state, CommandService commander, DeployService deployer)
    {
        foreach (var faction in state.Factions.OrderBy(f => f.Id.Value).ToList())
        {
            state = Retarget(state, faction.Id);

            foreach (var city in state.Cities.Where(c => c.Owner == faction.Id).OrderBy(c => c.Id.Value).ToList())
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
                    .Where(g => g.City == city.Id && g.TroopCode == Troop)
                    .Sum(g => g.Troops);

                if (garrison >= DeployTarget && free.Count >= 2)
                {
                    var target = NearestEnemyCity(state, faction.Id, city.Position);
                    if (target is { } dest)
                    {
                        var r = deployer.Deploy(state, new DeployRequest(
                            city.Id, Troop, System.Math.Min(garrison, DeploySize), gid, Mode: UnitMode.Attack, Target: dest));
                        if (r.Ok)
                        {
                            state = r.State;
                        }
                    }
                }
                else if (city.Ore >= MinOre)
                {
                    var r = commander.Issue(state, new CommandRequest(
                        city.Id, CommandKind.Recruit, gid, TroopCode: Troop));
                    if (r.Ok)
                    {
                        state = r.State;
                    }
                }
            }
        }

        return state;
    }

    // 야전 공격 부대를 가장 가까운 적 성으로 재조준한다(멈춘 부대·무효 목표를 다시 진격시킨다).
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

    private static void PrintWeek(int week, GameState state,
        IReadOnlyList<SiegeExchange> sieges, IReadOnlyList<CaptureReport> captures)
    {
        var parts = state.Factions.OrderBy(f => f.Id.Value).Select(f =>
        {
            var owned = state.Cities.Where(c => c.Owner == f.Id).Select(c => c.Id).ToHashSet();
            var gold = state.Cities.Where(c => owned.Contains(c.Id)).Sum(c => c.Gold);
            var garr = state.Garrisons.Where(g => owned.Contains(g.City)).Sum(g => g.Troops);
            var fieldTroops = state.Armies.Where(u => u.Field.Owner == f.Id).Sum(u => u.Pool.Active);
            return $"{f.Name} 성{owned.Count} 금{gold} 병{garr + fieldTroops}";
        });

        var note = "";
        if (sieges.Count > 0)
        {
            note += $"  [공성 {sieges.Count}]";
        }

        foreach (var c in captures)
        {
            var cityName = state.Cities.FirstOrDefault(x => x.Id == c.City)?.Name ?? $"성{c.City.Value}";
            var newOwner = state.Factions.FirstOrDefault(f => f.Id == c.NewOwner)?.Name ?? $"세력{c.NewOwner.Value}";
            note += $"  ★{cityName}→{newOwner}";
            if (c.Captured.Count > 0)
            {
                note += $"(포로 {c.Captured.Count})";
            }

            if (c.FactionEliminated)
            {
                note += "(멸망)";
            }
        }

        Console.WriteLine($"[주{week,3}] {string.Join(" | ", parts)}{note}");
    }
}
