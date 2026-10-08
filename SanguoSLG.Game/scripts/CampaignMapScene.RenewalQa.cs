using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
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
    }
}
