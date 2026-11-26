using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>
/// 건설 지휘소와 건설부대 편성의 콘텐츠·예약 이벤트. 프레임 그림은
/// DeployCommandAssetRoot/deploy_command_panel.png, 버튼은 CommandCenterButton,
/// 건축물 아이콘은 res://assets/ui/game/struct_card/0..4.png를 재사용한다.
/// 편성 프레임의 구획 크기와 공통 여백은 ConstructionComposeModal의 부모인
/// SupplyComposeModal.cs에서 정하므로, 이 파일은 각 구획 안의 콘텐츠만 배치한다.
/// </summary>
public sealed partial class CampaignMapScene
{
    private int _constructionSelectedIndex = -1;
    private static readonly string[] ConstructionCodes =
        ["palisade", "scout_post", "watchtower", "fort", "formation"];

    private int ConstructionProvisionsFor60Days(int troops, int buildingProvisions)
    {
        // AdvanceOrchestrator의 병력 1만 명당 하루 소비량과 건설부대 소비율(20%)을 사용한다.
        var consumption = ((long)troops * _balance.ProvisionsPer10kPerDay * 60 * 20
            + 10_000_000L - 1) / 10_000_000L;
        return buildingProvisions + (int)consumption;
    }

    private void OpenConstructionHub(CityId cityId)
    {
        _depModalCity = cityId;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.68f),
            MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var viewport = GetViewport().GetVisibleRect().Size;
        // [지휘소 전체 크기] 28은 화면 가장자리 여백, 970은 최대 높이, 1.5는 가로세로 비율.
        var height = Mathf.Min(viewport.Y - 28f, 970f);
        var width = Mathf.Min(viewport.X - 28f, height * 1.5f);
        var panel = new PanelContainer { Name = "ConstructionCommandCenter",
            CustomMinimumSize = new Vector2(width, height), Size = new Vector2(width, height),
            Position = (viewport - new Vector2(width, height)) / 2f,
            MouseFilter = Control.MouseFilterEnum.Stop };
        panel.SetMeta("modal_root", true);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        layer.AddChild(panel);
        var background = new TextureRect { Name = "ConstructionCommandBackground",
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(background);
        var safe = new MarginContainer();
        safe.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // [프레임 내부 여백] 좌우 값은 금장 측면 테두리와 콘텐츠 사이 간격.
        // top/bottom은 PNG의 상단 장식·하단 장식에 콘텐츠가 닿지 않는 범위다.
        safe.AddThemeConstantOverride("margin_left", 45);
        safe.AddThemeConstantOverride("margin_right", 45);
        safe.AddThemeConstantOverride("margin_top", 13);
        safe.AddThemeConstantOverride("margin_bottom", 38);
        panel.AddChild(safe);
        var root = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        safe.AddChild(root);
        var name = _state.Cities.First(city => city.Id == cityId).Name;
        // [제목줄 높이] 아래 상태줄(48)과 독립적으로 조절할 수 있다.
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 76) };
        // 배경 PNG의 왼쪽 깃발은 약 50px까지 차지한다. 제목 시작을 깃발 바깥으로 보낸다.
        header.AddChild(new Control { Name = "ConstructionTitleFlagClearance",
            CustomMinimumSize = new Vector2(52, 0), MouseFilter = Control.MouseFilterEnum.Ignore });


var titleMargin = new MarginContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
};

titleMargin.AddThemeConstantOverride("margin_top", 15);
header.AddChild(titleMargin);

var title = MakeLabel($"건설 지휘소  《 {name} 》", 26, GoldBright);
title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
title.VerticalAlignment = VerticalAlignment.Center;
titleMargin.AddChild(title);



        // 보급 지휘소 BuildSupplyHubHeader와 동일한 30×30 닫기 버튼 스타일.
        var close = MakeButton("×");
        close.CustomMinimumSize = new Vector2(30, 30);
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        close.AddThemeFontSizeOverride("font_size", 18);
        close.AddThemeStyleboxOverride("normal", Frame(Colors.Transparent, Gold, 1, 4, 2));
        close.AddThemeStyleboxOverride("hover", Frame(new Color("5b1712"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("pressed", Frame(new Color("35100d"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        close.Name = "ConstructionCommandClose";
        close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        close.Pressed += () => { CloseModal(); SelectCity(cityId); };
        header.AddChild(close);
        root.AddChild(header);

        var mine = Enumerable.Range(0, _pendingDeploys.Count)
            .Where(index => _pendingDeploys[index].Req.City == cityId
                && _pendingDeploys[index].Req.ConstructionDefinitionCode is not null).ToList();
        if (!mine.Contains(_constructionSelectedIndex)) _constructionSelectedIndex = mine.FirstOrDefault(-1);
        var status = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        status.AddChild(MakeLabel($"예약된 건설부대  {mine.Count}", 17, GoldBright));
        status.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });



var addMargin = new MarginContainer();
addMargin.AddThemeConstantOverride("margin_top", 20);
status.AddChild(addMargin);

var add = CommandCenterButton("＋ 신규 편성", true, new Vector2(170, 42));
add.Name = "ConstructionCommandNewUnit";
add.Pressed += () => OpenConstructionCompose(-1);
addMargin.AddChild(add);



        root.AddChild(status);

        var columns = new HBoxContainer { Name = "ConstructionCommandColumns",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        // [좌우 칸 간격] 왼쪽 목록 폭은 아래 405, 남은 공간이 오른쪽 상세로 간다.
        columns.AddThemeConstantOverride("separation", 12);
        root.AddChild(columns);
        var left = CommandCenterSection("예약 건설부대 목록");
        left.CustomMinimumSize = new Vector2(405, 0);
        left.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(left);
        var listScroll = new ScrollContainer { Name = "ConstructionCommandUnitList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        GameUiSkin.ApplyQuickListScroll(listScroll);
        left.AddChild(listScroll);


var listInset = new MarginContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
};

listInset.AddThemeConstantOverride("margin_right", 6);
listScroll.AddChild(listInset);

var list = new VBoxContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
};




        list.AddThemeConstantOverride("separation", 8);
        listInset.AddChild(list);
        foreach (var index in mine)
        {
            var request = _pendingDeploys[index].Req;
            var selected = index == _constructionSelectedIndex;
            var card = CommandCenterButton("", selected, new Vector2(0, 112));
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            card.Pressed += () => { _constructionSelectedIndex = index; OpenConstructionHub(cityId); };
            list.AddChild(card);
            var cardRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            cardRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect,
                Control.LayoutPresetMode.Minsize, 14);
            cardRow.AddThemeConstantOverride("separation", 10);
            card.AddChild(cardRow);
            cardRow.AddChild(DeployUnitArtwork(UnitCard("supply", TroopClass.Supply), 66));
            // var detail = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            // cardRow.AddChild(detail);

var detail = new VBoxContainer
{
    MouseFilter = Control.MouseFilterEnum.Ignore,
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
    Alignment = BoxContainer.AlignmentMode.Center,
};

cardRow.AddChild(detail);

            detail.AddChild(MakeLabel($"{TroopName(request.TroopCode)} {request.Troops:N0}명", 17, GoldBright));
            detail.AddChild(MakeLabel($"{_state.Generals.First(g => g.Id == request.Vanguard).Name} · {_fieldBuildingDefinitions.First(b => b.Code == request.ConstructionDefinitionCode).Name}",
                12, Parchment));
            detail.AddChild(MakeLabel(request.Target is { } target ? $"목표 ({target.Q},{target.R})" : "목표 미지정", 12, Gold));
        }
        //if (mine.Count == 0) list.AddChild(MakeLabel("예약된 건설부대가 없습니다.", 15, Parchment));

if (mine.Count == 0)
{
    var emptyLabel = MakeLabel(
        "예약된 건설부대가 없습니다.", 15, Parchment);

    emptyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    emptyLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

    emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
    emptyLabel.VerticalAlignment = VerticalAlignment.Center;

    list.AddChild(emptyLabel);
}

        var right = CommandCenterSection("선택 건설부대 정보");
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(right);
        BuildConstructionHubDetail(right, cityId);
        var footer = new HBoxContainer { CustomMinimumSize = new Vector2(0, 43) };
        footer.AddThemeConstantOverride("separation", 8);
        if (_constructionSelectedIndex >= 0)
        {
            var selected = _constructionSelectedIndex;
            var remove = CommandCenterButton("부대 삭제", false, new Vector2(132, 42));
            remove.Pressed += () => { _pendingDeploys.RemoveAt(selected); _constructionSelectedIndex = -1; OpenConstructionHub(cityId); };
            footer.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, new Vector2(145, 42));
            edit.Pressed += () => OpenConstructionCompose(selected);
            footer.AddChild(edit);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(cityId); };
        footer.AddChild(done);
        root.AddChild(footer);
        CenterAndDrag(panel, header, width, height, root);
        panel.Size = new Vector2(width, height);
        panel.Position = (viewport - panel.Size) / 2f;
        DrawDeployPaths();
    }

    private void BuildConstructionHubDetail(VBoxContainer right, CityId cityId)
    {
        // if (_constructionSelectedIndex < 0 || _constructionSelectedIndex >= _pendingDeploys.Count)
        // {
        //     right.AddChild(MakeLabel("왼쪽에서 건설부대를 선택하세요.", 16, Parchment));
        //     return;
        // }

if (_constructionSelectedIndex < 0 ||
    _constructionSelectedIndex >= _pendingDeploys.Count)
{
    var emptyLabel = MakeLabel(
        "왼쪽에서 건설부대를 선택하세요.", 16, Parchment);

    emptyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    emptyLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

    emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
    emptyLabel.VerticalAlignment = VerticalAlignment.Center;

    right.AddChild(emptyLabel);

    return;
}        
        var index = _constructionSelectedIndex;
        var request = _pendingDeploys[index].Req;
        var building = _fieldBuildingDefinitions.First(b => b.Code == request.ConstructionDefinitionCode);
        var leader = _state.Generals.First(g => g.Id == request.Vanguard);
        var overview = new PanelContainer();
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", 12);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard("supply", TroopClass.Supply), 76));
        overviewRow.AddChild(MakeLabel($"{TroopName(request.TroopCode)} {request.Troops:N0}명\n주장 {leader.Name} · 군량 {request.Provisions:N0} · 금 {request.Gold:N0}", 16, GoldBright));
        overview.AddChild(overviewRow);
        right.AddChild(overview);
        right.AddChild(MakeLabel("건설 대상", 17, GoldBright));
        // [건설 대상] 110은 아이콘 50px과 이름이 들어가는 가로 스크롤 영역의 높이.
        var targets = new ScrollContainer { Name = "ConstructionTargetsScroll",
            CustomMinimumSize = new Vector2(0, 110),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(targets);
        var choices = new HBoxContainer();
        choices.AddThemeConstantOverride("separation", 7);
        targets.AddChild(choices);
        for (var i = 0; i < ConstructionCodes.Length; i++)
        {
            var definition = _fieldBuildingDefinitions.FirstOrDefault(b => b.Code == ConstructionCodes[i]);
            if (definition is null) continue;
            // 대상 버튼은 지휘소의 버튼 PNG를 쓰지 않는다. 투명 배경에 금장 테두리만 둔다.
            var choice = MakeButton("");
            choice.CustomMinimumSize = new Vector2(105, 94);



var isSelected = definition.Code == building.Code;

// 일반 및 선택 상태
choice.AddThemeStyleboxOverride("normal", Frame(
    isSelected ? new Color("6a2018") : Colors.Transparent,
    isSelected ? GoldBright : Gold,
    isSelected ? 3 : 1,
    4, 5
));

// 마우스 Hover
choice.AddThemeStyleboxOverride("hover", Frame(
    isSelected ? new Color("802b20") : new Color("3d2a1c"),
    GoldBright,
    isSelected ? 3 : 2,
    4, 5
));

// 마우스 클릭
choice.AddThemeStyleboxOverride("pressed", Frame(
    new Color("49150f"),
    GoldBright,
    3,
    4, 5
));

            // choice.AddThemeStyleboxOverride("normal", Frame(Colors.Transparent,
            //     definition.Code == building.Code ? GoldBright : Gold,
            //     definition.Code == building.Code ? 2 : 1, 4, 5));
            // choice.AddThemeStyleboxOverride("hover", Frame(Colors.Transparent, GoldBright, 2, 4, 5));
            // choice.AddThemeStyleboxOverride("pressed", Frame(Colors.Transparent, GoldBright, 2, 4, 5));





            choice.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            choice.TooltipText = $"{definition.Name} · 금 {definition.GoldCost} · 군량 {definition.ProvisionsCost}";
            choices.AddChild(choice);
            var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Center };
            content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            choice.AddChild(content);
            var image = new TextureRect { Texture = GD.Load<Texture2D>($"res://assets/ui/game/struct_card/{i}.png"),
                CustomMinimumSize = new Vector2(50, 50),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            content.AddChild(image);
            var caption = MakeLabel(definition.Name, 12, GoldBright);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            content.AddChild(caption);
            choice.Pressed += () =>
            {
                var updated = request with { ConstructionDefinitionCode = definition.Code,
                    Gold = System.Math.Max(request.Gold, definition.GoldCost),
                    Provisions = System.Math.Max(request.Provisions, definition.ProvisionsCost) };
                _pendingDeploys[index] = (updated, _pendingDeploys[index].Label);
                OpenConstructionHub(cityId);
            };
        }
        var cost = new PanelContainer();
        cost.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        cost.AddChild(MakeLabel($"{building.Name} · 금 {building.GoldCost:N0} · 군량 {building.ProvisionsCost:N0} · {building.BuildDays}일\n이동 중 군량 소비 80% 감소 · 공격/방어 최하", 14, Parchment));
        right.AddChild(cost);
        var destination = new HBoxContainer();
        destination.AddChild(MakeLabel(request.Target is { } hex
            ? $"현재 목표: ({hex.Q},{hex.R})" : "현재 목표: 미지정", 14, GoldBright));
        destination.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var targetButton = CommandCenterButton("목표 지정", true, new Vector2(132, 38));
        targetButton.Pressed += () => BeginTargeting(index);
        destination.AddChild(targetButton);
        right.AddChild(destination);
    }

    private void OpenConstructionCompose(int editIndex)
    {
        var cityId = _depModalCity;
        var city = _state.Cities.First(c => c.Id == cityId);
        var old = editIndex >= 0 && editIndex < _pendingDeploys.Count
            ? _pendingDeploys[editIndex].Req : null;
        var definition = _fieldBuildingDefinitions.First(b => b.Code == (old?.ConstructionDefinitionCode ?? "palisade"));
        string? troopCode = old?.TroopCode;
        var amount = old?.Troops ?? 0;
        GeneralId? leaderId = old?.Vanguard;
        var gold = old?.Gold ?? definition.GoldCost;
        var provisions = old?.Provisions ?? ConstructionProvisionsFor60Days(System.Math.Max(500, amount), definition.ProvisionsCost);
        var delay = old?.DelayDays ?? 0;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var modal = new ConstructionComposeModal(GetViewport().GetVisibleRect().Size,
            GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"), _font,
            $"건설부대 편성  《 {city.Name} 》");
        layer.AddChild(modal);
        modal.BackButton.Pressed += () => OpenConstructionHub(cityId);
        ApplyDeployComposeButtonTexture(modal.BackButton, false);
        GameUiSkin.ApplyQuickListScroll(modal.LeftScroll);
        var left = modal.LeftContent;
        var right = modal.RightContent;
        left.AddChild(MakeLabel("병종 선택 (1종)", 16, GoldBright));
        // [편성 왼쪽] 병종 카드 영역 높이. 전체 왼쪽 칸의 스크롤은 부모 LeftScroll이 담당한다.
        var troopScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 210),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        GameUiSkin.ApplyQuickListScroll(troopScroll);
        left.AddChild(troopScroll);
        var troopInset = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        troopInset.AddThemeConstantOverride("margin_right", 7);
        troopScroll.AddChild(troopInset);
        var troopGrid = new GridContainer { Name = "ConstructionTroopGrid", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        troopGrid.AddThemeConstantOverride("h_separation", 7);
        troopGrid.AddThemeConstantOverride("v_separation", 7);
        troopInset.AddChild(troopGrid);
        var amountSpin = ApplyNumberInputStyle(new SpinBox { MinValue = 500, MaxValue = 500,
            Step = 100, Value = amount > 0 ? amount : 500,
            CustomMinimumSize = new Vector2(130, 30) });
        var troopButtons = new List<(string Code, PanelContainer Card)>();
        void RefreshTroops()
        {
            foreach (var (code, card) in troopButtons)
                card.AddThemeStyleboxOverride("panel", Frame(code == troopCode
                    ? new Color("6a2018") : InkSoft, code == troopCode ? GoldBright : Gold,
                    code == troopCode ? 2 : 1, 7, 8));
        }
        var reserved = ReservedTroopsByCode(cityId, editIndex, editingSupply: false);
        foreach (var garrison in _state.Garrisons.Where(g => g.City == cityId && !g.Trainee && g.Troops > 0))
        {
            var available = garrison.Troops - reserved.GetValueOrDefault(garrison.TroopCode);
            if (available < 500) continue;
            var template = _troops.FirstOrDefault(t => t.Code == garrison.TroopCode);
            if (template?.Class == TroopClass.Naval) continue;
            var code = garrison.TroopCode;
            var card = DeployCard(UnitCard(code, template?.Class), template?.Name ?? code,
                $"{available:N0}명 · 훈{garrison.TrainingLevel}");
            troopButtons.Add((code, card));
            card.GuiInput += input =>
            {
                if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
                troopCode = code;
                amountSpin.MaxValue = System.Math.Min(available, DeployMaxTroopsFor(cityId, leaderId));
                amountSpin.Value = System.Math.Min(amount > 0 ? amount : available, amountSpin.MaxValue);
                amount = (int)amountSpin.Value;
                RefreshTroops();
            };
            troopGrid.AddChild(card);
        }
        if (troopCode is not null)
        {
            var current = _state.Garrisons.FirstOrDefault(force => force.City == cityId
                && force.TroopCode == troopCode);
            if (current is not null)
            {
                amountSpin.MaxValue = System.Math.Max(500,
                    System.Math.Min(current.Troops - reserved.GetValueOrDefault(troopCode),
                        DeployMaxTroopsFor(cityId, leaderId)));
                amountSpin.Value = System.Math.Clamp(amount, 500, (int)amountSpin.MaxValue);
            }
        }
        RefreshTroops();
        left.AddChild(MakeLabel("병력 수량 (최소 500명)", 13, GoldBright));
        amountSpin.ValueChanged += value => amount = (int)value;
        left.AddChild(amountSpin);
        left.AddChild(DeployComposeResourceHeading("food.png", "휴대 군량"));
        var provisionLabel = MakeLabel("", 13, Parchment);
        provisionLabel.Name = "ConstructionProvision60Days";
        var provisionSpin = ApplyNumberInputStyle(new SpinBox { Name = "ConstructionProvisionInput",
            MinValue = definition.ProvisionsCost, MaxValue = System.Math.Max(definition.ProvisionsCost, city.Provisions),
            Step = 1, Value = provisions, CustomMinimumSize = new Vector2(150, 30) });
        var foodEdited = old is not null;
        var updatingFoodDefault = false;
        void RefreshProvisions()
        {
            var recommended = ConstructionProvisionsFor60Days((int)amountSpin.Value, definition.ProvisionsCost);
            if (!foodEdited)
            {
                updatingFoodDefault = true;
                provisionSpin.Value = System.Math.Min(recommended, provisionSpin.MaxValue);
                updatingFoodDefault = false;
            }
            provisions = (int)provisionSpin.Value;
            provisionLabel.Text = $"60일 권장 {recommended:N0} · 건설 비용 {definition.ProvisionsCost:N0} 포함";
        }
        amountSpin.ValueChanged += _ => RefreshProvisions();
        provisionSpin.ValueChanged += value =>
        { if (!updatingFoodDefault) foodEdited = true; provisions = (int)value; };
        RefreshProvisions();
        left.AddChild(provisionLabel);
        // [휴대 자원] 슬라이더와 숫자 입력은 같은 값을 공유한다. 오른쪽 7px은 왼쪽 스크롤바 여백.
        var provisionRow = new HBoxContainer();
        provisionRow.AddThemeConstantOverride("separation", 8);
        var provisionSlider = ApplySliderStyle(new HSlider { Name = "ConstructionProvisionSlider",
            MinValue = definition.ProvisionsCost, MaxValue = provisionSpin.MaxValue, Step = 1,
            Value = provisions, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        provisionSlider.ValueChanged += value => provisionSpin.Value = value;
        provisionSpin.ValueChanged += value => provisionSlider.Value = value;
        provisionRow.AddChild(provisionSlider);
        provisionRow.AddChild(provisionSpin);
        left.AddChild(provisionRow);
        left.AddChild(DeployComposeResourceHeading("money.png", "휴대 금"));
        var goldSpin = ApplyNumberInputStyle(new SpinBox { MinValue = 0,
            MaxValue = city.Gold, Step = 1, Value = gold,
            CustomMinimumSize = new Vector2(150, 30) });
        gold = (int)goldSpin.Value;
        goldSpin.ValueChanged += value => gold = (int)value;
        var goldRow = new HBoxContainer();
        goldRow.AddThemeConstantOverride("separation", 8);
        var goldSlider = ApplySliderStyle(new HSlider { Name = "ConstructionGoldSlider",
            MinValue = 0, MaxValue = System.Math.Max(1, city.Gold), Step = 1,
            Value = gold, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        goldSlider.ValueChanged += value => goldSpin.Value = value;
        goldSpin.ValueChanged += value => goldSlider.Value = value;
        goldRow.AddChild(goldSlider);
        goldRow.AddChild(goldSpin);
        left.AddChild(goldRow);
        AddDeploymentDelaySelector(left, delay, days => delay = days);
        right.AddChild(MakeLabel("주장 선택 (부관 없음)", 16, GoldBright));
        // [편성 오른쪽] 일반 장수 Tree 테이블. 이 칸은 테이블 자체만 세로 스크롤한다.
        // 열 제목과 최소 너비는 바로 아래 SetColumnTitle/SetColumnCustomMinimumWidth에서 조절한다.
        var officerTable = new Tree { Name = "ConstructionOfficerTable", Columns = 7,
            ColumnTitlesVisible = true, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollVerticalEnabled = true };
        officerTable.AddThemeFontOverride("font", _font);
        officerTable.AddThemeFontSizeOverride("font_size", 13);
        officerTable.AddThemeFontOverride("title_button_font", _font);
        officerTable.AddThemeFontSizeOverride("title_button_font_size", 12);
        foreach (var (column, title) in new[] { (0, "선택"), (1, "이름"), (2, "무"),
            (3, "지"), (4, "정"), (5, "보급"), (6, "현재 담당업무") })
            officerTable.SetColumnTitle(column, title);
        foreach (var column in new[] { 0, 2, 3, 4, 5 })
        {
            officerTable.SetColumnExpand(column, false);
            officerTable.SetColumnCustomMinimumWidth(column, column == 0 ? 52 : 42);
        }
        officerTable.SetColumnExpandRatio(1, 2);
        officerTable.SetColumnExpandRatio(6, 2);
        GameUiSkin.ApplyCommandTable(officerTable);
        right.AddChild(officerTable);
        var reservedGenerals = ReservedDeployGenerals(editIndex, editingSupply: false);
        var officerRoot = officerTable.CreateItem();
        foreach (var id in _state.GeneralsAt(cityId).Where(id => !_state.IsGeneralBusy(id)
            && !reservedGenerals.Contains(id)))
        {
            var general = _state.Generals.First(g => g.Id == id);
            var item = officerTable.CreateItem(officerRoot);
            item.SetMetadata(0, id.Value);
            item.SetCellMode(0, TreeItem.TreeCellMode.Check);
            item.SetChecked(0, id == leaderId);
            item.SetText(1, general.Name);
            ApplyGeneralTreePortrait(item, 1, id, goldBorder: true);
            item.SetText(2, general.Might.ToString());
            item.SetText(3, general.Intellect.ToString());
            item.SetText(4, general.Politics.ToString());
            item.SetText(5, GradeText(general.AptitudeFor(TroopClass.Supply)));
            item.SetText(6, CurrentDuty(id));
            for (var column = 0; column < 7; column++) item.SetSelectable(column, true);
            if (id == leaderId) item.Select(0);
        }
        officerTable.ItemSelected += () =>
        {
            var selected = officerTable.GetSelected();
            if (selected is null) return;
            leaderId = new GeneralId((int)selected.GetMetadata(0));
            for (var item = officerRoot.GetFirstChild(); item is not null; item = item.GetNext())
                item.SetChecked(0, (int)item.GetMetadata(0) == leaderId.Value.Value);
        };
        var summary = MakeLabel($"건설 대상 {definition.Name} · 금 {definition.GoldCost:N0} · 군량 {definition.ProvisionsCost:N0}",
            13, GoldBright);
        // [편성 하단] 부모 FooterContent의 높이·여백은 SupplyComposeModal.cs에서 결정한다.
        var footerRow = new HBoxContainer();
        summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footerRow.AddChild(summary);
        var confirm = CommandCenterButton("건설 예약", true, new Vector2(170, 42));
        confirm.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        footerRow.AddChild(confirm);
        modal.FooterContent.AddChild(footerRow);
        confirm.Pressed += () =>
        {
            if (troopCode is null || amount < 500 || leaderId is null)
            { ShowNotice("건설 편성 불가", "병종 1개, 병력 500명 이상, 주장을 선택하세요."); return; }
            if (gold < definition.GoldCost || provisions > city.Provisions)
            { ShowNotice("건설 편성 불가", "건설 비용 또는 60일분 휴대 군량이 도시 보유량을 초과합니다."); return; }
            var request = new DeployRequest(cityId, troopCode, amount, leaderId.Value,
                Mode: UnitMode.March, Target: old?.Target,
                Provisions: provisions, Gold: gold, DelayDays: delay,
                ConstructionDefinitionCode: definition.Code);
            var label = $"건설 {TroopName(troopCode)} {amount:N0}명 · {definition.Name}";
            ShowConfirm("건설부대 예약 확인", label, () =>
            {
                if (_advancing || _state.IsGeneralBusy(leaderId.Value)
                    || ReservedDeployGenerals(editIndex, editingSupply: false).Contains(leaderId.Value))
                { ShowNotice("건설 편성 불가", "선택한 장수가 다른 업무를 수행 중입니다."); return; }
                _state = _state.ReleaseOfficerDuties([leaderId.Value]);
                if (editIndex >= 0) _pendingDeploys[editIndex] = (request, label);
                else _pendingDeploys.Add((request, label));
                _constructionSelectedIndex = editIndex >= 0 ? editIndex : _pendingDeploys.Count - 1;
                OpenConstructionHub(cityId);
            });
        };
    }

    private async void RunConstructionUiQa()
    {
        var city = _state.Cities.First(candidate => candidate.Owner == Player && !candidate.IsPort);
        OpenConstructionHub(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var hub = _modalLayer?.FindChild("ConstructionCommandCenter", true, false) as PanelContainer;
        var add = _modalLayer?.FindChild("ConstructionCommandNewUnit", true, false) as Button;
        var passed = hub is not null && add is not null
            && _modalLayer?.FindChild("ConstructionCommandBackground", true, false) is TextureRect
            && _modalLayer?.FindChild("ConstructionTitleFlagClearance", true, false) is Control clearance
            && clearance.CustomMinimumSize.X >= 52;
        add?.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        passed &= _modalLayer?.FindChild("ConstructionComposeModal", true, false) is ConstructionComposeModal
            && _modalLayer?.FindChild("ConstructionTroopGrid", true, false) is GridContainer troopGrid
            && troopGrid.Columns == 2
            && _modalLayer?.FindChild("ConstructionComposeOfficer", true, false) is VBoxContainer
            && _modalLayer?.FindChild("ConstructionOfficerTable", true, false) is Tree
            && _modalLayer?.FindChild("ConstructionProvision60Days", true, false) is Label
            && _modalLayer?.FindChild("ConstructionProvisionInput", true, false) is SpinBox
            && _modalLayer?.FindChild("ConstructionProvisionSlider", true, false) is HSlider
            && _modalLayer?.FindChild("ConstructionGoldSlider", true, false) is HSlider;
        passed &= ConstructionProvisionsFor60Days(500, 0) == 1
            && ConstructionProvisionsFor60Days(10_000, 0) == 12
            && ConstructionProvisionsFor60Days(500, 5) == 6;
        var garrison = _state.Garrisons.FirstOrDefault(force => force.City == city.Id && force.Troops >= 500);
        var leader = _state.GeneralsAt(city.Id).FirstOrDefault();
        if (garrison is not null && leader != default)
        {
            var index = _pendingDeploys.Count;
            _pendingDeploys.Add((new DeployRequest(city.Id, garrison.TroopCode, 500, leader,
                Mode: UnitMode.March, Provisions: 5, Gold: 100,
                ConstructionDefinitionCode: "palisade"), "QA"));
            _constructionSelectedIndex = index;
            OpenConstructionHub(city.Id);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var choices = _modalLayer?.FindChild("ConstructionTargetsScroll", true, false) as ScrollContainer;
            passed &= choices?.GetChild(0).GetChildCount() == ConstructionCodes.Length
                && _modalLayer?.FindChild("ConstructionCommandUnitList", true, false) is ScrollContainer;
            if (choices?.GetChild(0).GetChild(0) is Button firstChoice)
                passed &= firstChoice.GetThemeStylebox("normal") is StyleBoxFlat
                    && firstChoice.FindChildren("*", "Label", true, false).OfType<Label>()
                        .Any(label => label.HorizontalAlignment == HorizontalAlignment.Center);
            _pendingDeploys.RemoveAt(index);
        }
        GD.Print($"[construction-ui-qa] passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
