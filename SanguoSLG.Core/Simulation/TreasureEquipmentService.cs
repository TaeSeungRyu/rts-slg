namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class TreasureEquipmentService(IReadOnlyList<EventTreasureDefinition> definitions)
{
    public CommandResult Equip(GameState state, string treasureId, GeneralId generalId)
    {
        var treasure = state.Treasures.FirstOrDefault(x => x.Id == treasureId);
        var posting = state.Assignments.FirstOrDefault(x => x.General == generalId);
        if (treasure is null || !definitions.Any(x => x.Code == treasure.Code))
            return CommandResult.Fail("보물을 찾을 수 없습니다.", state);
        if (posting is null || posting.Faction != treasure.Faction || posting.Location is null
            || state.IsGeneralBusy(generalId))
            return CommandResult.Fail("대기 중인 같은 세력 장수만 장착할 수 있습니다.", state);
        if (treasure.EquippedBy is not null || state.Treasures.Any(x => x.EquippedBy == generalId))
            return CommandResult.Fail("아이템은 장수당 1개만 장착할 수 있습니다.", state);
        return CommandResult.Success(state with { FactionTreasures = state.Treasures.Select(x =>
            x.Id == treasureId ? x with { EquippedBy = generalId } : x).ToList() });
    }

    public CommandResult Unequip(GameState state, string treasureId)
    {
        var treasure = state.Treasures.FirstOrDefault(x => x.Id == treasureId);
        if (treasure?.EquippedBy is not { } generalId)
            return CommandResult.Fail("장착 중인 보물이 아닙니다.", state);
        if (state.IsGeneralBusy(generalId) || !state.Assignments.Any(x => x.General == generalId
                && x.Faction == treasure.Faction && x.Location is not null))
            return CommandResult.Fail("대기 중인 장수만 해제할 수 있습니다.", state);
        return CommandResult.Success(state with { FactionTreasures = state.Treasures.Select(x =>
            x.Id == treasureId ? x with { EquippedBy = null } : x).ToList() });
    }
}
