namespace SanguoSLG.Sandbox;

using SanguoSLG.Core.AI;
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
    /// <summary>밸런스 검증: 여러 시드로 조용히 수렴까지 돌려 무예외·수렴(승자·소요 주)을 집계한다.</summary>
    public static void Balance(string dataDir, int seeds, int capWeeks)
    {
        Console.WriteLine("=== 밸런스 검증(세력 AI 자율 전쟁) ===");
        Console.WriteLine($"시드 {seeds}개 · 상한 {capWeeks}주");
        var converged = 0;
        var totalWeeks = 0;
        var wins = new Dictionary<string, int>();

        for (var seed = 1; seed <= seeds; seed++)
        {
            var (winner, w) = RunSilent(dataDir, capWeeks, seed);
            if (winner is not null)
            {
                converged++;
                totalWeeks += w;
                wins[winner] = wins.GetValueOrDefault(winner) + 1;
                Console.WriteLine($"  seed {seed,3}: {winner} 통일 (주 {w})");
            }
            else
            {
                Console.WriteLine($"  seed {seed,3}: 미결착({capWeeks}주)");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"수렴 {converged}/{seeds}  평균 {(converged > 0 ? (totalWeeks / converged).ToString() : "-")}주  " +
            $"승자 {string.Join(", ", wins.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}"))}");
    }

    // 로그 없이 한 캠페인을 수렴/상한까지 돌린다. (승자 이름, 소요 주) — 미결착이면 (null, cap).
    private static (string? Winner, int Weeks) RunSilent(string dataDir, int capWeeks, int seed)
    {
        var (ai, engine, state) = Build(dataDir, seed);
        for (var w = 1; w <= capWeeks; w++)
        {
            foreach (var f in state.Factions.OrderBy(f => f.Id.Value).ToList())
            {
                state = ai.PlanWeek(state, f.Id);
            }

            state = engine.AdvanceWeek(state, out _, out _, out _);
            var alive = state.Factions.Where(f => state.CityCount(f.Id) > 0).ToList();
            if (alive.Count <= 1)
            {
                return (alive.Count == 1 ? alive[0].Name : "무승부", w);
            }
        }

        return (null, capWeeks);
    }

    private static (FactionAI Ai, CampaignEngine Engine, GameState State) Build(string dataDir, int seed)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(dataDir);
        var cmdBalance = new CommandBalanceLoader().LoadFromDirectory(dataDir);
        var troops = new TroopTypeLoader().LoadFromDirectory(dataDir);
        var actives = new ActiveSkillLoader().LoadFromDirectory(dataDir);
        var passives = new PassiveSkillLoader().LoadFromDirectory(dataDir);
        var adminSkills = new AdminSkillLoader().LoadFromDirectory(dataDir);

        var ai = new FactionAI(new CommandService(cmdBalance, troops),
            new DeployService(cmdBalance, troops, actives, passives));
        var world = new WorldEngine(scenario.Balance, cmdBalance, adminSkills);
        var orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(scenario.Map, scenario.Features, scenario.Cities)),
            new CombatPhaseResolver(
                new BattleResolver(scenario.Balance.MultiTargetSecondaryPercent),
                scenario.Balance.WoundedPercent));
        var engine = new CampaignEngine(orchestrator, world,
            new CampaignSiege(new BattleResolver(scenario.Balance.MultiTargetSecondaryPercent), troops),
            new CityCapture(), new SeededRandomSource(seed));
        return (ai, engine, GameState.FromScenario(scenario));
    }

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
        var ai = new FactionAI(commander, deployer);
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

        Console.WriteLine("=== 관전 캠페인 (세력 AI 자율 전쟁) ===");
        Console.WriteLine($"seed={seed}  weeks={weeks}");
        foreach (var f in state.Factions.OrderBy(f => f.Id.Value))
        {
            var names = state.GeneralsOf(f.Id).Select(id => state.Generals.First(g => g.Id == id).Name);
            Console.WriteLine($"  {f.Name}: 도시 {state.CityCount(f.Id)}, 장수 {string.Join("·", names)}");
        }

        Console.WriteLine();
        for (var w = 1; w <= weeks; w++)
        {
            foreach (var f in state.Factions.OrderBy(f => f.Id.Value).ToList())
            {
                state = ai.PlanWeek(state, f.Id);
            }

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
