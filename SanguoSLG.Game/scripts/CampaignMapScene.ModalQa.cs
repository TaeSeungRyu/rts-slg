using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    /// <summary>탐색·외교 및 8명 이하 수행 장수 표는 행 전체를 보이고 내부 세로 스크롤을 쓰지 않는다.</summary>
    private async void RunOfficerTableLayoutQa()
    {
        var city = _state.Cities.FirstOrDefault(c => c.Owner == Player);
        var commandIndices = Cmds.Select((command, index) => (command, index))
            .Where(x => x.command.Kind is SanguoSLG.Core.Domain.CommandKind.Explore
                or SanguoSLG.Core.Domain.CommandKind.FormAlliance
                or SanguoSLG.Core.Domain.CommandKind.AppointSecurityOfficer)
            .Select(x => x.index)
            .ToList();
        if (city is null || commandIndices.Count != 3)
        {
            GD.PrintErr("[officer-table-qa] passed=False reason=missing-city-or-command");
            GetTree().Quit(1);
            return;
        }

        var passed = true;
        var results = new List<string>();
        _selected = city.Id;
        foreach (var commandIndex in commandIndices)
        {
            OpenModal(commandIndex);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var table = _modalLayer?.FindChild("CommandOfficerTable", true, false) as Tree;
            var rows = table?.GetRoot()?.GetChildCount() ?? 0;
            var expectedHeight = 48 + rows * 46;
            var firstRow = table?.GetRoot()?.GetFirstChild();
            var portrait = firstRow?.GetIcon(0);
            var portraitImage = portrait?.GetImage();
            var ringPixel = portraitImage is not null && portraitImage.GetWidth() >= 64
                ? portraitImage.GetPixel(32, 2)
                : Colors.Transparent;
            var goldRing = ringPixel.A > 0.7f && ringPixel.R > 0.7f && ringPixel.G > 0.5f;
            var portraitLayout = firstRow is not null
                && portrait is not null
                && string.IsNullOrEmpty(firstRow.GetText(0))
                && !string.IsNullOrWhiteSpace(firstRow.GetText(1))
                && goldRing;
            var ok = table?.GetMeta("all_officer_rows").AsBool() == true
                && table.ScrollVerticalEnabled == false
                && table.MouseForcePassScrollEvents == false
                && table.CustomMinimumSize.Y >= expectedHeight
                && portraitLayout;
            passed &= ok;
            results.Add($"{Cmds[commandIndex].Label}:{rows}:{table?.CustomMinimumSize.Y ?? 0}:금테두리{goldRing}:초상{portraitLayout}:{ok}");
            CloseModal();
        }

        var researchIndex = System.Array.FindIndex(Cmds,
            command => command.Kind == SanguoSLG.Core.Domain.CommandKind.Research && command.Param == "general");
        OpenModal(researchIndex);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var researchTable = _modalLayer?.FindChild("CommandOfficerTable", true, false) as Tree;
        var wheelIsolated = researchTable?.GetMeta("isolated_wheel_scroll").AsBool() == true
            && researchTable.MouseFilter == Control.MouseFilterEnum.Stop
            && researchTable.MouseForcePassScrollEvents == false;
        passed &= wheelIsolated;
        results.Add($"일반연구:휠격리:{wheelIsolated}");
        CloseModal();

        GD.Print($"[officer-table-qa] passed={passed} results={string.Join('|', results)}");
        GetTree().Quit(passed ? 0 : 1);
    }

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
        var doctrineOptions = OptionList(Cmds[commandIndex], city);
        var detailPanel = _modalLayer?.FindChild("DoctrineResearchSelectionDetail", true, false) as PanelContainer;
        var fundingPanel = _modalLayer?.FindChild("ResearchFundingPanel", true, false) as PanelContainer;
        var doctrineDetailPlacement = detailPanel?.HasMeta("doctrine_research_detail") == true
            && fundingPanel is not null
            && detailPanel.GetGlobalRect().End.Y <= fundingPanel.GetGlobalRect().Position.Y + 1f;
        var specialResearchUsesStars = doctrineOptions
            .Where(option => option.Name is "통솔 병력" or "집단군")
            .All(option => option.Detail.Contains('★') || option.Detail.Contains('☆'))
            && doctrineOptions.Where(option => option.Name is "통솔 병력" or "집단군")
                .All(option => !option.Detail.Contains("Lv.", System.StringComparison.Ordinal));
        var sevenStars = ResearchStars(3, 7);
        var tenStars = ResearchStars(4, 10);
        var exactStarRules = sevenStars.Count(c => c == '★') == 7
            && tenStars.Count(c => c == '★') == 10
            && sevenStars.Contains("#777777", System.StringComparison.Ordinal)
            && sevenStars.Count(c => c == '☆') == 0;
        var specialStarCounts = doctrineOptions
            .Where(option => option.Name == "통솔 병력")
            .All(option => option.Detail.Count(c => c == '★') == 10)
            && doctrineOptions.Where(option => option.Name == "집단군")
                .All(option => option.Detail.Count(c => c == '★') == _cb.ArmyGroupResearchMaxLevel);
        var passed = thumbnailTextures && withinLimit && _optionCards.Count >= 7 && doctrineDetailPlacement
            && specialResearchUsesStars && exactStarRules && specialStarCounts;
        GD.Print($"[doctrine-performance-qa] passed={passed} elapsedMs={watch.ElapsedMilliseconds}/750 cards={_optionCards.Count} textures={_unitCardTextures.Count} thumbnails={thumbnailTextures} detailAboveFunding={doctrineDetailPlacement} specialStars={specialResearchUsesStars} exactStars={exactStarRules} specialCounts={specialStarCounts}");
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
