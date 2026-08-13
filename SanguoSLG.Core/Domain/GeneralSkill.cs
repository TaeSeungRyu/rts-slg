namespace SanguoSLG.Core.Domain;

/// <summary>장수가 보유한 스킬 하나 — 스킬 코드와 숙련 티어(1~3). 코드 해석은 조립 계층이 한다.</summary>
public sealed record GeneralSkill(string Code, int Tier);
