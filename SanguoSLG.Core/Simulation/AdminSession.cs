namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 내정 전용 세션 — **게임 씬(GUI)이 바인딩하는 엔진 비의존 컨트롤러**(13단계 전 내정 씬용). Godot
/// 노드에는 규칙을 넣지 않으므로(CLAUDE.md), 화면은 이 세션을 호출하고 결과만 반영한다. 전투·출전은
/// 없다 — 도시 조회 + 내정 명령 발행(모병·징병·훈련·건설·세율) + 주 단위 진행(내정 정산)만.
/// 내부 상태(<see cref="State"/>)를 명령·진행이 갱신한다. 결정론: 하부 서비스가 순수·시드 기반.
/// </summary>
public sealed class AdminSession
{
    private readonly CommandService _commands;
    private readonly WorldEngine _world;

    public AdminSession(GameState initial, FactionId player, CommandService commands, WorldEngine world)
    {
        State = initial;
        Player = player;
        _commands = commands;
        _world = world;
    }

    /// <summary>현재 게임 상태(불변 스냅샷) — 명령·진행마다 새 값으로 바뀐다.</summary>
    public GameState State { get; private set; }

    /// <summary>이 세션을 조작하는 플레이어 세력.</summary>
    public FactionId Player { get; }

    /// <summary>플레이어가 소유한 도시(id 순) — 화면 좌측 도시 목록.</summary>
    public IReadOnlyList<City> PlayerCities()
        => State.Cities.Where(c => c.Owner == Player).OrderBy(c => c.Id.Value).ToList();

    /// <summary>이 도시가 플레이어 소유인가.</summary>
    public bool OwnsCity(CityId city) => State.Cities.Any(c => c.Id == city && c.Owner == Player);

    /// <summary>이 도시에서 명령을 수행할 수 있는 장수(주둔·비잠금, id 순).</summary>
    public IReadOnlyList<GeneralId> AvailableGenerals(CityId city)
        => State.GeneralsAt(city).Where(g => !State.IsGeneralBusy(g)).OrderBy(g => g.Value).ToList();

    /// <summary>이 도시에서 진행 중인 명령(완료 대기) — 화면에 남은 일수 표시용.</summary>
    public IReadOnlyList<CityCommand> PendingAt(CityId city)
        => State.Commands.Where(c => c.City == city).OrderBy(c => c.CompletionDay).ToList();

    /// <summary>
    /// 내정 명령을 발행한다. 내정 종류(모병·징병·훈련·건설·세율)와 플레이어 소유 도시만 허용한다.
    /// 성공 시 내부 상태를 갱신하고, 실패면 상태는 그대로 두고 사유를 돌려준다.
    /// </summary>
    public CommandResult Issue(CommandRequest request)
    {
        if (!IsAdminKind(request.Kind))
        {
            return CommandResult.Fail("내정 명령이 아니다.", State);
        }

        if (!OwnsCity(request.City))
        {
            return CommandResult.Fail("내 도시가 아니다.", State);
        }

        var result = _commands.Issue(State, request);
        if (result.Ok)
        {
            State = result.State;
        }

        return result;
    }

    /// <summary>7일을 진행한다(내정만 — 전투 없음). 명령 완료·월말 정산이 이 안에서 처리된다.</summary>
    public GameState AdvanceWeek()
    {
        State = _world.AdvanceDays(State, CampaignEngine.WeekDays);
        return State;
    }

    private static bool IsAdminKind(CommandKind kind)
        => kind is CommandKind.Recruit or CommandKind.Conscript or CommandKind.Train
            or CommandKind.Build or CommandKind.Upgrade or CommandKind.SetTaxRate or CommandKind.Research
            or CommandKind.Repair or CommandKind.CityStratagem or CommandKind.FormAlliance;
}
