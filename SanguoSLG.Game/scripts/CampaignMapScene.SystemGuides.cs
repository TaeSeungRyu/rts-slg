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
        var heading = new HBoxContainer { Name = "TroopGuideHeader" };
        heading.AddThemeConstantOverride("separation", 10);
        var rows = new VBoxContainer { Name = "TroopGuideRows" };
        string sort = "class";
        bool descending = false;
        foreach (var (key, caption, size) in new (string, string, float)[] { ("class", "병종 카드", 250), ("unit", "부대 공격", 105), ("building", "건물 공격", 105), ("defense", "방어", 80), ("move", "이동/일", 90), ("range", "사거리 (부대/건물/성)", 225) })
        {
            var cell = MakeButton(caption);
            cell.Name = $"TroopGuideSort_{key}";
            cell.CustomMinimumSize = new Vector2(size, 30);
            cell.Pressed += () => { descending = sort == key && !descending; sort = key; FillRows(); };
            heading.AddChild(cell);
        }
        box.AddChild(heading);
        box.AddChild(rows);
        void FillRows()
        {
            foreach (var child in rows.GetChildren()) { rows.RemoveChild(child); child.QueueFree(); }
            var ordered = sort switch
            {
                "unit" => _troops.OrderBy(t => t.AtkUnit).ThenBy(t => t.Name, StringComparer.Ordinal),
                "building" => _troops.OrderBy(t => t.AtkBuilding).ThenBy(t => t.Name, StringComparer.Ordinal),
                "defense" => _troops.OrderBy(t => t.Df).ThenBy(t => t.Name, StringComparer.Ordinal),
                "move" => _troops.OrderBy(t => t.MovementPerDay).ThenBy(t => t.Name, StringComparer.Ordinal),
                "range" => _troops.OrderBy(t => t.RangeUnit).ThenBy(t => t.RangeBuilding).ThenBy(t => t.RangeCastle),
                _ => _troops.OrderBy(t => t.Class).ThenBy(t => t.Name, StringComparer.Ordinal),
            };
            foreach (var troop in descending ? ordered.Reverse() : ordered)
            {
            var row = new PanelContainer();
            row.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 6, 7));
            var cells = new HBoxContainer();
            cells.AddThemeConstantOverride("separation", 10);
            row.AddChild(cells);
            var identity = new HBoxContainer { CustomMinimumSize = new Vector2(250, 68) };
            identity.AddThemeConstantOverride("separation", 8);
            var artwork = DeployUnitArtwork(UnitCard(troop.Code, troop.Class), 58);
            artwork.Name = "TroopGuideCard";
            identity.AddChild(artwork);
            var identityText = new VBoxContainer();
            identityText.AddChild(MakeLabel(troop.Name, 15, GoldBright));
            identityText.AddChild(MakeLabel(ClassName(troop.Class), 12, Parchment));
            identity.AddChild(identityText);
            cells.AddChild(identity);
            foreach (var (value, size) in new (string, float)[] {
                ($"{troop.AtkUnit} → {troop.AtkUnit + maxResearchBonus}", 105),
                ($"{troop.AtkBuilding} → {troop.AtkBuilding + maxResearchBonus}", 105),
                ($"{troop.Df} → {troop.Df + maxResearchBonus}", 80),
                (troop.MovementPerDay.ToString(), 90),
                ($"{troop.RangeUnit} / {troop.RangeBuilding} / {troop.RangeCastle}", 225) })
            {
                var stat = MakeLabel(value, 14, Parchment);
                stat.CustomMinimumSize = new Vector2(size, 68);
                stat.VerticalAlignment = VerticalAlignment.Center;
                cells.AddChild(stat);
            }
            rows.AddChild(row);
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
        var cards = _modalLayer?.FindChildren("TroopGuideCard", "PanelContainer", true, false).OfType<PanelContainer>().ToList() ?? [];
        var rows = _modalLayer?.FindChild("TroopGuideRows", true, false) as VBoxContainer;
        var sortButton = _modalLayer?.FindChild("TroopGuideSort_unit", true, false) as Button;
        sortButton?.EmitSignal(Button.SignalName.Pressed);
        var ascendingFirst = rows?.GetChild<PanelContainer>(0).FindChildren("*", "Label", true, false)
            .OfType<Label>().FirstOrDefault()?.Text;
        sortButton?.EmitSignal(Button.SignalName.Pressed);
        var descendingFirst = rows?.GetChild<PanelContainer>(0).FindChildren("*", "Label", true, false)
            .OfType<Label>().FirstOrDefault()?.Text;
        var passed = trigger is not null && labels.Any(text => text.Contains("투석기"))
            && labels.Any(text => text.Contains("공성탑"))
            && _modalLayer?.FindChild("SystemViewBack", true, false) is Button
            && _modalLayer?.FindChild("TroopGuideHeader", true, false) is HBoxContainer
            && ascendingFirst != descendingFirst && labels.Any(text => text.Contains("+13"))
            && cards.Count == _troops.Count && cards.All(card =>
                card.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(image => image.Texture is not null));
        GD.Print($"[troop-guide-qa] passed={passed} entries={_troops.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void OpenSkillGuide()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Clamp(viewportSize.X * 0.92f, 920f, 1220f);
        var height = Mathf.Clamp(viewportSize.Y * 0.86f, 610f, 760f);
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
        list.ColumnTitlesVisible = true;
        list.SetColumnTitle(0, "이름");
        list.SetColumnTitle(1, "종류");
        list.SetColumnTitle(2, "형태");
        list.SetColumnCustomMinimumWidth(0, 180);
        list.SetColumnCustomMinimumWidth(1, 78);
        list.SetColumnCustomMinimumWidth(2, 68);
        list.AddThemeFontOverride("font", _font);
        columns.AddChild(list);
        var detailScroll = new ScrollContainer { Name = "SkillGuideDetailScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        columns.AddChild(detailScroll);
        var detailBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        detailBox.AddThemeConstantOverride("separation", 14);
        detailScroll.AddChild(detailBox);
        var card = new PanelContainer { Name = "SkillGuideCard", CustomMinimumSize = new Vector2(0, 112) };
        card.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 9, 12));
        detailBox.AddChild(card);
        var cardRow = new HBoxContainer();
        cardRow.AddThemeConstantOverride("separation", 14);
        card.AddChild(cardRow);
        var portrait = ActiveSkillIcons.Preview(null, 82);
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
        detailBox.AddChild(preview);
        var replay = MakeButton("6일 발동 다시 보기");
        replay.Name = "SkillGuideReplay";
        detailBox.AddChild(replay);
        replay.Pressed += () => preview.ShowSkill(_activeSkills.FirstOrDefault(skill => skill.Code == preview.SkillCode));
        var detail = MakeLabel("", 15, Parchment);
        detail.Name = "SkillGuideDetail";
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
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
        var hasSkillColumns = list?.Columns == 3 && list.GetColumnTitle(0) == "이름"
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
            && preview?.SkillCode == "peerless"
            && preview.CameraSize == 4.4f
            && preview.FindChild("PreviewPlain", true, false) is MeshInstance3D;
        GetTree().CreateTimer(2.7).Timeout += () =>
        {
            passed &= preview?.Day == 6;
            GD.Print($"[skill-guide-qa] passed={passed} entries={initialCount} missingIcons={missingIcons} previewDay={preview?.Day}");
            GetTree().Quit(passed ? 0 : 1);
        };
    }
}
