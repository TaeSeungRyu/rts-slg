namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed record RenewalGarrisonPayload(
    IReadOnlyDictionary<string, int> Troops,
    int Provisions = 0,
    int Gold = 0,
    bool DissolveArmyGroup = false)
{
    public static readonly RenewalGarrisonPayload Empty =
        new(new Dictionary<string, int>());
}

public sealed record RenewalDeploymentReservation(
    int Id,
    RenewalUnitState Unit,
    CityId OriginSite,
    int OrderedDay = 1,
    int DelayDays = 0,
    CityId? DestinationSite = null,
    RenewalGarrisonPayload? Payload = null)
{
    public int AvailableDay => checked(OrderedDay + DelayDays);
}

public sealed record RenewalEntryOrder(
    UnitId Unit,
    CityId Site,
    ContinuousPosition Approach,
    RenewalGarrisonPayload Payload);

public enum RenewalDeploymentEventKind
{
    Released,
    WaitingForDate,
    ExitBlocked,
    Entered,
    ArmyGroupDissolved,
}

public sealed record RenewalDeploymentEvent(
    RenewalDeploymentEventKind Kind,
    UnitId Unit,
    CityId Site,
    ContinuousPosition? Position = null);

public sealed record RenewalDeploymentReleaseResult(
    IReadOnlyList<RenewalUnitState> Released,
    IReadOnlyList<RenewalDeploymentReservation> Waiting,
    IReadOnlyList<RenewalEntryOrder> EntryOrders,
    IReadOnlyList<RenewalDeploymentEvent> Events);

public sealed record RenewalGarrisonTransfer(
    UnitId Unit,
    CityId Site,
    RenewalGarrisonPayload Payload);

public sealed record RenewalEntryResult(
    IReadOnlyList<RenewalUnitState> FieldUnits,
    IReadOnlyList<RenewalEntryOrder> PendingOrders,
    IReadOnlyList<RenewalGarrisonTransfer> Transfers,
    IReadOnlyList<RenewalDeploymentEvent> Events);

/// <summary>
/// Phase 18D 신규 이동 모드의 거점 경계 어댑터.
/// 아군끼리는 같은 출구를 공유하고, 적·지형·건물에 막힌 예약은 거점 내부에 보존한다.
/// 캠페인 상태를 직접 변경하지 않고 출격/입성 사건과 원자적 주둔 이전 자료만 반환한다.
/// </summary>
public sealed class RenewalDeploymentService
{
    private readonly RenewalMovementMap _map;

    public RenewalDeploymentService(RenewalMovementMap map) => _map = map;

    public RenewalDeploymentReleaseResult Release(
        int day,
        IEnumerable<RenewalDeploymentReservation> reservations,
        IReadOnlyList<RenewalUnitState> fieldUnits,
        IReadOnlyList<City> sites)
    {
        var siteById = sites.ToDictionary(x => x.Id);
        var released = new List<RenewalUnitState>();
        var waiting = new List<RenewalDeploymentReservation>();
        var orders = new List<RenewalEntryOrder>();
        var events = new List<RenewalDeploymentEvent>();

        foreach (var reservation in reservations.OrderBy(x => x.Id))
        {
            Validate(reservation);
            var unit = reservation.Unit;
            if (!siteById.TryGetValue(reservation.OriginSite, out var origin)
                || origin.Owner != unit.Owner)
            {
                waiting.Add(reservation);
                events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.ExitBlocked,
                    unit.Id, reservation.OriginSite));
                continue;
            }
            if (day < reservation.AvailableDay)
            {
                waiting.Add(reservation);
                events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.WaitingForDate,
                    unit.Id, origin.Id));
                continue;
            }

            var target = reservation.DestinationSite is { } destinationId
                && siteById.TryGetValue(destinationId, out var destination)
                    ? RenewalHexSpace.Center(destination.Position)
                    : unit.Destination;
            var targetHex = RenewalHexSpace.NearestHex(target);
            bool Usable(HexCoord hex) => _map.CanEnter(unit.Domain, hex);
            var direction = DeploymentEgressRules.Recommend(origin, targetHex, Usable);
            var exit = DeploymentEgressRules.RepresentativeExit(origin, direction, targetHex, Usable);
            if (exit is null)
            {
                waiting.Add(reservation);
                events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.ExitBlocked,
                    unit.Id, origin.Id));
                continue;
            }

            var exitPosition = RenewalHexSpace.Center(exit.Value);
            var enemyBlocks = fieldUnits.Concat(released)
                .Where(x => x.Owner != unit.Owner)
                .Any(x => x.Position.DistanceSquaredTo(exitPosition)
                    <= 4 * RenewalAdvanceSimulator.UnitCollisionRadius
                    * RenewalAdvanceSimulator.UnitCollisionRadius);
            if (enemyBlocks)
            {
                waiting.Add(reservation);
                events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.ExitBlocked,
                    unit.Id, origin.Id, exitPosition));
                continue;
            }

            ContinuousPosition destinationPosition = unit.Destination;
            RenewalEntryOrder? entryOrder = null;
            if (reservation.DestinationSite is { } siteId)
            {
                if (!siteById.TryGetValue(siteId, out var entrySite)
                    || entrySite.Owner != unit.Owner)
                {
                    waiting.Add(reservation);
                    events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.ExitBlocked,
                        unit.Id, origin.Id));
                    continue;
                }
                var approach = EntryApproach(entrySite, unit.Domain, exitPosition);
                if (approach is null)
                {
                    waiting.Add(reservation);
                    events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.ExitBlocked,
                        unit.Id, origin.Id));
                    continue;
                }
                destinationPosition = approach.Value;
                entryOrder = new RenewalEntryOrder(unit.Id, siteId, approach.Value,
                    reservation.Payload ?? RenewalGarrisonPayload.Empty);
            }

            var deployed = unit with
            {
                Position = exitPosition,
                Destination = destinationPosition,
                ArrivalPosition = null,
                Path = null,
                PathIndex = 0,
                MovementRemainder = 0,
                StopReason = RenewalStopReason.None,
                Arrived = exitPosition == destinationPosition,
            };
            released.Add(deployed);
            if (entryOrder is not null)
            {
                orders.Add(entryOrder);
            }
            events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.Released,
                unit.Id, origin.Id, exitPosition));
        }

        return new RenewalDeploymentReleaseResult(released, waiting, orders, events);
    }

    public RenewalEntryResult ResolveEntries(
        IReadOnlyList<RenewalUnitState> fieldUnits,
        IReadOnlyList<RenewalEntryOrder> orders,
        IReadOnlyList<City> sites)
    {
        var siteById = sites.ToDictionary(x => x.Id);
        var orderByUnit = orders.ToDictionary(x => x.Unit);
        var remaining = new List<RenewalUnitState>();
        var pending = new List<RenewalEntryOrder>();
        var transfers = new List<RenewalGarrisonTransfer>();
        var events = new List<RenewalDeploymentEvent>();

        foreach (var unit in fieldUnits.OrderBy(x => x.Id.Value))
        {
            if (!orderByUnit.TryGetValue(unit.Id, out var order)
                || !siteById.TryGetValue(order.Site, out var site)
                || site.Owner != unit.Owner
                || unit.Position.DistanceTo(order.Approach) > RenewalAdvanceSimulator.ArrivalTolerance)
            {
                remaining.Add(unit);
                continue;
            }

            transfers.Add(new RenewalGarrisonTransfer(unit.Id, site.Id, order.Payload));
            events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.Entered,
                unit.Id, site.Id, order.Approach));
            if (order.Payload.DissolveArmyGroup)
            {
                events.Add(new RenewalDeploymentEvent(RenewalDeploymentEventKind.ArmyGroupDissolved,
                    unit.Id, site.Id, order.Approach));
            }
        }

        var entered = transfers.Select(x => x.Unit).ToHashSet();
        pending.AddRange(orders.Where(x => !entered.Contains(x.Unit)));
        return new RenewalEntryResult(remaining, pending, transfers, events);
    }

    private ContinuousPosition? EntryApproach(City site, MovementDomain domain,
        ContinuousPosition from)
    {
        return DeploymentEgressRules.ExteriorTiles(site)
            .Where(hex => _map.CanEnter(domain, hex))
            .Select(RenewalHexSpace.Center)
            .OrderBy(point => point.DistanceSquaredTo(from))
            .ThenBy(point => point.X)
            .ThenBy(point => point.Y)
            .Cast<ContinuousPosition?>()
            .FirstOrDefault();
    }

    private static void Validate(RenewalDeploymentReservation reservation)
    {
        if (reservation.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reservation), "예약 ID는 양수여야 합니다.");
        }
        if (reservation.DelayDays is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(reservation), "출전 지연은 0~6일입니다.");
        }
    }
}
