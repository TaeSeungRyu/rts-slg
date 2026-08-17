namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>명령 발행 요청(design-administration.md). 산출량은 능력에서 계산되므로 지정하지 않는다.</summary>
/// <param name="Value">세율 명령의 목표 세율(그 외 무시).</param>
/// <param name="Facility">건설 명령의 시설 종류("paddy"/"farm"/"village"/"workshop").</param>
/// <param name="TroopCode">모집(모병·징병)·훈련의 병종 코드 — 2026-08-16 확정: 병종은 모집 시 지정.</param>
public sealed record CommandRequest(
    CityId City,
    CommandKind Kind,
    GeneralId Main,
    GeneralId? Assist = null,
    int Value = 0,
    string Facility = "",
    string TroopCode = "");

/// <summary>명령 발행 결과 — 실패면 <see cref="Error"/>에 사유, 상태는 그대로.</summary>
public sealed record CommandResult(bool Ok, string? Error, GameState State)
{
    public static CommandResult Fail(string error, GameState state) => new(false, error, state);
    public static CommandResult Success(GameState state) => new(true, null, state);
}

/// <summary>
/// 도시 명령 발행(design-administration.md "명령 실행 공통 규칙"). 검증 → 산출량 확정 →
/// 자원·금 예약(즉시 차감) → 수행 장수 잠금(진행 목록 등록)까지 한다. 정산은 WorldEngine이 완료일에.
/// 결정론: 순수 계산, 난수·시계 미사용.
/// </summary>
public sealed class CommandService
{
    private readonly CommandBalance _b;
    private readonly IReadOnlyDictionary<string, TroopTemplate> _troops;

    public CommandService(CommandBalance balance, IReadOnlyList<TroopTemplate>? troops = null)
    {
        _b = balance;
        _troops = (troops ?? []).ToDictionary(t => t.Code);
    }

    public CommandResult Issue(GameState state, CommandRequest req)
    {
        var city = state.Cities.FirstOrDefault(c => c.Id == req.City);
        if (city is null)
        {
            return CommandResult.Fail("도시를 찾을 수 없다.", state);
        }

        var main = state.Generals.FirstOrDefault(g => g.Id == req.Main);
        if (main is null)
        {
            return CommandResult.Fail("주관 장수를 찾을 수 없다.", state);
        }

        General? assist = null;
        if (req.Assist is { } assistId)
        {
            if (assistId == req.Main)
            {
                return CommandResult.Fail("보좌는 주관과 달라야 한다.", state);
            }

            assist = state.Generals.FirstOrDefault(g => g.Id == assistId);
            if (assist is null)
            {
                return CommandResult.Fail("보좌 장수를 찾을 수 없다.", state);
            }
        }

        if (state.IsGeneralBusy(req.Main) || (assist is not null && state.IsGeneralBusy(assist.Id)))
        {
            return CommandResult.Fail("수행 장수가 다른 명령에 매여 있다.", state);
        }

        // 배속 검증(소유·배속 기반) — 배속이 하나라도 있을 때만 강제한다. 배속을 안 넣은
        // 포커스 테스트/샌드박스는 통과시키되, 실제 시나리오에서는 소속·주둔을 지킨다.
        if (state.Assignments.Count > 0)
        {
            if (PostingError(state, req.Main, city) is { } e1)
            {
                return CommandResult.Fail($"주관 {e1}", state);
            }

            if (assist is not null && PostingError(state, assist.Id, city) is { } e2)
            {
                return CommandResult.Fail($"보좌 {e2}", state);
            }
        }

        var eff = CommandEfficiency.Effective(main, assist, city, req.Kind, _b);

        return req.Kind switch
        {
            CommandKind.Recruit => IssueRecruit(state, city, req, assist, eff, CommandKind.Recruit),
            CommandKind.Conscript => IssueRecruit(state, city, req, assist, eff, CommandKind.Conscript),
            CommandKind.Train => IssueTrain(state, city, req, assist, eff),
            CommandKind.Build => IssueBuild(state, city, req, assist, main),
            CommandKind.SetTaxRate => IssueTax(state, city, req, assist),
            CommandKind.Research => IssueResearch(state, city, req, assist, main),
            _ => CommandResult.Fail("알 수 없는 명령이다.", state),
        };
    }

    private CommandResult IssueRecruit(GameState state, City city, CommandRequest req, General? assist,
        int eff, CommandKind kind)
    {
        // 병종은 모집 시 지정(2026-08-16 확정) — 그때 광석 + 말/코끼리를 소비한다.
        if (!_troops.TryGetValue(req.TroopCode, out var template))
        {
            return CommandResult.Fail("모집할 병종을 지정해야 한다.", state);
        }

        // 병력 = 인구 × 상한% × 동원율(유효 정치/100). 광석 1/명이 하드 캡.
        var capPercent = kind == CommandKind.Recruit ? _b.RecruitPopCapPercent : _b.ConscriptPopCapPercent;
        var byPolitics = CommandEfficiency.RecruitTroops(city.Population, capPercent, eff);
        var troops = System.Math.Min(byPolitics, city.Ore);

        // 병종별 추가 자원이 하드 캡을 더 조인다: 말 = 3명당 1, 코끼리 = 1000명당 1.
        if (template.Class == TroopClass.Cavalry)
        {
            troops = System.Math.Min(troops, city.Horses * 3);
        }
        else if (template.Class == TroopClass.Elephant)
        {
            troops = System.Math.Min(troops, city.Elephants * 1000);
        }

        if (troops <= 0)
        {
            return CommandResult.Fail("자원·인구가 부족해 모집할 수 없다.", state);
        }

        // 예약: 광석·인구 + 병종별 자원(말·코끼리, 올림) 즉시 차감. 정산 때 병력 지급.
        var horses = template.Class == TroopClass.Cavalry ? (troops + 2) / 3 : 0;
        var elephants = template.Class == TroopClass.Elephant ? (troops + 999) / 1000 : 0;
        var reserved = city with
        {
            Ore = city.Ore - troops,
            Population = city.Population - troops,
            Horses = city.Horses - horses,
            Elephants = city.Elephants - elephants,
        };
        return Register(state, reserved, req, assist, troops, _b.CommandDays, kind, "", req.TroopCode);
    }

    private CommandResult IssueTrain(GameState state, City city, CommandRequest req, General? assist, int eff)
    {
        if (!state.Garrisons.Any(g => g.City == city.Id && g.TroopCode == req.TroopCode && g.Troops > 0))
        {
            return CommandResult.Fail("훈련할 대기 병력(병종)이 없다.", state);
        }

        return Register(state, city, req, assist,
            CommandEfficiency.TrainGain(eff, _b), _b.CommandDays, CommandKind.Train, "", req.TroopCode);
    }

    private CommandResult IssueBuild(GameState state, City city, CommandRequest req, General? assist, General main)
    {
        if (main.Politics <= _b.BuildPoliticsRequired)
        {
            return CommandResult.Fail($"건설은 정치 {_b.BuildPoliticsRequired} 초과 장수만 가능하다.", state);
        }

        var cost = BuildCost(req.Facility);
        if (cost < 0)
        {
            return CommandResult.Fail("알 수 없는 시설이다.", state);
        }

        if (req.Facility == "workshop")
        {
            if (city.Workshop)
            {
                return CommandResult.Fail("공방은 성별 1개만 지을 수 있다.", state);
            }
        }
        else
        {
            var used = city.Paddies + city.Farms + city.Villages;
            if (used >= CommandEfficiency.BuildSlots(city.Castle, _b))
            {
                return CommandResult.Fail("시설 슬롯이 가득 찼다.", state);
            }
        }

        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        var reserved = city.AddGold(-cost);
        return Register(state, reserved, req, assist, amount: 0, _b.BuildDays, CommandKind.Build, req.Facility);
    }

    private CommandResult IssueResearch(GameState state, City city, CommandRequest req, General? assist, General main)
    {
        // 공방 게이트(design-combat "병종 연구는 공방에서") — 공방 없는 도시에선 연구 불가.
        if (!city.Workshop)
        {
            return CommandResult.Fail("연구는 공방이 있는 도시에서만 가능하다.", state);
        }

        if (!_troops.ContainsKey(req.TroopCode))
        {
            return CommandResult.Fail("연구할 병종을 지정해야 한다.", state);
        }

        // 세력당 동시 1개 연구만(2026-08-17 확정) — 이미 진행 중인 연구가 있으면 거부.
        var faction = city.Owner;
        if (state.Commands.Any(c => c.Kind == CommandKind.Research
            && state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == faction))
        {
            return CommandResult.Fail("세력은 한 번에 하나의 연구만 할 수 있다.", state);
        }

        var level = state.ResearchOf(city.Owner, req.TroopCode);
        if (level >= _b.ResearchMaxLevel)
        {
            return CommandResult.Fail("이미 최대 단계까지 연구했다.", state);
        }

        var cost = CommandEfficiency.ResearchCost(level + 1, _b);
        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        // 효율 능력 = 지력: 지력이 높을수록 기간 단축(기본 30일, 지력 100이면 −10일).
        var days = System.Math.Max(_b.ResearchBaseDays - System.Math.Clamp((main.Intellect - 50) / 5, 0, 10), 1);
        var reserved = city.AddGold(-cost);
        return Register(state, reserved, req, assist, amount: 0, days, CommandKind.Research, "", req.TroopCode);
    }

    private CommandResult IssueTax(GameState state, City city, CommandRequest req, General? assist)
    {
        if (req.Value is < 0 or > 50)
        {
            return CommandResult.Fail("세율은 0~50%만 가능하다.", state);
        }

        return Register(state, city, req, assist, req.Value, _b.CommandDays, CommandKind.SetTaxRate, "");
    }

    private static CommandResult Register(GameState state, City reservedCity, CommandRequest req, General? assist,
        int amount, int days, CommandKind kind, string facility, string troopCode = "")
    {
        var cities = state.Cities.Select(c => c.Id == reservedCity.Id ? reservedCity : c).ToList();
        var command = new CityCommand(req.City, kind, req.Main, assist?.Id,
            state.Day, state.Day + days, amount, facility, troopCode);
        var pending = state.Commands.Append(command).ToList();
        return CommandResult.Success(state with { Cities = cities, PendingCommands = pending });
    }

    // 배속 규칙: 그 도시 소유 세력 소속 + 그 도시에 주둔 중이어야 명령을 수행할 수 있다.
    private static string? PostingError(GameState state, Domain.GeneralId general, City city)
    {
        var posting = state.PostingOf(general);
        if (posting is null)
        {
            return "장수가 어느 세력에도 소속돼 있지 않다(재야).";
        }

        if (posting.Faction != city.Owner)
        {
            return "장수가 이 도시 소유 세력 소속이 아니다.";
        }

        if (posting.Location != city.Id)
        {
            return "장수가 이 도시에 주둔하고 있지 않다.";
        }

        return null;
    }

    private int BuildCost(string facility) => facility switch
    {
        "paddy" => _b.BuildCostPaddy,
        "farm" => _b.BuildCostFarm,
        "village" => _b.BuildCostVillage,
        "workshop" => _b.BuildCostWorkshop,
        _ => -1,
    };
}
