namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class TreasureEquipmentService(IReadOnlyList<EventTreasureDefinition> definitions)
{
    /// <summary>장수 사망·타 세력 편입 시 원 세력 창고로 자동 회수한다. 아이템 소유 세력은 바뀌지 않는다.</summary>
    public static GameState ReconcileOwners(GameState state)
        => state with { FactionTreasures = state.Treasures.Select(item =>
            item.EquippedBy is { } wearer && !state.Assignments.Any(posting =>
                posting.General == wearer && posting.Faction == item.Faction)
                ? item with { EquippedBy = null } : item).ToList() };

    public CommandResult Equip(GameState state, string treasureId, GeneralId generalId)
    {
        var treasure = state.Treasures.FirstOrDefault(x => x.Id == treasureId);
        var posting = state.Assignments.FirstOrDefault(x => x.General == generalId);
        if (treasure is null || !definitions.Any(x => x.Code == treasure.Code))
            return CommandResult.Fail("보물을 찾을 수 없습니다.", state);
        if (state.Encounters.Any(x => x.Id == treasure.EncounterId && x.RewardChoicePending))
            return CommandResult.Fail("신수 처치 보상을 먼저 선택해야 합니다.", state);
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
