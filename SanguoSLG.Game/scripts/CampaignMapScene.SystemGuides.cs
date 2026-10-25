using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenTroopGuide()
    {
        const float width = 1040f;
        const float height = 650f;
        var box = SystemView("부대설명", width, out var scroll, out var panel, out var titleRow);
        var maxResearchBonus = SanguoSLG.Core.Simulation.ResearchCurve.Bonus(10);
        box.AddChild(MakeLabel($"기본 → 해당 병종 연구 Lv.10 최대치 (공격·방어 각각 +{maxResearchBonus}). 이동·사거리는 연구로 변하지 않습니다. 헤더를 눌러 정렬하세요.", 13, Parchment));
        var table = new Tree
        {
            Name = "TroopGuideTable", Columns = 6, ColumnTitlesVisible = true,
            HideRoot = true, SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, height - 110),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        table.AddThemeFontOverride("font", _font);
        table.AddThemeFontSizeOverride("font_size", 14);
        table.AddThemeFontOverride("title_button_font", _font);
        table.AddThemeFontSizeOverride("title_button_font_size", 13);
        var captions = new[] { "병종 카드", "부대 공격", "건물 공격", "방어", "이동/일", "사거리 (부대/건물/성)" };
        var widths = new[] { 215, 105, 105, 80, 90, 195 };
        for (var col = 0; col < captions.Length; col++)
        {
            table.SetColumnTitle(col, captions[col]);
            table.SetColumnCustomMinimumWidth(col, widths[col]);
            table.SetColumnExpand(col, col == 0 || col == 5);
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
                1 => _troops.OrderBy(t => t.AtkUnit).ThenBy(t => t.Name, StringComparer.Ordinal),
                2 => _troops.OrderBy(t => t.AtkBuilding).ThenBy(t => t.Name, StringComparer.Ordinal),
                3 => _troops.OrderBy(t => t.Df).ThenBy(t => t.Name, StringComparer.Ordinal),
                4 => _troops.OrderBy(t => t.MovementPerDay).ThenBy(t => t.Name, StringComparer.Ordinal),
                5 => _troops.OrderBy(t => t.RangeUnit).ThenBy(t => t.RangeBuilding).ThenBy(t => t.RangeCastle),
                _ => _troops.OrderBy(t => t.Class).ThenBy(t => t.Name, StringComparer.Ordinal),
            };
            foreach (var troop in descending ? ordered.Reverse() : ordered)
            {
                var row = table.CreateItem(root);
                row.SetText(0, $"{troop.Name} · {ClassName(troop.Class)}");
                row.SetIcon(0, RoundedOptionTexture(UnitCard(troop.Code, troop.Class), 36));
                row.SetIconMaxWidth(0, 36);
                row.SetText(1, $"{troop.AtkUnit} → {troop.AtkUnit + maxResearchBonus}");
                row.SetText(2, $"{troop.AtkBuilding} → {troop.AtkBuilding + maxResearchBonus}");
                row.SetText(3, $"{troop.Df} → {troop.Df + maxResearchBonus}");
                row.SetText(4, troop.MovementPerDay.ToString());
                row.SetText(5, $"{troop.RangeUnit} / {troop.RangeBuilding} / {troop.RangeCastle}");
            }
        }
        FillRows();
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
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
            && items.Any(item => item.GetText(0).Contains("투석기"))
            && items.Any(item => item.GetText(0).Contains("공성탑"));
        table?.EmitSignal(Tree.SignalName.ColumnTitleClicked, 1, 1);
        var ascendingFirst = table?.GetRoot()?.GetFirstChild()?.GetText(0);
        table?.EmitSignal(Tree.SignalName.ColumnTitleClicked, 1, 1);
        var descendingFirst = table?.GetRoot()?.GetFirstChild()?.GetText(0);
        var passed = trigger is not null
            && _modalLayer?.FindChild("SystemViewBack", true, false) is Button
            && table?.Columns == 6 && table.ColumnTitlesVisible
            && ascendingFirst != descendingFirst && labels.Any(text => text.Contains("+13"))
            && validItems;
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
        var list = new Tree { Name = "SkillGuideList", CustomMinimumSize = new Vector2(375, height - 150),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        list.Columns = 3;
        list.HideRoot = true;
        list.SelectMode = Tree.SelectModeEnum.Row;
        list.ColumnTitlesVisible = true;
        list.SetColumnTitle(0, "이름");
        list.SetColumnTitle(1, "종류");
        list.SetColumnTitle(2, "형태");
        list.SetColumnCustomMinimumWidth(0, 180);
        list.SetColumnCustomMinimumWidth(1, 78);
        list.SetColumnCustomMinimumWidth(2, 68);
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
        preview.CustomMinimumSize = new Vector2(420, 225);
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
            && list.GetColumnTitle(1) == "종류" && list.GetColumnTitle(2) == "형태";
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
            && preview.CameraSize == 4.4f
            && preview.FindChild("PreviewPlainTiles", true, false) is Node3D tiles
            && tiles.GetChildCount() == 7;
        GetTree().CreateTimer(2.7).Timeout += () =>
        {
            passed &= preview?.Day == 6 && preview.EffectAttached && preview.HasUnitModels;
            GD.Print($"[skill-guide-qa] passed={passed} entries={initialCount} missingIcons={missingIcons} previewDay={preview?.Day}");
            GetTree().Quit(passed ? 0 : 1);
        };
    }
}
