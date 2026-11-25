using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenTroopGuide()
    {
        const float width = 1040f;
        const float height = 650f;
        var box = SystemView("부대설명", width, out var scroll, out var panel, out var titleRow);
        panel.Name = "TroopGuideListModal";
        // 공통 금색 이미지 테두리 적용
        ApplyUnitMenuFrame(panel);        
        var maxResearchBonus = SanguoSLG.Core.Simulation.ResearchCurve.Bonus(10);
        box.AddChild(MakeLabel($"기본 → 해당 병종 연구 Lv.10 최대치 (공격·방어 각각 +{maxResearchBonus}). 이동·사거리는 연구로 변하지 않습니다. 헤더를 눌러 정렬하세요.", 13, Parchment));
        var table = new Tree
        {
            Name = "TroopGuideTable", Columns = 9, ColumnTitlesVisible = true,
            HideRoot = true, SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, height - 110),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        table.AddThemeFontOverride("font", _font);
        table.AddThemeFontSizeOverride("font_size", 14);
        table.AddThemeFontOverride("title_button_font", _font);
        table.AddThemeFontSizeOverride("title_button_font_size", 13);
        var captions = new[] { "병종 카드", "병종", "부대 공격", "건물 공격", "방어", "이동/일", "부대 사거리", "건물 사거리", "성 사거리" };
        var widths = new[] { 170, 70, 105, 105, 80, 90, 85, 85, 75 };
        for (var col = 0; col < captions.Length; col++)
        {
            table.SetColumnTitle(col, captions[col]);
            table.SetColumnCustomMinimumWidth(col, widths[col]);
            table.SetColumnExpand(col, col == 0 || col >= 6);
        }
        box.AddChild(table);
        var sort = 0;
        bool descending = false;
        table.ColumnTitleClicked += (column, _) =>
        {
            descending = sort == column && !descending;
            sort = (int)column;
            FillRows();
        };
        void FillRows()
        {
            table.Clear();
            var root = table.CreateItem();
            var ordered = sort switch
            {
                1 => _troops.OrderBy(t => ClassName(t.Class), StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal),
                2 => _troops.OrderBy(t => t.AtkUnit).ThenBy(t => t.Name, StringComparer.Ordinal),
                3 => _troops.OrderBy(t => t.AtkBuilding).ThenBy(t => t.Name, StringComparer.Ordinal),
                4 => _troops.OrderBy(t => t.Df).ThenBy(t => t.Name, StringComparer.Ordinal),
                5 => _troops.OrderBy(t => t.MovementPerDay).ThenBy(t => t.Name, StringComparer.Ordinal),
                6 => _troops.OrderBy(t => t.RangeUnit).ThenBy(t => t.Name, StringComparer.Ordinal),
                7 => _troops.OrderBy(t => t.RangeBuilding).ThenBy(t => t.Name, StringComparer.Ordinal),
                8 => _troops.OrderBy(t => t.RangeCastle).ThenBy(t => t.Name, StringComparer.Ordinal),
                _ => _troops.OrderBy(t => t.Class).ThenBy(t => t.Name, StringComparer.Ordinal),
            };
            foreach (var troop in descending ? ordered.Reverse() : ordered)
            {
                var row = table.CreateItem(root);
                row.SetMetadata(0, troop.Code);
                row.SetText(0, troop.Name);
                row.SetIcon(0, RoundedOptionTexture(UnitCard(troop.Code, troop.Class), 36));
                row.SetIconMaxWidth(0, 36);
                row.SetText(1, ClassName(troop.Class));
                row.SetText(2, $"{troop.AtkUnit} → {troop.AtkUnit + maxResearchBonus}");
                row.SetText(3, $"{troop.AtkBuilding} → {troop.AtkBuilding + maxResearchBonus}");
                row.SetText(4, $"{troop.Df} → {troop.Df + maxResearchBonus}");
                row.SetText(5, TroopGuideTenths(troop.EffectiveMovementTenths));
                row.SetText(6, TroopGuideTenths(troop.EffectiveRangeUnitTenths));
                row.SetText(7, troop.RangeBuilding.ToString());
                row.SetText(8, troop.RangeCastle.ToString());
            }
            AppendSupportTroopGuideRows(table, root);
        }
        FillRows();
        table.ItemSelected += () =>
        {
            var code = table.GetSelected()?.GetMetadata(0).AsString();
            if (code is "supply" or "transport") OpenSupportTroopGuideDetail(code);
            else if (!string.IsNullOrWhiteSpace(code)) OpenTroopGuideDetail(code);
        };
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

private void OpenTroopGuideDetail(string code)
{
    const float width = 720f;
    const float height = 460f;

    // ==========================================
    // UI 설정값
    // ==========================================
    const int statColumnGap = 6;
    const int statRowGap = 6;
    const int sectionGap = 8;
    const int statCardHeight = 46;
    const int statLabelFontSize = 12;
    const int statValueFontSize = 18;

    var troop = _troops.FirstOrDefault(
        candidate => candidate.Code == code);

    if (troop is null) return;

    var special = new SanguoSLG.Core.Data.SpecialUnitLoader()
        .LoadFromDirectory(_dataDirectory)
        .FirstOrDefault(candidate => candidate.Code == code);

    var box = SystemView(
        troop.Name, width,
        out var scroll, out var panel, out var titleRow);

    panel.Name = "TroopGuideDetailModal";

    ApplyUnitMenuFrame(panel);

    // 뒤로가기
    var back = titleRow.GetChild<Button>(0);
    back.ButtonDown -= OpenSystemPalette;
    back.ButtonDown += OpenTroopGuide;
    back.Text = "◀";

    // ==========================================
    // 상단 좌우 레이아웃
    // ==========================================
    var top = new HBoxContainer();
    top.AddThemeConstantOverride("separation", 16);
    box.AddChild(top);

    // ==========================================
    // 좌측 병종 이미지
    // ==========================================
    var artworkFrame = new PanelContainer
    {
        CustomMinimumSize = new Vector2(198, 142),
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        ClipContents = true,
    };

    artworkFrame.AddThemeStyleboxOverride(
        "panel",
        Frame(
            new Color(0.045f, 0.035f, 0.03f),
            new Color(Gold, 0.6f),
            1, 7, 5
        )
    );

    artworkFrame.AddChild(new TextureRect
    {
        Texture = UnitCard(troop.Code, troop.Class),
        CustomMinimumSize = new Vector2(188, 132),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
    });

    top.AddChild(artworkFrame);

    // ==========================================
    // 우측 능력치 영역
    // ==========================================
    var summary = new VBoxContainer
    {
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    summary.AddThemeConstantOverride("separation", sectionGap);
    top.AddChild(summary);

    // 병종 제목
    var classRow = new HBoxContainer();
    classRow.AddThemeConstantOverride("separation", 12);

    classRow.AddChild(MakeLabel("병종", 16, Gold));
    classRow.AddChild(
        MakeLabel(ClassName(troop.Class), 18, Parchment)
    );

    summary.AddChild(classRow);

    // ==========================================
    // 능력치 카드 생성 함수
    // ==========================================
    PanelContainer CreateStatCard(string caption, string value)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, statCardHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        card.AddThemeStyleboxOverride(
            "panel",
            Frame(
                new Color("211711"),
                new Color(Gold, 0.25f),
                1, 5, 4
            )
        );

        var content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };

        content.AddThemeConstantOverride("separation", 2);
        card.AddChild(content);

        var captionLabel = MakeLabel(
            caption, statLabelFontSize, Parchment);

        captionLabel.HorizontalAlignment =
            HorizontalAlignment.Center;

        content.AddChild(captionLabel);

        var valueLabel = MakeLabel(
            value, statValueFontSize, GoldBright);

        valueLabel.HorizontalAlignment =
            HorizontalAlignment.Center;

        content.AddChild(valueLabel);

        return card;
    }

    // ==========================================
    // 능력치 그룹 생성 함수
    // ==========================================
    GridContainer CreateStatSection(string sectionTitle)
    {
        var section = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        section.AddThemeStyleboxOverride(
            "panel",
            Frame(
                new Color("302219"),
                new Color(Gold, 0.35f),
                1, 6, 8
            )
        );

        summary.AddChild(section);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        section.AddChild(content);

        content.AddChild(
            MakeLabel(sectionTitle, 14, GoldBright)
        );

        var grid = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        grid.AddThemeConstantOverride(
            "h_separation", statColumnGap);

        grid.AddThemeConstantOverride(
            "v_separation", statRowGap);

        content.AddChild(grid);

        return grid;
    }

    // ==========================================
    // 기본 능력치
    // ==========================================
    var basicStats = CreateStatSection("기본 능력치");

    basicStats.AddChild(
        CreateStatCard("부대 공격", troop.AtkUnit.ToString()));

    basicStats.AddChild(
        CreateStatCard("건물 공격", troop.AtkBuilding.ToString()));

    basicStats.AddChild(
        CreateStatCard("방어", troop.Df.ToString()));

    basicStats.AddChild(
        CreateStatCard("이동/일", TroopGuideTenths(troop.EffectiveMovementTenths)));

    basicStats.AddChild(
        CreateStatCard("시야", troop.Vision.ToString()));

    // ==========================================
    // 공격 사거리
    // ==========================================
    var rangeStats = CreateStatSection("공격 사거리");

    rangeStats.AddChild(
        CreateStatCard("부대", TroopGuideTenths(troop.EffectiveRangeUnitTenths)));

    rangeStats.AddChild(
        CreateStatCard("건물", troop.RangeBuilding.ToString()));

    rangeStats.AddChild(
        CreateStatCard("성", troop.RangeCastle.ToString()));

    // ==========================================
    // 병종 특징 (기존 기능 유지)
    // ==========================================
    box.AddChild(GoldRule());

    box.AddChild(MakeLabel("병종 특징", 18, Gold));

    foreach (var line in TroopGuideFeatures(troop, special))
    {
        var feature = MakeLabel($"• {line}", 15, Parchment);

        feature.AutowrapMode =
            TextServer.AutowrapMode.WordSmart;

        box.AddChild(feature);
    }

    // ==========================================
    // 모달 크기 및 배치
    // ==========================================
    scroll.CustomMinimumSize = new Vector2(width, height);

    CenterAndDrag(panel, titleRow, width, height, box);
}
    private static string TroopGuideTenths(int tenths) =>
        (tenths / 10m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> TroopGuideFeatures(
        SanguoSLG.Core.Domain.TroopTemplate troop, SanguoSLG.Core.Domain.SpecialUnit? special)
    {
        var lines = new List<string>();
        if (troop.Code == "mudang")
            lines.Add("일반 육상 부대가 지나갈 수 없는 산 지물을 통과할 수 있습니다. 절벽과 설산은 통과할 수 없습니다.");
        if (troop.Code == "deunggap")
            lines.Add("화공 피해를 60% 더 받습니다.");
        if (troop.Class == SanguoSLG.Core.Domain.TroopClass.Archer)
        {
            lines.Add("출전한 궁병의 명령 팔레트에서 일반화살과 불화살을 선택할 수 있습니다. 기본은 일반화살입니다.");
            lines.Add("일반화살은 화살 종류에 따른 추가 피해 보정 없이 공격합니다.");
            lines.Add("불화살은 숲·산악의 적에게 피해 +10%, 등갑병에게 추가 +60%입니다.");
            lines.Add("그 외 지형에서는 피해 -10%, 기병에게는 추가 -10%입니다. 평지의 기병에게는 총 -20%입니다.");
            lines.Add("비가 오는 지역에서 불화살 피해는 90% 감소합니다.");
        }
        if (troop.Class == SanguoSLG.Core.Domain.TroopClass.Naval)
            lines.Add("해상 경로를 이동하며 육상 지형은 통과할 수 없습니다.");
        if (troop.Class == SanguoSLG.Core.Domain.TroopClass.Siege)
            lines.Add("공성 병종입니다. 건물과 성 공격력을 별도로 사용합니다.");
        if (troop.Class == SanguoSLG.Core.Domain.TroopClass.Supply)
            lines.Add("아군에게 병력·군량·금을 보충하고 보급 범위의 군량 소모를 줄입니다.");
        if (special is not null)
        {
            foreach (var bonus in special.AtkBonusVsClass)
                lines.Add($"{ClassName(bonus.Key)} 공격 시 공격력 +{bonus.Value}%");
            foreach (var bonus in special.AttackerBonusFromClass)
                lines.Add($"{ClassName(bonus.Key)}에게 받는 피해 +{bonus.Value}%");
            if (special.AtkBonusAll > 0) lines.Add($"모든 공격력 +{special.AtkBonusAll}%");
            if (special.AtkBonusBuilding > 0) lines.Add($"건물 공격력 +{special.AtkBonusBuilding}%");
            if (special.AtkBonusImpassable > 0) lines.Add($"이동 불가 지형의 적 공격력 +{special.AtkBonusImpassable}%");
            if (special.DfOverride is int defense) lines.Add($"방어 판정값 {defense} 적용");
            if (special.BuildingAtkOverride is int attack) lines.Add($"건물 공격 판정값 {attack} 적용");
        }
        if (lines.Count == 0) lines.Add("기본 병종입니다. 위에 표시된 이동·사거리·공격·방어 수치를 적용합니다.");
        return lines;
    }

    private void RunTroopGuideQa()
    {
        OpenSystemPalette();
        var trigger = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "부대설명");
        trigger?.EmitSignal(Button.SignalName.Pressed);
        var labels = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToList() ?? [];
        var table = _modalLayer?.FindChild("TroopGuideTable", true, false) as Tree;
        var items = table?.GetRoot()?.GetChildren().ToList() ?? [];
        var validItems = items.Count == _troops.Count + 2 && items.All(item => item.GetIcon(0) is not null)
            && items.All(item => !string.IsNullOrWhiteSpace(item.GetMetadata(0).AsString()))
            && items.Any(item => item.GetText(0) == "극병" && item.GetText(1) == "보병")
            && items.Any(item => item.GetText(0).Contains("투석기"))
            && items.Any(item => item.GetText(0).Contains("공성탑"))
            && items.Any(item => item.GetText(0) == "극병"
                && item.GetText(5) == "2.2" && item.GetText(6) == "1.2")
            && items.Any(item => item.GetText(0) == "궁기병"
                && item.GetText(5) == "2.8" && item.GetText(6) == "1.5")
            && items.Any(item => item.GetText(0) == "철기병" && item.GetText(5) == "2.7")
            && items.Any(item => item.GetText(0) == "화랑궁병" && item.GetText(6) == "2.2")
            && items.Any(item => item.GetText(0) == "투석기" && item.GetText(2).StartsWith("10 →"))
            && items.Any(item => item.GetText(0) == "충차" && item.GetText(4).StartsWith("14 →"));
        validItems &= items.Any(item => item.GetText(0) == "보급부대")
            && items.Any(item => item.GetText(0) == "수송부대");
        table?.EmitSignal(Tree.SignalName.ColumnTitleClicked, 1, 1);
        var ascendingFirst = table?.GetRoot()?.GetFirstChild()?.GetText(0);
        table?.EmitSignal(Tree.SignalName.ColumnTitleClicked, 1, 1);
        var descendingFirst = table?.GetRoot()?.GetFirstChild()?.GetText(0);
        var passed = trigger is not null
            && _modalLayer?.FindChild("SystemViewBack", true, false) is Button
            && table?.Columns == 9 && table.ColumnTitlesVisible
            && table.GetColumnTitle(1) == "병종"
            && table.GetColumnTitle(6) == "부대 사거리"
            && table.GetColumnTitle(7) == "건물 사거리"
            && table.GetColumnTitle(8) == "성 사거리"
            && ascendingFirst != descendingFirst && labels.Any(text => text.Contains("+13"))
            && validItems;
        var geukFeatures = TroopGuideFeatures(_troops.First(t => t.Code == "geukbyeong"),
            new SanguoSLG.Core.Data.SpecialUnitLoader().LoadFromDirectory(_dataDirectory)
                .First(s => s.Code == "geukbyeong"));
        var mudangFeatures = TroopGuideFeatures(_troops.First(t => t.Code == "mudang"),
            new SanguoSLG.Core.Data.SpecialUnitLoader().LoadFromDirectory(_dataDirectory)
                .First(s => s.Code == "mudang"));
        passed &= geukFeatures.Any(line => line.Contains("기병") && line.Contains("+15%"))
            && mudangFeatures.Any(line => line.Contains("산 지물"));
        var archerFeatures = TroopGuideFeatures(_troops.First(t => t.Code == "archer"), null);
        passed &= archerFeatures.Any(line => line.Contains("일반화살") && line.Contains("불화살"))
            && archerFeatures.Any(line => line.Contains("등갑병") && line.Contains("+60%"))
            && archerFeatures.Any(line => line.Contains("평지의 기병") && line.Contains("-20%"))
            && archerFeatures.Any(line => line.Contains("비가 오는 지역") && line.Contains("90%"));
        OpenTroopGuideDetail("geukbyeong");
        passed &= _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text.Contains("기병 공격 시 공격력 +15%")) == true;
        var detailArt = _modalLayer?.FindChildren("*", "TextureRect", true, false)
            .OfType<TextureRect>().FirstOrDefault(image => image.Texture is not null);
        passed &= detailArt?.CustomMinimumSize.X == 188;
        passed &= _modalLayer?.FindChildren("*", "ScrollContainer", true, false)
            .OfType<ScrollContainer>().Any(container => container.CustomMinimumSize.Y >= 390) == true;
        passed &= _modalLayer?.FindChild("TroopGuideDetailModal", false, false) is PanelContainer;
        passed &= (_modalLayer?.FindChild("SystemViewBack", true, false) as Button)?.Text == "◀";
        OpenSupportTroopGuideDetail("supply");
        passed &= _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text.Contains("군량 소비를 줄입니다")) == true;
        OpenSupportTroopGuideDetail("transport");
        passed &= _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text.Contains("공격과 반격을 하지 않으며")) == true;
        GD.Print($"[troop-guide-qa] passed={passed} entries={_troops.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void OpenSkillGuide() => OpenSkillGuideWorkspace();

    private void RunSkillGuideQa() => RunSkillGuideWorkspaceQa();
}
