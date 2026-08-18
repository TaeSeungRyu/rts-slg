namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 도시 계략 공통 계산(design-stratagem "도시 계략 — 6종"·"수행 규칙"). 발행 전 컨펌 UI가
/// 소요일·성공률을 보여줄 수 있게 Core가 노출한다(모든 계략 사전 컨펌 — 2026-08-17 확정).
/// </summary>
public static class CityStratagems
{
    /// <summary>도시 계략 종류 코드(명령의 Facility 파라미터로 쓴다).</summary>
    public static readonly IReadOnlyList<string> Kinds =
        ["wall_break", "incite", "scout", "arson", "steal", "sow_discord"];

    /// <summary>유효한 계략 종류인가.</summary>
    public static bool IsKind(string code) => Kinds.Contains(code);

    /// <summary>정찰 전제가 필요한가 — 정찰 자체만 전제가 없다.</summary>
    public static bool RequiresScout(string code) => code != "scout";

    /// <summary>
    /// 소요일 = 기본(7일 공작) + ⌈거리 ÷ 사절 속도(기병 3)⌉ × 2(왕복) — 항상 7일 초과.
    /// 발행 전 UI 확정 표시용(design-administration "명령 소요일").
    /// </summary>
    public static int Days(HexCoord from, HexCoord to, CommandBalance b)
    {
        var distance = from.Distance(to);
        var oneWay = (distance + b.CourierSpeed - 1) / b.CourierSpeed;
        return b.CommandDays + oneWay * 2;
    }

    /// <summary>
    /// 성공률(%) = clamp(50 + 수행 지력 − 대상 태수 지력, 10, 90). 태수가 없으면 지력 40으로 간주.
    /// </summary>
    public static int SuccessPercent(int casterIntellect, int? defenderIntellect)
        => System.Math.Clamp(50 + casterIntellect - (defenderIntellect ?? 40), 10, 90);
}
