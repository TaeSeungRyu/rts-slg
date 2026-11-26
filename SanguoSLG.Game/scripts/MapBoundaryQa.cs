namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Spatial;

public partial class MapBoundaryQa : Node
{
    public override void _Ready()
    {
        try
        {
            const int width = 20;
            const int height = 20;
            var preset = new ScenarioMapPreset(1, width, height,
                Enumerable.Repeat(new string('P', width), height).ToArray(), []);
            var valid = Enumerable.Range(0, width)
                .SelectMany(column => Enumerable.Range(0, height)
                    .Select(row => preset.ToAxial(column, row))).ToHashSet();
            var map = new HexMap(0, width - 1, valid.Min(tile => tile.R),
                valid.Max(tile => tile.R), validTiles: valid);
            var view = new MapView3D();
            AddChild(view);
            view.Build(map, new HashSet<HexCoord>(), new TileConditionMap());
            if (view.GetChildCount() != map.Count || view.GetChildren().OfType<Node3D>()
                .Any(tile => !map.Contains(view.WorldToHex(tile.Position))))
                throw new InvalidOperationException("맵 바깥 타일이 생성되었습니다.");
            GD.Print($"MAP_BOUNDARY_QA PASS: {map.Count} valid tiles, {view.GetChildCount()} rendered tiles");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
