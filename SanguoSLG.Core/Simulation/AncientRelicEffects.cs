namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class AncientRelicEffects
{
    public static int Bonus(GameState state, IReadOnlyList<AncientRelicDefinition> definitions,
        GeneralId? general, FactionId faction, string effect, TroopClass? troopClass = null)
    {
        if (general is null) return 0;
        var equipped = state.Treasures.FirstOrDefault(x => x.Faction == faction && x.EquippedBy == general);
        if (equipped is null) return 0;
        var definition = definitions.FirstOrDefault(x => x.Code == equipped.Code && x.Effect == effect);
        if (definition is null) return 0;
        if (troopClass is not null && definition.TroopClass != troopClass.Value.ToString()) return 0;
        return Math.Clamp(definition.Value, 0, 5);
    }

    public static int UnitBonus(GameState state, IReadOnlyList<AncientRelicDefinition> definitions,
        CombatUnit unit, string effect, TroopClass? troopClass = null)
        => Math.Max(Bonus(state, definitions, unit.VanguardId, unit.Field.Owner, effect, troopClass),
            Bonus(state, definitions, unit.AdjutantId, unit.Field.Owner, effect, troopClass));
}
