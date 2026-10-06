namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalCombatProfile(
    UnitId Unit,
    BattleParticipant Participant,
    int? BuildingAttack = null,
    int CarryingGold = 0,
    int Provisions = 0);
