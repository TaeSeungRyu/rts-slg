namespace SanguoSLG.Core.Domain;

/// <summary>
/// 지역(실제 지명 기반). 장수 출신지가 참조하고, 이후 맵의 도시·타일 배치가 적용받는다.
/// <paramref name="Realm"/>은 권역(china/korea/japan).
/// </summary>
public sealed record Region(string Code, string Name, string Realm, string Note);
