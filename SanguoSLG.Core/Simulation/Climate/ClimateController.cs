namespace SanguoSLG.Core.Simulation.Climate;

using SanguoSLG.Core.Spatial;

public sealed class ClimateController(HexMap map, int patchChancePercent = 12,
    int maxPatches = 2, int patchSize = 6, int moveEveryDays = 2,
    int winterSnowPercent = 60, int minDurationDays = 3, int maxDurationDays = 6)
{
    public ClimateState Advance(ClimateState state, int day, int month)
    {
        if (day < state.Day) throw new ArgumentOutOfRangeException(nameof(day));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (day == state.Day) return state;

        var winter = month is 12 or 1 or 2;
        var patches = new List<ClimatePatch>();
        foreach (var patch in state.Patches.Where(patch => patch.EndDay >= day
                     && (patch.Kind != ClimateKind.Snow || winter)).OrderBy(patch => patch.Id))
        {
            var candidate = day > patch.StartDay && (day - patch.StartDay) % Math.Max(1, moveEveryDays) == 0
                ? patch with { Origin = Shift(patch.Origin, patch.Id, day) }
                : patch;
            patches.Add(patches.Any(existing => Tiles(existing).Intersect(Tiles(candidate)).Any())
                ? patch : candidate);
        }
        var nextId = state.NextPatchId;
        if (patches.Count < maxPatches && Roll(state.Seed, day, 1) % 100 < patchChancePercent)
        {
            var kind = winter && Roll(state.Seed, day, 2) % 100 < winterSnowPercent
                ? ClimateKind.Snow : ClimateKind.Rain;
            var spanQ = Math.Max(1, map.MaxQ - map.MinQ - patchSize + 2);
            var spanR = Math.Max(1, map.MaxR - map.MinR - patchSize + 2);
            var origin = new HexCoord(map.MinQ + (int)(Roll(state.Seed, day, 3) % (uint)spanQ),
                map.MinR + (int)(Roll(state.Seed, day, 4) % (uint)spanR));
            var durationRange = Math.Max(1, maxDurationDays - minDurationDays + 1);
            var candidate = new ClimatePatch(nextId, kind, origin, day,
                day + Math.Max(1, minDurationDays) - 1
                    + (int)(Roll(state.Seed, day, 5) % (uint)durationRange));
            if (Tiles(candidate).Any() && patches.All(existing =>
                    !Tiles(existing).Intersect(Tiles(candidate)).Any()))
            {
                patches.Add(candidate);
                nextId++;
            }
        }
        return new ClimateState(day, state.Seed, nextId, patches);
    }

    public ClimateKind At(ClimateState state, HexCoord tile) =>
        state.Patches.OrderBy(patch => patch.Id)
            .FirstOrDefault(patch => Tiles(patch).Contains(tile))?.Kind ?? ClimateKind.Clear;

    public IEnumerable<HexCoord> Tiles(ClimatePatch patch)
    {
        for (var q = patch.Origin.Q; q < patch.Origin.Q + patchSize; q++)
            for (var r = patch.Origin.R; r < patch.Origin.R + patchSize; r++)
            {
                var tile = new HexCoord(q, r);
                if (map.Contains(tile)) yield return tile;
            }
    }

    public ClimateState AddTestPatch(ClimateState state, ClimateKind kind, HexCoord origin,
        int durationDays = 5)
    {
        if (kind == ClimateKind.Clear) return state;
        var candidate = new ClimatePatch(state.NextPatchId, kind, origin, state.Day,
            state.Day + durationDays - 1);
        return Tiles(candidate).Any() && state.Patches.All(existing =>
                !Tiles(existing).Intersect(Tiles(candidate)).Any())
            ? state with { NextPatchId = state.NextPatchId + 1,
                Patches = state.Patches.Append(candidate).ToList() }
            : state;
    }

    private HexCoord Shift(HexCoord origin, int id, int day)
    {
        var direction = (id + day / 2) % 4;
        var shifted = direction switch
        {
            0 => origin + new HexCoord(1, 0),
            1 => origin + new HexCoord(0, 1),
            2 => origin + new HexCoord(-1, 0),
            _ => origin + new HexCoord(0, -1),
        };
        return new HexCoord(Math.Clamp(shifted.Q, map.MinQ,
                Math.Max(map.MinQ, map.MaxQ - patchSize + 1)),
            Math.Clamp(shifted.R, map.MinR,
                Math.Max(map.MinR, map.MaxR - patchSize + 1)));
    }

    private static uint Roll(int seed, int day, int salt)
    {
        var value = unchecked((uint)seed ^ ((uint)day * 0x9e3779b9u) ^ ((uint)salt * 0x85ebca6bu));
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        return value ^ (value >> 16);
    }
}
