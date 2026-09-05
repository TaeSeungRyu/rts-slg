namespace SanguoSLG.Core.Domain;

/// <summary>
/// 포로가 된 장수(design-general-lifecycle §2). 함락 시 주둔 장수 일부나 등용 실패 수행 장수가
/// 포로가 된다. 억류 세력(<paramref name="Holder"/>)이 붙잡고, 원(元) 세력(<paramref name="Origin"/>)을
/// 기억한다 — 원 세력 소멸 처리의 기준.
/// 포로는 어느 세력에도 배속(GeneralPosting)돼 있지 않다. 불변 값.
/// </summary>
public sealed record Prisoner(GeneralId General, FactionId Holder, FactionId Origin);
