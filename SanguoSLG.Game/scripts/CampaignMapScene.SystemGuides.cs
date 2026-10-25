using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenTroopGuide()
    {
        const float width = 1040f;
        const float height = 650f;
        var box = SystemView("부대설명", width, out var scroll, out var panel, out var titleRow);
        box.AddChild(MakeLabel("병종별 기본 능력 · 실제 전투력은 병력 수, 장수 적성, 연구와 지형에 따라 달라집니다.", 13, Parchment));
        var heading = new HBoxContainer { Name = "TroopGuideHeader" };
        heading.AddThemeConstantOverride("separation", 10);
        foreach (var (caption, size) in new (string, float)[] { ("병종 카드", 250), ("부대 공격", 105), ("건물 공격", 105), ("방어", 80), ("이동/일", 90), ("사거리 (부대/건물/성)", 225) })
        {
            var cell = MakeLabel(caption, 13, GoldBright);
            cell.CustomMinimumSize = new Vector2(size, 30);
            heading.AddChild(cell);
        }
        box.AddChild(heading);
        foreach (var troop in _troops.OrderBy(t => t.Class).ThenBy(t => t.Name, StringComparer.Ordinal))
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
                (troop.AtkUnit.ToString(), 105), (troop.AtkBuilding.ToString(), 105),
                (troop.Df.ToString(), 80), (troop.MovementPerDay.ToString(), 90),
                ($"{troop.RangeUnit} / {troop.RangeBuilding} / {troop.RangeCastle}", 225) })
            {
                var stat = MakeLabel(value, 14, Parchment);
                stat.CustomMinimumSize = new Vector2(size, 68);
                stat.VerticalAlignment = VerticalAlignment.Center;
                cells.AddChild(stat);
            }
            box.AddChild(row);
        }
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
        var passed = trigger is not null && labels.Any(text => text.Contains("투석기"))
            && labels.Any(text => text.Contains("공성탑"))
            && _modalLayer?.FindChild("SystemViewBack", true, false) is Button
            && _modalLayer?.FindChild("TroopGuideHeader", true, false) is HBoxContainer
            && cards.Count == _troops.Count && cards.All(card =>
                card.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(image => image.Texture is not null));
        GD.Print($"[troop-guide-qa] passed={passed} entries={_troops.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void OpenSkillGuide()
    {
        const float width = 900f;
        const float height = 620f;
        var box = SystemView("스킬설명", width, out var scroll, out var panel, out var titleRow);
        var entries = _activeSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .Select(skill => ($"액티브 · {skill.Name}", SkillDescriptions.Active(skill), skill.Code, "액티브"))
            .Concat(_passiveSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => ($"전투 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Passive(skill, level))), skill.Code, "전투 패시브")))
            .Concat(_adminSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => ($"내정 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Admin(skill, level))), skill.Code, "내정 패시브")))
            .ToList();
        var search = new LineEdit { Name = "SkillGuideSearch", PlaceholderText = "스킬 이름 검색",
            MaxLength = 30 };
        box.AddChild(search);
        var columns = new HBoxContainer { CustomMinimumSize = new Vector2(0, 480),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        box.AddChild(columns);
        var list = new ItemList { Name = "SkillGuideList", CustomMinimumSize = new Vector2(300, 480),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
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
        var detail = MakeLabel("", 15, Parchment);
        detail.Name = "SkillGuideDetail";
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        detailBox.AddChild(detail);
        var shown = entries;
        Texture2D? SkillImage(string code, string kind)
        {
            if (kind == "액티브") return ActiveSkillIcons.Load(code);
            var folder = kind == "전투 패시브" ? "passive" : "admin";
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
                return;
            }
            var entry = shown[index];
            portrait.Texture = SkillImage(entry.Item3, entry.Item4);
            category.Text = entry.Item4;
            skillName.Text = entry.Item1[(entry.Item1.IndexOf('·') + 1)..].Trim();
            detail.Text = entry.Item2;
        }
        void Refill(string query)
        {
            shown = entries.Where(entry => entry.Item1.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            list.Clear();
            foreach (var entry in shown) list.AddItem(entry.Item1, SkillImage(entry.Item3, entry.Item4));
            Select(shown.Count == 0 ? -1 : 0);
            if (shown.Count > 0) list.Select(0);
        }
        list.ItemSelected += index => Select((int)index);
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
        var list = _modalLayer?.FindChild("SkillGuideList", true, false) as ItemList;
        var search = _modalLayer?.FindChild("SkillGuideSearch", true, false) as LineEdit;
        var detail = _modalLayer?.FindChild("SkillGuideDetail", true, false) as Label;
        var initialCount = list?.ItemCount ?? 0;
        var missingIcons = list is null ? initialCount : Enumerable.Range(0, initialCount)
            .Count(index => list.GetItemIcon(index) is null);
        search?.EmitSignal(LineEdit.SignalName.TextChanged, "무쌍");
        var image = _modalLayer?.FindChild("SkillGuideCardImage", true, false) as TextureRect;
        var passed = trigger is not null && initialCount == _activeSkills.Count + _passiveSkills.Count + _adminSkills.Count
            && list?.ItemCount == 1 && detail?.Text.Contains("일반 공격 피해", StringComparison.Ordinal) == true
            && image?.Texture is not null && missingIcons == 0
            && _modalLayer?.FindChild("SkillGuideCard", true, false) is PanelContainer;
        GD.Print($"[skill-guide-qa] passed={passed} entries={initialCount} missingIcons={missingIcons}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
