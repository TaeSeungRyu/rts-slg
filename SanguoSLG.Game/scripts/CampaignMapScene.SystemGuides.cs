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
        var maxResearchBonus = SanguoSLG.Core.Simulation.ResearchCurve.Bonus(10);
        box.AddChild(MakeLabel($"기본 → 해당 병종 연구 Lv.10 최대치 (공격·방어 각각 +{maxResearchBonus}). 이동·사거리는 연구로 변하지 않습니다. 헤더를 눌러 정렬하세요.", 13, Parchment));
        var table = new Tree
        {
            Name = "TroopGuideTable", Columns = 7, ColumnTitlesVisible = true,
            HideRoot = true, SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, height - 110),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        table.AddThemeFontOverride("font", _font);
        table.AddThemeFontSizeOverride("font_size", 14);
        table.AddThemeFontOverride("title_button_font", _font);
        table.AddThemeFontSizeOverride("title_button_font_size", 13);
        var captions = new[] { "병종 카드", "병종", "부대 공격", "건물 공격", "방어", "이동/일", "사거리 (부대/건물/성)" };
        var widths = new[] { 170, 70, 105, 105, 80, 90, 195 };
        for (var col = 0; col < captions.Length; col++)
        {
            table.SetColumnTitle(col, captions[col]);
            table.SetColumnCustomMinimumWidth(col, widths[col]);
            table.SetColumnExpand(col, col == 0 || col == 6);
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
                6 => _troops.OrderBy(t => t.RangeUnit).ThenBy(t => t.RangeBuilding).ThenBy(t => t.RangeCastle),
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
                row.SetText(5, troop.MovementPerDay.ToString());
                row.SetText(6, $"{troop.RangeUnit} / {troop.RangeBuilding} / {troop.RangeCastle}");
            }
        }
        FillRows();
        table.ItemSelected += () =>
        {
            var code = table.GetSelected()?.GetMetadata(0).AsString();
            if (!string.IsNullOrWhiteSpace(code)) OpenTroopGuideDetail(code);
        };
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void OpenTroopGuideDetail(string code)
    {
        var troop = _troops.FirstOrDefault(candidate => candidate.Code == code);
        if (troop is null) return;
        var special = new SanguoSLG.Core.Data.SpecialUnitLoader().LoadFromDirectory(_dataDirectory)
            .FirstOrDefault(candidate => candidate.Code == code);
        const float width = 720f;
        const float height = 390f;
        var box = SystemView(troop.Name, width, out var scroll, out var panel, out var titleRow);
        panel.Name = "TroopGuideDetailModal";
        var back = titleRow.GetChild<Button>(0);
        back.ButtonDown -= OpenSystemPalette;
        back.ButtonDown += OpenTroopGuide;
        back.Text = "◀";
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 16);
        box.AddChild(top);
        var artworkFrame = new PanelContainer { CustomMinimumSize = new Vector2(198, 142),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, ClipContents = true };
        artworkFrame.AddThemeStyleboxOverride("panel", Frame(new Color(0.045f, 0.035f, 0.03f),
            new Color(Gold, 0.6f), 1, 7, 5));
        artworkFrame.AddChild(new TextureRect { Texture = UnitCard(troop.Code, troop.Class),
            CustomMinimumSize = new Vector2(188, 132),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        top.AddChild(artworkFrame);
        var summary = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(summary);
        summary.AddChild(MakeLabel($"병종  {ClassName(troop.Class)}", 18, Gold));
        summary.AddChild(MakeLabel($"부대 공격 {troop.AtkUnit}  ·  건물 공격 {troop.AtkBuilding}  ·  방어 {troop.Df}", 14, Parchment));
        summary.AddChild(MakeLabel($"이동 {troop.MovementPerDay}/일  ·  시야 {troop.Vision}", 14, Parchment));
        summary.AddChild(MakeLabel($"사거리  부대 {troop.RangeUnit} / 건물 {troop.RangeBuilding} / 성 {troop.RangeCastle}", 14, Parchment));
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("병종 특징", 18, Gold));
        foreach (var line in TroopGuideFeatures(troop, special))
        {
            var feature = MakeLabel($"• {line}", 15, Parchment);
            feature.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(feature);
        }
        // DeployScaffold의 ScrollContainer는 최소 높이가 0이면 자식 높이를 전달하지 않아
        // 내용이 접힌 얇은 패널만 표시된다.
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private static IReadOnlyList<string> TroopGuideFeatures(
        SanguoSLG.Core.Domain.TroopTemplate troop, SanguoSLG.Core.Domain.SpecialUnit? special)
    {
        var lines = new List<string>();
        if (troop.Code == "mudang")
            lines.Add("일반 육상 부대가 지나갈 수 없는 산 지물을 통과할 수 있습니다. 절벽과 설산은 통과할 수 없습니다.");
        if (troop.Code == "deunggap")
            lines.Add("화공 피해를 60% 더 받습니다.");
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
        var validItems = items.Count == _troops.Count && items.All(item => item.GetIcon(0) is not null)
            && items.All(item => !string.IsNullOrWhiteSpace(item.GetMetadata(0).AsString()))
            && items.Any(item => item.GetText(0) == "극병" && item.GetText(1) == "보병")
            && items.Any(item => item.GetText(0).Contains("투석기"))
            && items.Any(item => item.GetText(0).Contains("공성탑"));
        table?.EmitSignal(Tree.SignalName.ColumnTitleClicked, 1, 1);
        var ascendingFirst = table?.GetRoot()?.GetFirstChild()?.GetText(0);
        table?.EmitSignal(Tree.SignalName.ColumnTitleClicked, 1, 1);
        var descendingFirst = table?.GetRoot()?.GetFirstChild()?.GetText(0);
        var passed = trigger is not null
            && _modalLayer?.FindChild("SystemViewBack", true, false) is Button
            && table?.Columns == 7 && table.ColumnTitlesVisible
            && table.GetColumnTitle(1) == "병종"
            && ascendingFirst != descendingFirst && labels.Any(text => text.Contains("+13"))
            && validItems;
        var geukFeatures = TroopGuideFeatures(_troops.First(t => t.Code == "geukbyeong"),
            new SanguoSLG.Core.Data.SpecialUnitLoader().LoadFromDirectory(_dataDirectory)
                .First(s => s.Code == "geukbyeong"));
        var mudangFeatures = TroopGuideFeatures(_troops.First(t => t.Code == "mudang"),
            new SanguoSLG.Core.Data.SpecialUnitLoader().LoadFromDirectory(_dataDirectory)
                .First(s => s.Code == "mudang"));
        passed &= geukFeatures.Any(line => line.Contains("기병") && line.Contains("+10%"))
            && mudangFeatures.Any(line => line.Contains("산 지물"));
        OpenTroopGuideDetail("geukbyeong");
        passed &= _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text.Contains("기병 공격 시 공격력 +10%")) == true;
        var detailArt = _modalLayer?.FindChildren("*", "TextureRect", true, false)
            .OfType<TextureRect>().FirstOrDefault(image => image.Texture is not null);
        passed &= detailArt?.CustomMinimumSize.X == 188;
        passed &= _modalLayer?.FindChildren("*", "ScrollContainer", true, false)
            .OfType<ScrollContainer>().Any(container => container.CustomMinimumSize.Y >= 390) == true;
        passed &= _modalLayer?.FindChild("TroopGuideDetailModal", false, false) is PanelContainer;
        passed &= (_modalLayer?.FindChild("SystemViewBack", true, false) as Button)?.Text == "◀";
        GD.Print($"[troop-guide-qa] passed={passed} entries={_troops.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void OpenSkillGuide()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Clamp(viewportSize.X * 0.92f, 920f, 1220f);
        var height = Mathf.Clamp(viewportSize.Y * 0.90f, 640f, 820f);
        var box = SystemView("스킬설명", width, out var scroll, out var panel, out var titleRow);
        var entries = _activeSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .Select(skill => (skill.Name, SkillDescriptions.Active(skill), skill.Code, "액티브",
                skill.Type switch { SanguoSLG.Core.Simulation.ActiveType.Defense => "방어",
                    SanguoSLG.Core.Simulation.ActiveType.Heal => "회복",
                    SanguoSLG.Core.Simulation.ActiveType.Tactic => "계략", _ => "공격" }))
            .Concat(_passiveSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => ($"전투 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Passive(skill, level))), skill.Code, "패시브",
                    skill.Effects.Any(effect => effect.Bucket == SanguoSLG.Core.Simulation.SkillBucket.Attack) ? "공격" : "방어")))
            .Concat(_adminSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => ($"내정 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Admin(skill, level))), skill.Code, "패시브", "내정")))
            .ToList();
        var search = new LineEdit { Name = "SkillGuideSearch", PlaceholderText = "스킬 이름 검색",
            MaxLength = 30 };
        box.AddChild(search);
        var columns = new HBoxContainer { CustomMinimumSize = new Vector2(0, height - 150),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        box.AddChild(columns);
        var list = new Tree { Name = "SkillGuideList", CustomMinimumSize = new Vector2(245, height - 150),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        list.Columns = 3;
        list.HideRoot = true;
        list.SelectMode = Tree.SelectModeEnum.Row;
        list.ColumnTitlesVisible = true;
        list.SetColumnTitle(0, "이름");
        list.SetColumnTitle(1, "종류");
        list.SetColumnTitle(2, "형태");
        list.SetColumnCustomMinimumWidth(0, 127);
        list.SetColumnCustomMinimumWidth(1, 57);
        list.SetColumnCustomMinimumWidth(2, 53);
        list.AddThemeFontOverride("font", _font);
        columns.AddChild(list);
        var detailBox = new VBoxContainer { Name = "SkillGuideDetailBox",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        detailBox.AddThemeConstantOverride("separation", 8);
        columns.AddChild(detailBox);
        var card = new PanelContainer { Name = "SkillGuideCard", CustomMinimumSize = new Vector2(0, 88) };
        card.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 9, 12));
        detailBox.AddChild(card);
        var cardRow = new HBoxContainer();
        cardRow.AddThemeConstantOverride("separation", 14);
        card.AddChild(cardRow);
        var portrait = ActiveSkillIcons.Preview(null, 68);
        portrait.Name = "SkillGuideCardImage";
        cardRow.AddChild(portrait);
        var titleBox = new VBoxContainer();
        titleBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cardRow.AddChild(titleBox);
        var category = MakeLabel("", 12, Gold);
        category.Name = "SkillGuideCategory";
        titleBox.AddChild(category);
        var skillName = MakeLabel("", 20, GoldBright);
        skillName.Name = "SkillGuideName";
        titleBox.AddChild(skillName);
        var preview = new SkillGuideEffectPreview3D();
        preview.CustomMinimumSize = new Vector2(390, 205);
        preview.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        detailBox.AddChild(preview);
        var replay = MakeButton("6일 발동 다시 보기");
        replay.Name = "SkillGuideReplay";
        detailBox.AddChild(replay);
        replay.Pressed += () => preview.ShowSkill(_activeSkills.FirstOrDefault(skill => skill.Code == preview.SkillCode));
        var detail = MakeLabel("", 15, Parchment);
        detail.Name = "SkillGuideDetail";
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        detail.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        detailBox.AddChild(detail);
        var shown = entries;
        Texture2D? SkillImage(string code, string kind, string form)
        {
            if (kind == "액티브") return ActiveSkillIcons.Load(code);
            var folder = form == "내정" ? "admin" : "passive";
            var path = $"res://assets/icons/skills/{folder}/{code}.png";
            return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        }
        void Select(int index)
        {
            if (index < 0 || index >= shown.Count)
            {
                portrait.Texture = null;
                category.Text = "";
                skillName.Text = "";
                detail.Text = "일치하는 스킬이 없습니다.";
                preview.ShowSkill(null);
                replay.Visible = false;
                return;
            }
            var entry = shown[index];
            portrait.Texture = SkillImage(entry.Item3, entry.Item4, entry.Item5);
            category.Text = $"{entry.Item4} · {entry.Item5}";
            skillName.Text = entry.Item1;
            detail.Text = entry.Item2;
            detail.AddThemeFontSizeOverride("font_size", entry.Item2.Length > 260 ? 11 : entry.Item2.Length > 170 ? 12 : 14);
            preview.ShowSkill(entry.Item4 == "액티브"
                ? _activeSkills.FirstOrDefault(skill => skill.Code == entry.Item3) : null);
            replay.Visible = entry.Item4 == "액티브";
        }
        void Refill(string query)
        {
            shown = entries.Where(entry => entry.Item1.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            list.Clear();
            var root = list.CreateItem();
            for (var i = 0; i < shown.Count; i++)
            {
                var entry = shown[i];
                var item = list.CreateItem(root);
                item.SetText(0, entry.Item1);
                item.SetIcon(0, SkillImage(entry.Item3, entry.Item4, entry.Item5));
                item.SetIconMaxWidth(0, 42);
                item.SetText(1, entry.Item4);
                item.SetText(2, entry.Item5);
                item.SetMetadata(0, i);
            }
            Select(shown.Count == 0 ? -1 : 0);
            if (shown.Count > 0) list.SetSelected(root.GetFirstChild(), 0);
        }
        list.ItemSelected += () => Select(list.GetSelected()?.GetMetadata(0).AsInt32() ?? -1);
        search.TextChanged += Refill;
        Refill("");
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void RunSkillGuideQa()
    {
        OpenSystemPalette();
        var trigger = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "스킬설명");
        trigger?.EmitSignal(Button.SignalName.Pressed);
        var list = _modalLayer?.FindChild("SkillGuideList", true, false) as Tree;
        var search = _modalLayer?.FindChild("SkillGuideSearch", true, false) as LineEdit;
        var detail = _modalLayer?.FindChild("SkillGuideDetail", true, false) as Label;
        var initialCount = list?.GetRoot()?.GetChildren().Count() ?? 0;
        var missingIcons = list?.GetRoot()?.GetChildren().Count(item => item.GetIcon(0) is null) ?? initialCount;
        var hasSkillColumns = list?.Columns == 3 && list.SelectMode == Tree.SelectModeEnum.Row
            && list.GetColumnTitle(0) == "이름"
            && list.GetColumnTitle(1) == "종류" && list.GetColumnTitle(2) == "형태"
            && list.CustomMinimumSize.X == 245;
        search?.EmitSignal(LineEdit.SignalName.TextChanged, "무쌍");
        var image = _modalLayer?.FindChild("SkillGuideCardImage", true, false) as TextureRect;
        var preview = _modalLayer?.FindChild("SkillGuideEffectPreview", true, false) as SkillGuideEffectPreview3D;
        var passed = trigger is not null && hasSkillColumns
            && initialCount == _activeSkills.Count + _passiveSkills.Count + _adminSkills.Count
            && list?.GetRoot()?.GetChildren().Count() == 1
            && detail?.Text.Contains("일반 공격 피해", StringComparison.Ordinal) == true
            && image?.Texture is not null && missingIcons == 0
            && _modalLayer?.FindChild("SkillGuideCard", true, false) is PanelContainer
            && _modalLayer?.FindChild("SkillGuideDetailScroll", true, false) is null
            && preview?.SkillCode == "peerless"
            && preview.CustomMinimumSize.X == 390
            && preview.CameraSize == 2.9f
            && preview.FindChild("PreviewPlainTiles", true, false) is Node3D tiles
            && tiles.GetChildCount() == 7
            && preview.TileCenterDistance > 0f && preview.HasUnitModels
            && preview.OpponentDistance < preview.TileCenterDistance * 1.3f
            && tiles.FindChild("PreviewForestTile", false, false) is Node3D
            && tiles.FindChild("PreviewMountainTile", false, false) is Node3D
            && preview.GreenTerrainApplied
            && tiles.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Any(mesh => mesh.MaterialOverride is StandardMaterial3D material
                    && material.AlbedoColor.G > material.AlbedoColor.B);
        GetTree().CreateTimer(4.6).Timeout += () =>
        {
            passed &= preview?.Day == 6 && preview.EffectAttached && preview.HasUnitModels
                && preview.CombatMotionCount >= 2;
            GD.Print($"[skill-guide-qa] passed={passed} entries={initialCount} missingIcons={missingIcons} previewDay={preview?.Day}");
            GetTree().Quit(passed ? 0 : 1);
        };
    }
}
