namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>명령 발행 요청(design-administration.md). 산출량은 능력에서 계산되므로 지정하지 않는다.</summary>
/// <param name="Value">세율 명령의 목표 세율(그 외 무시).</param>
/// <param name="Facility">건설 명령의 시설 종류("paddy"/"farm"/"village"/"workshop").</param>
public sealed record CommandRequest(
    CityId City,
    CommandKind Kind,
    GeneralId Main,
    GeneralId? Assist = null,
    int Value = 0,
    string Facility = "");

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

    public CommandService(CommandBalance balance) => _b = balance;

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

        var eff = CommandEfficiency.Effective(main, assist, city, req.Kind, _b);

        return req.Kind switch
        {
            CommandKind.Recruit => IssueRecruit(state, city, req, assist, eff, CommandKind.Recruit),
            CommandKind.Conscript => IssueRecruit(state, city, req, assist, eff, CommandKind.Conscript),
            CommandKind.Train => IssueSimple(state, city, req, assist,
                amount: CommandEfficiency.TrainGain(eff, _b), days: _b.CommandDays),
            CommandKind.Build => IssueBuild(state, city, req, assist, main),
            CommandKind.SetTaxRate => IssueTax(state, city, req, assist),
            _ => CommandResult.Fail("알 수 없는 명령이다.", state),
        };
    }

    private CommandResult IssueRecruit(GameState state, City city, CommandRequest req, General? assist,
        int eff, CommandKind kind)
    {
        var capPercent = kind == CommandKind.Recruit ? _b.RecruitPopCapPercent : _b.ConscriptPopCapPercent;
        var byAbility = CommandEfficiency.RecruitTroops(eff, _b);
        var byPopulation = city.Population * capPercent / 100;
        var troops = System.Math.Min(System.Math.Min(byAbility, byPopulation), city.Ore); // 광석 1/명

        if (troops <= 0)
        {
            return CommandResult.Fail("자원·인구가 부족해 모집할 수 없다.", state);
        }

        // 예약: 광석·인구 즉시 차감(정산 때 병력 지급). 병종별 말·코끼리는 편성 시스템(후속)에서.
        var reserved = city with { Ore = city.Ore - troops, Population = city.Population - troops };
        return Register(state, reserved, req, assist, troops, _b.CommandDays, kind, "");
    }

    private CommandResult IssueSimple(GameState state, City city, CommandRequest req, General? assist,
        int amount, int days)
        => Register(state, city, req, assist, amount, days, req.Kind, "");

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

    private CommandResult IssueTax(GameState state, City city, CommandRequest req, General? assist)
    {
        if (req.Value is < 0 or > 50)
        {
            return CommandResult.Fail("세율은 0~50%만 가능하다.", state);
        }

        return Register(state, city, req, assist, req.Value, _b.CommandDays, CommandKind.SetTaxRate, "");
    }

    private static CommandResult Register(GameState state, City reservedCity, CommandRequest req, General? assist,
        int amount, int days, CommandKind kind, string facility)
    {
        var cities = state.Cities.Select(c => c.Id == reservedCity.Id ? reservedCity : c).ToList();
        var command = new CityCommand(req.City, kind, req.Main, assist?.Id,
            state.Day, state.Day + days, amount, facility);
        var pending = state.Commands.Append(command).ToList();
        return CommandResult.Success(state with { Cities = cities, PendingCommands = pending });
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
