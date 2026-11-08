using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class ScenarioEditorScene
{
    private Control? _generalPlacementOverlay;
    private readonly HashSet<int> _generalPlacementSelection = [];
    private readonly Dictionary<string, Texture2D> _generalPortraitIcons = [];

    private Control GeneralPortrait(General? general, int size)
    {
        var texture = general is null ? null : LoadGeneralPortrait(general);
        if (texture is null)
        {
            var fallback = new Label
            {
                Text = "초상\n없음",
                CustomMinimumSize = new Vector2(size, size),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            fallback.AddThemeFontSizeOverride("font_size", 10);
            return fallback;
        }
        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = Colors.White,
            SelfModulate = Colors.White,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }

    private Texture2D? LoadGeneralPortrait(General general)
    {
        var candidate = !string.IsNullOrWhiteSpace(general.PortraitPath)
            ? general.PortraitPath!.Replace('\\', '/')
            : $"res://assets/portraits/{general.Id.Value}.png";
        var thumbnail = $"res://assets/portraits/thumbnails/{general.Id.Value}.png";
        if (File.Exists(ProjectSettings.GlobalizePath(thumbnail))) candidate = thumbnail;
        if (candidate.StartsWith("SanguoSLG.Game/", StringComparison.OrdinalIgnoreCase))
            candidate = "res://" + candidate["SanguoSLG.Game/".Length..];
        string physicalPath;
        if (candidate.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            physicalPath = ProjectSettings.GlobalizePath(candidate);
        else physicalPath = Path.IsPathRooted(candidate) ? candidate
            : Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", candidate));
        if (!File.Exists(physicalPath)) return null;
        if (_generalPortraitIcons.TryGetValue(physicalPath, out var cached)) return cached;
        using var image = Image.LoadFromFile(physicalPath);
        if (image.IsEmpty()) return null;
        if (image.GetWidth() > 96)
            image.Resize(96, Math.Max(1, (int)Math.Round(image.GetHeight() * 96d / image.GetWidth())), Image.Interpolation.Lanczos);
        var texture = ImageTexture.CreateFromImage(image);
        _generalPortraitIcons[physicalPath] = texture;
        return texture;
    }

    private void OpenGeneralPlacementDialog(ScenarioCityEntry city)
    {
        CloseGeneralPlacementDialog();
        _generalPlacementSelection.Clear();
        var map = CurrentMap().Preset;
        var site = map.Sites.First(entry => entry.Id == city.SiteId);
        var faction = _draft.Factions.First(entry => entry.Id == city.OwnerFactionId);

        var overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.68f),
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 200,
        };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        _generalPlacementOverlay = overlay;

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1120, 690) };
        panel.AddThemeStyleboxOverride("panel", GeneralWorkspaceStyle.SectionBox());
        center.AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        panel.AddChild(body);

        var titleRow = new HBoxContainer();
        body.AddChild(titleRow);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleRow.AddChild(title);
        var heading = new Label { Text = "장수 배치" };
        heading.AddThemeFontSizeOverride("font_size", 22);
        title.AddChild(heading);
        title.AddChild(new Label { Text = $"배치할 장수를 선택하세요.  ·  {faction.Name} / {site.Name}" });
        var close = new Button { Text = "×", CustomMinimumSize = new Vector2(42, 34) };
        close.Pressed += CloseGeneralPlacementDialog;
        titleRow.AddChild(close);
        body.AddChild(new HSeparator());

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 8);
        body.AddChild(filters);
        var search = new LineEdit { PlaceholderText = "이름 또는 ID 검색", CustomMinimumSize = new Vector2(230, 36),
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        filters.AddChild(search);
        NativeTextInputDialog.Attach(search);
        var factionFilter = new OptionButton { CustomMinimumSize = new Vector2(185, 36) };
        factionFilter.AddItem("전체 세력", 0);
        factionFilter.AddItem("현재 선택 세력", -1);
        foreach (var entry in _draft.Factions) factionFilter.AddItem(entry.Name, entry.Id);
        GeneralWorkspaceStyle.StyleOptionButton(factionFilter, 240);
        filters.AddChild(factionFilter);
        var stateFilter = new OptionButton { CustomMinimumSize = new Vector2(165, 36) };
        foreach (var (text, id) in new[] { ("전체 배치 상태", 0), ("미배치", 1), ("현재 거점 배치", 2), ("다른 거점 배치", 3) })
            stateFilter.AddItem(text, id);
        GeneralWorkspaceStyle.StyleOptionButton(stateFilter, 220);
        filters.AddChild(stateFilter);
        var sort = new OptionButton { CustomMinimumSize = new Vector2(155, 36) };
        foreach (var (text, id) in new[] { ("ID순", 0), ("이름순", 1), ("무력 높은 순", 2), ("지력 높은 순", 3), ("정치 높은 순", 4) })
            sort.AddItem(text, id);
        GeneralWorkspaceStyle.StyleOptionButton(sort, 220);
        filters.AddChild(sort);

        var tree = BuildGeneralPlacementTree();
        body.AddChild(tree);
        var empty = new Label { Visible = false, Text = "조건에 맞는 장수가 없습니다.",
            HorizontalAlignment = HorizontalAlignment.Center };
        body.AddChild(empty);
        var selectionLabel = new Label { Text = "선택 0명", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center };

        void RefreshRows()
        {
            PopulateGeneralPlacementTree(tree, city, search.Text, factionFilter.GetSelectedId(),
                stateFilter.GetSelectedId(), sort.GetSelectedId());
            empty.Visible = tree.GetRoot()?.GetChildCount() == 0;
            selectionLabel.Text = $"선택 {_generalPlacementSelection.Count}명";
        }
        search.TextChanged += _ => RefreshRows();
        factionFilter.ItemSelected += _ => RefreshRows();
        stateFilter.ItemSelected += _ => RefreshRows();
        sort.ItemSelected += _ => RefreshRows();
        tree.ItemEdited += () =>
        {
            var item = tree.GetEdited();
            if (item is null || !item.IsEditable(0)) return;
            var id = item.GetMetadata(0).AsInt32();
            if (item.IsChecked(0)) _generalPlacementSelection.Add(id);
            else _generalPlacementSelection.Remove(id);
            selectionLabel.Text = $"선택 {_generalPlacementSelection.Count}명";
        };
        RefreshRows();

        body.AddChild(new HSeparator());
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        body.AddChild(footer);
        footer.AddChild(selectionLabel);
        var clear = new Button { Text = "선택 초기화" };
        clear.Pressed += () => { _generalPlacementSelection.Clear(); RefreshRows(); };
        footer.AddChild(clear);
        var cancel = new Button { Text = "취소" };
        cancel.Pressed += CloseGeneralPlacementDialog;
        footer.AddChild(cancel);
        var confirm = new Button { Text = "선택한 장수 배치", CustomMinimumSize = new Vector2(180, 38) };
        confirm.Pressed += () => ConfirmGeneralPlacement(city, selectionLabel);
        footer.AddChild(confirm);
        close.GrabFocus();
    }

    private static Tree BuildGeneralPlacementTree()
    {
        var tree = new Tree
        {
            Columns = 7,
            HideRoot = true,
            ColumnTitlesVisible = true,
            CustomMinimumSize = new Vector2(0, 490),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        foreach (var (column, title) in new[] { (0, "선택"), (1, "장수"), (2, "소속 세력"),
            (3, "무력"), (4, "지력"), (5, "정치"), (6, "현재 배치 상태") })
            tree.SetColumnTitle(column, title);
        tree.SetColumnExpand(0, false); tree.SetColumnCustomMinimumWidth(0, 58);
        tree.SetColumnExpand(1, true); tree.SetColumnExpandRatio(1, 2);
        tree.SetColumnExpand(2, true);
        foreach (var column in new[] { 3, 4, 5 })
        {
            tree.SetColumnExpand(column, false);
            tree.SetColumnCustomMinimumWidth(column, 52);
        }
        tree.SetColumnExpand(6, true); tree.SetColumnExpandRatio(6, 2);
        tree.Ready += () =>
        {
            foreach (var bar in tree.FindChildren("*", "VScrollBar", true, false).OfType<VScrollBar>())
                GeneralWorkspaceStyle.StyleVerticalScrollBar(bar, 8);
        };
        return tree;
    }

    private void PopulateGeneralPlacementTree(Tree tree, ScenarioCityEntry city, string query,
        int factionFilter, int stateFilter, int sort)
    {
        tree.Clear();
        var root = tree.CreateItem();
        var map = CurrentMap().Preset;
        var factionNames = _draft.Factions.ToDictionary(entry => entry.Id, entry => entry.Name);
        var postings = _draft.Generals.ToDictionary(entry => entry.GeneralId);
        IEnumerable<General> result = _source.Generals.Where(general =>
            (string.IsNullOrWhiteSpace(query) || general.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || general.Id.Value.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)));
        result = result.Where(general =>
        {
            postings.TryGetValue(general.Id.Value, out var posting);
            var matchesFaction = factionFilter == 0
                || factionFilter == -1 && posting?.FactionId == city.OwnerFactionId
                || factionFilter > 0 && posting?.FactionId == factionFilter;
            var status = posting?.SiteId is null ? 1 : posting.SiteId == city.SiteId ? 2 : 3;
            return matchesFaction && (stateFilter == 0 || stateFilter == status);
        });
        result = sort switch
        {
            1 => result.OrderBy(general => general.Name).ThenBy(general => general.Id.Value),
            2 => result.OrderByDescending(general => general.Might).ThenBy(general => general.Name),
            3 => result.OrderByDescending(general => general.Intellect).ThenBy(general => general.Name),
            4 => result.OrderByDescending(general => general.Politics).ThenBy(general => general.Name),
            _ => result.OrderBy(general => general.Id.Value),
        };
        foreach (var general in result)
        {
            postings.TryGetValue(general.Id.Value, out var posting);
            var eligible = posting is null || posting.SiteId is null && posting.FactionId == city.OwnerFactionId;
            var status = posting?.SiteId is { } siteId
                ? siteId == city.SiteId ? "현재 거점 배치" : "다른 거점: " + (map.Sites.FirstOrDefault(site => site.Id == siteId)?.Name ?? siteId.ToString())
                : posting is null ? "미소속 · 미배치" : posting.FactionId == city.OwnerFactionId ? "미배치" : "다른 세력 소속";
            var row = tree.CreateItem(root);
            row.SetMetadata(0, general.Id.Value);
            row.SetCellMode(0, TreeItem.TreeCellMode.Check);
            row.SetEditable(0, eligible);
            row.SetChecked(0, eligible && _generalPlacementSelection.Contains(general.Id.Value));
            row.SetText(1, $"{general.Name}  #{general.Id.Value}");
            var portrait = LoadGeneralPortrait(general);
            if (portrait is not null) { row.SetIcon(1, portrait); row.SetIconMaxWidth(1, 38); }
            row.SetText(2, posting is null ? "미소속" : factionNames.GetValueOrDefault(posting.FactionId, "알 수 없음"));
            row.SetText(3, general.Might.ToString());
            row.SetText(4, general.Intellect.ToString());
            row.SetText(5, general.Politics.ToString());
            row.SetText(6, status);
            if (!eligible)
                for (var column = 0; column < tree.Columns; column++) row.SetCustomColor(column, new Color("8c8175"));
        }
    }

    private void ConfirmGeneralPlacement(ScenarioCityEntry city, Label feedback)
    {
        if (_generalPlacementSelection.Count == 0)
        {
            feedback.Text = "배치할 장수를 1명 이상 선택하세요.";
            return;
        }
        if (!TryApplyGeneralPlacement(city, out var placed, out var error))
        {
            feedback.Text = error;
            return;
        }
        CloseGeneralPlacementDialog();
        ShowCities();
        _message.Text = $"장수 {placed}명을 선택한 거점에 배치했습니다.";
    }

    private bool TryApplyGeneralPlacement(ScenarioCityEntry city, out int placed, out string error)
    {
        placed = 0;
        error = "";
        var valid = new List<int>();
        foreach (var id in _generalPlacementSelection)
        {
            var existing = _draft.Generals.FirstOrDefault(entry => entry.GeneralId == id);
            if (existing is null || existing.SiteId is null && existing.FactionId == city.OwnerFactionId)
                valid.Add(id);
        }
        if (valid.Count != _generalPlacementSelection.Count)
        {
            _generalPlacementSelection.IntersectWith(valid);
            error = "배치 상태가 변경된 장수가 있어 다시 확인해 주세요.";
            return false;
        }
        foreach (var id in valid)
        {
            if (_draft.Generals.Any(entry => entry.GeneralId == id))
                UpdateGeneral(id, entry => entry with { SiteId = city.SiteId });
            else _draft = _draft with { Generals = [.. _draft.Generals,
                new ScenarioGeneralEntry(id, city.OwnerFactionId, city.SiteId)] };
        }
        placed = valid.Count;
        return true;
    }

    private bool RunGeneralPlacementQa(ScenarioCityEntry city)
    {
        var original = _draft;
        try
        {
            _selectedCityId = city.SiteId;
            ShowCities();
            var open = _body.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(button => button.Text == "+ 장수 배치");
            OpenGeneralPlacementDialog(city);
            var tree = _generalPlacementOverlay?.FindChildren("*", "Tree", true, false).OfType<Tree>().FirstOrDefault();
            var filters = _generalPlacementOverlay?.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().ToArray() ?? [];
            var uiPassed = open is not null && tree is { Columns: 7 } && filters.Length >= 3;
            CloseGeneralPlacementDialog();

            var candidates = _source.Generals.Where(general => _draft.Generals.All(entry => entry.GeneralId != general.Id.Value))
                .Take(2).Select(general => general.Id.Value).ToArray();
            if (candidates.Length < 2) return false;
            _generalPlacementSelection.UnionWith(candidates);
            var applied = TryApplyGeneralPlacement(city, out var placed, out _)
                && placed == 2 && candidates.All(id => _draft.Generals.Any(entry => entry.GeneralId == id
                    && entry.FactionId == city.OwnerFactionId && entry.SiteId == city.SiteId));
            UpdateGeneral(candidates[0], entry => entry with { SiteId = null });
            var releasePreserved = _draft.Generals.Any(entry => entry.GeneralId == candidates[0]
                && entry.FactionId == city.OwnerFactionId && entry.SiteId is null);

            var duplicate = _draft.Generals.FirstOrDefault(entry => entry.SiteId is not null
                && entry.SiteId != city.SiteId);
            var duplicateBlocked = true;
            if (duplicate is not null)
            {
                var snapshot = SnapshotDraft();
                _generalPlacementSelection.Clear();
                _generalPlacementSelection.Add(duplicate.GeneralId);
                duplicateBlocked = !TryApplyGeneralPlacement(city, out _, out _)
                    && SnapshotDraft() == snapshot;
            }

            var searchTree = BuildGeneralPlacementTree();
            AddChild(searchTree);
            var searched = _source.Generals[0];
            PopulateGeneralPlacementTree(searchTree, city, searched.Id.Value.ToString(), 0, 0, 0);
            var searchPassed = searchTree.GetRoot()?.GetChildCount() > 0;
            RemoveChild(searchTree);
            searchTree.QueueFree();
            var passed = uiPassed && applied && releasePreserved && duplicateBlocked && searchPassed;
            if (!passed) GD.Print($"[scenario-general-placement-qa] ui={uiPassed} applied={applied} release={releasePreserved} duplicate={duplicateBlocked} search={searchPassed} source={_source.Generals.Count}");
            return passed;
        }
        finally
        {
            CloseGeneralPlacementDialog();
            _generalPlacementSelection.Clear();
            _draft = original;
        }
    }

    private void CloseGeneralPlacementDialog()
    {
        if (_generalPlacementOverlay is null) return;
        _generalPlacementOverlay.QueueFree();
        _generalPlacementOverlay = null;
        _generalPlacementSelection.Clear();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_generalPlacementOverlay is not null && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            CloseGeneralPlacementDialog();
            GetViewport().SetInputAsHandled();
        }
    }
}
