namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public readonly record struct RenewalTargetId(RenewalTargetKind Kind, long Value)
{
    public static RenewalTargetId ForUnit(UnitId id) => new(RenewalTargetKind.Unit, id.Value);

    public override string ToString() => $"{Kind}:{Value}";
}
