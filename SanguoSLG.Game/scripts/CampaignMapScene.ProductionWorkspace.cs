using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenProductionModal(CityId city, HexCoord? fixedTarget = null)
    {
        if (_advancing) return;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        CloseDoctrinePicker();
        var cityData = _state.Cities.First(candidate => candidate.Id == city);
        var targets = ProductionTargets(cityData).ToList();
        var fixedOption = fixedTarget is { } position
            ? targets.FirstOrDefault(target => target.Plot == position) : default;
        var hasFixedTarget = fixedTarget is not null && !string.IsNullOrWhiteSpace(fixedOption.Code);
        var visibleTargets = hasFixedTarget ? [fixedOption] : targets;
        var generals = _state.GeneralsAt(city)
            .Where(general => !OfficerUnavailable(general) && !_state.IsGeneralInField(general))
            .Select(id => _state.Generals.First(general => general.Id == id))
            .OrderByDescending(general => general.Politics).ThenBy(general => general.Id.Value).ToList();
        var garrisons = _state.Garrisons
            .Where(force => force.City == city && !force.Trainee && force.Troops >= ProductionOperation.FixedTroops)
            .Join(_troops.Where(troop => troop.Class != TroopClass.Naval), force => force.TroopCode,
                troop => troop.Code, (force, troop) => (Garrison: force, Troop: troop))
            .OrderBy(entry => entry.Troop.Name, StringComparer.Ordinal).ToList();

        HexCoord? selectedTarget = visibleTargets.Count > 0 ? visibleTargets[0].Plot : null;
        var selectedFacility = visibleTargets.Count > 0 ? visibleTargets[0].Code : string.Empty;
        GeneralId? selectedGeneral = generals.FirstOrDefault()?.Id;
        var selectedTroop = garrisons.FirstOrDefault().Troop?.Code ?? string.Empty;

        var layer = new CanvasLayer { Layer = 20 };
        _modalLayer = layer;
        AddChild(layer);
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CloseModal();
        };
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var scaffold = new AdaptiveModalScaffold { Name = "ProductionOperationScaffold" };
        scaffold.Configure(GetViewport().GetVisibleRect().Size, AdaptiveModalScaffold.WidthProfile.Wide,
            AdaptiveModalScaffold.ContentLayout.Mixed, AdaptiveModalScaffold.WorkspaceProfile.DoctrineWorkflow);
        center.AddChild(scaffold);
        var close = AdaptiveModalScaffold.CreateCloseButton();
        close.Pressed += () => { CloseModal(); SelectCity(city); };
        scaffold.SetHeader(MakeLabel($"생산 작전   《 {cityData.Name} 》", 26, Gold), close, GoldRule());
        scaffold.BodyScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scaffold.Body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scaffold.Body.CustomMinimumSize = new Vector2(0f, scaffold.BodyLayoutHeight);

        var workspace = new ProductionOperationWorkspace();
        scaffold.Body.AddChild(workspace);
        workspace.DoctrineList.AddChild(MakeLabel("생산할 시설을 선택하세요", 18, GoldBright));
        var targetGrid = new GridContainer
        {
            Name = "ProductionTargetCards",
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        targetGrid.AddThemeConstantOverride("h_separation", 10);
        targetGrid.AddThemeConstantOverride("v_separation", 10);
        workspace.DoctrineList.AddChild(targetGrid);
        var cards = new List<(Button Card, HexCoord Plot, string Code)>();

        void StyleTargetCard(Button card, bool selected)
        {
            card.ToggleMode = true;
            card.SetPressedNoSignal(selected);
            card.AddThemeColorOverride("font_color", selected ? Colors.White : Parchment);
            card.AddThemeColorOverride("font_hover_color", Colors.White);
            card.AddThemeColorOverride("font_pressed_color", Colors.White);
            card.AddThemeStyleboxOverride("normal", Frame(new Color("241511"), new Color("765024"), 1, 8, 10));
            card.AddThemeStyleboxOverride("hover", Frame(new Color("3a1b15"), GoldBright, 1, 8, 10));
            card.AddThemeStyleboxOverride("pressed", Frame(new Color("76251c"), GoldBright, 2, 8, 10));
            card.AddThemeStyleboxOverride("hover_pressed", Frame(new Color("8b2c20"), GoldBright, 2, 8, 10));
        }

        void Refresh()
        {
            var general = selectedGeneral is { } generalId
                ? _state.Generals.FirstOrDefault(candidate => candidate.Id == generalId) : null;
            var troop = garrisons.FirstOrDefault(entry => entry.Troop.Code == selectedTroop);
            var reward = general is null || string.IsNullOrWhiteSpace(selectedFacility)
                ? (Gold: 0, Provisions: 0)
                : ProductionRules.Reward(selectedFacility, general.Politics);
            var days = general is null ? 0 : ProductionRules.GatherDays(general.Politics);
            var rewardText = reward.Gold > 0 ? $"금 +{reward.Gold:N0}"
                : reward.Provisions > 0 ? $"군량 +{reward.Provisions:N0}" : "보상 -";

            foreach (var (card, plot, code) in cards)
            {
                var selected = selectedTarget == plot;
                var cardReward = general is null ? (Gold: 0, Provisions: 0)
                    : ProductionRules.Reward(code, general.Politics);
                var cardRewardText = cardReward.Gold > 0 ? $"금 +{cardReward.Gold:N0}"
                    : cardReward.Provisions > 0 ? $"군량 +{cardReward.Provisions:N0}" : "장수 선택 필요";
                card.Text = $"{FacilityName(code)}{(selected ? "  ✓" : "")}\n{cardRewardText}";
                StyleTargetCard(card, selected);
            }

            foreach (var child in workspace.DetailHost.GetChildren()) child.QueueFree();
            workspace.DetailHost.AddChild(MakeLabel("선택 시설 상세", 18, GoldBright));
            workspace.DetailHost.AddChild(MakeLabel(selectedTarget is null
                ? "생산 가능한 논·밭·마을이 없습니다."
                : $"{FacilityName(selectedFacility)}\n채집 {days}일 · 예상 {rewardText}\n투입 병력 {ProductionOperation.FixedTroops:N0}명",
                15, Parchment));
            workspace.FundingSummary.Text = string.IsNullOrWhiteSpace(selectedTroop)
                ? "500명 이상 대기 중인 지상 병종이 없습니다."
                : $"{troop.Troop.Name} · 대기 {troop.Garrison.Troops:N0}명 · 훈련 {troop.Garrison.TrainingLevel}";
            workspace.OfficerSummary.Text = general is null
                ? "투입 가능한 장수가 없습니다."
                : $"{general.Name} · 정치 {general.Politics} · {CurrentDuty(general.Id)}";
            workspace.TotalCost.Text = selectedTarget is null || general is null || string.IsNullOrWhiteSpace(selectedTroop)
                ? "시설·병종·수행 장수를 선택하세요."
                : $"편성 요약 · {FacilityName(selectedFacility)} · {general.Name} · {troop.Troop.Name} 500명 · {days}일 · {rewardText}";
            workspace.StartButton.Disabled = selectedTarget is null || general is null || string.IsNullOrWhiteSpace(selectedTroop);
        }

        if (visibleTargets.Count == 0)
            workspace.DoctrineList.AddChild(MakeLabel("생산 가능한 논·밭·마을이 없습니다.", 15, Parchment));
        foreach (var target in visibleTargets)
        {
            var targetCopy = target;
            var card = new Button { CustomMinimumSize = new Vector2(170f, 92f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.Pressed += () =>
            {
                selectedTarget = targetCopy.Plot;
                selectedFacility = targetCopy.Code;
                Refresh();
            };
            cards.Add((card, target.Plot, target.Code));
            targetGrid.AddChild(card);
        }

        workspace.FundingChangeButton.Pressed += () => OpenProductionTroopPicker(garrisons, selectedTroop, code =>
        {
            selectedTroop = code;
            Refresh();
        });
        workspace.OfficerChangeButton.Pressed += () => OpenProductionGeneralPicker(generals, selectedGeneral, general =>
        {
            selectedGeneral = general;
            Refresh();
        });
        workspace.StartButton.Pressed += () =>
        {
            if (selectedTarget is not { } target || selectedGeneral is not { } general
                || string.IsNullOrWhiteSpace(selectedTroop)) return;
            var generalData = _state.Generals.First(candidate => candidate.Id == general);
            var troopName = _troops.First(troop => troop.Code == selectedTroop).Name;
            ShowConfirm("생산 작전 확인",
                $"{cityData.Name}에서 {FacilityName(selectedFacility)} 생산을 시작합니다.\n수행 장수: {generalData.Name}\n투입 병종: {troopName} 500명{DutyReleaseNotice(general)}\n\n시작 후 취소할 수 없습니다.",
                () =>
                {
                    if (_advancing) return;
                    if (OfficerUnavailable(general))
                    {
                        ShowNotice("생산 불가", "장수가 다른 업무 또는 출전 예약 중입니다.");
                        return;
                    }
                    var result = _producer.Start(_state, city, target, selectedFacility, selectedTroop, general);
                    if (!result.Ok)
                    {
                        ShowNotice("생산 실패", result.Error ?? "조건에 맞지 않아 실행할 수 없습니다.");
                        return;
                    }
                    _state = result.State;
                    Report($"[생산] {cityData.Name}에서 {generalData.Name} 장수가 {FacilityName(selectedFacility)} 생산 작전을 시작했습니다.", Parchment);
                    CloseModal();
                    SelectCity(city);
                    Redraw("생산 작전 시작");
                });
        };
        Refresh();
    }

    private void OpenProductionGeneralPicker(IReadOnlyList<General> generals, GeneralId? current,
        Action<GeneralId> applySelection)
    {
        CloseDoctrinePicker();
        var selected = current;
        var layer = CreateDoctrinePickerLayer("수행 장수 선택", out var body, out var close);
        _doctrinePickerLayer = layer;
        close.Pressed += CloseDoctrinePicker;
        var tree = new Tree { Name = "ProductionGeneralPicker", Columns = 6, ColumnTitlesVisible = true,
            HideRoot = true, SelectMode = Tree.SelectModeEnum.Row, CustomMinimumSize = new Vector2(0f, 450f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tree.AddThemeFontOverride("font", _font);
        tree.AddThemeFontSizeOverride("font_size", 15);
        tree.SetColumnTitle(0, ""); tree.SetColumnCustomMinimumWidth(0, 54); tree.SetColumnExpand(0, false);
        tree.SetColumnTitle(1, "이름");
        foreach (var (column, title) in new[] { (2, "무력"), (3, "지력"), (4, "정치") })
        { tree.SetColumnTitle(column, title); tree.SetColumnCustomMinimumWidth(column, 70); tree.SetColumnExpand(column, false); }
        tree.SetColumnTitle(5, "현재 담당 업무");
        var root = tree.CreateItem();
        foreach (var general in generals)
        {
            var item = tree.CreateItem(root);
            ApplyGeneralTreePortrait(item, 0, general.Id, 42, true);
            item.SetText(1, general.Name); item.SetText(2, general.Might.ToString());
            item.SetText(3, general.Intellect.ToString()); item.SetText(4, general.Politics.ToString());
            item.SetText(5, CurrentDuty(general.Id)); item.SetMetadata(0, general.Id.Value);
            if (selected == general.Id) tree.SetSelected(item, 1);
        }
        tree.ItemSelected += () =>
        {
            if (tree.GetSelected() is { } item) selected = new GeneralId(item.GetMetadata(0).AsInt32());
        };
        body.AddChild(tree);
        AddProductionPickerActions(body, () =>
        {
            if (selected is not { } general) return false;
            applySelection(general);
            return true;
        });
    }

    private void OpenProductionTroopPicker(
        IReadOnlyList<(GarrisonForce Garrison, TroopTemplate Troop)> garrisons, string current,
        Action<string> applySelection)
    {
        CloseDoctrinePicker();
        var selected = current;
        var layer = CreateDoctrinePickerLayer("투입 병종 선택", out var body, out var close);
        _doctrinePickerLayer = layer;
        close.Pressed += CloseDoctrinePicker;
        var tree = new Tree { Name = "ProductionTroopPicker", Columns = 5, ColumnTitlesVisible = true,
            HideRoot = true, SelectMode = Tree.SelectModeEnum.Row, CustomMinimumSize = new Vector2(0f, 420f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tree.AddThemeFontOverride("font", _font);
        tree.AddThemeFontSizeOverride("font_size", 15);
        tree.SetColumnTitle(0, "선택"); tree.SetColumnCustomMinimumWidth(0, 60); tree.SetColumnExpand(0, false);
        tree.SetColumnTitle(1, "병종");
        foreach (var (column, title) in new[] { (2, "대기 병력"), (3, "훈련"), (4, "이동") })
        { tree.SetColumnTitle(column, title); tree.SetColumnCustomMinimumWidth(column, 90); tree.SetColumnExpand(column, false); }
        var root = tree.CreateItem();
        foreach (var entry in garrisons)
        {
            var item = tree.CreateItem(root);
            item.SetText(0, selected == entry.Troop.Code ? "◆" : "◇"); item.SetText(1, entry.Troop.Name);
            item.SetText(2, entry.Garrison.Troops.ToString("N0"));
            item.SetText(3, entry.Garrison.TrainingLevel.ToString()); item.SetText(4, entry.Troop.MovementPerDay.ToString());
            item.SetMetadata(0, entry.Troop.Code);
            if (selected == entry.Troop.Code) tree.SetSelected(item, 1);
        }
        tree.ItemSelected += () =>
        {
            if (tree.GetSelected() is { } item) selected = item.GetMetadata(0).AsString();
        };
        body.AddChild(tree);
        AddProductionPickerActions(body, () =>
        {
            if (string.IsNullOrWhiteSpace(selected)) return false;
            applySelection(selected);
            return true;
        });
    }

    private void AddProductionPickerActions(VBoxContainer body, Func<bool> apply)
    {
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 8);
        var cancel = MakeButton("취소");
        cancel.Pressed += CloseDoctrinePicker;
        actions.AddChild(cancel);
        var accept = MakeButton("적용", true);
        accept.Pressed += () =>
        {
            if (!apply()) return;
            CloseDoctrinePicker();
        };
        actions.AddChild(accept);
        body.AddChild(actions);
    }
}
