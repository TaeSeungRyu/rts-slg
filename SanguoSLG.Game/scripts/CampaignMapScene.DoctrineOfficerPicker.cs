using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    partial void OpenDoctrineOfficerPicker(CityId city, int cmdIndex)
    {
        CloseDoctrinePicker();
        var command = Cmds[cmdIndex];
        var selected = _doctrineOfficer;
        var query = "";
        var sortColumn = command.Kind == CommandKind.AppointRecruitmentOfficer ? 2 : 3;
        var ascending = false;
        var layer = CreateDoctrinePickerLayer("수행 장수 선택", out var body, out var close);
        _doctrinePickerLayer = layer;
        close.Pressed += CloseDoctrinePicker;

        var search = new LineEdit
        {
            Name = "DoctrineOfficerSearch",
            PlaceholderText = "장수명 검색",
            ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(0f, 38f),
        };
        body.AddChild(search);
        var tree = new Tree
        {
            Name = "DoctrineOfficerTable",
            Columns = 6,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0f, 450f),
            ScrollVerticalEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        tree.AddThemeFontOverride("font", _font);
        tree.AddThemeFontSizeOverride("font_size", 15);
        tree.AddThemeFontOverride("title_button_font", _font);
        tree.AddThemeFontSizeOverride("title_button_font_size", 14);
        tree.AddThemeConstantOverride("item_margin", 7);
        tree.SetColumnTitle(0, "");
        tree.SetColumnExpand(0, false);
        tree.SetColumnCustomMinimumWidth(0, 56);
        tree.SetColumnTitle(1, "이름");
        tree.SetColumnExpand(1, true);
        tree.SetColumnExpandRatio(1, 2);
        foreach (var (column, title) in new[] { (2, "무력"), (3, "지력"), (4, "정치") })
        {
            tree.SetColumnTitle(column, title);
            tree.SetColumnExpand(column, false);
            tree.SetColumnCustomMinimumWidth(column, 76);
        }
        tree.SetColumnTitle(5, "현재 담당 업무");
        tree.SetColumnExpand(5, true);
        tree.SetColumnExpandRatio(5, 3);
        body.AddChild(tree);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 8);
        var cancel = MakeButton("취소");
        cancel.Pressed += CloseDoctrinePicker;
        actions.AddChild(cancel);
        var apply = MakeButton("적용", true);
        apply.Name = "DoctrineOfficerApply";
        apply.Disabled = selected is null;
        apply.Pressed += () =>
        {
            if (selected is null) return;
            _doctrineOfficer = selected;
            CloseDoctrinePicker();
            var selectedCity = _state.Cities.First(candidate => candidate.Id == city);
            RefreshIntegratedCommandWorkspace(selectedCity);
        };
        actions.AddChild(apply);
        body.AddChild(actions);

        void Rebuild()
        {
            tree.Clear();
            var root = tree.CreateItem();
            var appointment = command.Kind == CommandKind.AppointRecruitmentOfficer;
            IEnumerable<General> generals = _state.GeneralsAt(city)
                .Where(id => appointment || !OfficerUnavailable(id) && !_state.IsGeneralInField(id))
                .Select(id => _state.Generals.First(general => general.Id == id))
                .Where(general => string.IsNullOrWhiteSpace(query)
                    || general.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase));
            generals = sortColumn switch
            {
                1 => ascending ? generals.OrderBy(general => general.Name) : generals.OrderByDescending(general => general.Name),
                2 => ascending ? generals.OrderBy(general => general.Might) : generals.OrderByDescending(general => general.Might),
                4 => ascending ? generals.OrderBy(general => general.Politics) : generals.OrderByDescending(general => general.Politics),
                5 => ascending ? generals.OrderBy(general => CurrentDuty(general.Id)) : generals.OrderByDescending(general => CurrentDuty(general.Id)),
                _ => ascending ? generals.OrderBy(general => general.Intellect) : generals.OrderByDescending(general => general.Intellect),
            };
            var rowIndex = 0;
            foreach (var general in generals)
            {
                var item = tree.CreateItem(root);
                ApplyGeneralTreePortrait(item, 0, general.Id, 42, true);
                item.SetText(1, general.Name);
                item.SetCustomColor(1, GoldBright);
                item.SetText(2, general.Might.ToString());
                item.SetText(3, general.Intellect.ToString());
                item.SetText(4, general.Politics.ToString());
                item.SetText(5, CurrentDuty(general.Id));
                item.SetTooltipText(5, CurrentDuty(general.Id));
                item.SetMetadata(0, general.Id.Value);
                item.SetTextAlignment(0, HorizontalAlignment.Center);
                for (var column = 2; column <= 4; column++) item.SetTextAlignment(column, HorizontalAlignment.Center);
                if (rowIndex++ % 2 == 1)
                {
                    for (var column = 0; column < tree.Columns; column++)
                        item.SetCustomBgColor(column, new Color(Gold, 0.055f));
                }
                if (selected == general.Id) tree.SetSelected(item, 1);
            }
        }

        tree.ItemSelected += () =>
        {
            var item = tree.GetSelected();
            if (item is null) return;
            selected = new GeneralId(item.GetMetadata(0).AsInt32());
            apply.Disabled = false;
        };
        tree.ColumnTitleClicked += (column, _) =>
        {
            var next = (int)column;
            if (next == 0) return;
            if (sortColumn == next) ascending = !ascending;
            else { sortColumn = next; ascending = next == 1; }
            Rebuild();
        };
        search.TextChanged += value => { query = value.Trim(); Rebuild(); };
        Rebuild();
    }
}
