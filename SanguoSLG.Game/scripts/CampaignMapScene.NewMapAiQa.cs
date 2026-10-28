using Godot;
using SanguoSLG.Core.Domain;
using System;
using System.Diagnostics;
using System.Linq;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RunNewMapAiQa()
    {
        const int weeks = 12;
        var clock = Stopwatch.StartNew();
        var startingOwners = _state.Cities.ToDictionary(city => city.Id, city => city.Owner);
        var peakArmies = 0;
        var aiDeployments = 0;
        try
        {
            for (var week = 1; week <= weeks; week++)
            {
                var beforeIds = _state.Armies.Select(unit => unit.Id).ToHashSet();
                StartAdvance();
                var after = _pendingState;
                aiDeployments += after.Armies.Count(unit => unit.Field.Owner != Player && !beforeIds.Contains(unit.Id));
                peakArmies = Math.Max(peakArmies, after.Armies.Count);
                FinishAdvance();
                GD.Print($"[newmap-ai-qa] week={week} day={_state.Day} armies={_state.Armies.Count} "
                    + $"enemyArmies={_state.Armies.Count(unit => unit.Field.Owner != Player)} "
                    + $"captured={_state.Cities.Count(city => city.Owner != startingOwners[city.Id])} "
                    + $"elapsedMs={clock.ElapsedMilliseconds}");
            }
            var passed = _state.Day >= weeks * 7 && aiDeployments > 0;
            GD.Print($"[newmap-ai-qa] passed={passed} weeks={weeks} aiDeployments={aiDeployments} "
                + $"peakArmies={peakArmies} captured={_state.Cities.Count(city => city.Owner != startingOwners[city.Id])} "
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
