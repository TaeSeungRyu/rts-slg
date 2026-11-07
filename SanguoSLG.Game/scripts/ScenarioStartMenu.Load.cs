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
    private bool _loadInProgress;
    private CanvasLayer? _loadConfirmationLayer;
    private Action? _loadConfirmationCancel;

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

    private enum LoadButtonKind { Menu, Delete, Load }

    private static StyleBoxFlat LoadButtonStyle(string background, string border, int borderWidth = 1)
    {
        var style = NewGameFrame(new Color(background), new Color(border), borderWidth, 7, 0);
        style.ContentMarginLeft = 18;
        style.ContentMarginRight = 18;
        style.ContentMarginTop = 5;
        style.ContentMarginBottom = 5;
        return style;
    }

    private static Button LoadActionButton(string name, string caption, LoadButtonKind kind, int width, int height)
    {
        var (normalBg, normalBorder, normalText, hoverBg, hoverBorder, hoverText, pressedBg, pressedBorder) = kind switch
        {
            LoadButtonKind.Delete => ("30201b", "ae8249", "e7c99a", "583026", "e2a66c", "ffe0b0", "251512", "c18c53"),
            LoadButtonKind.Load => ("70251d", "d7a45a", "ffdda1", "94352a", "ffd17a", "fff0c6", "501913", "c68d42"),
            _ => ("241a16", "b48a49", "e9cf9b", "38251c", "e0b86b", "ffe1a8", "18100e", "d1a153")
        };
        var button = new Button { Name = name, Text = caption,
            CustomMinimumSize = new Vector2(width, height),
            Alignment = HorizontalAlignment.Center,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeStyleboxOverride("normal", LoadButtonStyle(normalBg, normalBorder));
        button.AddThemeStyleboxOverride("hover", LoadButtonStyle(hoverBg, hoverBorder, 2));
        button.AddThemeStyleboxOverride("pressed", LoadButtonStyle(pressedBg, pressedBorder, 2));
        button.AddThemeStyleboxOverride("hover_pressed", LoadButtonStyle(pressedBg, pressedBorder, 2));
        button.AddThemeStyleboxOverride("disabled", LoadButtonStyle("29211e", "675941"));
        var focus = LoadButtonStyle("00000000", "f0cf7c", 2);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeFontOverride("font", NewGameSerif(kind == LoadButtonKind.Load ? 700 : 550));
        button.AddThemeFontSizeOverride("font_size", height < 40 ? 13 : kind == LoadButtonKind.Load ? 17 : 16);
        button.AddThemeColorOverride("font_color", new Color(normalText));
        button.AddThemeColorOverride("font_hover_color", new Color(hoverText));
        button.AddThemeColorOverride("font_pressed_color", new Color(normalText));
        button.AddThemeColorOverride("font_disabled_color", new Color("8e7964"));
        button.AddThemeColorOverride("font_focus_color", new Color(hoverText));
        return button;
    }

    private void OpenLoadSlots(Control menuCenter)
    {
        if (_loadView is not null) return;
        menuCenter.Visible = false;
        _selectedLoadSlot = null;
        _loadInProgress = false;
        _loadCards.Clear();
        _corruptLoadSlots.Clear();
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        _loadView = center;

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1080, 390) };
        var frame = new StyleBoxFlat { BgColor = new Color("1e1715", 0.96f), BorderColor = LoadGold };
        frame.SetBorderWidthAll(2);
        frame.SetCornerRadiusAll(12);
        frame.SetContentMarginAll(20);
        panel.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);
        var title = LoadLabel("이어하기", 24, NewGameGoldBright);
        title.AddThemeFontOverride("font", NewGameSerif(700));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(title);
        var subtitle = LoadLabel("저장된 천하의 기록", 13, LoadMuted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(subtitle);
        content.AddChild(new HSeparator());

        var store = new SaveSlotStore(ProjectSettings.GlobalizePath("user://saves"),
            ProjectSettings.GlobalizePath("user://sanguo-save.json"));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(1036, 184),
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
        var previous = LoadActionButton("LoadPreviousPage", "이전", LoadButtonKind.Menu, 91, 32);
        var pageLabel = LoadLabel("", 14, LoadMuted);
        var next = LoadActionButton("LoadNextPage", "다음", LoadButtonKind.Menu, 91, 32);
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
            var visibleCount = Math.Min(10, Math.Max(0, saves.Count - page * 10));
            var rows = Math.Max(1, (visibleCount + 1) / 2);
            var listHeight = Math.Min(504, rows * 184 + (rows - 1) * 12);
            scroll.CustomMinimumSize = new Vector2(1036, listHeight);
            if (saves.Count == 0)
                grid.AddChild(LoadLabel("저장된 게임이 없습니다.", 17, LoadMuted));
            foreach (var info in saves.Skip(page * 10).Take(10)) grid.AddChild(CreateLoadCard(info));
            var pages = Math.Max(1, (saves.Count + 9) / 10);
            pageLabel.Text = $"{page + 1} / {pages}";
            previous.Disabled = page == 0;
            next.Disabled = page >= pages - 1;
            pager.Visible = pages > 1;
            panel.CustomMinimumSize = new Vector2(1080, Mathf.Min(740,
                Mathf.Max(390, listHeight + (pager.Visible ? 200 : 167))));
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

        content.AddChild(new HSeparator());
        var footerInset = new MarginContainer();
        footerInset.AddThemeConstantOverride("margin_left", 16);
        footerInset.AddThemeConstantOverride("margin_right", 16);
        content.AddChild(footerInset);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 14);
        footerInset.AddChild(actions);
        var back = LoadActionButton("LoadBackButton", "← 메인 메뉴로 돌아가기", LoadButtonKind.Menu, 260, 50);
        back.Pressed += () => { center.QueueFree(); _loadView = null; menuCenter.Visible = true; };
        actions.AddChild(back);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _deleteSelectedButton = LoadActionButton("LoadDeleteButton", "기록 삭제", LoadButtonKind.Delete, 170, 50);
        _deleteSelectedButton.Disabled = true;
        actions.AddChild(_deleteSelectedButton);
        _loadSelectedButton = LoadActionButton("LoadStartButton", "선택한 게임 불러오기 →", LoadButtonKind.Load, 270, 50);
        _loadSelectedButton.Disabled = true;
        actions.AddChild(_loadSelectedButton);
        var hint = LoadLabel("", 12, LoadMuted);
        hint.Visible = false;
        content.AddChild(hint);
        _loadSelectedButton.Pressed += () =>
        {
            if (_selectedLoadSlot is not { } slot || _loadInProgress || _loadConfirmationLayer is not null) return;
            var selected = store.Inspect(slot);
            if (!selected.Exists || selected.Corrupt)
            {
                hint.Text = "선택한 저장 기록을 읽을 수 없습니다.";
                hint.Visible = true;
                return;
            }
            ShowLoadConfirmation(store, selected, content);
        };
        _deleteSelectedButton.Pressed += () =>
        {
            if (_selectedLoadSlot is not { } slot) return;
            var target = saves.FirstOrDefault(info => info.Slot == slot);
            var targetName = target is null ? $"저장 기록 {slot:00}"
                : $"저장 기록 {slot:00} · {target.ScenarioTitle ?? "시나리오 미상"} · {target.FactionName ?? "세력 미상"}";
            var confirm = new ConfirmationDialog
            {
                Title = "저장 기록 삭제",
                DialogText = $"{targetName}\n이 기록을 삭제하시겠습니까? 되돌릴 수 없습니다.",
                OkButtonText = "삭제",
                CancelButtonText = "취소"
            };
            center.AddChild(confirm);
            confirm.Confirmed += () =>
            {
                try { store.Delete(slot); RefreshCards(); hint.Text = $"저장 기록 {slot:00}을 삭제했습니다."; }
                catch (Exception error) { hint.Text = $"삭제 실패: {error.Message}"; }
                hint.Visible = true;
                confirm.QueueFree();
            };
            confirm.Canceled += confirm.QueueFree;
            confirm.PopupCentered();
        };
        RefreshCards();
    }

    private void ShowLoadConfirmation(SaveSlotStore store, SaveSlotInfo info, VBoxContainer loadContent)
    {
        if (_loadConfirmationLayer is not null) return;
        var priorFocus = GetViewport().GuiGetFocusOwner();
        var layer = new CanvasLayer { Name = "LoadConfirmation", Layer = 45 };
        _loadConfirmationLayer = layer;
        AddChild(layer);
        void Close()
        {
            if (!ReferenceEquals(_loadConfirmationLayer, layer)) return;
            _loadConfirmationLayer = null;
            _loadConfirmationCancel = null;
            layer.QueueFree();
            if (priorFocus is not null && GodotObject.IsInstanceValid(priorFocus)
                && priorFocus.IsVisibleInTree()) priorFocus.GrabFocus();
        }
        _loadConfirmationCancel = Close;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { Name = "LoadConfirmationPanel",
            CustomMinimumSize = new Vector2(580, 350), MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color("190e0d"), new Color("b58b49"), 2, 8, 24));
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);
        var title = LoadLabel("천하의 기록을 불러오시겠습니까?", 22, new Color("efd49b"));
        title.AddThemeFontOverride("font", NewGameSerif(700));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(title);
        var subtitle = LoadLabel("저장된 게임 확인", 12, new Color("b99a6c"));
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(subtitle);
        content.AddChild(NewGameRule());
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 18);
        content.AddChild(body);
        var portraitFrame = new PanelContainer { CustomMinimumSize = new Vector2(122, 164),
            Modulate = Colors.White, SelfModulate = Colors.White };
        portraitFrame.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color("261714"), new Color("a98247"), 1, 6, 1));
        body.AddChild(portraitFrame);
        const string diaochanPath = "res://assets/portraits/113.png";
        if (Godot.FileAccess.FileExists(diaochanPath))
        {
            portraitFrame.AddChild(new TextureRect
            {
                Name = "LoadGuidePortrait",
                Texture = GD.Load<Texture2D>(diaochanPath),
                CustomMinimumSize = new Vector2(120, 160),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                Modulate = Colors.White,
                SelfModulate = Colors.White,
                MouseFilter = MouseFilterEnum.Ignore
            });
        }
        else portraitFrame.AddChild(LoadLabel("안내 초상화 없음", 12, LoadMuted));
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 8);
        body.AddChild(details);
        var speech = LoadLabel("장군님, 지난 천하의 기록으로 돌아가시겠습니까?", 15, LoadText);
        speech.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        details.AddChild(speech);
        details.AddChild(NewGameRule());
        AddLoadDetail(details, "시나리오", info.ScenarioTitle ?? "기록 없음");
        AddLoadDetail(details, "세력 · 군주", $"{info.FactionName ?? "기록 없음"} · {info.RulerName ?? "기록 없음"}");
        AddLoadDetail(details, "진행 시점", info.Year is { } year && info.Month is { } month
            ? $"{year}년 {month}월" : "기록 없음");
        AddLoadDetail(details, "마지막 저장", info.SavedAtUtc is { } saved
            ? saved.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "기록 없음");
        content.AddChild(NewGameRule());
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        content.AddChild(buttons);
        var cancel = LoadActionButton("LoadConfirmCancel", "취소", LoadButtonKind.Menu, 170, 50);
        cancel.Pressed += Close;
        buttons.AddChild(cancel);
        var confirm = LoadActionButton("LoadConfirmAccept", "불러오기", LoadButtonKind.Load, 170, 50);
        confirm.Pressed += () =>
        {
            if (_loadInProgress || !ReferenceEquals(_loadConfirmationLayer, layer)) return;
            confirm.Disabled = true;
            Close();
            StartSavedGame(store, info.Slot, loadContent);
        };
        buttons.AddChild(confirm);
        cancel.FocusNext = confirm.GetPath();
        cancel.FocusPrevious = confirm.GetPath();
        confirm.FocusNext = cancel.GetPath();
        confirm.FocusPrevious = cancel.GetPath();
        cancel.GrabFocus();
    }

    private static void AddLoadDetail(VBoxContainer parent, string name, string value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        var heading = LoadLabel(name, 12, new Color("d9b978"));
        heading.CustomMinimumSize = new Vector2(85, 0);
        row.AddChild(heading);
        var text = LoadLabel(value, 14, LoadText);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(text);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_loadConfirmationLayer is null || @event is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        if (key.Keycode == Key.Escape)
        {
            _loadConfirmationCancel?.Invoke();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode is not (Key.Tab or Key.Enter or Key.KpEnter or Key.Space))
            GetViewport().SetInputAsHandled();
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
            || _corruptLoadSlots.Contains(_selectedLoadSlot.Value) || _loadInProgress;
        if (_deleteSelectedButton is not null) _deleteSelectedButton.Disabled = _selectedLoadSlot is null
            || _loadInProgress;
    }

    private void StartSavedGame(SaveSlotStore store, int slot, VBoxContainer content)
    {
        if (_loadInProgress) return;
        _loadInProgress = true;
        UpdateLoadSelection();
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
            _loadInProgress = false;
            UpdateLoadSelection();
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
        if (_loadConfirmationLayer is null)
        {
            GD.Print("[menu-load-qa] passed=False reason=confirmation-not-opened");
            GetTree().Quit(1);
            return;
        }
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        if (_loadConfirmationLayer is not null || _selectedLoadSlot != valid.Slot || _loadInProgress)
        {
            GD.Print("[menu-load-qa] passed=False reason=cancel-state");
            GetTree().Quit(1);
            return;
        }
        _loadSelectedButton?.EmitSignal(BaseButton.SignalName.Pressed);
        var confirm = _loadConfirmationLayer?.FindChild("LoadConfirmAccept", true, false) as Button;
        if (confirm is null) { GD.Print("[menu-load-qa] passed=False reason=missing-confirmation"); GetTree().Quit(1); return; }
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
