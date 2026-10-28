using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.AI;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation;
using System;
using System.Diagnostics;
using System.Linq;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RunNewMapAiQa()
    {
        const int weeks = 24;
        var clock = Stopwatch.StartNew();
        var startingOwners = _state.Cities.ToDictionary(city => city.Id, city => city.Owner);
        var peakArmies = 0;
        var aiDeployments = 0;
        var activeSkillFires = 0;
        var activeGaugeUnits = 0;
        var japaneseNavalDeployments = 0;
        var initialKoreanPortTroops = _state.Garrisons.Where(line => line.City == _state.Cities.First(city => city.Name == "한국항").Id)
            .Sum(line => line.Troops);
        var japan = new FactionId(3);
        var koreanPort = _state.Cities.First(city => city.Name == "한국항");
        var initialKoreanPortWall = koreanPort.Wall;
        var navalRoutes = AiWarCandidatePolicy.Evaluate(_state, japan, _map,
            new BalanceConfig(MonthlyTaxPerCity: 100), 1).Where(candidate => candidate.Domain == MovementDomain.DeepWater
                && candidate.Target == koreanPort.Id).ToArray();
        GD.Print($"[newmap-naval-qa] japanRoutesToBusan={navalRoutes.Length} "
            + $"japanShips={_state.PortShips.Where(ship => _state.Cities.Any(city => city.Id == ship.City && city.Owner == japan)).Sum(ship => ship.Count)}");
        try
        {
            for (var week = 1; week <= weeks; week++)
            {
                var beforeIds = _state.Armies.Select(unit => unit.Id).ToHashSet();
                StartAdvance();
                activeSkillFires += _animActives.Count;
                activeGaugeUnits += _state.Armies.Count(unit =>
                    (unit.State.VanguardActive is not null || unit.State.AdjutantActive is not null)
                    && _activeGauges.ContainsKey(unit.Id.Value));
                var after = _pendingState;
                aiDeployments += after.Armies.Count(unit => unit.Field.Owner != Player && !beforeIds.Contains(unit.Id));
                japaneseNavalDeployments += after.Armies.Count(unit => unit.Field.Owner == japan
                    && unit.Class == TroopClass.Naval && unit.Field.Target == koreanPort.Position
                    && !beforeIds.Contains(unit.Id));
                peakArmies = Math.Max(peakArmies, after.Armies.Count);
                FinishAdvance();
                GD.Print($"[newmap-ai-qa] week={week} day={_state.Day} armies={_state.Armies.Count} "
                    + $"enemyArmies={_state.Armies.Count(unit => unit.Field.Owner != Player)} "
                    + $"captured={_state.Cities.Count(city => city.Owner != startingOwners[city.Id])} "
                    + $"elapsedMs={clock.ElapsedMilliseconds}");
            }
            var koreanPortTroops = _state.Garrisons.Where(line => line.City == koreanPort.Id)
                .Sum(line => line.Troops);
            var finalKoreanPort = _state.Cities.First(city => city.Id == koreanPort.Id);
            var passed = _state.Day >= weeks * 7 && aiDeployments > 0
                && activeSkillFires > 0 && activeGaugeUnits > 0
                && navalRoutes.Length > 0 && japaneseNavalDeployments > 0
                && (finalKoreanPort.Wall < initialKoreanPortWall || finalKoreanPort.Owner == japan);
            GD.Print($"[newmap-ai-qa] passed={passed} weeks={weeks} aiDeployments={aiDeployments} "
                + $"japaneseNavalAttacksOnKoreanPort={japaneseNavalDeployments} "
                + $"koreanPortTroops={initialKoreanPortTroops}->{koreanPortTroops} "
                + $"koreanPortWall={initialKoreanPortWall}->{finalKoreanPort.Wall} "
                + $"koreanPortOwner={finalKoreanPort.Owner.Value} "
                + $"peakArmies={peakArmies} activeFires={activeSkillFires} gaugeUnits={activeGaugeUnits} captured={_state.Cities.Count(city => city.Owner != startingOwners[city.Id])} "
                + $"elapsedMs={clock.ElapsedMilliseconds}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            GD.PushError($"[newmap-ai-qa] failed week day={_state.Day}: {error}");
            GetTree().Quit(1);
        }
    }
}
