namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record FieldStratagemPreview(
    Stratagem Stratagem,
    UnitId Caster,
    UnitId Target,
    int FireDay,
    int CasterIntellect,
    int TargetIntellect,
    int IntellectDifference,
    int StrengthPercent);

public sealed record FieldStratagemResult(
    bool Ok,
    string? Error,
    GameState State,
    FieldStratagemPreview? Preview)
{
    public static FieldStratagemResult Fail(string error, GameState state) => new(false, error, state, null);

    public static FieldStratagemResult Success(GameState state, FieldStratagemPreview preview)
        => new(true, null, state, preview);
}

public sealed class FieldStratagemService
{
    private readonly IReadOnlyDictionary<string, Stratagem> _stratagems;
    private readonly Func<HexCoord, TerrainType> _terrainAt;

    public FieldStratagemService(IReadOnlyList<Stratagem> stratagems, Func<HexCoord, TerrainType> terrainAt)
    {
        _stratagems = stratagems.ToDictionary(s => s.Code, StringComparer.Ordinal);
        _terrainAt = terrainAt;
    }

    public IReadOnlyList<Stratagem> Castable(GameState state, UnitId casterId)
    {
        var caster = FindLiving(state, casterId);
        if (caster is null || caster.VanguardId is null || caster.State.Reservation is not null)
        {
            return [];
        }

        return _stratagems.Values
            .Where(s => caster.State.Resource.CanSpend(s.Cost)
                && StratagemMastery.IsUnlocked(s.RequiredLevel, caster.State.MasteryLevel))
            .OrderBy(s => s.RequiredLevel)
            .ThenBy(s => s.Code, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<CombatUnit> Targets(GameState state, UnitId casterId, string code)
    {
        var caster = FindLiving(state, casterId);
        if (caster is null || !Castable(state, casterId).Any(s => s.Code == code)
            || !_stratagems.TryGetValue(code, out var stratagem))
        {
            return [];
        }

        var targetsAllies = stratagem.EffectKind == StratagemEffectKind.Purge;
        return state.Armies
            .Where(target => target.Pool.Active > 0
                && (targetsAllies
                    ? target.Field.Owner == caster.Field.Owner
                    : target.Field.Owner != caster.Field.Owner)
                && caster.Field.Position.Distance(target.Field.Position) <= stratagem.Range
                && stratagem.CanCastOn(_terrainAt(target.Field.Position)))
            .OrderBy(target => target.Id.Value)
            .ToList();
    }

    public FieldStratagemResult Reserve(GameState state, UnitId casterId, string code, UnitId targetId)
    {
        var caster = FindLiving(state, casterId);
        if (caster is null)
        {
            return FieldStratagemResult.Fail("시전 부대를 찾을 수 없다.", state);
        }

        if (caster.VanguardId is null)
        {
            return FieldStratagemResult.Fail("선봉 장수가 없는 부대는 계략을 시전할 수 없다.", state);
        }

        if (!_stratagems.TryGetValue(code, out var stratagem))
        {
            return FieldStratagemResult.Fail("계략을 찾을 수 없다.", state);
        }

        if (caster.State.Reservation is not null)
        {
            return FieldStratagemResult.Fail("이미 준비 중인 계략이 있다.", state);
        }

        if (!caster.State.Resource.CanSpend(stratagem.Cost))
        {
            return FieldStratagemResult.Fail("모략력이 부족하다.", state);
        }

        if (!StratagemMastery.IsUnlocked(stratagem.RequiredLevel, caster.State.MasteryLevel))
        {
            return FieldStratagemResult.Fail("계략 숙달 단계가 부족하다.", state);
        }

        var target = Targets(state, casterId, code).FirstOrDefault(u => u.Id == targetId);
        if (target is null)
        {
            return FieldStratagemResult.Fail("지정할 수 없는 대상이다.", state);
        }

        var updated = caster with { State = caster.State.ReserveStratagem(stratagem, targetId) };
        var armies = state.Armies.Select(u => u.Id == casterId ? updated : u).ToList();
        var strength = stratagem.EffectKind == StratagemEffectKind.Purge
            ? 100
            : StratagemStrength.Percent(caster.Intellect, target.Intellect);
        var preview = new FieldStratagemPreview(
            stratagem,
            casterId,
            targetId,
            state.Day + StratagemReservation.LeadDays,
            caster.Intellect,
            target.Intellect,
            caster.Intellect - target.Intellect,
            strength);

        return FieldStratagemResult.Success(state with { FieldArmies = armies }, preview);
    }

    private static CombatUnit? FindLiving(GameState state, UnitId id)
        => state.Armies.FirstOrDefault(u => u.Id == id && u.Pool.Active > 0);
}
