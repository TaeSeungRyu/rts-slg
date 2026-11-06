using Godot;

namespace SanguoSLG.Game;

public partial class ImeInputTestScene : Control
{
    private LineEdit _line = null!;
    private TextEdit _multiline = null!;
    private Label _state = null!;
    private Label _events = null!;
    private int _lineChanges;
    private int _multilineChanges;

    public override void _Ready()
    {
        GD.Print($"[ime-environment] godot={Engine.GetVersionInfo()["string"]} os={OS.GetName()} display={DisplayServer.GetName()} renderer={RenderingServer.GetVideoAdapterName()} window_mode={GetWindow().Mode}");
        var layout = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 24,
            OffsetTop = 24,
            OffsetRight = -24,
            OffsetBottom = -24
        };
        AddChild(layout);
        layout.AddChild(new Label { Text = "Godot 기본 한글 입력 검사: 안녕하세요 → 공백 → 백스페이스 → 방향키 → 복사·붙여넣기 → Tab" });
        layout.AddChild(new Label { Text = "LineEdit" });
        _line = new LineEdit { PlaceholderText = "여기에 한글을 입력하세요" };
        layout.AddChild(_line);
        layout.AddChild(new Label { Text = "TextEdit: 여러 줄과 Enter를 확인하세요" });
        _multiline = new TextEdit { CustomMinimumSize = new Vector2(0, 150) };
        layout.AddChild(_multiline);
        _state = new Label();
        layout.AddChild(_state);
        _events = new Label();
        layout.AddChild(_events);
        _line.TextChanged += value =>
        {
            _lineChanges++;
            _events.Text = $"LineEdit TextChanged #{_lineChanges}: [{value}]";
            GD.Print($"[ime-line] #{_lineChanges} text=[{value}] caret={_line.CaretColumn} composing={_line.HasImeText()} selection={_line.HasSelection()}");
        };
        _multiline.TextChanged += () =>
        {
            _multilineChanges++;
            _events.Text = $"TextEdit TextChanged #{_multilineChanges}: [{_multiline.Text.Replace("\n", "\\n")}]";
            GD.Print($"[ime-text] #{_multilineChanges} text=[{_multiline.Text.Replace("\n", "\\n")}] caret={_multiline.GetCaretLine()}:{_multiline.GetCaretColumn()} composing={_multiline.HasImeText()} selection={_multiline.HasSelection()}");
        };
    }

    public override void _Process(double delta)
    {
        var focus = GetViewport().GuiGetFocusOwner();
        var lineSelection = _line.HasSelection()
            ? $"{_line.GetSelectionFromColumn()}-{_line.GetSelectionToColumn()}" : "없음";
        var multilineSelection = _multiline.HasSelection()
            ? $"{_multiline.GetSelectionFromLine()}:{_multiline.GetSelectionFromColumn()}-" +
              $"{_multiline.GetSelectionToLine()}:{_multiline.GetSelectionToColumn()}" : "없음";
        _state.Text = $"Focus: {focus?.Name ?? "없음"} | " +
            $"LineEdit caret: {_line.CaretColumn}, composing: {_line.HasImeText()}, selection: {lineSelection} | " +
            $"TextEdit caret: {_multiline.GetCaretLine()}:{_multiline.GetCaretColumn()}, composing: {_multiline.HasImeText()}, selection: {multilineSelection}";
    }
}
