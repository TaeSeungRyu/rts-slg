using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
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
var adminSkills = new AdminSkillLoader().LoadFromDirectory(FindDataDirectory());
var engine = new WorldEngine(scenario.Balance, adminSkills: adminSkills);
var random = new SeededRandomSource(seed);
var state = GameState.FromScenario(scenario);

Console.WriteLine("=== SanguoSLG Sandbox ===");
Console.WriteLine($"seed={seed}  turns={turns}  (월 기본 수입: 금 {scenario.Balance.GoldBaseSmall}/{scenario.Balance.GoldBaseMedium}/{scenario.Balance.GoldBaseLarge})");
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

// ── 내정 ③ 명령 데모: 모병(주관 장수) → 7일 진행 → 병력 지급·장수 잠금 해제 ──
var cmdBalance = new CommandBalanceLoader().LoadFromDirectory(FindDataDirectory());
var troopTemplates = new TroopTypeLoader().LoadFromDirectory(FindDataDirectory());
var commander = new CommandService(cmdBalance, troopTemplates);
var worldC = new WorldEngine(scenario.Balance, cmdBalance, adminSkills);
var demo = GameState.FromScenario(scenario);

Console.WriteLine("=== 세력 배속 ===");
foreach (var faction in demo.Factions.OrderBy(f => f.Id.Value))
{
    var names = demo.GeneralsOf(faction.Id)
        .Select(id => demo.Generals.First(g => g.Id == id).Name);
    Console.WriteLine($"  {faction.Name}: {string.Join(", ", names)}");
}

Console.WriteLine();
Console.WriteLine("=== 명령 데모 ===");
// 배속을 준수: 어느 도시에 실제 주둔 중인 장수로 명령한다.
var city0 = demo.Cities.First(c => demo.GeneralsAt(c.Id).Any());
var govId = demo.GeneralsAt(city0.Id).First();
var gov = demo.Generals.First(g => g.Id == govId);

Console.WriteLine($"{city0.Name}: 인구 {city0.Population} 광석 {city0.Ore}, 주둔 {gov.Name}");
var issued = commander.Issue(demo, new CommandRequest(city0.Id, CommandKind.Recruit, gov.Id, TroopCode: "swordsman"));
Console.WriteLine(issued.Ok
    ? $"모병 발행 — 수행 {gov.Name}(정치 {gov.Politics}), {gov.Name} 잠김={issued.State.IsGeneralBusy(gov.Id)}"
    : $"발행 실패: {issued.Error}");
var after = worldC.AdvanceDays(issued.State, cmdBalance.CommandDays);
var c0 = after.Cities.First(c => c.Id == city0.Id);
var g0 = after.Garrisons.FirstOrDefault(g => g.City == city0.Id);
Console.WriteLine($"{cmdBalance.CommandDays}일 뒤 — 대기 {g0?.TroopCode} {g0?.Troops}명(훈련도 {g0?.TrainingLevel}) 광석 {c0.Ore}, {gov.Name} 잠김={after.IsGeneralBusy(gov.Id)}");
Console.WriteLine();

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
