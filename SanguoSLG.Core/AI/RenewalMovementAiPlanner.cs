namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>플레이어와 같은 <see cref="RenewalCommandService"/>를 사용하는 신규 이동 AI 어댑터.</summary>
public sealed class RenewalMovementAiPlanner
{
    private readonly RenewalCommandService _commands;

    public RenewalMovementAiPlanner(RenewalCommandService? commands = null) =>
        _commands = commands ?? new RenewalCommandService();

    public RenewalAdvanceState Plan(RenewalAdvanceState state, FactionId faction)
    {
        var current = state;
        foreach (var unit in state.Units
            .Where(x => x.Owner == faction && x.IsActive
                && x.GarrisonStructure is null && x.ProductionOperationId is null)
            .OrderBy(x => x.Id.Value))
        {
            var fresh = current.Units.First(x => x.Id == unit.Id);
            if (fresh.Mode == RenewalOrderMode.Attack && fresh.AssignedTarget is not null)
            {
                continue;
            }

            var target = Targets(current)
                .Where(x => x.Owner != faction && x.IsActive && x.IsVisible)
                .OrderBy(x => fresh.Position.DistanceSquaredTo(x.Position))
                .ThenBy(x => x.Id.Kind)
                .ThenBy(x => x.Id.Value)
                .FirstOrDefault();
            if (target is null)
            {
                continue;
            }

            current = _commands.Apply(current, new RenewalUnitCommand(
                Math.Max(1, fresh.CommandId + 1), fresh.Id, RenewalOrderMode.Attack,
                target.Position, AssignedTarget: target.Id));
        }

        return current;
    }

    private static IEnumerable<RenewalTargetState> Targets(RenewalAdvanceState state) =>
        state.Units.Select(x => new RenewalTargetState(
            RenewalTargetId.ForUnit(x.Id), x.Owner, x.Position, x.IsActive, x.IsVisible))
        .Concat(state.ExternalTargets ?? [])
        .Concat((state.Structures ?? []).Select(x => new RenewalTargetState(
            x.Id, x.Owner, x.Position, x.IsActive, x.IsVisible)))
        .Concat((state.Sites ?? []).Select(x => new RenewalTargetState(
            x.Id, x.Owner, x.Position, true, x.IsVisible)));
}
