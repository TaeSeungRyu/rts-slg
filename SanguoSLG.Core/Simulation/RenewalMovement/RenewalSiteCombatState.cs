namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalSiteCombatState(
    RenewalTargetId Id,
    FactionId Owner,
    ContinuousPosition Position,
    CastleState Castle,
    CastleSize CastleSize = CastleSize.Small,
    PortSize PortSize = PortSize.None,
    bool IsVisible = true)
{
    public int ParticipationLimit => PortSize switch
    {
        PortSize.Small => 3,
        PortSize.Medium => 5,
        _ => CastleSize switch
        {
            CastleSize.Small => 3,
            CastleSize.Medium => 5,
            CastleSize.Large => 7,
            _ => throw new ArgumentOutOfRangeException(),
        },
    };
}
