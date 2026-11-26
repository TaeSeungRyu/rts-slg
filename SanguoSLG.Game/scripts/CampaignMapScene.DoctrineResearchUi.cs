using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private CanvasLayer? _doctrinePickerLayer;
    private DoctrineResearchWorkspace? _doctrineWorkspace;
    private GeneralId? _doctrineOfficer;

    private void BuildIntegratedCommandWorkspace(AdaptiveModalScaffold scaffold, City city, int cmdIndex)
    {
        scaffold.BodyScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scaffold.Body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scaffold.Body.CustomMinimumSize = new Vector2(0f, scaffold.BodyLayoutHeight);
        _optionCards.Clear();
        _disabledOptions.Clear();
        _modalMultiParams.Clear();
        _modalParam = 0;
        _doctrineOfficer = null;
        _researchFundingRatios.Clear();

        var command = Cmds[cmdIndex];
        var research = command.Kind == CommandKind.Research;
        if (research)
        {
            var hiddenFunding = new VBoxContainer { Name = "DoctrineResearchFundingState", Visible = false };
            scaffold.Body.AddChild(hiddenFunding);
            foreach (var fundingCity in _state.Cities.Where(candidate => candidate.Owner == city.Owner).OrderBy(candidate => candidate.Id.Value))
            {
                var ratio = new SpinBox { Value = 1, MinValue = 0, MaxValue = 10, Step = 1 };
                hiddenFunding.AddChild(ratio);
                _researchFundingRatios[fundingCity.Id.Value] = ratio;
            }
        }

        var workspace = new DoctrineResearchWorkspace();
        _doctrineWorkspace = workspace;
        scaffold.Body.AddChild(workspace);
        var doctrine = command.Param == "troop";
        var stratagem = command.Param == "stratagem_research";
        var cityStratagem = command.Kind == CommandKind.CityStratagem;
        var alliance = command.Kind == CommandKind.FormAlliance;
        var declareWar = command.Kind == CommandKind.DeclareWar;
        var recruitment = command.Kind == CommandKind.AppointRecruitmentOfficer;
        workspace.Configure(cityStratagem ? "대상 도시" : "연구 도시", research, "수행 장수",
            cityStratagem ? "지도에서 대상 지정" : alliance ? "동맹 제안" : declareWar ? "선전포고"
                : recruitment ? "병력담당 임명" : "연구 시작");
        if (research) workspace.FundingChangeButton.Pressed += () => OpenDoctrineFundingPicker(city);
        workspace.OfficerChangeButton.Pressed += () => OpenDoctrineOfficerPicker(city.Id, cmdIndex);
        workspace.StartButton.Pressed += () =>
        {
            if (_doctrineOfficer is { } officer)
            {
                if (cityStratagem) BeginStratagemMapTargeting(city.Id, cmdIndex, officer, _modalParam);
                else AskExecute(city.Id, cmdIndex, officer, _modalParam);
            }
        };

        workspace.DetailHost.AddChild(MakeLabel(doctrine ? "선택 교리 상세"
            : stratagem ? "선택 계략연구 상세" : cityStratagem ? "선택 계략 상세" : alliance ? "선택 세력 상세"
                : declareWar ? "선택 성 상세" : recruitment ? "자동 생산 설정" : "선택 연구 상세", 18, GoldBright));
        _modalDetail = MakeLabel(doctrine ? "전투교리 대상을 선택하세요."
            : stratagem ? "계략연구 대상을 선택하세요." : cityStratagem ? "실행할 도시계략을 선택하세요."
                : alliance ? "동맹을 제안할 세력을 선택하세요."
                : declareWar ? "선전포고할 성을 선택하세요." : recruitment ? "자동 생산 병종을 선택하세요."
                    : "일반연구 대상을 선택하세요.", 14, Parchment);
        _modalDetail.Name = doctrine ? "DoctrineResearchDetailText"
            : stratagem ? "StratagemResearchDetailText" : cityStratagem ? "CityStratagemDetailText"
                : "GeneralResearchDetailText";
        _modalDetail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        workspace.DetailHost.AddChild(_modalDetail);
        if (recruitment) AddClearOfficerButton(workspace.DetailHost, city, command.Kind);

        workspace.DoctrineList.AddChild(MakeLabel(doctrine
            ? "연구할 전투교리를 선택하세요"
            : stratagem ? "연구할 계략을 선택하세요" : cityStratagem ? "실행할 도시계략을 선택하세요"
                : alliance ? "동맹을 제안할 세력을 선택하세요"
                : declareWar ? "선전포고할 성을 선택하세요" : recruitment ? "자동 생산 병종을 선택하세요"
                    : "연구할 일반연구를 선택하세요", 18, GoldBright));
        var options = OptionList(Cmds[cmdIndex], city);
        var grid = new GridContainer
        {
            Name = "CommandOptionGrid",
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        workspace.DoctrineList.AddChild(grid);

        for (var i = 0; doctrine && i < _troops.Count; i++)
        {
            if (!FactionTroopUnlock.CheckResearch(_state, city.Owner, _troops[i].Code).Allowed)
                _disabledOptions.Add(i + 2);
        }
        if (recruitment)
        {
            _autoRecruitRateParam = city.RecruitmentOfficer is null
                ? 1 : System.Math.Clamp(city.AutoRecruitRate <= 0 ? 1 : city.AutoRecruitRate, 1, 3);
            var autoOptions = AutoRecruitTroopOptions();
            for (var i = 0; i < autoOptions.Count; i++)
            {
                if (!FactionTroopUnlock.Check(_state, city.Owner, autoOptions[i].Code).Allowed)
                    _disabledOptions.Add(i);
            }
            var current = CurrentAutoRecruitTroopCodes(city).ToHashSet(System.StringComparer.Ordinal);
            for (var i = 0; i < autoOptions.Count; i++)
            {
                if (current.Contains(autoOptions[i].Code) && !_disabledOptions.Contains(i))
                    _modalMultiParams.Add(i);
            }
            if (_modalMultiParams.Count == 0)
            {
                var first = Enumerable.Range(0, options.Count).FirstOrDefault(index => !_disabledOptions.Contains(index));
                if (options.Count > 0 && !_disabledOptions.Contains(first)) _modalMultiParams.Add(first);
            }
        }

        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var disabled = _disabledOptions.Contains(index);
            var card = OptionCard(options[index], disabled);
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _optionCards.Add(card);
            if (!disabled)
            {
                card.GuiInput += input =>
                {
                    if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
                    if (recruitment) ToggleMultiOption(index, options[index]);
                    else PickOption(index, options[index]);
                    RefreshIntegratedCommandWorkspace(city);
                };
            }
            grid.AddChild(card);
        }

        _modalParam = Enumerable.Range(0, options.Count).FirstOrDefault(index => !_disabledOptions.Contains(index));
        if (recruitment)
        {
            AddAutoRecruitRatePicker(workspace.DoctrineList, options);
            RefreshMultiOptionCards(options);
        }
        else if (options.Count > 0) PickOption(_modalParam, options[_modalParam]);
        else workspace.DoctrineList.AddChild(MakeLabel("선택할 수 있는 대상이 없습니다.", 16, Parchment));
        RefreshIntegratedCommandWorkspace(city);
    }

    private void RefreshIntegratedCommandWorkspace(City city)
    {
        if (!GodotObject.IsInstanceValid(_doctrineWorkspace)) return;
        var command = Cmds[_cmdIndex];
        var research = command.Kind == CommandKind.Research;
        var cost = CurrentResearchCost(city);
        var shares = CurrentResearchFundingShares();
        var allocations = AllocateResearchFunding(cost, shares);
        var cityMap = _state.Cities.ToDictionary(candidate => candidate.Id);
        var shortage = allocations.FirstOrDefault(allocation =>
            cityMap.TryGetValue(allocation.City, out var fundingCity) && allocation.Amount > fundingCity.Gold);
        var selectedNames = shares
            .Select(share => cityMap.TryGetValue(share.City, out var fundingCity) ? fundingCity.Name : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var cityStratagem = command.Kind == CommandKind.CityStratagem;
        _doctrineWorkspace!.FundingSummary.Text = cityStratagem
            ? "계략과 수행 장수를 정한 뒤 지도에서 적 성을 선택합니다."
            : shares.Count == 0
                ? "선택된 도시가 없습니다."
                : $"{shares.Count}개 도시 · {string.Join(", ", selectedNames.Take(4))}{(selectedNames.Count > 4 ? " 외" : "")}"
                    + (shortage.City.Value == 0 ? "" : "\n금이 부족한 도시가 있습니다.");
        _doctrineWorkspace.FundingSummary.Modulate = cityStratagem
            ? Colors.White : shares.Count == 0 || shortage.City.Value != 0 ? Red : Colors.White;
        _doctrineWorkspace.OfficerSummary.Text = _doctrineOfficer is { } officer
            ? OfficerSummaryText(officer, command.Kind)
            : "수행 장수를 선택하세요.";
        _doctrineWorkspace.TotalCost.Text = research
            ? $"총 연구비 {cost:N0}금"
            : cityStratagem ? "실행 후 지도에서 대상 성 지정"
            : command.Kind == CommandKind.FormAlliance ? $"사절 비용 {_cb.AllianceGoldCost:N0}금"
            : command.Kind == CommandKind.DeclareWar ? "선전포고 유효 기간 100일"
            : command.Kind == CommandKind.AppointRecruitmentOfficer
                ? $"자동 생산 {_modalMultiParams.Count}개 병종 · {_autoRecruitRateParam}배" : "";
        _doctrineWorkspace.StartButton.Disabled = _doctrineOfficer is null || _optionCards.Count == 0
            || command.Kind == CommandKind.AppointRecruitmentOfficer && _modalMultiParams.Count == 0
            || _disabledOptions.Contains(_modalParam)
            || research && (shares.Count == 0 || shortage.City.Value != 0);
    }

    private void RefreshDoctrineResearchWorkspace(City city) => RefreshIntegratedCommandWorkspace(city);

    private string OfficerSummaryText(GeneralId id, CommandKind kind = CommandKind.Research)
    {
        var general = _state.Generals.First(candidate => candidate.Id == id);
        var ability = kind is CommandKind.FormAlliance or CommandKind.DeclareWar ? $"정치 {general.Politics}"
            : kind == CommandKind.AppointRecruitmentOfficer ? $"무력 {general.Might}" : $"지력 {general.Intellect}";
        return $"{general.Name} · {ability} · {CurrentDuty(id)}";
    }

    private void OpenDoctrineFundingPicker(City city)
    {
        CloseDoctrinePicker();
        var original = _researchFundingRatios.ToDictionary(pair => pair.Key, pair => (int)pair.Value.Value);
        var draft = new Dictionary<int, int>(original);
        var query = "";
        var sortColumn = 0;
        var ascending = true;
        var amountLabels = new Dictionary<int, Label>();
        var ratioLabels = new Dictionary<int, Label>();
        var layer = CreateDoctrinePickerLayer("연구 도시 선택", out var body, out var close);
        _doctrinePickerLayer = layer;
        close.Pressed += CloseDoctrinePicker;

        var search = new LineEdit
        {
            Name = "DoctrineFundingSearch",
            PlaceholderText = "도시명 검색",
            ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(0f, 38f),
        };
        body.AddChild(search);
        var header = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 6);
        body.AddChild(header);
        var list = new VBoxContainer { Name = "DoctrineFundingRows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        var scroll = new ScrollContainer
        {
            Name = "DoctrineFundingTableScroll",
            CustomMinimumSize = new Vector2(0f, 430f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        GameUiSkin.ApplyQuickListScroll(scroll);
        scroll.AddChild(list);
        body.AddChild(scroll);

        var columns = new[] { ("도시명", 0, 2.2f), ("보유 금", 1, 1.4f), ("선택", 2, 0.8f), ("분담 비율", 3, 1.4f), ("분담 금액", 4, 1.5f) };
        foreach (var column in columns)
        {
            var button = MakeButton(column.Item1, false);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.SizeFlagsStretchRatio = column.Item3;
            button.Pressed += () =>
            {
                if (sortColumn == column.Item2) ascending = !ascending;
                else { sortColumn = column.Item2; ascending = true; }
                Rebuild();
            };
            header.AddChild(button);
        }

        void Rebuild()
        {
            Clear(list);
            amountLabels.Clear();
            ratioLabels.Clear();
            IEnumerable<City> cities = _state.Cities.Where(candidate => candidate.Owner == city.Owner)
                .Where(candidate => string.IsNullOrWhiteSpace(query)
                    || candidate.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase));
            cities = sortColumn switch
            {
                1 => ascending ? cities.OrderBy(candidate => candidate.Gold) : cities.OrderByDescending(candidate => candidate.Gold),
                2 or 3 => ascending ? cities.OrderBy(candidate => draft[candidate.Id.Value]) : cities.OrderByDescending(candidate => draft[candidate.Id.Value]),
                4 => ascending ? cities.OrderBy(candidate => FundingAmount(candidate.Id)) : cities.OrderByDescending(candidate => FundingAmount(candidate.Id)),
                _ => ascending ? cities.OrderBy(candidate => candidate.Name) : cities.OrderByDescending(candidate => candidate.Name),
            };
            foreach (var fundingCity in cities)
            {
                var row = new HBoxContainer { CustomMinimumSize = new Vector2(0f, 42f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 6);
                row.AddChild(Cell(fundingCity.Name, 2.2f));
                row.AddChild(Cell($"{fundingCity.Gold:N0}", 1.4f));
                var selected = new CheckButton { ButtonPressed = draft[fundingCity.Id.Value] > 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.8f };
                row.AddChild(selected);
                var ratio = Cell("-", 1.4f);
                ratio.Name = $"DoctrineFundingRatio_{fundingCity.Id.Value}";
                ratio.HorizontalAlignment = HorizontalAlignment.Center;
                ratioLabels[fundingCity.Id.Value] = ratio;
                row.AddChild(ratio);
                var amount = Cell($"{FundingAmount(fundingCity.Id):N0}", 1.5f);
                amount.Name = $"DoctrineFundingAmount_{fundingCity.Id.Value}";
                amountLabels[fundingCity.Id.Value] = amount;
                row.AddChild(amount);
                selected.Toggled += enabled =>
                {
                    draft[fundingCity.Id.Value] = enabled ? 1 : 0;
                    RefreshAutomaticShares();
                };
                list.AddChild(row);
            }
            RefreshAutomaticShares();
        }

        void RefreshAutomaticShares()
        {
            var selectedCount = draft.Count(pair => pair.Value > 0);
            foreach (var pair in ratioLabels)
                pair.Value.Text = draft[pair.Key] > 0 ? $"1 / {selectedCount}" : "-";
            foreach (var pair in amountLabels)
                pair.Value.Text = $"{FundingAmount(new CityId(pair.Key)):N0}";
        }

        int FundingAmount(CityId id)
        {
            var shares = draft.Where(pair => pair.Value > 0)
                .Select(pair => new ResearchFundingShare(new CityId(pair.Key), pair.Value)).ToList();
            return AllocateResearchFunding(CurrentResearchCost(city), shares)
                .FirstOrDefault(allocation => allocation.City == id).Amount;
        }

        search.TextChanged += value => { query = value.Trim(); Rebuild(); };
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 8);
        var cancel = MakeButton("취소");
        cancel.Pressed += CloseDoctrinePicker;
        actions.AddChild(cancel);
        var apply = MakeButton("적용", true);
        apply.Pressed += () =>
        {
            foreach (var pair in draft) _researchFundingRatios[pair.Key].Value = pair.Value;
            CloseDoctrinePicker();
            RefreshIntegratedCommandWorkspace(city);
        };
        actions.AddChild(apply);
        body.AddChild(actions);
        Rebuild();
        return;

        Label Cell(string text, float ratio)
        {
            var label = MakeLabel(text, 14, Parchment);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.SizeFlagsStretchRatio = ratio;
            return label;
        }
    }

    private CanvasLayer CreateDoctrinePickerLayer(string titleText, out VBoxContainer body, out BaseButton close)
    {
        var layer = new CanvasLayer { Layer = 30 };
        AddChild(layer);
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CloseDoctrinePicker();
        };
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var scaffold = new AdaptiveModalScaffold();
        scaffold.Configure(GetViewport().GetVisibleRect().Size, AdaptiveModalScaffold.WidthProfile.Standard,
            AdaptiveModalScaffold.ContentLayout.Table, AdaptiveModalScaffold.WorkspaceProfile.Default);
        center.AddChild(scaffold);
        close = AdaptiveModalScaffold.CreateCloseButton();
        scaffold.SetHeader(MakeLabel(titleText, 24, Gold), close, GoldRule());
        body = scaffold.Body;
        return layer;
    }

    private void CloseDoctrinePicker()
    {
        if (_doctrinePickerLayer is not null && GodotObject.IsInstanceValid(_doctrinePickerLayer))
            _doctrinePickerLayer.QueueFree();
        _doctrinePickerLayer = null;
    }

    partial void OpenDoctrineOfficerPicker(CityId city, int cmdIndex);
}
