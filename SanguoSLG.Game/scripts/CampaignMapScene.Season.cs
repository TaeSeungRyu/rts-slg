using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private static MapView3D.PreviewSeason VisualSeasonForMonth(int month) => month switch
    {
        9 or 10 or 11 => MapView3D.PreviewSeason.Autumn,
        12 or 1 => MapView3D.PreviewSeason.Winter,
        _ => MapView3D.PreviewSeason.Default,
    };

    private async void RefreshSeasonVisuals()
    {
        var season = VisualSeasonForMonth(_state.Month);
        if (_view.SeasonPreview == season) return;
        try { await _view.SetSeasonBatchedAsync(season); }
        catch (Exception error) { GD.PushError($"계절 지형 전환 실패: {error}"); }
    }

    private async void RunSeasonQa()
    {
        try
        {
            var capture = OS.GetCmdlineUserArgs().Contains("--maptestseasoncapture");
            if (capture)
            {
                GetWindow().Size = new Vector2I(1600, 900);
                GetWindow().ContentScaleSize = new Vector2I(1600, 900);
            }
            for (var month = 1; month <= 12; month++)
            {
                var expected = month is 9 or 10 or 11 ? MapView3D.PreviewSeason.Autumn
                    : month is 12 or 1 ? MapView3D.PreviewSeason.Winter
                    : MapView3D.PreviewSeason.Default;
                if (VisualSeasonForMonth(month) != expected)
                    throw new InvalidOperationException($"{month}월 계절 대응 불일치");
            }
            var view = _view;
            var childCount = view.GetChildCount();
            foreach (var season in new[] { MapView3D.PreviewSeason.Autumn,
                         MapView3D.PreviewSeason.Winter, MapView3D.PreviewSeason.Default })
            {
                var timer = Stopwatch.StartNew();
                await view.SetSeasonBatchedAsync(season);
                timer.Stop();
                if (view != _view || view.GetChildCount() != childCount || view.SeasonPreview != season)
                    throw new InvalidOperationException($"{season} 전환 중 지형 노드 변경");
                GD.Print($"SEASON_QA {season} transition_ms={timer.ElapsedMilliseconds} max_batch_ms={view.LastSeasonMaxBatchMs:F1} nodes={childCount}");
                if (capture)
                {
                    for (var frame = 0; frame < 3; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var path = ProjectSettings.GlobalizePath($"user://season-qa-{season.ToString().ToLowerInvariant()}.png");
                    if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok)
                        throw new InvalidOperationException($"계절 화면 캡처 실패: {path}");
                    GD.Print($"SEASON_QA_CAPTURE {season} {path}");
                }
            }
            view.SetSeasonImmediate(VisualSeasonForMonth(_state.Month));
            GD.Print("SEASON_QA PASS: calendar mapping and in-place terrain transitions");
            GetParent().CallDeferred("RequestGracefulShutdown");
        }
        catch (Exception error)
        {
            GD.PushError($"SEASON_QA FAIL: {error}");
            GetTree().Quit(1);
        }
    }
}
