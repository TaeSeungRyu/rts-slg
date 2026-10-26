namespace SanguoSLG.Core.Data;

public static class ScenarioMapPresetResizer
{
    public static ScenarioMapPreset Resize(ScenarioMapPreset source, int width, int height)
    {
        source.Validate();
        if (source.Width == width && source.Height == height) return source.Snapshot();
        if (width < source.Width || height < source.Height)
            throw new ArgumentOutOfRangeException(nameof(width));

        var rows = Enumerable.Range(0, height).Select(row => new string(
            Enumerable.Range(0, width).Select(column => source.Rows[
                Math.Min(source.Height - 1, (int)((row + 0.5) * source.Height / height))][
                Math.Min(source.Width - 1, (int)((column + 0.5) * source.Width / width))])
            .ToArray())).ToArray();
        var sites = new List<MapEditorSite>();
        foreach (var site in source.Sites.OrderBy(site => site.Kind == "city" ? 0
                     : site.Kind == "port" ? 1 : site.Kind == "landmark" ? 2 : 3))
        {
            var x = Math.Clamp((int)Math.Round((site.Column + 0.5) * width / source.Width - 0.5), 0, width - 1);
            var y = Math.Clamp((int)Math.Round((site.Row + 0.5) * height / source.Height - 0.5), 0, height - 1);
            MapEditorSite? placed = null;
            for (var radius = 0; radius <= 12 && placed is null; radius++)
            for (var dy = -radius; dy <= radius && placed is null; dy++)
            for (var dx = -radius; dx <= radius && placed is null; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                var candidate = site with { Column = x + dx, Row = y + dy };
                if (candidate.Column < 0 || candidate.Column >= width
                    || candidate.Row < 0 || candidate.Row >= height) continue;
                if (candidate.Kind == "city" && sites.Any(existing => existing.Kind == "city"
                    && HexDistance(candidate, existing) < 6)) continue;
                try
                {
                    new ScenarioMapPreset(source.Version, width, height, rows,
                        sites.Append(candidate).ToArray()).Validate();
                    placed = candidate;
                }
                catch (InvalidDataException) { }
            }
            if (placed is null)
                throw new InvalidDataException($"확대된 지도에 {site.Name} 배치가 불가능합니다.");
            sites.Add(placed);
        }
        return new ScenarioMapPreset(source.Version, width, height, rows, sites).Snapshot();
    }

    private static int HexDistance(MapEditorSite left, MapEditorSite right)
    {
        var dq = left.Column - right.Column;
        var leftR = left.Row - (left.Column - (left.Column & 1)) / 2;
        var rightR = right.Row - (right.Column - (right.Column & 1)) / 2;
        var dr = leftR - rightR;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }
}
