using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private async void RunModalCameraBlockQa()
    {
        var city = _state.Cities.First(c => c.Owner == Player);
        _selected = city.Id;
        var commandIndex = System.Array.FindIndex(Cmds, command => command.Kind == SanguoSLG.Core.Domain.CommandKind.Research);
        OpenModal(commandIndex);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var blockedWhileOpen = _camera.IsNavigationInputBlocked;
        CloseModal();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var releasedAfterClose = !_camera.IsNavigationInputBlocked;
        var passed = blockedWhileOpen && releasedAfterClose;
        GD.Print($"[modal-camera-block-qa] passed={passed} open={blockedWhileOpen} closed={releasedAfterClose}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunCityAmbienceQa()
    {
        var ambience = GetTree().GetNodesInGroup("city_villager_ambience")
            .OfType<VillagerAmbience>()
            .Where(node => node.HasMeta("city_id"))
            .ToList();
        var coveredCities = ambience.Select(node => node.GetMeta("city_id").AsInt32()).Distinct().ToHashSet();
        var cityCoverage = _cities.All(city => coveredCities.Contains(city.Id.Value));
        var portCoverage = _cities.Where(city => city.IsPort)
            .All(city => ambience.Any(node => node.GetMeta("city_id").AsInt32() == city.Id.Value
                && node.MaxVillagers >= 4));
        var passed = cityCoverage && portCoverage && ambience.All(node => node.SpawnEnabled);
        GD.Print($"[city-ambience-qa] passed={passed} cityCoverage={coveredCities.Count}/{_cities.Count} portCoverage={portCoverage} ambience={ambience.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunCastleDamageQa()
    {
        var city = _state.Cities.First(current => !current.IsPort);
        var original = _state;
        _state = _state with
        {
            Cities = _state.Cities.Select(current => current.Id == city.Id ? current with { Wall = 0 } : current).ToList(),
        };
        Redraw("성벽 파괴 QA");
        var brokenNode = _cityModels[city.Id.Value];
        var brokenApplied = _cityBrokenVisuals[city.Id.Value]
            && brokenNode.GetMeta("broken_city_visual").AsBool();
        _state = original;
        Redraw("성벽 복구 QA");
        var restoredNode = _cityModels[city.Id.Value];
        var restored = !_cityBrokenVisuals[city.Id.Value]
            && !restoredNode.GetMeta("broken_city_visual").AsBool();
        var passed = brokenApplied && restored && brokenNode != restoredNode;
        GD.Print($"[castle-damage-qa] passed={passed} brokenApplied={brokenApplied} restored={restored} replaced={brokenNode != restoredNode}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunCityDetailUiQa()
    {
        var city = _state.Cities.FirstOrDefault(c => c.Owner == Player);
        if (city is null)
        {
            GD.PrintErr("[city-detail-qa] passed=False reason=missing-city");
            GetTree().Quit(1);
            return;
        }

        OpenCityDetail(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var garrisonFrames = _modalLayer?.FindChildren("*", "PanelContainer", true, false)
            .OfType<PanelContainer>()
            .Where(panel => panel.HasMeta("rounded_garrison_art")
                && panel.GetMeta("rounded_garrison_art").AsBool())
            .ToList() ?? [];
        var expected = _state.Garrisons.Count(g => g.City == city.Id);
        var roundedGarrisons = garrisonFrames.Count == expected && garrisonFrames.All(frame =>
        {
            var style = frame.GetThemeStylebox("panel") as StyleBoxFlat;
            return style is not null && style.CornerRadiusTopLeft == 7 && style.BorderWidthLeft == 1;
        });
        var wheelBlockers = _modalLayer?.FindChildren("*", "Control", true, false).OfType<Control>()
            .Count(control => control.HasMeta("blocks_map_wheel") && control.GetMeta("blocks_map_wheel").AsBool()) ?? 0;
        var mapWheelBlocked = wheelBlockers >= 2;
        var outerScroll = _modalLayer?.FindChild("CityDetailOuterScroll", true, false) as ScrollContainer;
        var tabPanel = _modalLayer?.FindChild("CityDetailTabPanel", true, false) as PanelContainer;
        var stationedTable = _modalLayer?.FindChild("CityDetailStationedTable", true, false) as Tree;
        var stationedGoldPortraits = stationedTable?.GetMeta("thin_gold_portraits").AsBool() == true;
        var stationedInternalScroll = outerScroll?.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled
            && tabPanel?.CustomMinimumSize.Y >= 250f
            && stationedTable?.ScrollVerticalEnabled == true
            && stationedTable.MouseForcePassScrollEvents == false
            && stationedTable.SizeFlagsVertical.HasFlag(Control.SizeFlags.ExpandFill);
        var modalHeight = outerScroll?.CustomMinimumSize.Y ?? 0;
        var tabHeight = tabPanel?.CustomMinimumSize.Y ?? 0;
        CloseModal();

        var originalState = _state;
        var originalTab = _cityDetailTab;
        var actor = _state.GeneralsAt(city.Id).First();
        var sampleCommand = new SanguoSLG.Core.Simulation.CityCommand(city.Id,
            SanguoSLG.Core.Domain.CommandKind.Explore, actor, null, _state.Day, _state.Day + 7, 0);
        _state = _state with { PendingCommands = _state.Commands.Append(sampleCommand).ToList() };
        _cityDetailTab = 1;
        OpenCityDetail(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var commandRows = _modalLayer?.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
            .Count(panel => panel.HasMeta("city_detail_officer_row")) ?? 0;
        var commandPortraitNodes = _modalLayer?.FindChildren("CityDetailOfficerPortrait", "TextureRect", true, false)
            .OfType<TextureRect>().ToList() ?? [];
        var commandPortraits = commandPortraitNodes.Count;
        var commandGoldPortraits = commandPortraitNodes.Count > 0
            && commandPortraitNodes.All(portrait => portrait.GetMeta("thin_gold_border").AsBool());
        var commandTableLayout = commandRows >= 1 && commandPortraits >= 1;
        CloseModal();

        _state = originalState;
        var garrison = _state.Garrisons.First(g => g.City == city.Id);
        _pendingDeploys.Add((new SanguoSLG.Core.Simulation.DeployRequest(city.Id, garrison.TroopCode,
            Math.Min(1000, garrison.Troops), actor), "QA 전투편성 예약"));
        _cityDetailTab = 2;
        OpenCityDetail(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var deployRows = _modalLayer?.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
            .Count(panel => panel.HasMeta("city_detail_officer_row")) ?? 0;
        var deployPortraitNodes = _modalLayer?.FindChildren("CityDetailOfficerPortrait", "TextureRect", true, false)
            .OfType<TextureRect>().ToList() ?? [];
        var deployPortraits = deployPortraitNodes.Count;
        var deployGoldPortraits = deployPortraitNodes.Count > 0
            && deployPortraitNodes.All(portrait => portrait.GetMeta("thin_gold_border").AsBool());
        var deployTableLayout = deployRows >= 1 && deployPortraits >= 1;
        _pendingDeploys.RemoveAt(_pendingDeploys.Count - 1);
        _cityDetailTab = originalTab;
        var passed = expected > 0 && roundedGarrisons && mapWheelBlocked && stationedInternalScroll
            && commandTableLayout && deployTableLayout
            && stationedGoldPortraits && commandGoldPortraits && deployGoldPortraits;
        GD.Print($"[city-detail-qa] passed={passed} roundedGarrisons={garrisonFrames.Count}/{expected}:{roundedGarrisons} mapWheelBlocked={mapWheelBlocked}:{wheelBlockers} stationedInternalScroll={stationedInternalScroll} modalHeight={modalHeight} tabHeight={tabHeight} commandRows={commandRows}:{commandTableLayout} deployRows={deployRows}:{deployTableLayout} goldPortraits={stationedGoldPortraits}/{commandGoldPortraits}/{deployGoldPortraits}");
        GetTree().Quit(passed ? 0 : 1);
    }

    /// <summary>탐색·외교 및 8명 이하 수행 장수 표는 행 전체를 보이고 내부 세로 스크롤을 쓰지 않는다.</summary>
    private async void RunOfficerTableLayoutQa()
    {
        var city = _state.Cities.FirstOrDefault(c => c.Owner == Player);
        var commandIndices = Cmds.Select((command, index) => (command, index))
            .Where(x => x.command.Kind is SanguoSLG.Core.Domain.CommandKind.Explore
                or SanguoSLG.Core.Domain.CommandKind.FormAlliance
                or SanguoSLG.Core.Domain.CommandKind.AppointSecurityOfficer
                or SanguoSLG.Core.Domain.CommandKind.AppointDomesticOfficer)
            .Select(x => x.index)
            .ToList();
        if (city is null || commandIndices.Count != 4)
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
            var showAllRows = Cmds[commandIndex].Kind is SanguoSLG.Core.Domain.CommandKind.Explore
                or SanguoSLG.Core.Domain.CommandKind.FormAlliance
                or SanguoSLG.Core.Domain.CommandKind.BreakAlliance;
            var fillsBottom = Cmds[commandIndex].Kind is SanguoSLG.Core.Domain.CommandKind.AppointSecurityOfficer
                or SanguoSLG.Core.Domain.CommandKind.AppointDomesticOfficer;
            var expectedHeight = showAllRows ? 48 + rows * 46 : Mathf.Min(48 + rows * 46, 330);
            var modalScroll = _modalLayer?.FindChild("ModalBodyScroll", true, false) as ScrollContainer;
            var bottomGap = table is not null && modalScroll is not null
                ? modalScroll.GetGlobalRect().End.Y - table.GetGlobalRect().End.Y
                : -1f;
            var bottomFillOk = !fillsBottom || (table?.GetMeta("fills_modal_bottom").AsBool() == true
                && table.GetMeta("modal_bottom_reserve_ratio").AsSingle() == 0.05f
                && bottomGap >= -1f && bottomGap <= modalScroll!.Size.Y * 0.08f);
            var firstRow = table?.GetRoot()?.GetFirstChild();
            var hasObsoleteHints = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains("행 클릭", System.StringComparison.Ordinal)
                    || label.Text.Contains("상단 눌러 정렬", System.StringComparison.Ordinal)) == true;
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
            var ok = table?.GetMeta("all_officer_rows").AsBool() == showAllRows
                && table.ScrollVerticalEnabled == !showAllRows
                && table.MouseForcePassScrollEvents == showAllRows
                && table.GetMeta("passes_wheel_to_modal").AsBool() == showAllRows
                && table.CustomMinimumSize.Y >= expectedHeight
                && bottomFillOk
                && portraitLayout
                && !hasObsoleteHints;
            passed &= ok;
            results.Add($"{Cmds[commandIndex].Label}:{rows}:{table?.CustomMinimumSize.Y ?? 0}:하단{bottomGap:0}:금테두리{goldRing}:초상{portraitLayout}:안내문구{!hasObsoleteHints}:{ok}");
            CloseModal();
        }

        var researchIndex = System.Array.FindIndex(Cmds,
            command => command.Kind == SanguoSLG.Core.Domain.CommandKind.Research && command.Param == "general");
        OpenModal(researchIndex);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var researchTable = _modalLayer?.FindChild("CommandOfficerTable", true, false) as Tree;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var tableHasScrollRange = (researchTable?.GetRoot()?.GetChildCount() ?? 0) > 8
            && researchTable?.CustomMinimumSize.Y <= 330;
        var wheelIsolated = researchTable?.GetMeta("isolated_wheel_scroll").AsBool() == true
            && researchTable.MouseFilter == Control.MouseFilterEnum.Stop
            && researchTable.ScrollVerticalEnabled
            && researchTable.MouseForcePassScrollEvents == false
            && researchTable.GetMeta("passes_wheel_to_modal").AsBool() == false
            && researchTable.GetMeta("passes_wheel_at_boundary").AsBool()
            && researchTable.GetMeta("wheel_scroll_priority").AsString() == "down_modal_then_table_up_table_then_modal"
            && researchTable.GetMeta("explicit_internal_wheel_scroll").AsBool()
            && tableHasScrollRange;
        var directionalPriority = ResolveOfficerWheelTarget(1, true, 0, 17) == OfficerWheelTarget.Modal
            && ResolveOfficerWheelTarget(1, false, 0, 17) == OfficerWheelTarget.Table
            && ResolveOfficerWheelTarget(-1, true, 16, 17) == OfficerWheelTarget.Table
            && ResolveOfficerWheelTarget(-1, true, 0, 17) == OfficerWheelTarget.Modal;
        wheelIsolated &= directionalPriority;
        passed &= wheelIsolated;
        results.Add($"일반연구:모달우선·표스크롤범위{tableHasScrollRange}:{wheelIsolated}:행{researchTable?.GetRoot()?.GetChildCount() ?? 0}");
        CloseModal();

        var trainingIndex = System.Array.FindIndex(Cmds,
            command => command.Kind == SanguoSLG.Core.Domain.CommandKind.AppointTrainingOfficer);
        OpenModal(trainingIndex);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var trainingTable = _modalLayer?.FindChild("CommandOfficerTable", true, false) as Tree;
        var trainingModalScroll = _modalLayer?.FindChild("ModalBodyScroll", true, false) as ScrollContainer;
        var trainingBottomGap = trainingTable is not null && trainingModalScroll is not null
            ? trainingModalScroll.GetGlobalRect().End.Y - trainingTable.GetGlobalRect().End.Y
            : -1f;
        var trainingModalBar = trainingModalScroll?.GetVScrollBar();
        var noOuterScroll = trainingModalBar is not null
            && trainingModalBar.MaxValue - trainingModalBar.Page <= trainingModalBar.MinValue + 0.5;
        var trainingFillOk = trainingTable?.GetMeta("officer_table_fill_ratio").AsSingle() >= 0.9f
            && trainingTable.GetMeta("fills_modal_bottom").AsBool()
            && trainingTable.GetMeta("modal_bottom_reserve_ratio").AsSingle() == 0.05f
            && trainingBottomGap >= -1f && trainingBottomGap <= trainingModalScroll!.Size.Y * 0.08f
            && trainingTable.SizeFlagsVertical == Control.SizeFlags.ExpandFill
            && trainingTable.ScrollVerticalEnabled
            && noOuterScroll;
        passed &= trainingFillOk;
        results.Add($"훈련담당:하단5%·외부스크롤없음:{trainingFillOk}:{trainingTable?.CustomMinimumSize.Y ?? 0:0}/{trainingBottomGap:0}/{noOuterScroll}");
        CloseModal();

        var changanCount = _state.GeneralsAt(new SanguoSLG.Core.Domain.CityId(1)).Distinct().Count();
        var changanRosterOk = changanCount == 17;
        passed &= changanRosterOk;
        results.Add($"장안장수:{changanCount}/17:{changanRosterOk}");

        var recruitmentIndex = System.Array.FindIndex(Cmds,
            command => command.Kind == SanguoSLG.Core.Domain.CommandKind.AppointRecruitmentOfficer);
        var recruitmentOptions = OptionList(Cmds[recruitmentIndex], city);
        var lockedSiegeDetails = recruitmentOptions
            .Where(option => option.Name is "투석기" or "공성탑")
            .Select(option => option.Detail)
            .ToList();
        var lockedSiegeLineBreak = lockedSiegeDetails.Count == 2
            && lockedSiegeDetails.All(detail => detail.Contains("\n현재 Lv.", System.StringComparison.Ordinal));
        passed &= lockedSiegeLineBreak;
        results.Add($"병력담당:공성잠금개행:{lockedSiegeLineBreak}");

        OpenModal(recruitmentIndex);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var troopCards = _optionCards.Where(card => card.GetMeta("auto_recruit_troop_card").AsBool()).ToList();
        var uniformTroopCards = troopCards.Count == recruitmentOptions.Count
            && troopCards.All(card => card.CustomMinimumSize == new Vector2(186, 160))
            && troopCards.Select(card => card.Size).Distinct().Count() == 1;
        var framedTroopArt = troopCards.All(card => card.FindChildren("*", "PanelContainer", true, false)
            .OfType<PanelContainer>().Any(frame => frame.GetMeta("rounded_option_art").AsBool()
                && frame.GetMeta("option_art_size").AsInt32() == 54));
        var rateCards = _autoRecruitRateCards.ToList();
        var uniformRateCards = rateCards.Count == 3
            && rateCards.All(card => card.GetMeta("auto_recruit_rate_card").AsBool()
                && card.CustomMinimumSize == new Vector2(168, 138))
            && rateCards.Select(card => card.Size).Distinct().Count() == 1;
        _autoRecruitRateParam = 2;
        RefreshAutoRecruitRateCards(recruitmentOptions);
        rateCards[1].EmitSignal(Control.SignalName.MouseExited);
        var selectedRateStyle = rateCards[1].GetThemeStylebox("panel") as StyleBoxFlat;
        var rateSelectionPersists = selectedRateStyle?.BorderWidthLeft == 4
            && rateCards.Where((_, index) => index != 1)
                .All(card => (card.GetThemeStylebox("panel") as StyleBoxFlat)?.BorderWidthLeft == 1);
        passed &= uniformTroopCards && framedTroopArt && uniformRateCards && rateSelectionPersists;
        results.Add($"병력담당:동일카드:{uniformTroopCards}:둥근사진{framedTroopArt}:생산비율{uniformRateCards}:선택유지{rateSelectionPersists}:{troopCards.FirstOrDefault()?.Size.ToString() ?? "-"}/{rateCards.FirstOrDefault()?.Size.ToString() ?? "-"}");
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
        var framedDoctrineArt = _optionCards.All(card => card.FindChildren("*", "PanelContainer", true, false)
            .OfType<PanelContainer>().Any(frame => frame.GetMeta("rounded_option_art").AsBool()
                && frame.GetMeta("option_art_size").AsInt32() == 54));
        var detailPanel = _modalLayer?.FindChild("DoctrineResearchSelectionDetail", true, false) as PanelContainer;
        var fundingPanel = _modalLayer?.FindChild("ResearchFundingPanel", true, false) as PanelContainer;
        var fundingTitle = _modalLayer?.FindChild("ResearchFundingTitle", true, false) as Label;
        var fundingDivider = _modalLayer?.FindChild("DoctrineFundingDivider", true, false) as VBoxContainer;
        var officerDivider = _modalLayer?.FindChild("DoctrineResearchOfficerDivider", true, false) as VBoxContainer;
        var officerTitle = _modalLayer?.FindChild("CommandOfficerSectionTitle", true, false) as Label;
        var doctrineDetailPlacement = detailPanel?.HasMeta("doctrine_research_detail") == true
            && fundingPanel is not null
            && detailPanel.GetGlobalRect().End.Y <= fundingPanel.GetGlobalRect().Position.Y + 1f;
        var fundingDividerPlacement = detailPanel is not null && fundingTitle is not null
            && fundingDivider?.HasMeta("section_divider") == true
            && fundingDivider.GetGlobalRect().Position.Y >= detailPanel.GetGlobalRect().End.Y - 1f
            && fundingDivider.GetGlobalRect().End.Y <= fundingTitle.GetGlobalRect().Position.Y + 1f
            && fundingDivider.CustomMinimumSize.Y <= fundingDivider.GetCombinedMinimumSize().Y;
        var officerDividerPlacement = fundingPanel is not null && officerTitle is not null
            && officerDivider?.HasMeta("section_divider") == true
            && officerDivider.GetGlobalRect().Position.Y >= fundingPanel.GetGlobalRect().End.Y - 1f
            && officerDivider.GetGlobalRect().End.Y <= officerTitle.GetGlobalRect().Position.Y + 1f;
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
        var passed = thumbnailTextures && withinLimit && _optionCards.Count >= 7 && framedDoctrineArt
            && doctrineDetailPlacement && fundingDividerPlacement
            && officerDividerPlacement
            && specialResearchUsesStars && exactStarRules && specialStarCounts;
        GD.Print($"[doctrine-performance-qa] passed={passed} elapsedMs={watch.ElapsedMilliseconds}/750 cards={_optionCards.Count} textures={_unitCardTextures.Count} thumbnails={thumbnailTextures} framedArt={framedDoctrineArt} detailAboveFunding={doctrineDetailPlacement} fundingDivider={fundingDividerPlacement} officerDivider={officerDividerPlacement} specialStars={specialResearchUsesStars} exactStars={exactStarRules} specialCounts={specialStarCounts}");
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

    /// <summary>전투·보급·수송·집단군 편성창이 좌우형이며 전체 스크롤을 사용하지 않는지 검증한다.</summary>
    private async void RunDeployComposeLayoutQa()
    {
        var city = _state.Cities.First(c => c.Owner == Player);
        _depModalCity = city.Id;
        var cases = new (string Name, System.Action Open)[]
        {
            ("combat", () => OpenDeployCompose(-1)),
            ("supply", () => OpenSupplyCompose(-1)),
            ("transport", () => OpenTransportCompose(city.Id)),
            ("army-group", () => OpenArmyGroupCompose(-1)),
        };
        var passed = true;
        foreach (var qa in cases)
        {
            qa.Open();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var columns = FindChild("DeployComposeColumns", true, false) as HBoxContainer;
            var resources = FindChild("ComposeResourcesScroll", true, false) as ScrollContainer;
            var officers = FindChild("ComposeOfficersScroll", true, false) as ScrollContainer;
            var officerFrame = FindChild("ComposeOfficersFrame", true, false) as PanelContainer;
            var officerTable = FindChild("ComposeOfficerTable", true, false) as Tree;
            var outer = _modalLayer?.FindChildren("*", "ScrollContainer", true, false)
                .OfType<ScrollContainer>()
                .FirstOrDefault(s => s.HasMeta("compose_outer_scroll_disabled"));
            var aptitudeHeader = qa.Name != "combat" || officerTable?.GetColumnTitle(6) == "적성";
            var aptitudeGradesOnly = true;
            if (qa.Name == "combat" && officerTable?.GetRoot()?.GetFirstChild() is { } officerRow)
            {
                var grade = officerRow.GetText(6);
                aptitudeGradesOnly = grade == "—" || System.Text.RegularExpressions.Regex.IsMatch(grade, "^(F|D|C|B|A|A\\+|S|SS|SSS)$");
            }
            var casePassed = columns is not null
                && columns.GetMeta("horizontal_compose_layout").AsBool()
                && resources?.VerticalScrollMode == (qa.Name == "combat"
                    ? ScrollContainer.ScrollMode.Disabled
                    : ScrollContainer.ScrollMode.Auto)
                && (qa.Name != "combat" || resources.GetMeta("compose_left_scroll_disabled").AsBool())
                && officers?.VerticalScrollMode == ScrollContainer.ScrollMode.Auto
                && officerFrame?.SizeFlagsStretchRatio >= 1.35f
                && officerTable?.GetMeta("compose_officer_table_expanded").AsBool() == true
                && officerTable.GetMeta("compose_officer_fill_ratio").AsSingle() >= 0.9f
                && officerTable.CustomMinimumSize.Y >= columns.CustomMinimumSize.Y * 0.9f - 1f
                && officerTable.SizeFlagsVertical == Control.SizeFlags.ExpandFill
                && officerTable.ScrollVerticalEnabled
                && outer?.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled
                && aptitudeHeader
                && aptitudeGradesOnly;
            passed &= casePassed;
            GD.Print($"[deploy-layout-qa] type={qa.Name} passed={casePassed} columns={columns is not null} outerDisabled={outer?.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled} internalScrolls={(resources is not null ? 1 : 0) + (officers is not null ? 1 : 0)} aptitude={aptitudeHeader}/{aptitudeGradesOnly}");
            CloseModal();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GD.Print($"[deploy-layout-qa] passed={passed} cases={cases.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunDeployGoldUiQa()
    {
        var city = _state.Cities.First(c => c.Owner == Player && !c.IsPort);
        _depModalCity = city.Id;
        var cases = new (string Name, System.Action Open)[]
        {
            ("combat", () => OpenDeployCompose(-1)),
            ("supply", () => OpenSupplyCompose(-1)),
            ("batch", () => OpenBatchDeployCompose(city.Id)),
            ("army-group", () => OpenArmyGroupCompose(-1)),
        };
        var passed = true;
        foreach (var qa in cases)
        {
            qa.Open();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var selectors = _modalLayer?.FindChildren("*", "Control", true, false)
                .OfType<Control>()
                .Count(control => control.HasMeta("deploy_gold_selector")) ?? 0;
            var labels = _modalLayer?.FindChildren("*", "Label", true, false)
                .OfType<Label>().Select(label => label.Text).ToList() ?? [];
            var casePassed = selectors > 0 && labels.Any(label => label.Contains("금"));
            passed &= casePassed;
            GD.Print($"[deploy-gold-qa] type={qa.Name} passed={casePassed} selectors={selectors}");
            CloseModal();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GD.Print($"[deploy-gold-qa] passed={passed} cases={cases.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunDeployImageStyleQa()
    {
        var city = _state.Cities.First(c => c.Owner == Player && !c.IsPort);
        _depModalCity = city.Id;
        var cases = new (string Name, System.Action Open)[]
        {
            ("combat", () => OpenDeployCompose(-1)),
            ("supply", () => OpenSupplyCompose(-1)),
            ("transport", () => OpenTransportCompose(city.Id)),
            ("batch", () => OpenBatchDeployCompose(city.Id)),
            ("army-group", () => OpenArmyGroupCompose(-1)),
        };
        var passed = true;
        foreach (var qa in cases)
        {
            qa.Open();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var art = _modalLayer?.FindChildren("*", "PanelContainer", true, false)
                .OfType<PanelContainer>().Count(panel => panel.HasMeta("deploy_unit_art")) ?? 0;
            var treePortraits = _modalLayer?.FindChildren("*", "Tree", true, false)
                .OfType<Tree>()
                .SelectMany(tree =>
                {
                    var icons = new List<Texture2D?>();
                    var item = tree.GetRoot()?.GetFirstChild();
                    while (item is not null)
                    {
                        for (var column = 0; column < tree.Columns; column++) icons.Add(item.GetIcon(column));
                        item = item.GetNext();
                    }
                    return icons;
                })
                .Count(icon => icon is not null && _officerTablePortraits.Values.Any(texture => ReferenceEquals(texture, icon))) ?? 0;
            var optionPortraits = _modalLayer?.FindChildren("*", "OptionButton", true, false)
                .OfType<OptionButton>()
                .Sum(option => Enumerable.Range(0, option.ItemCount).Count(index => option.GetItemIcon(index) is not null)) ?? 0;
            var casePassed = art > 0 && (treePortraits > 0 || optionPortraits > 0);
            passed &= casePassed;
            GD.Print($"[deploy-image-qa] type={qa.Name} passed={casePassed} roundedUnitArt={art} circularPortraits={treePortraits + optionPortraits}");
            CloseModal();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GD.Print($"[deploy-image-qa] passed={passed} cases={cases.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
