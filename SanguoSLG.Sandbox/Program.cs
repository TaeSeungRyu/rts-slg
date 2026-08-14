using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;

// 밸런스 시뮬레이션 실행기. 사용법: --turns N --seed S
var turns = 12;
var seed = 42;
for (var i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--turns" && int.TryParse(args[i + 1], out var t))
    {
        turns = t;
    }
    else if (args[i] == "--seed" && int.TryParse(args[i + 1], out var s))
    {
        seed = s;
    }
}

var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());
var engine = new WorldEngine(scenario.Balance);
var random = new SeededRandomSource(seed);
var state = GameState.FromScenario(scenario);

Console.WriteLine("=== SanguoSLG Sandbox ===");
Console.WriteLine($"seed={seed}  turns={turns}  (월 세수 계수={scenario.Balance.MonthlyTaxPerCity})");
Console.WriteLine();

// 시드가 결과에 미치는 영향을 눈으로 확인하기 위한 임시 probe.
// 같은 시드는 항상 같은 값을 낸다. 실제 확률 시스템(전투 등)은 이후 단계에서 이 난수를 소비한다.
var probe = string.Join(", ", Enumerable.Range(0, 5).Select(_ => random.Next(0, 100)));
Console.WriteLine($"[난수 probe] 시드 {seed} -> {probe}");
Console.WriteLine();

PrintState("시작", state);

for (var i = 0; i < turns; i++)
{
    state = engine.AdvanceMonth(state);
}

Console.WriteLine($"... {turns}턴 진행 ...");
Console.WriteLine();
PrintState("종료", state);

static void PrintState(string label, GameState state)
{
    Console.WriteLine($"[{label}] {state.Year}년 {state.Month}월 {state.DayOfMonth}일 (누적 {state.Day}일)");
    foreach (var faction in state.Factions.OrderBy(f => f.Id.Value))
    {
        var owned = state.Cities.Where(c => c.Owner == faction.Id).ToList();
        Console.WriteLine($"  {faction.Name}({faction.Id}): 도시 금고 합 {owned.Sum(c => c.Gold)}  도시 {owned.Count}");
    }

    Console.WriteLine();
}

static string FindDataDirectory()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "data");
        if (File.Exists(Path.Combine(candidate, "factions.json")))
        {
            return candidate;
        }

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("data 디렉토리를 찾지 못했습니다.");
}
