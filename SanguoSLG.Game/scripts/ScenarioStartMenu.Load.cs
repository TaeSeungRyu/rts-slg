using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu
{
    private Control? _loadView;
    private int? _selectedLoadSlot;
    private readonly Dictionary<int, Button> _loadCards = [];
    private readonly HashSet<int> _corruptLoadSlots = [];
    private Button? _loadSelectedButton;
    private Button? _deleteSelectedButton;

    private static readonly Color LoadGold = new("d4a252");
    private static readonly Color LoadText = new("f1d6a8");
    private static readonly Color LoadMuted = new("d7c5ab");

    private static StyleBoxFlat LoadCardStyle(bool selected, bool hover = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(selected ? "442019" : hover ? "30201a" : "261816"),
            BorderColor = new Color(selected ? "d4a252" : hover ? "a8824d" : "765637")
        };
        style.SetBorderWidthAll(selected ? 2 : 1);
        style.SetCornerRadiusAll(7);
        return style;
    }

    private static Label LoadLabel(string text, int size = 14, Color? color = null)
    {
        var label = new Label { Text = text, Modulate = color ?? LoadText, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private void OpenLoadSlots(Control menuCenter)
    {
        if (_loadView is not null) return;
        menuCenter.Visible = false;
        _selectedLoadSlot = null;
        _loadCards.Clear();
        _corruptLoadSlots.Clear();
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        _loadView = center;

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1080, 0) };
        var frame = new StyleBoxFlat { BgColor = new Color("1e1715"), BorderColor = LoadGold };
        frame.SetBorderWidthAll(2);
        frame.SetCornerRadiusAll(12);
        frame.SetContentMarginAll(20);
        panel.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        panel.AddChild(content);
        var header = new HBoxContainer();
        content.AddChild(header);
        var title = LoadLabel("저장된 게임 시작하기", 23);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var close = new Button { Text = "← 메인 메뉴", CustomMinimumSize = new Vector2(140, 36) };
        close.Pressed += () => { center.QueueFree(); _loadView = null; menuCenter.Visible = true; };
        header.AddChild(close);
        content.AddChild(new HSeparator());

        var store = new SaveSlotStore(ProjectSettings.GlobalizePath("user://saves"),
            ProjectSettings.GlobalizePath("user://sanguo-save.json"));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(1036, 556),
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(scroll);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        scroll.AddChild(grid);

        var pager = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pager.AddThemeConstantOverride("separation", 12);
        content.AddChild(pager);
        var previous = new Button { Text = "이전", CustomMinimumSize = new Vector2(72, 30) };
        var pageLabel = LoadLabel("", 13, LoadMuted);
        var next = new Button { Name = "LoadNextPage", Text = "다음", CustomMinimumSize = new Vector2(72, 30) };
        pager.AddChild(previous);
        pager.AddChild(pageLabel);
        pager.AddChild(next);
        IReadOnlyList<SaveSlotInfo> saves = [];
        var page = 0;

        void RenderPage()
        {
            foreach (Node child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            _loadCards.Clear();
            _corruptLoadSlots.Clear();
            _selectedLoadSlot = null;
            if (saves.Count == 0)
                grid.AddChild(LoadLabel("저장된 게임이 없습니다.", 17, LoadMuted));
            foreach (var info in saves.Skip(page * 10).Take(10)) grid.AddChild(CreateLoadCard(info));
            var pages = Math.Max(1, (saves.Count + 9) / 10);
            pageLabel.Text = $"{page + 1} / {pages}";
            previous.Disabled = page == 0;
            next.Disabled = page >= pages - 1;
            UpdateLoadSelection();
        }

        void RefreshCards()
        {
            saves = store.InspectSaved();
            page = Math.Min(page, Math.Max(0, (saves.Count - 1) / 10));
            RenderPage();
        }

        previous.Pressed += () => { page--; RenderPage(); };
        next.Pressed += () => { page++; RenderPage(); };

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        content.AddChild(actions);
        var hint = LoadLabel("저장 기록을 선택하세요.", 14, LoadMuted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(hint);
        _deleteSelectedButton = new Button { Text = "삭제", Disabled = true,
            CustomMinimumSize = new Vector2(100, 38) };
        actions.AddChild(_deleteSelectedButton);
        _loadSelectedButton = new Button { Text = "불러오기", Disabled = true,
            CustomMinimumSize = new Vector2(140, 38) };
        actions.AddChild(_loadSelectedButton);
        _loadSelectedButton.Pressed += () =>
        {
            if (_selectedLoadSlot is { } slot) StartSavedGame(store, slot, content);
        };
        _deleteSelectedButton.Pressed += () =>
        {
            if (_selectedLoadSlot is not { } slot) return;
            var confirm = new ConfirmationDialog
            {
                Title = "저장 기록 삭제",
                DialogText = $"저장 기록 {slot:00}을 삭제하시겠습니까?",
                OkButtonText = "삭제",
                CancelButtonText = "취소"
            };
            center.AddChild(confirm);
            confirm.Confirmed += () =>
            {
                try { store.Delete(slot); RefreshCards(); hint.Text = $"저장 기록 {slot:00}을 삭제했습니다."; }
                catch (Exception error) { hint.Text = $"삭제 실패: {error.Message}"; }
                confirm.QueueFree();
            };
            confirm.Canceled += confirm.QueueFree;
            confirm.PopupCentered();
        };
        RefreshCards();
    }

    private Button CreateLoadCard(SaveSlotInfo info)
    {
        var card = new Button
        {
            Name = $"LoadCard_{info.Slot}",
            CustomMinimumSize = new Vector2(505, 184),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            FocusMode = FocusModeEnum.None
        };
        card.AddThemeStyleboxOverride("normal", LoadCardStyle(false));
        card.AddThemeStyleboxOverride("hover", LoadCardStyle(false, true));
        card.AddThemeStyleboxOverride("pressed", LoadCardStyle(false, true));
        card.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        card.Pressed += () => { _selectedLoadSlot = info.Slot; UpdateLoadSelection(); };
        _loadCards[info.Slot] = card;
        if (info.Corrupt) _corruptLoadSlots.Add(info.Slot);

        var inset = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        inset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        inset.AddThemeConstantOverride("margin_left", 13);
        inset.AddThemeConstantOverride("margin_right", 13);
        inset.AddThemeConstantOverride("margin_top", 12);
        inset.AddThemeConstantOverride("margin_bottom", 11);
        card.AddChild(inset);
        var vertical = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        vertical.AddThemeConstantOverride("separation", 7);
        inset.AddChild(vertical);
        var main = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore };
        main.AddThemeConstantOverride("separation", 13);
        vertical.AddChild(main);
        var portraitFrame = new PanelContainer { CustomMinimumSize = new Vector2(104, 119),
            MouseFilter = MouseFilterEnum.Ignore };
        var portraitStyle = new StyleBoxFlat { BgColor = new Color("261714"), BorderColor = LoadGold };
        portraitStyle.SetBorderWidthAll(1);
        portraitFrame.AddThemeStyleboxOverride("panel", portraitStyle);
        main.AddChild(portraitFrame);
        var portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore, Modulate = Colors.White, SelfModulate = Colors.White };
        portraitFrame.AddChild(portrait);
        if (info.RulerId is { } rulerId)
        {
            var path = $"res://assets/portraits/{rulerId}.png";
            if (Godot.FileAccess.FileExists(path)) portrait.Texture = GD.Load<Texture2D>(path);
        }
        if (portrait.Texture is null && !string.IsNullOrWhiteSpace(info.RulerPortraitPath))
        {
            var path = info.RulerPortraitPath;
            if (Godot.FileAccess.FileExists(path)) portrait.Texture = GD.Load<Texture2D>(path);
        }
        if (portrait.Texture is null)
        {
            portraitFrame.AddChild(LoadLabel("초상 없음", 12, LoadMuted));
        }

        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore };
        details.AddThemeConstantOverride("separation", 3);
        main.AddChild(details);
        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        details.AddChild(titleRow);
        var scenario = LoadLabel(info.Corrupt ? "손상된 저장" :
            info.ScenarioTitle ?? (info.StartYear is { } y ? $"{y}년 시나리오" : "시나리오 미상"), 16);
        scenario.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scenario.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        scenario.MaxLinesVisible = 2;
        titleRow.AddChild(scenario);
        var diamond = LoadLabel("◆", 15, LoadGold);
        diamond.Name = "SelectedMark";
        diamond.Visible = false;
        titleRow.AddChild(diamond);
        if (!info.Corrupt)
        {
            details.AddChild(LoadLabel($"세력  {info.FactionName ?? "미상"}     군주  {info.RulerName ?? "미상"}", 14));
            if (info.Year is { } year && info.Month is { } month)
                details.AddChild(LoadLabel($"진행  {year}년 {month}월", 13, LoadMuted));
            var stats = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsVertical = SizeFlags.ExpandFill };
            stats.AddThemeConstantOverride("separation", 8);
            details.AddChild(stats);
            if (info.OwnedSiteCount is { } sites) AddLoadStat(stats, "거점", sites.ToString("N0"));
            if (info.OfficerCount is { } officers) AddLoadStat(stats, "장수", officers.ToString("N0"));
            if (info.TotalTroops is { } troops) AddLoadStat(stats, "병력", troops.ToString("N0"));
        }
        else details.AddChild(LoadLabel("저장 파일을 읽을 수 없습니다.", 13, LoadMuted));
        var rule = new HSeparator { MouseFilter = MouseFilterEnum.Ignore };
        vertical.AddChild(rule);
        var footer = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        vertical.AddChild(footer);
        var saved = LoadLabel(info.SavedAtUtc is { } at
            ? $"마지막 저장  {at.ToLocalTime():yyyy-MM-dd HH:mm}" : "저장 시각 미상", 12,
            new Color("ac9274"));
        saved.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(saved);
        footer.AddChild(LoadLabel($"기록 {info.Slot:00}", 12, LoadMuted));
        return card;
    }

    private static void AddLoadStat(HBoxContainer row, string title, string value)
    {
        var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(cell);
        cell.AddChild(LoadLabel(title, 11, LoadMuted));
        cell.AddChild(LoadLabel(value, 14));
    }

    private void UpdateLoadSelection()
    {
        foreach (var (slot, card) in _loadCards)
        {
            var selected = slot == _selectedLoadSlot;
            card.AddThemeStyleboxOverride("normal", LoadCardStyle(selected));
            card.AddThemeStyleboxOverride("hover", LoadCardStyle(selected, true));
            card.AddThemeStyleboxOverride("pressed", LoadCardStyle(selected, true));
            var mark = card.FindChild("SelectedMark", true, false) as Label;
            if (mark is not null) mark.Visible = selected;
        }
        if (_loadSelectedButton is not null) _loadSelectedButton.Disabled = _selectedLoadSlot is null
            || _corruptLoadSlots.Contains(_selectedLoadSlot.Value);
        if (_deleteSelectedButton is not null) _deleteSelectedButton.Disabled = _selectedLoadSlot is null;
    }

    private void StartSavedGame(SaveSlotStore store, int slot, VBoxContainer content)
    {
        try
        {
            var loaded = store.Load(slot);
            var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
            var source = CreatedGeneralPresets.ApplyAtNewGame(new ScenarioLoader().LoadFromDirectory(data));
            CampaignMapScene.ConfigureLoadedState(loaded, source);
            CampaignStartedFromMenu = true;
            _launchRequested = false;
            LoadSlotsOnStart = false;
            ChangeToCampaign();
        }
        catch (Exception error)
        {
            var warning = content.GetChildren().OfType<Label>().FirstOrDefault(label => label.Name == "LoadWarning");
            if (warning is null)
            {
                warning = new Label { Name = "LoadWarning", Modulate = new Color("f0a486") };
                content.AddChild(warning);
            }
            warning.Text = $"슬롯 {slot:00}을 불러올 수 없습니다: {error.Message}";
        }
    }

    private void RunMenuLoadQa(Control menuCenter)
    {
        var store = new SaveSlotStore(ProjectSettings.GlobalizePath("user://saves"),
            ProjectSettings.GlobalizePath("user://sanguo-save.json"));
        var valid = store.InspectSaved().FirstOrDefault(slot => !slot.Corrupt);
        if (valid is null)
        {
            GD.Print("[menu-load-qa] skipped=no-save");
            GetTree().Quit();
            return;
        }
        OpenLoadSlots(menuCenter);
        while (!_loadCards.ContainsKey(valid.Slot))
        {
            var next = _loadView?.FindChild("LoadNextPage", true, false) as Button;
            if (next is null || next.Disabled) break;
            next.EmitSignal(BaseButton.SignalName.Pressed);
        }
        var choice = _loadCards.GetValueOrDefault(valid.Slot);
        if (choice is null) { GD.Print("[menu-load-qa] passed=False reason=missing-slot"); GetTree().Quit(1); return; }
        choice.EmitSignal(BaseButton.SignalName.Pressed);
        _loadSelectedButton?.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
