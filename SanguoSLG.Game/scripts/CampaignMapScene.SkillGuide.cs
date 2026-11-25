using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public partial class CampaignMapScene
{
    private const float SkillGuideLeftRatio = 0.4f;
    private const float SkillGuideRightRatio = 0.6f;
    private const float SkillGuideColumnGap = 12f;
    private const float SkillGuidePreviewWidth = 420f;
    private const float SkillGuidePreviewHeight = 240f;
    private const int SkillGuideCardHeight = 66;
    private const int SkillGuideBodyFontSize = 14;

    private sealed record SkillGuideEntry(string Name, string Description, string Code,
        string Kind, string Form, ActiveSkill? Active);

    private void OpenSkillGuideWorkspace()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Clamp(viewportSize.X * 0.92f, 920f, 1320f);
        var height = Mathf.Clamp(viewportSize.Y * 0.9f, 640f, 880f);
        var box = SystemView("스킬설명", width, out var outerScroll, out var panel,
            out var titleRow, useModalChrome: true);
        panel.CustomMinimumSize = new Vector2(width, height);
        outerScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;

        var entries = _activeSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .Select(skill => new SkillGuideEntry(skill.Name, SkillDescriptions.Active(skill), skill.Code,
                "액티브", skill.Type switch
                {
                    ActiveType.Defense => "방어", ActiveType.Heal => "회복",
                    ActiveType.Tactic => "계략", _ => "공격",
                }, skill))
            .Concat(_passiveSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => new SkillGuideEntry($"전투 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Passive(skill, level))),
                    skill.Code, "패시브",
                    skill.Effects.Any(effect => effect.Bucket == SkillBucket.Attack) ? "공격" : "방어", null)))
            .Concat(_adminSkills.OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Select(skill => new SkillGuideEntry($"내정 패시브 · {skill.Name}",
                    string.Join("\n\n", Enumerable.Range(1, 3)
                        .Select(level => SkillDescriptions.Admin(skill, level))),
                    skill.Code, "패시브", "내정", null)))
            .ToList();

        Texture2D? Icon(SkillGuideEntry entry)
        {
            if (entry.Active is not null) return ActiveSkillIcons.Load(entry.Code);
            var folder = entry.Form == "내정" ? "admin" : "passive";
            var path = $"res://assets/icons/skills/{folder}/{entry.Code}.png";
            return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        }

        var columns = new HBoxContainer { Name = "SkillGuideColumns",
            CustomMinimumSize = new Vector2(0, Mathf.Max(390f, height - 200f)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", (int)SkillGuideColumnGap);
        box.AddChild(columns);
        var left = new VBoxContainer { Name = "SkillGuideLeft",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = SkillGuideLeftRatio };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);


var searchMargin = new MarginContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
};

searchMargin.AddThemeConstantOverride("margin_top", 5);
left.AddChild(searchMargin);

var search = new LineEdit
{
    Name = "SkillGuideSearch",
    PlaceholderText = "스킬 이름 검색",
    MaxLength = 30,
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
};

searchMargin.AddChild(search);
NativeTextInputDialog.Attach(search);




        var filters = new HBoxContainer { Name = "SkillGuideFilters" };
        filters.AddThemeConstantOverride("separation", 6);
        left.AddChild(filters);
        var cardScroll = new ScrollContainer { Name = "SkillGuideListScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        left.AddChild(cardScroll);
        var listPadding = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listPadding.AddThemeConstantOverride("margin_right", 8);
        cardScroll.AddChild(listPadding);
        var cards = new VBoxContainer { Name = "SkillGuideCards",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("separation", 6);
        listPadding.AddChild(cards);

        var divider = new ColorRect { Name = "SkillGuideColumnDivider",
            Color = new Color("95632b"), CustomMinimumSize = new Vector2(2, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore };
        columns.AddChild(divider);


var rightMargin = new MarginContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
    SizeFlagsStretchRatio = SkillGuideRightRatio
};

// 우측 영역 상단 여백
rightMargin.AddThemeConstantOverride("margin_top", 5);

columns.AddChild(rightMargin);

var right = new VBoxContainer
{
    Name = "SkillGuideDetailBox",
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    SizeFlagsVertical = Control.SizeFlags.ExpandFill
};

right.AddThemeConstantOverride("separation", 8);
rightMargin.AddChild(right);


        var header = new PanelContainer { Name = "SkillGuideCard",
            CustomMinimumSize = new Vector2(0, 76) };
        header.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 8, 10));
        right.AddChild(header);
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        header.AddChild(headerRow);
        var portrait = ActiveSkillIcons.Preview(null, 58);
        portrait.Name = "SkillGuideCardImage";
        headerRow.AddChild(portrait);
        var headerText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        headerRow.AddChild(headerText);
        var skillName = MakeLabel("", 18, GoldBright);
        skillName.Name = "SkillGuideName";
        headerText.AddChild(skillName);
        var category = MakeLabel("", 12, Gold);
        category.Name = "SkillGuideCategory";
        headerText.AddChild(category);

        // var previewFrame = new PanelContainer { Name = "SkillGuidePreviewFrame",
        //     SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        // previewFrame.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 6, 4));
        // right.AddChild(previewFrame);
        // var preview = new SkillGuideEffectPreview3D { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        //     CustomMinimumSize = new Vector2(SkillGuidePreviewWidth, SkillGuidePreviewHeight) };
        // previewFrame.AddChild(preview);

        var statRow = new HBoxContainer { Name = "SkillGuideStatCards" };
        statRow.AddThemeConstantOverride("separation", 6);
        right.AddChild(statRow);
        var statLabels = new List<Label>();
        foreach (var heading in new[] { "종류", "형태", "주요 수치" })
        {
            var statCard = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 1f, CustomMinimumSize = new Vector2(0, 60) };
            statCard.AddThemeStyleboxOverride("panel", Frame(new Color("211711"), new Color("95632b"), 1, 5, 5));
            statRow.AddChild(statCard);
            var stack = new VBoxContainer();
            statCard.AddChild(stack);
            stack.AddChild(MakeLabel(heading, 11, Gold));
            var value = MakeLabel("", 14, GoldBright);
            value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            stack.AddChild(value);
            statLabels.Add(value);
        }

        var detailScroll = new ScrollContainer { Name = "SkillGuideDetailScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(detailScroll);
        var detailSections = new VBoxContainer { Name = "SkillGuideDetailSections",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        detailSections.AddThemeConstantOverride("separation", 8);
        detailScroll.AddChild(detailSections);
        Label Section(string heading)
        {
            var section = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            section.AddThemeStyleboxOverride("panel", Frame(InkSoft, new Color("593015"), 1, 6, 8));
            detailSections.AddChild(section);
            var stack = new VBoxContainer();
            section.AddChild(stack);
            stack.AddChild(MakeLabel(heading, 13, Gold));
            var body = MakeLabel("", SkillGuideBodyFontSize, Parchment);
            body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            stack.AddChild(body);
            return body;
        }
        var effectText = Section("스킬 효과");
        effectText.Name = "SkillGuideDetail";
        var triggerText = Section("발동 조건");
        var noteText = Section("참고사항");

        SkillGuideEntry? selected = null;
        var filter = "전체";
        var filterButtons = new List<Button>();
        var entryButtons = new List<(SkillGuideEntry Entry, Button Button)>();
        void RefreshButtonStyles()
        {
            foreach (var (entry, button) in entryButtons)
            {
                var active = selected?.Code == entry.Code && selected?.Kind == entry.Kind
                    && selected?.Form == entry.Form;
                button.AddThemeStyleboxOverride("normal", Frame(active ? new Color("5d2119") : InkSoft,
                    active ? GoldBright : new Color("95632b"), active ? 2 : 1, 6, 7));
            }
            foreach (var button in filterButtons)
                button.AddThemeStyleboxOverride("normal", Frame(button.Text == filter
                    ? new Color("6a2719") : InkSoft, Gold, 1, 5, 7));
        }
        void Select(SkillGuideEntry? entry)
        {
            selected = entry;
            skillName.Text = entry?.Name ?? "스킬을 선택하세요";
            category.Text = entry is null ? "" : $"{entry.Kind} · {entry.Form}";
            portrait.Texture = entry is null ? null : Icon(entry);
            statLabels[0].Text = entry?.Kind ?? "—";
            statLabels[1].Text = entry?.Form ?? "—";
            statLabels[2].Text = entry?.Active is { } active
                ? active.ExecutePercent > 0 ? $"처형 {active.ExecutePercent}%"
                    : active.HealPercent > 0 ? $"회복 {active.HealPercent}%"
                    : active.DamageReductionPercent > 0 ? $"피해 감소 {active.DamageReductionPercent}%"
                    : active.DamageMultPercent != 100 ? $"피해 {active.DamageMultPercent}%" : "효과 참조"
                : "레벨별 효과";
            effectText.Text = entry?.Description ?? "목록에서 스킬을 선택하세요.";
            triggerText.Text = entry?.Active is not null
                ? "실제 교전 5회 이후 다음 교전에서 액티브 스킬이 발동합니다."
                : entry is null ? "" : "패시브 효과는 해당 스킬의 적용 조건에서 발동합니다.";
            noteText.Text = entry?.Kind == "패시브"
                ? "레벨 1~3의 효과가 위에 순서대로 표시됩니다."
                : entry is null ? "" : "방어 > 계략 = 공격 순서로 액티브 스킬은 반영 됩니다.";
            //previewFrame.Visible = entry?.Active is not null;
            //preview.ShowSkill(entry?.Active);
            RefreshButtonStyles();
        }
        void Refill()
        {
            foreach (var child in cards.GetChildren()) cards.RemoveChild(child);
            foreach (var (_, button) in entryButtons) button.QueueFree();
            entryButtons.Clear();
            var shown = entries.Where(entry => (filter == "전체" || entry.Kind == filter)
                && entry.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var entry in shown)
            {
                var button = new Button { Name = "SkillGuideEntryCard",
                    CustomMinimumSize = new Vector2(0, SkillGuideCardHeight),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Text = "" };
                button.AddThemeStyleboxOverride("normal", Frame(InkSoft, new Color("95632b"), 1, 6, 7));
                button.AddThemeStyleboxOverride("hover", Frame(new Color("422019"), GoldBright, 1, 6, 7));
                button.AddThemeStyleboxOverride("pressed", Frame(new Color("5d2119"), GoldBright, 2, 6, 7));
                cards.AddChild(button);

var content = new HBoxContainer
{
    MouseFilter = Control.MouseFilterEnum.Ignore
};

content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

// 카드 내부 왼쪽 여백
content.OffsetLeft = 5;

content.AddThemeConstantOverride("separation", 12);
button.AddChild(content);



var image = ActiveSkillIcons.Preview(null, 46);
image.Name = "SkillGuideListIcon";
image.Texture = Icon(entry);
image.MouseFilter = Control.MouseFilterEnum.Ignore;

// 이미지 세로 가운데 정렬
image.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

content.AddChild(image);

var text = new VBoxContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
    Alignment = BoxContainer.AlignmentMode.Center,
    MouseFilter = Control.MouseFilterEnum.Ignore
};

content.AddChild(text);






                text.AddChild(MakeLabel(entry.Name, 14, GoldBright));
                text.AddChild(MakeLabel($"{entry.Kind} · {entry.Form}", 11, Gold));
                button.Pressed += () => Select(entry);
                entryButtons.Add((entry, button));
            }
            if (selected is null || !shown.Contains(selected)) Select(shown.FirstOrDefault());
            else RefreshButtonStyles();
        }
        foreach (var name in new[] { "전체", "액티브", "패시브" })
        {
            var button = MakeButton(name);
            button.Name = $"SkillGuideFilter{name}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () => { filter = name; Refill(); };
            filters.AddChild(button);
            filterButtons.Add(button);
        }
        search.TextChanged += _ => Refill();
        Refill();
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private async void RunSkillGuideWorkspaceQa()
    {
        OpenSkillGuideWorkspace();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var cards = _modalLayer?.FindChild("SkillGuideCards", true, false) as VBoxContainer;
        var columns = _modalLayer?.FindChild("SkillGuideColumns", true, false) as HBoxContainer;
        var left = _modalLayer?.FindChild("SkillGuideLeft", true, false) as VBoxContainer;
        var right = _modalLayer?.FindChild("SkillGuideDetailBox", true, false) as VBoxContainer;
        var chrome = _modalLayer?.FindChild("ModalChrome", true, false) as ModalChrome;
        var preview = _modalLayer?.FindChild("SkillGuideEffectPreview", true, false) as SkillGuideEffectPreview3D;
        var search = _modalLayer?.FindChild("SkillGuideSearch", true, false) as LineEdit;
        var allCount = cards?.GetChildCount() ?? 0;
        var passed = allCount == _activeSkills.Count + _passiveSkills.Count + _adminSkills.Count
            && preview is { IsPlaying: false, Day: 0 }
            && chrome is not null && columns is not null && left is not null && right is not null
            && Mathf.Abs(left.Size.X / (left.Size.X + right.Size.X) - SkillGuideLeftRatio) < 0.03f
            && preview.CustomMinimumSize == new Vector2(SkillGuidePreviewWidth, SkillGuidePreviewHeight)
            && _modalLayer?.FindChild("SkillGuideColumnDivider", true, false) is ColorRect
            && _modalLayer?.FindChild("SkillGuideListScroll", true, false) is ScrollContainer;
        if (search is not null) search.Text = "무쌍";
        var active = _modalLayer?.FindChild("SkillGuideFilter액티브", true, false) as Button;
        active?.EmitSignal(Button.SignalName.Pressed);
        var passive = _modalLayer?.FindChild("SkillGuideFilter패시브", true, false) as Button;
        passed &= active is not null && passive is not null;
        preview?.ShowSkill(_activeSkills.FirstOrDefault());
        passed &= preview is { IsPlaying: false, Day: 0 } && preview.PlayButton.Visible;
        if (search is not null) search.Text = "";
        passive?.EmitSignal(Button.SignalName.Pressed);
        passed &= cards?.GetChildCount() == _passiveSkills.Count + _adminSkills.Count
            && preview is { IsPlaying: false } && !preview.PlayButton.Visible;
        active?.EmitSignal(Button.SignalName.Pressed);
        preview?.ShowSkill(_activeSkills.FirstOrDefault());
        preview?.AdvanceToActivationForQa();
        passed &= preview is { IsPlaying: true, Day: 6, EffectAttached: true };
        await ToSignal(GetTree().CreateTimer(1.35), SceneTreeTimer.SignalName.Timeout);
        passed &= preview is { IsPlaying: false, Day: 0 } && preview.PlayButton.Visible;
        passive?.EmitSignal(Button.SignalName.Pressed);
        passed &= preview is { IsPlaying: false } && !preview.PlayButton.Visible;
        GD.Print($"[skill-guide-qa] passed={passed} entries={allCount} manualReplay=true");
        GetTree().Quit(passed ? 0 : 1);
    }
}
