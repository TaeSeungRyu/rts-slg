namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>스텝에서 일어난 사건. <paramref name="Other"/>는 상대 유닛(있으면).</summary>
public sealed record TickEvent(TickEventKind Kind, UnitId Unit, UnitId? Other);
