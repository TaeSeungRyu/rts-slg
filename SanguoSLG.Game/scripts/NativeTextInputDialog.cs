using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace SanguoSLG.Game;

public partial class NativeTextInputDialog : Node
{
    private static int _dialogOpen;
    private Control _field = null!;
    private string _title = "텍스트 입력";
    private volatile bool _busy;
    private readonly object _pendingLock = new();
    private string? _pendingText;

    public static void Attach(LineEdit field)
    {
        if (OS.GetName() != "Windows")
            return;
        field.Editable = false;
        field.TooltipText = "클릭하여 텍스트를 입력합니다.";
        var dialog = new NativeTextInputDialog { _field = field };
        field.AddChild(dialog);
        field.GuiInput += dialog.OnInput;
    }

    public static void Attach(TextEdit field, string title = "텍스트 입력")
    {
        if (OS.GetName() != "Windows")
            return;
        field.Editable = false;
        field.TooltipText = "클릭하여 텍스트를 입력합니다.";
        var dialog = new NativeTextInputDialog { _field = field, _title = title };
        field.AddChild(dialog);
        field.GuiInput += dialog.OnInput;
    }

    private void OnInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } || _busy)
            return;
        _field.AcceptEvent();
        if (Interlocked.CompareExchange(ref _dialogOpen, 1, 0) != 0)
            return;
        _busy = true;
        var current = _field is LineEdit line ? line.Text : ((TextEdit)_field).Text;
        var multiline = _field is TextEdit;
        var root = ProjectSettings.GlobalizePath("res://");
        var project = Path.GetFullPath(Path.Combine(root, "..", "tools", "ime-native-probe", "ImeNativeProbe.csproj"));
        _ = Task.Run(() => RunDialog(project, current, multiline, System.Environment.ProcessId, _title));
    }

    private void RunDialog(string project, string current, bool multiline, int parentProcessId, string title)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"sanguo-ime-{Guid.NewGuid():N}.txt");
        try
        {
            var executable = Path.Combine(Path.GetDirectoryName(project)!, "bin", "Release", "net9.0-windows", "ImeNativeProbe.exe");
            if (!File.Exists(executable))
            {
                var build = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
                build.ArgumentList.Add("build");
                build.ArgumentList.Add(project);
                build.ArgumentList.Add("--configuration");
                build.ArgumentList.Add("Release");
                build.ArgumentList.Add("--verbosity");
                build.ArgumentList.Add("quiet");
                using var compiler = Process.Start(build);
                compiler?.WaitForExit();
                if (compiler?.ExitCode != 0 || !File.Exists(executable))
                    throw new InvalidOperationException("Windows 입력 도구 빌드에 실패했습니다.");
            }
            var start = new ProcessStartInfo(executable) { UseShellExecute = true };
            start.ArgumentList.Add("--edit");
            start.ArgumentList.Add(multiline ? "multi" : "single");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(current)));
            start.ArgumentList.Add(outputPath);
            start.ArgumentList.Add("--parent");
            start.ArgumentList.Add(parentProcessId.ToString());
            start.ArgumentList.Add("--title");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(title)));
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Windows 입력 도구를 시작하지 못했습니다.");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Windows 입력 도구가 오류 코드 {process.ExitCode}로 종료했습니다.");
            if (File.Exists(outputPath))
                lock (_pendingLock)
                    _pendingText = File.ReadAllText(outputPath, Encoding.UTF8);
        }
        catch (Exception error)
        {
            lock (_pendingLock)
                _pendingError = error.Message;
        }
        finally
        {
            try
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
            catch (IOException) { }
            _busy = false;
            Interlocked.Exchange(ref _dialogOpen, 0);
        }
    }

    public override void _Process(double delta)
    {
        string? text;
        string? error;
        lock (_pendingLock)
        {
            text = _pendingText;
            _pendingText = null;
            error = _pendingError;
            _pendingError = null;
        }
        if (error is not null)
            GD.PushError($"Windows 텍스트 입력창을 열지 못했습니다: {error}");
        if (text is null || !GodotObject.IsInstanceValid(_field))
            return;
        ApplyResult(_field, text);
    }

    public static void ApplyResult(Control field, string text)
    {
        if (field is LineEdit line)
        {
            line.Text = text;
            line.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }
        else if (field is TextEdit editor)
        {
            editor.Text = text;
            editor.EmitSignal(TextEdit.SignalName.TextChanged);
        }
    }

    private string? _pendingError;
}
