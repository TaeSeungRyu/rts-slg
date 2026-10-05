namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>성·항구·건축물 등 소유 세력과 무관한 논리 장애물.</summary>
public sealed record RenewalStaticObstacle(string Id, ContinuousPosition Center, long Radius);
