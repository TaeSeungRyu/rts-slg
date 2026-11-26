namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record MerchantVisit(CityId City, HexCoord Position, int ArrivedDay,
    int ExpiresDay, int BuyGrainPercent, int SellGrainPercent,
    IReadOnlyList<string> ArtifactCodes);
