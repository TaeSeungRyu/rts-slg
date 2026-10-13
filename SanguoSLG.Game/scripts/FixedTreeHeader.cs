namespace SanguoSLG.Game;

using Godot;

public sealed partial class FixedTreeHeader : Control
{
    public const float HeaderHeight = 28f;

    public Tree Table { get; init; } = null!;
    public Font HeaderFont { get; init; } = null!;
    public Color TextColor { get; init; } = Colors.White;

    private Label[] _labels = [];

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, HeaderHeight);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.Fill;
        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
        _labels = new Label[Table.Columns];
        for (var column = 0; column < Table.Columns; column++)
        {
            var label = new Label
            {
                Text = Table.GetColumnTitle(column),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontOverride("font", HeaderFont);
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", TextColor);
            AddChild(label);
            _labels[column] = label;
        }
        UpdateColumns();
    }

    public override void _Process(double delta) => UpdateColumns();

    private void UpdateColumns()
    {
        if (!GodotObject.IsInstanceValid(Table) || !Table.IsInsideTree()) return;
        var offset = Table.GetThemeStylebox("panel").GetOffset().X - Table.GetScroll().X;
        for (var column = 0; column < _labels.Length; column++)
        {
            var width = Table.GetColumnWidth(column);
            var position = new Vector2(offset, 0);
            var size = new Vector2(width, HeaderHeight);
            if (!_labels[column].Position.IsEqualApprox(position)) _labels[column].Position = position;
            if (!_labels[column].Size.IsEqualApprox(size)) _labels[column].Size = size;
            offset += width;
        }
    }
}
