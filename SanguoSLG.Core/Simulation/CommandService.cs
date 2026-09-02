namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>명령 발행 요청(design-administration.md). 산출량은 능력에서 계산되므로 지정하지 않는다.</summary>
/// <param name="Value">세율 명령의 목표 세율(그 외 무시).</param>
/// <param name="Facility">건설·수리 시설 종류 또는 도시 계략 종류(CityStratagems.Kinds).</param>
/// <param name="TroopCode">모집(모병·징병)·훈련의 병종 코드 — 2026-08-16 확정: 병종은 모집 시 지정.</param>
/// <param name="TargetCity">도시 계략의 대상(적) 도시.</param>
public sealed record CommandRequest(
    CityId City,
    CommandKind Kind,
    GeneralId Main,
    GeneralId? Assist = null,
    int Value = 0,
    string Facility = "",
    string TroopCode = "",
    CityId? TargetCity = null,
    bool TraineePool = false,
    GeneralId? TargetGeneral = null,
    Spatial.HexCoord? Plot = null);

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
    private readonly BalanceConfig? _balance;
    private readonly IReadOnlyDictionary<string, AdminSkill> _adminSkills;
    private readonly IRandomSource _random;

    public CommandService(CommandBalance balance, IReadOnlyList<TroopTemplate>? troops = null,
        BalanceConfig? economy = null, IReadOnlyList<AdminSkill>? adminSkills = null, IRandomSource? random = null)
    {
        _b = balance;
        _troops = (troops ?? []).ToDictionary(t => t.Code);
        _balance = economy; // 성벽 수리(연구 최대치 산출)에 필요 — 없으면 성벽 수리 불가
        _adminSkills = (adminSkills ?? []).ToDictionary(a => a.Code);
        _random = random ?? new SeededRandomSource(0); // 포상 상승폭 등 즉시 명령의 소소한 난수(시드 — 결정론)
    }

    /// <summary>
    /// 그 도시의 **재임 태수**(도시에 실제 주둔한 소속 장수 = City.Governor)가 가진 내정 스킬의 버킷 합.
    /// 태수 없거나 출전·타지 주둔이면 0. 수입 스킬과 달리 정치≥60 게이트를 두지 않는다 —
    /// 정치 게이트는 수입(design-administration F) 전용 규칙이고, 명령 스킬(모병관·인망·교관·축성)은
    /// 무력·정치를 가리지 않고 태수 재임만으로 작동한다(2026-08-24 결정).
    /// </summary>
    private int GovernorBucket(GameState state, City city, string bucket)
    {
        if (_adminSkills.Count == 0 || city.Governor is not { } gid)
        {
            return 0;
        }

        var posting = state.PostingOf(gid);
        if (posting is null || posting.Location != city.Id || posting.Faction != city.Owner)
        {
            return 0;
        }

        var gov = state.Generals.FirstOrDefault(g => g.Id == gid);
        return AdminBonus.Bucket(gov, _adminSkills, bucket);
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

        // 태수·군사 임명은 즉시 상태 변경(기간·비용·잠금 없음) — 진행 명령 기계를 타지 않는다.
        if (req.Kind == CommandKind.AppointGovernor)
        {
            return AppointGovernor(state, city, main);
        }

        if (req.Kind == CommandKind.AppointStrategist)
        {
            return AppointStrategist(state, city, main);
        }

        if (req.Kind is CommandKind.AppointSecurityOfficer or CommandKind.AppointDomesticOfficer
            or CommandKind.AppointRecruitmentOfficer or CommandKind.AppointTrainingOfficer)
        {
            return AppointCityOfficer(state, city, main, req);
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
            CommandKind.Upgrade => IssueUpgrade(state, city, req, assist),
            CommandKind.SetTaxRate => IssueTax(state, city, req, assist),
            CommandKind.Research => IssueResearch(state, city, req, assist, main),
            CommandKind.Repair => IssueRepair(state, city, req, assist),
            CommandKind.CityStratagem => IssueCityStratagem(state, city, req, assist),
            CommandKind.Enlist => IssueEnlist(state, city, req, assist, main),
            _ => CommandResult.Fail("알 수 없는 명령이다.", state),
        };
    }

    // 등용 발행(design-general-lifecycle §6): 대상 = 정찰된 적 성 주둔 장수 · 출전중 적 장수 · 내 포로.
    // 성공 판정은 완료 시점(WorldEngine)에서 2단계 난수로. 소요일 = 거리 비례(도시 계략과 같은 식).
    private CommandResult IssueEnlist(GameState state, City city, CommandRequest req, General? assist, General main)
    {
        if (req.TargetGeneral is not { } targetId)
        {
            return CommandResult.Fail("등용 대상 장수를 지정해야 한다.", state);
        }

        if (targetId == req.Main || (assist is not null && targetId == assist.Id))
        {
            return CommandResult.Fail("수행 장수는 등용 대상이 될 수 없다.", state);
        }

        var kind = EnlistTargetKind(state, city, targetId, out var targetPos);
        if (kind == EnlistKind.Invalid)
        {
            return CommandResult.Fail("등용할 수 없는 대상이다(내 포로·정찰된 적 성 장수·출전중 적 장수만).", state);
        }

        // 소요일: 포로(내 도시)는 거리 0 → 기본 7일. 성/출전중은 수행 도시↔대상 위치 거리 비례.
        var days = targetPos is { } pos ? CityStratagems.Days(city.Position, pos, _b) : _b.CommandDays;
        return Register(state, city, req, assist, amount: 0, days, CommandKind.Enlist, "", "", null, targetGeneral: targetId);
    }

    /// <summary>등용 대상의 종류(내 포로 / 정찰된 적 성 장수 / 출전중 적 장수 / 불가)와 대상 위치.</summary>
    public enum EnlistKind { Invalid, Prisoner, CityGeneral, FieldGeneral }

    public EnlistKind EnlistTargetKind(GameState state, City casterCity, GeneralId target, out HexCoord? targetPos)
    {
        targetPos = null;
        var faction = casterCity.Owner;

        // 내 포로.
        if (state.PrisonerOf(target) is { } p && p.Holder == faction)
        {
            return EnlistKind.Prisoner;
        }

        // 출전중(야전 부대) 적 장수 — 선봉 또는 부관.
        var army = state.Armies.FirstOrDefault(u => u.Field.Owner != faction
            && (u.VanguardId == target || u.AdjutantId == target));
        if (army is not null)
        {
            targetPos = army.Field.Position;
            return EnlistKind.FieldGeneral;
        }

        // 정찰된 적 성 주둔 장수.
        var posting = state.PostingOf(target);
        if (posting is { Location: { } loc } && posting.Faction != faction)
        {
            var targetCity = state.Cities.FirstOrDefault(c => c.Id == loc);
            if (targetCity is not null && state.IsScouted(faction, loc))
            {
                targetPos = targetCity.Position;
                return EnlistKind.CityGeneral;
            }
        }

        return EnlistKind.Invalid;
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

        // 모병관(재임 태수): 모병·징병 병력 +10/20/30%. 자원·인구 하드 캡 이전에 증폭한다.
        var amountBonus = GovernorBucket(state, city, "recruit_amount");
        if (amountBonus > 0) { byPolitics = byPolitics * (100 + amountBonus) / 100; }

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
        // 인망(재임 태수): 모병으로 인한 인구 감소를 −8/15/25%(민심으로 자원해 인구 손실을 던다).
        // 병력 수·광석은 그대로 — '비용'은 인구 드레인을 뜻한다(design-skill-admin 인망).
        var costCut = GovernorBucket(state, city, "recruit_cost");
        var popCost = costCut > 0 ? troops - (troops * costCut / 100) : troops;
        var horses = template.Class == TroopClass.Cavalry ? (troops + 2) / 3 : 0;
        var elephants = template.Class == TroopClass.Elephant ? (troops + 999) / 1000 : 0;
        var reserved = city with
        {
            Ore = city.Ore - troops,
            Population = city.Population - popCost,
            Horses = city.Horses - horses,
            Elephants = city.Elephants - elephants,
        };
        return Register(state, reserved, req, assist, troops, _b.CommandDays, kind, "", req.TroopCode);
    }

    private CommandResult IssueTrain(GameState state, City city, CommandRequest req, General? assist, int eff)
    {
        if (!state.Garrisons.Any(g => g.City == city.Id && g.TroopCode == req.TroopCode
            && g.Trainee == req.TraineePool && g.Troops > 0))
        {
            return CommandResult.Fail("훈련할 대기 병력(병종)이 없다.", state);
        }

        // 교관(재임 태수): 훈련 상승량 +2/4/6(수행 장수 무력 기반 상승량에 가산).
        var gain = CommandEfficiency.TrainGain(eff, _b) + GovernorBucket(state, city, "training");
        return Register(state, city, req, assist, gain, _b.CommandDays, CommandKind.Train, "", req.TroopCode);
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
            if (city.Workshop || state.Commands.Any(c => c.City == city.Id
                    && c.Kind == CommandKind.Build && c.Facility == "workshop"))
            {
                return CommandResult.Fail("공방은 성별 1개만 지을 수 있다.", state);
            }
        }
        else
        {
            // 잔해(약탈로 부서진 시설)도 슬롯을 차지한다 — 새로 짓지 말고 수리(50%)로 복구하라는 압력.
            var pending = state.Commands.Count(c => c.City == city.Id && c.Kind == CommandKind.Build && c.Facility != "workshop");
            var used = city.Paddies + city.Farms + city.Villages
                + city.RuinedPaddies + city.RuinedFarms + city.RuinedVillages + pending;
            if (used >= CommandEfficiency.BuildSlots(city.Castle, _b))
            {
                return CommandResult.Fail("시설 슬롯이 가득 찼다(잔해는 수리로 복구).", state);
            }
        }

        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        // 공사 인력 = 인구에서 뗀다. 공사장 체력(BuildSiteHp)이 곧 이 인력이고, 인구가 그보다 적으면 못 짓는다.
        if (city.Population <= _b.BuildSiteHp)
        {
            return CommandResult.Fail($"인구가 부족하다(공사 인력 {_b.BuildSiteHp} 초과 필요).", state);
        }

        // 배치 타일 검증 — 성 중심 반경 안, 성 타일 아님, 아직 다른 시설이 없는 칸.
        // (평지·숲만 허용하는 지형 조건은 지형 데이터를 가진 표현 계층이 후보를 걸러 보장한다.)
        if (req.Plot is not { } plot)
        {
            return CommandResult.Fail("설치할 타일을 지정해야 한다.", state);
        }

        if (plot == city.Position || plot.Distance(city.Position) > _b.BuildPlotRadius)
        {
            return CommandResult.Fail("성 주변에만 지을 수 있다.", state);
        }

        if (state.Placements.Any(p => p.Plot == plot) || state.Cities.Any(c => c.Position == plot)
            || state.Commands.Any(c => c.Kind == CommandKind.Build && c.Plot == plot))
        {
            return CommandResult.Fail("이미 무언가 있는 칸이다.", state);
        }

        var reserved = city.AddGold(-cost) with { Population = city.Population - _b.BuildSiteHp };
        return Register(state, reserved, req, assist, amount: 0, _b.BuildDays, CommandKind.Build, req.Facility, plot: plot);
    }

    private CommandResult IssueUpgrade(GameState state, City city, CommandRequest req, General? assist)
    {
        if (req.Plot is not { } plot)
        {
            return CommandResult.Fail("업그레이드할 시설을 지정해야 한다.", state);
        }

        var placement = state.Placements.FirstOrDefault(p => p.City == city.Id && p.Plot == plot);
        if (placement is null)
        {
            return CommandResult.Fail("업그레이드할 시설이 없다.", state);
        }

        var cost = BuildCost(placement.Code);
        if (cost < 0)
        {
            return CommandResult.Fail("알 수 없는 시설이다.", state);
        }

        if (FacilityHealth.NextTier(placement.HitPoints) is not { } next)
        {
            return CommandResult.Fail("이미 최대 단계까지 업그레이드했다.", state);
        }

        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        if (state.Commands.Any(c => c.Kind == CommandKind.Upgrade && c.Plot == plot))
        {
            return CommandResult.Fail("이미 업그레이드 중인 시설이다.", state);
        }

        var reserved = city.AddGold(-cost);
        return Register(state, reserved, req, assist, amount: next, _b.BuildDays,
            CommandKind.Upgrade, placement.Code, plot: plot);
    }

    private CommandResult IssueResearch(GameState state, City city, CommandRequest req, General? assist, General main)
    {
        // 공방 게이트(design-combat "연구는 공방에서") — 공방 없는 도시에선 연구 불가.
        if (!city.Workshop)
        {
            return CommandResult.Fail("연구는 공방이 있는 도시에서만 가능하다.", state);
        }

        // 세력당 동시 1개 연구만(병종·성벽 공통 — 2026-08-17 확정).
        var faction = city.Owner;
        if (state.Commands.Any(c => c.Kind == CommandKind.Research
            && state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == faction))
        {
            return CommandResult.Fail("세력은 한 번에 하나의 연구만 할 수 있다.", state);
        }

        // 병종 연구 vs 성벽 연구(TroopCode == WallCode) — 단계 캡·비용 곡선이 다르다.
        var isWall = req.TroopCode == FactionResearch.WallCode;
        if (!isWall && !_troops.ContainsKey(req.TroopCode))
        {
            return CommandResult.Fail("연구할 병종을 지정해야 한다.", state);
        }

        var level = isWall ? state.WallLevelOf(city.Owner) : state.ResearchOf(city.Owner, req.TroopCode);
        var maxLevel = isWall ? _b.WallResearchMaxLevel : _b.ResearchMaxLevel;
        if (level >= maxLevel)
        {
            return CommandResult.Fail("이미 최대 단계까지 연구했다.", state);
        }

        var cost = isWall ? _b.WallResearchCostPerLevel * (level + 1) : CommandEfficiency.ResearchCost(level + 1, _b);
        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        // 효율 능력 = 지력: 지력이 높을수록 기간 단축(기본 30일, 지력 100이면 −10일).
        var days = System.Math.Max(_b.ResearchBaseDays - System.Math.Clamp((main.Intellect - 50) / 5, 0, 10), 1);
        var reserved = city.AddGold(-cost);
        return Register(state, reserved, req, assist, amount: 0, days, CommandKind.Research, "", req.TroopCode);
    }

    private CommandResult IssueRepair(GameState state, City city, CommandRequest req, General? assist)
    {
        // 성벽 수리(TroopCode == WallCode) 또는 시설 수리(Facility 지정) — design-administration "건물 수리".
        if (req.TroopCode == FactionResearch.WallCode)
        {
            return IssueWallRepair(state, city, req, assist);
        }

        var (ruined, cost) = req.Facility switch
        {
            "paddy" => (city.RuinedPaddies > 0, FacilityRepairCost(_b.BuildCostPaddy)),
            "farm" => (city.RuinedFarms > 0, FacilityRepairCost(_b.BuildCostFarm)),
            "village" => (city.RuinedVillages > 0, FacilityRepairCost(_b.BuildCostVillage)),
            "workshop" => (city.WorkshopRuined, FacilityRepairCost(_b.BuildCostWorkshop)),
            "mine" => (city.MineDestroyed, _b.ResourceFacilityRepairCost),
            "ranch" => (city.RanchDestroyed, _b.ResourceFacilityRepairCost),
            "elephant_garden" => (city.ElephantGardenDestroyed, _b.ResourceFacilityRepairCost),
            _ => (false, -1),
        };
        if (cost < 0)
        {
            return CommandResult.Fail("알 수 없는 수리 대상이다.", state);
        }

        if (!ruined)
        {
            return CommandResult.Fail("수리할 파괴 시설이 없다.", state);
        }

        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        var reserved = city.AddGold(-cost);
        return Register(state, reserved, req, assist, amount: 0, _b.RepairDays, CommandKind.Repair, req.Facility);
    }

    private CommandResult IssueWallRepair(GameState state, City city, CommandRequest req, General? assist)
    {
        if (_balance is null)
        {
            return CommandResult.Fail("성벽 수리에는 경제 설정이 필요하다.", state);
        }

        var maxWall = CastleWall.Max(city.Castle, _balance, state.WallLevelOf(city.Owner));
        // 축성(재임 태수): 성벽 수리 회복량 +10/20/30%p(기본 25%·공방 +25%p와 합산 — design-skill-admin).
        var recovery = _b.WallRepairPercent + (city.Workshop ? _b.WallRepairWorkshopBonus : 0)
            + GovernorBucket(state, city, "wall");
        var restore = System.Math.Min(maxWall - city.Wall, maxWall * recovery / 100);
        if (restore <= 0)
        {
            return CommandResult.Fail("수리할 성벽 손상이 없다.", state);
        }

        var cost = restore / _b.WallRepairGoldDivisor;
        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        var reserved = city.AddGold(-cost);
        return Register(state, reserved, req, assist, restore, _b.RepairDays, CommandKind.Repair, "", req.TroopCode);
    }

    private int FacilityRepairCost(int buildCost) => buildCost * _b.RepairCostPercent / 100;

    // 도시 계략 발행(design-stratagem "수행 규칙"): 대상 = 정찰된 적 도시(정찰 계략만 전제 없음),
    // 소요일 = 거리 비례(CityStratagems.Days — 발행 전 컨펌 UI가 같은 값을 보여준다). 성공 판정은 정산에서.
    private CommandResult IssueCityStratagem(GameState state, City city, CommandRequest req, General? assist)
    {
        if (!CityStratagems.IsKind(req.Facility))
        {
            return CommandResult.Fail("알 수 없는 도시 계략이다.", state);
        }

        if (req.TargetCity is not { } targetId)
        {
            return CommandResult.Fail("대상 도시를 지정해야 한다.", state);
        }

        var target = state.Cities.FirstOrDefault(c => c.Id == targetId);
        if (target is null || target.Owner == city.Owner)
        {
            return CommandResult.Fail("적 도시만 대상이 될 수 있다.", state);
        }

        if (CityStratagems.RequiresScout(req.Facility) && !state.IsScouted(city.Owner, targetId))
        {
            return CommandResult.Fail("먼저 정찰해야 한다.", state);
        }

        var days = CityStratagems.Days(city.Position, target.Position, _b);
        return Register(state, city, req, assist, amount: 0, days, CommandKind.CityStratagem, req.Facility,
            targetCity: targetId);
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
        int amount, int days, CommandKind kind, string facility, string troopCode = "", CityId? targetCity = null,
        GeneralId? targetGeneral = null, Spatial.HexCoord? plot = null)
    {
        var cities = state.Cities.Select(c => c.Id == reservedCity.Id ? reservedCity : c).ToList();
        var command = new CityCommand(req.City, kind, req.Main, assist?.Id,
            state.Day, state.Day + days, amount, facility, troopCode, targetCity, req.TraineePool, targetGeneral, plot);
        var pending = state.Commands.Append(command).ToList();
        return CommandResult.Success(state with { Cities = cities, PendingCommands = pending });
    }

    /// <summary>
    /// 시장 매입 단가(100단위당 금, design-administration "시장"). 품목 기본가 × 이번 달 시세
    /// (<see cref="GameState.MarketPricePercent"/>) × (100 − 교역 할인). 교역(재임 태수 market_discount)이
    /// 매입가를 낮춘다. 경제 설정이 없으면 0.
    /// </summary>
    public int MarketUnitPricePer100(GameState state, City city, MarketResource res)
    {
        if (_balance is null)
        {
            return 0;
        }

        var basePer100 = res switch
        {
            MarketResource.Ore => _balance.MarketOrePrice * 100,
            MarketResource.Horses => _balance.MarketHorsePrice * 100,
            MarketResource.Elephants => _balance.MarketElephantPrice * 100,
            _ => _balance.MarketGrainPricePer100,
        };
        var discount = GovernorBucket(state, city, "market_discount");
        return (int)((long)basePer100 * state.MarketPricePercent / 100 * (100 - discount) / 100);
    }

    /// <summary>
    /// 시장 매입(design-administration "시장"). 즉시 실행 — 성 금고로 자원을 산다(장수·기간·잠금 없음).
    /// 군량은 옵셔널 품목 — 약탈·보급 차단 등 비상 시 금으로 메운다. 교역 스킬이 매입가를 낮춘다.
    /// </summary>
    public CommandResult BuyFromMarket(GameState state, CityId cityId, MarketResource res, int units)
    {
        if (_balance is null)
        {
            return CommandResult.Fail("시장에는 경제 설정이 필요하다.", state);
        }

        var city = state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null)
        {
            return CommandResult.Fail("도시를 찾을 수 없다.", state);
        }

        if (units <= 0)
        {
            return CommandResult.Fail("구매 수량을 지정해야 한다.", state);
        }

        var cost = (int)(((long)MarketUnitPricePer100(state, city, res) * units + 99) / 100); // 올림
        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        var bought = city.AddGold(-cost);
        bought = res switch
        {
            MarketResource.Ore => bought with { Ore = bought.Ore + units },
            MarketResource.Horses => bought with { Horses = bought.Horses + units },
            MarketResource.Elephants => bought with { Elephants = bought.Elephants + units },
            _ => bought with { Provisions = bought.Provisions + units },
        };
        var cities = state.Cities.Select(c => c.Id == cityId ? bought : c).ToList();
        return CommandResult.Success(state with { Cities = cities });
    }

    /// <summary>
    /// 포상(design-general-lifecycle §1). 즉시 실행 — 성 금고에서 포상 비용을 써 그 도시 주둔 소속
    /// 장수의 충성을 급히 끌어올린다(+RewardLoyaltyGain, **상한 100**). 급여 회복(+1~2/월)보다 빠른
    /// 응급 수단 — 이간·미지급으로 흔들린 장수를 배신 전에 붙잡는다. 충성 100 이상(완충)은 효과 없음.
    /// </summary>
    public CommandResult Reward(GameState state, CityId cityId, GeneralId target)
    {
        if (_balance is null)
        {
            return CommandResult.Fail("포상에는 경제 설정이 필요하다.", state);
        }

        var city = state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null)
        {
            return CommandResult.Fail("도시를 찾을 수 없다.", state);
        }

        if (!state.GeneralsAt(cityId).Contains(target)
            || state.PostingOf(target)?.Faction != city.Owner)
        {
            return CommandResult.Fail("이 도시에 주둔한 소속 장수만 포상할 수 있다.", state);
        }

        var cost = _balance.RewardGoldCost;
        if (city.Gold < cost)
        {
            return CommandResult.Fail("금이 부족하다.", state);
        }

        var gain = _random.Next(_balance.RewardLoyaltyGainMin, _balance.RewardLoyaltyGainMax + 1);
        var cities = state.Cities.Select(c => c.Id == cityId ? c.AddGold(-cost) : c).ToList();
        var generals = state.Generals.Select(g => g.Id == target && g.Loyalty < 100
            ? g with { Loyalty = System.Math.Min(100, g.Loyalty + gain) }
            : g).ToList();
        return CommandResult.Success(state with { Cities = cities, Generals = generals });
    }

    /// <summary>
    /// 태수 임명(design-administration F). 즉시 실행 — 그 도시에 주둔한 소속 장수를 태수로 지정한다.
    /// 기간·비용·잠금이 없고 진행 명령을 만들지 않는다(태수는 상주 역할이라 다른 내정 명령과 병행 가능).
    /// 임명되면 그 장수의 능력으로 수입 효율(정치)·내정 스킬·계략 방어(지력)·성 반격(무력)이 돈다.
    /// </summary>
    private CommandResult AppointGovernor(GameState state, City city, General main)
    {
        if (state.Assignments.Count > 0 && PostingError(state, main.Id, city) is { } e)
        {
            return CommandResult.Fail($"태수 {e}", state);
        }

        if (city.Governor == main.Id)
        {
            return CommandResult.Fail("이미 이 도시의 태수다.", state);
        }

        var cities = state.Cities.Select(c => c.Id == city.Id ? c with { Governor = main.Id } : c).ToList();
        return CommandResult.Success(state with { Cities = cities });
    }

    /// <summary>
    /// 군사 임명(design-general-lifecycle §6). 즉시 실행 — 그 도시 주둔 소속 장수를 군사로 지정한다.
    /// 기간·비용·잠금 없음. 군사는 그 도시에서 발행하는 등용의 성공/실패를 지력%로 예측해 준다.
    /// </summary>
    private CommandResult AppointStrategist(GameState state, City city, General main)
    {
        if (state.Assignments.Count > 0 && PostingError(state, main.Id, city) is { } e)
        {
            return CommandResult.Fail($"군사 {e}", state);
        }

        if (city.Strategist == main.Id)
        {
            return CommandResult.Fail("이미 이 도시의 군사다.", state);
        }

        var cities = state.Cities.Select(c => c.Id == city.Id ? c with { Strategist = main.Id } : c).ToList();
        return CommandResult.Success(state with { Cities = cities });
    }

    private CommandResult AppointCityOfficer(GameState state, City city, General main, CommandRequest req)
    {
        var kind = req.Kind;
        if (state.Assignments.Count > 0 && PostingError(state, main.Id, city) is { } e)
        {
            return CommandResult.Fail($"담당자 {e}", state);
        }

        var autoRecruitTroopCode = city.AutoRecruitTroopCode;
        var autoRecruitTroopCodes = city.AutoRecruitTroopCodes;
        if (kind == CommandKind.AppointRecruitmentOfficer)
        {
            var selectedTroops = AutoRecruitTroopCodes(req.TroopCode).ToList();
            if (selectedTroops.Count == 0)
            {
                selectedTroops.Add(_b.AutoRecruitDefaultTroopCode);
            }

            foreach (var code in selectedTroops)
            {
                if (!_troops.TryGetValue(code, out var troop))
                {
                    return CommandResult.Fail("자동 생산 병종을 선택해야 한다.", state);
                }

                if (troop.Class == TroopClass.Naval)
                {
                    return CommandResult.Fail("해상 병종은 자동 생산할 수 없다.", state);
                }
            }

            autoRecruitTroopCode = selectedTroops[0];
            autoRecruitTroopCodes = string.Join(',', selectedTroops);
        }

        var already = kind switch
        {
            CommandKind.AppointSecurityOfficer => city.SecurityOfficer == main.Id,
            CommandKind.AppointDomesticOfficer => city.DomesticOfficer == main.Id,
            CommandKind.AppointRecruitmentOfficer => city.RecruitmentOfficer == main.Id
                && city.AutoRecruitTroopCodes == autoRecruitTroopCodes,
            CommandKind.AppointTrainingOfficer => city.TrainingOfficer == main.Id,
            _ => false,
        };
        if (already)
        {
            return CommandResult.Fail("이미 이 담당으로 지정된 장수다.", state);
        }

        var cities = state.Cities.Select(c => c.Id == city.Id ? kind switch
        {
            CommandKind.AppointSecurityOfficer => c with { SecurityOfficer = main.Id },
            CommandKind.AppointDomesticOfficer => c with { DomesticOfficer = main.Id },
            CommandKind.AppointRecruitmentOfficer => c with
            {
                RecruitmentOfficer = main.Id,
                AutoRecruitTroopCode = autoRecruitTroopCode,
                AutoRecruitTroopCodes = autoRecruitTroopCodes,
            },
            CommandKind.AppointTrainingOfficer => c with { TrainingOfficer = main.Id },
            _ => c,
        } : c).ToList();
        return CommandResult.Success(state with { Cities = cities });
    }

    internal static IEnumerable<string> AutoRecruitTroopCodes(string value)
        => value.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries)
            .Distinct(System.StringComparer.Ordinal);

    /// <summary>
    /// 명령 취소(2026-08-23 사용자 결정): **시작 전(발행한 그 주, 첫 진행 전)에만** 취소할 수 있다.
    /// 진행이 한 번이라도 지났으면(발행일 ≠ 현재일) 취소 불가 — 완료까지 간다.
    /// 취소하면 명령 제거 + 수행 장수 즉시 해제. 발행 시 예약된 자원·비용은 환불하지 않는다.
    /// </summary>
    public static GameState Cancel(GameState state, CityCommand command)
    {
        if (state.Day != command.StartDay)
        {
            return state; // 이미 진행이 시작된 명령 — 취소 불가
        }

        var pending = state.Commands.ToList();
        var idx = pending.IndexOf(command);
        if (idx < 0)
        {
            return state;
        }

        pending.RemoveAt(idx);
        return state with { PendingCommands = pending };
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
