using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private async Task RunRenewalCampaignPipelineQa()
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        var expected = _pendingDeploys.Select(entry => entry.Req.ContinuousTarget).ToArray();
        Require(expected.Length == 2 && expected.All(point => point.HasValue), "Exact pointer goals missing");
        Require(expected[0] != expected[1]
            && RenewalHexSpace.NearestHex(expected[0]!.Value)
                == RenewalHexSpace.NearestHex(expected[1]!.Value),
            "Two clicks in one tile did not preserve distinct point goals");
        var initialState = _state;
        var cityId = _pendingDeploys[0].Req.City;
        var generals = _pendingDeploys.Select(entry => entry.Req.Vanguard).ToArray();
        _state = _state with
        {
            FieldArmies = [], PendingCommands = [],
            RuinDefinitions = [], RuinStates = [],
            Postings = _state.Assignments.Where(posting => !generals.Contains(posting.General))
                .Concat(generals.Select(id => new GeneralPosting(id, Player, cityId))).ToList(),
            GarrisonForces = _state.Garrisons.Where(force => force.City != cityId || force.TroopCode != "cavalry")
                .Append(new GarrisonForce(cityId, "cavalry", 10000, 70)).ToList(),
        };
        foreach (var hex in _map.Tiles())
        {
            Require(WorldToContinuous(_view.HexToWorld(hex)) == RenewalHexSpace.Center(hex),
                $"World/Core basis mismatch at {hex}");
        }
        CloseModal();
        var startDay = _state.Day;
        SetProcess(false);
        for (var week = 0; week < 2; week++)
        {
            StartAdvance();
            Require(_advancing, "StartAdvance did not start playback");
            Require(_pendingState.Day == startDay + (week + 1) * 7, "Campaign clock did not advance seven days");
            var tracks = _animContinuousTracks.Where(track => track.UnitId > 0).ToList();
            Require(tracks.Count > 0, "Campaign supplied no continuous tracks");
            Require(!_animSteps.Any(step => tracks.Any(track => track.UnitId == step.UnitId)
                && step.Time < MoveSeconds), "Legacy deployment tween competes with continuous movement");
            var checks = 0;
            foreach (var fps in new[] { 30, 60, 144 })
            {
                _completedContinuousTracks.Clear();
                foreach (var track in tracks.OrderBy(track => track.Start))
                {
                    if (!_armyTokens.TryGetValue(track.UnitId, out var token)) continue;
                    for (var frame = 0; frame <= Math.Ceiling((track.End - track.Start) * fps); frame++)
                    {
                        _animT = Math.Min(track.End, track.Start + frame / (double)fps);
                        ApplyContinuousMovementPlayback();
                    }
                    var endpoint = ContinuousToWorld(track.Points[^1]) + new Vector3(0, _view.TileTopY, 0);
                    Require(token.Position.DistanceTo(endpoint) < .001f,
                        $"Playback endpoint mismatch fps={fps} unit={track.UnitId} day={track.Start}");
                    checks++;
                }
            }
            FinishAdvance();
            Require(_state.Armies.Count(unit => unit.Field.Owner == Player) == 2,
                $"Deployment/casualty mismatch: {_state.Armies.Count} armies");
            foreach (var unit in _state.Armies)
            {
                Require(unit.RenewalPosition.HasValue, "Campaign position lost");
                var world = ContinuousToWorld(unit.RenewalPosition!.Value) + new Vector3(0, _view.TileTopY, 0);
                Require(_armyTokens[unit.Id.Value].Position.DistanceTo(world) < .001f, "FinishAdvance snapped to tile center");
            }
            _state = SaveService.Deserialize(SaveService.Serialize(_state));
            GD.Print($"RENEWAL_CAMPAIGN_QA week={week + 1} day={_state.Day} armies={_state.Armies.Count} fpsEndpointChecks={checks} saveReload=PASS");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Require(_state.Armies.Where(unit => unit.Field.Owner == Player)
            .All(unit => unit.Field.ContinuousTarget is { } goal && expected.Contains(goal)),
            "Second advance changed the confirmed goal");
        GD.Print("RENEWAL_CAMPAIGN_QA PASS: pointer confirmations -> deployment -> 14 days -> 30/60/144FPS -> save/reload");
        RunCombatPlaybackTargetQa();
        RunRenewalRealMapQa(initialState);
        RunFieldBuildingActivePlaybackQa(initialState);
        RunBlockedFieldCombatQa(initialState);
    }

    private void RunCombatPlaybackTargetQa()
    {
        CombatUnit Unit(int id, int owner, HexCoord tile, ContinuousPosition position) => new(
            new FieldUnit(new UnitId(id), new FactionId(owner), tile, 1, 2, 2,
                MovementDomain.Land, UnitMode.Advance, null, id),
            new CombatStats(10000, 10, 10), new TroopPool(10000, 0),
            UnitCombatState.Create(60), RenewalPosition: position);
        var attacker = Unit(9101, 1, new HexCoord(0, 0), new ContinuousPosition(0, 0));
        var primary = Unit(9102, 2, new HexCoord(2, 0), new ContinuousPosition(1500, 100));
        var secondary = Unit(9103, 2, new HexCoord(1, 0), new ContinuousPosition(600, 0));
        var combat = new CombatPhaseResult(
            new Dictionary<UnitId, int> { [primary.Id] = 100, [secondary.Id] = 60 },
            new Dictionary<UnitId, int> { [attacker.Id] = 160 },
            new Dictionary<UnitId, TroopPool>
            {
                [attacker.Id] = attacker.Pool,
                [primary.Id] = primary.Pool,
                [secondary.Id] = secondary.Pool,
            });
        var movement = new AdvanceResult([], [attacker.Field, primary.Field, secondary.Field],
            StopReason.MaxDays, 1);
        var turn = new AdvanceTurn([attacker, primary, secondary], movement, combat,
            new Dictionary<UnitId, ActiveSkill>(), new Dictionary<UnitId, Stratagem>(),
            new Dictionary<UnitId, int>(), new Dictionary<UnitId, int>(),
            FieldCombatExchangeResults:
            [
                new FieldCombatExchange(attacker.Id, primary.Id,
                    attacker.RenewalPosition!.Value, primary.RenewalPosition!.Value, true),
                new FieldCombatExchange(attacker.Id, secondary.Id,
                    attacker.RenewalPosition!.Value, secondary.RenewalPosition!.Value, false),
            ]);
        _animAttacks.Clear();
        ScheduleAttackMotions(turn, 1.0, new Dictionary<int, CombatUnit>
        {
            [attacker.Id.Value] = attacker,
            [primary.Id.Value] = primary,
            [secondary.Id.Value] = secondary,
        });
        var scheduled = _animAttacks.Single(value => value.UnitId == attacker.Id.Value);
        var expected = ContinuousToWorld(primary.RenewalPosition.Value);
        if (scheduled.FaceTo.DistanceTo(expected) > .001f)
            throw new InvalidOperationException("Combat playback did not face the actual primary target");
        GD.Print("RENEWAL_COMBAT_PLAYBACK_QA PASS: actual primary target -> facing position");
    }

    private void RunBlockedFieldCombatQa(GameState initialState)
    {
        var fort = initialState.Buildings.Single(building => building.Id.Value == 800009);
        var attacker = new CombatUnit(new FieldUnit(new UnitId(990011), Player,
                new HexCoord(1, 4), 1, 3, 1, MovementDomain.Land, UnitMode.Attack,
                fort.Position, 1, RangeCastle: 1),
            new CombatStats(10000, 20, 20), new TroopPool(10000, 0), UnitCombatState.Create(60),
            TroopCode: "cavalry", RenewalPosition: RenewalHexSpace.Center(new HexCoord(1, 4)));
        var blocker = new CombatUnit(new FieldUnit(new UnitId(990012), fort.Owner,
                new HexCoord(1, 5), 1, 3, 1, MovementDomain.Land, UnitMode.March,
                new HexCoord(1, 5), 2),
            new CombatStats(10000, 20, 20), new TroopPool(10000, 0), UnitCombatState.Create(60),
            TroopCode: "swordsman", RenewalPosition: RenewalHexSpace.Center(new HexCoord(1, 5)));
        var fixture = initialState with
        {
            FieldArmies = [attacker, blocker], FieldBuildings = [fort],
            PendingCommands = [], Cities = [], GarrisonForces = [], RuinDefinitions = [], RuinStates = [],
        };
        _engine.AdvanceWeek(fixture, out var turns);
        var combatTurn = turns.FirstOrDefault(turn => turn.FieldCombatExchanges.Any(exchange =>
            exchange.Attacker == attacker.Id && exchange.Target == blocker.Id));
        if (combatTurn is null || combatTurn.Combat?.DamageDealt.GetValueOrDefault(attacker.Id) <= 0)
            throw new InvalidOperationException("Building order ignored the enemy unit blocking its path");
        var priorPendingState = _pendingState;
        var previousAttackCount = _animAttacks.Count;
        _pendingState = fixture;
        ScheduleAttackMotions(combatTurn, 1);
        var hit = combatTurn.FieldCombatExchanges.First(exchange =>
            exchange.Attacker == attacker.Id && exchange.Target == blocker.Id);
        if (!_animAttacks.Skip(previousAttackCount).Any(attack => attack.UnitId == attacker.Id.Value
                && attack.FaceTo.DistanceTo(ContinuousToWorld(hit.TargetPosition)) < .001f))
            throw new InvalidOperationException("Blocking unit combat did not schedule the attack animation");
        _animAttacks.RemoveRange(previousAttackCount, _animAttacks.Count - previousAttackCount);
        _pendingState = priorPendingState;
        GD.Print("RENEWAL_BLOCKED_COMBAT_QA PASS: real-map combat -> damage -> attack animation on blocking unit");
    }

    private void RunFieldBuildingActivePlaybackQa(GameState initialState)
    {
        var fort = initialState.Buildings.Single(building => building.Id.Value == 800009)
            with { HitPoints = 1000000 };
        var start = fort.Position.Neighbors().First(hex => _map.Contains(hex)
            && TerrainRules.CanEnter(MovementDomain.Land, _map.TerrainAt(hex)));
        var active = new ActiveSkill("crush", "분쇄", ActiveType.Strike, "high", 180, BuildingOnly: true);
        var unit = new CombatUnit(new FieldUnit(new UnitId(990001), Player, start, 1, 3, 1,
            MovementDomain.Land, UnitMode.Attack, fort.Position, 1, RangeCastle: 1),
            new CombatStats(10000, 20, 20), new TroopPool(10000, 0),
            UnitCombatState.Create(60, active).AdvanceField(5), TroopCode: "cavalry",
            RenewalPosition: RenewalHexSpace.Center(start));
        var fixture = initialState with { FieldArmies = [unit], FieldBuildings = [fort],
            PendingCommands = [], Cities = [], GarrisonForces = [], RuinDefinitions = [], RuinStates = [] };
        _engine.AdvanceWeek(fixture, out var turns);
        var turn = turns.First(value => value.FiredActives.ContainsKey(unit.Id));
        var priorState = _state;
        _state = fixture;
        var before = _animSiegeSkillEffects.Count;
        ScheduleFieldBuildingCombat(turn, 1, 2);
        if (_animSiegeSkillEffects.Count != before + 1
            || _animSiegeSkillEffects[^1].CasterUnitId != unit.Id.Value
            || _animSiegeSkillEffects[^1].Target.DistanceTo(_view.HexToWorld(fort.Position)
                + new Vector3(0f, _view.TileTopY + .25f, 0f)) > .001f)
            throw new InvalidOperationException("Building active effect was not scheduled at its actual target");
        _state = priorState;
        GD.Print("FIELD_BUILDING_ACTIVE_QA PASS: campaign firing -> building effect target -> gauge reset");
    }

    private void RunRenewalRealMapQa(GameState initialState)
    {
        _state = initialState;
        _pendingDeploys.Clear();
        _movementStatus.Clear();
        var city = _state.Cities.Single(city => city.Id.Value == 1);
        var general = _state.Assignments.First(posting => posting.Location == city.Id).General;
        var scout = _state.Buildings.Single(building => building.Id.Value == 800006);
        var fort = _state.Buildings.Single(building => building.Id.Value == 800009);
        if (scout.DefinitionCode != "scout_post" || fort.DefinitionCode != "fort")
            throw new InvalidOperationException("Real-map building fixture changed");
        var start = RenewalHexSpace.Center(new HexCoord(1, 4));
        var goal = RenewalHexSpace.Center(fort.Position);
        _pendingDeploys.Add((new DeployRequest(city.Id, "cavalry", 10000, general,
            Mode: UnitMode.Advance, Target: fort.Position, Provisions: 250,
            EgressDirection: DeploymentDirection.SouthEast, EgressExit: new HexCoord(1, 4),
            ContinuousTarget: goal), "실제 지도 정찰대/보루 QA"));
        StartAdvance();
        var tracks = _animContinuousTracks.Where(track => track.UnitId > 0).ToList();
        if (tracks.Count == 0 || !tracks.Any(track => track.Points.Any(point => point.Y >= start.Y + 1000)))
            throw new InvalidOperationException("Real-map cavalry did not pass the scout and advance toward the fort");
        var firstProgress = tracks.Max(track => track.Points.Max(point => point.DistanceTo(start)));
        FinishAdvance();
        StartAdvance();
        FinishAdvance();
        if (_state.Buildings.Any(building => building.Id == fort.Id && building.HitPoints >= fort.HitPoints))
            throw new InvalidOperationException("Real-map cavalry never attacked the fort after 14 days");
        GD.Print($"RENEWAL_REAL_MAP_QA PASS: scout=800006 fort=800009 progress={firstProgress} day={_state.Day}");
    }
}
