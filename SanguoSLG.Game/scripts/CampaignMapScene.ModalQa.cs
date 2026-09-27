using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    /// <summary>전투교리 최초 실행이 축소 카드 캐시를 사용해 지연 없이 열리는지 확인한다.</summary>
    private async void RunDoctrineModalPerformanceQa()
    {
        var city = _state.Cities.FirstOrDefault(c => c.Owner == Player);
        var commandIndex = System.Array.FindIndex(Cmds,
            c => c.Kind == SanguoSLG.Core.Domain.CommandKind.Research && c.Param == "troop");
        if (city is null || commandIndex < 0)
        {
            GD.PrintErr("[doctrine-performance-qa] passed=False reason=missing-city-or-command");
            GetTree().Quit(1);
            return;
        }

        _unitCardTextures.Clear();
        _selected = city.Id;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        OpenModal(commandIndex);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        watch.Stop();

        var thumbnailTextures = _unitCardTextures.Count > 0
            && _unitCardTextures.Values.All(texture => texture.GetWidth() == 256 && texture.GetHeight() == 256);
        var withinLimit = watch.ElapsedMilliseconds <= 750;
        var passed = thumbnailTextures && withinLimit && _optionCards.Count >= 7;
        GD.Print($"[doctrine-performance-qa] passed={passed} elapsedMs={watch.ElapsedMilliseconds}/750 cards={_optionCards.Count} textures={_unitCardTextures.Count} thumbnails={thumbnailTextures}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }

    /// <summary>UI-01: 3개 해상도×3개 폭 프로필에서 공용 모달의 경계와 고정 영역을 검증한다.</summary>
    private async void RunAdaptiveModalFrameworkQa()
    {
        var resolutions = new[]
        {
            new Vector2I(1280, 720),
            new Vector2I(1600, 900),
            new Vector2I(1920, 1080),
        };
        var profiles = System.Enum.GetValues<AdaptiveModalScaffold.WidthProfile>();
        var passed = true;
        var checkedCases = 0;

        foreach (var resolution in resolutions)
        {
            foreach (var profile in profiles)
            {
                var viewport = new SubViewport
                {
                    Name = $"ModalQaViewport{resolution.X}x{resolution.Y}{profile}",
                    Size = resolution,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
                };
                AddChild(viewport);

                var root = new Control
                {
                    Name = "ModalQaRoot",
                    CustomMinimumSize = resolution,
                    Size = resolution,
                };
                viewport.AddChild(root);

                var center = new CenterContainer
                {
                    Name = "ModalQaCenter",
                    Size = resolution,
                };
                root.AddChild(center);

                var scaffold = new AdaptiveModalScaffold();
                scaffold.Configure(resolution, profile);
                scaffold.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
                center.AddChild(scaffold);

                var title = MakeLabel($"{profile} 모달 QA", 20, Gold);
                var close = MakeButton("✕");
                scaffold.SetHeader(title, close, GoldRule());

                for (var i = 0; i < 24; i++)
                {
                    scaffold.Body.AddChild(MakeLabel($"QA 본문 {i + 1:00} — 긴 모달은 이 영역만 스크롤됩니다.", 14, Parchment));
                }

                var confirm = MakeButton("확인", accent: true);
                confirm.CustomMinimumSize = new Vector2(120, 36);
                scaffold.AddFooterControl(confirm);

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                var panelRect = scaffold.GetGlobalRect();
                var headerRect = scaffold.Header.GetGlobalRect();
                var bodyRect = scaffold.BodyScroll.GetGlobalRect();
                var footerRect = scaffold.Footer.GetGlobalRect();
                var closeRect = close.GetGlobalRect();
                var confirmRect = confirm.GetGlobalRect();
                var expected = AdaptiveModalScaffold.CalculateLayoutSize(resolution, profile);
                var insideViewport = panelRect.Position.X >= -0.5f
                    && panelRect.Position.Y >= -0.5f
                    && panelRect.End.X <= resolution.X + 0.5f
                    && panelRect.End.Y <= resolution.Y + 0.5f;
                var correctSize = panelRect.Size.IsEqualApprox(expected);
                var fixedRegions = headerRect.End.Y <= bodyRect.Position.Y + 1f
                    && bodyRect.End.Y <= footerRect.Position.Y + 1f;
                var actionsVisible = close.IsVisibleInTree() && confirm.IsVisibleInTree()
                    && panelRect.Encloses(closeRect) && panelRect.Encloses(confirmRect);
                var closeSize = closeRect.Size.X is >= 34f and <= 40f
                    && closeRect.Size.Y is >= 34f and <= 40f;
                var scrollCount = scaffold.FindChildren("*", "ScrollContainer", true, false).Count;
                var oneBodyScroll = scrollCount == 1;
                var casePassed = insideViewport && correctSize && fixedRegions && actionsVisible && closeSize && oneBodyScroll;
                passed &= casePassed;
                checkedCases++;
                GD.Print($"[modal-framework-qa] resolution={resolution.X}x{resolution.Y} profile={profile} panel={panelRect.Size} inside={insideViewport} fixed={fixedRegions} actions={actionsVisible} close={closeRect.Size} scrolls={scrollCount} passed={casePassed}");

                viewport.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        GD.Print($"[modal-framework-qa] cases={checkedCases}/9 passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
