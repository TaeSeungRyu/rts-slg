# AI 작업 노트 (working notes)

이 문서는 **AI 어시스턴트가 이 저장소에서 일할 때의 작업 방식·함정·머신 의존 경로**를 담는다.
원래 Claude Code의 사용자 로컬 메모리(`~/.claude/.../memory/`)에만 있던 규칙을, 다른 PC에서
클론해도 동일하게 적용되도록 저장소로 옮긴 것이다. 새 세션·새 환경에서 작업 시작 전 반드시 읽는다.

명령·데이터·설계 규칙은 [CLAUDE.md](../CLAUDE.md)와 `doc/`가 원본이다. 이 문서는 **어떻게 일하는가**에 집중한다.

---

## 1. 작업 방식 (상시 규칙)

### 스크린샷을 찍지 않는다 (2026-08-06~)
`--shot` 캡처, PIL 크롭 확대, 쇼케이스 렌더 모두 하지 않는다. 시각 변경의 검증은
`dotnet build` + `dotnet test` + Godot `--build-solutions` 컴파일 확인까지만 하고,
**알맞은 `.bat`으로 "확인해주세요"라고 안내한 뒤 사용자의 판단을 기다린다.** 결과를 봤다고 주장하지 않는다.
- 씬마다 배치가 다르다: 캠페인 지도는 `run-maptest.bat`(그냥 `run.bat` 아님), 그 외
  `run-movetest.bat`·`run-combattest.bat`·`run-effecttest.bat`·`run-admin.bat`. 안내 전 씬에 맞는 이름을 확인한다.

### 게임 창을 자동 실행하지 않는다
커밋·빌드 후 게임 창을 자동으로 띄우지 않는다. 사용자가 원할 때 `.bat`으로 직접 실행한다.
명시적으로 "띄워줘/실행해줘"라고 할 때만 실행한다.

### 코드 주석은 원칙적으로 쓰지 않는다
설명·요약·구역 배너·사양 중복 주석은 넣지 않는다. **유일한 예외**: *다르게 고치면 깨지는 이유*를
한 줄로 남기는 재발 방지 메모(예: "회전 오브젝트에 비등방 스케일 → 왜곡"). XML 문서 주석(`///`)도
공개 API의 비자명한 계약이 아니면 생략한다. 이름을 잘 짓고 주석을 지운다.
(CLAUDE.md의 "주석은 한국어로"는 *쓸 때의 언어* 규칙이지 작성 의무가 아니다.)

### claude.ai 아티팩트로 발행하지 않는다
표·명감·대시보드 시각화는 claude.ai Artifact 도구로 만들지 않는다(서버 업로드를 사용자가 원치 않음).
대신 `tools/gen_*.py` + 루트 `.bat`(예: `roster.bat`, `admin.bat`)으로 `doc/*.html`을 생성 —
사용자 PC에서만 열리는 완전 오프라인 산출물로 안내한다.

### 색 표현 해석
"톤 낮춰/연하게" = **채도 다운(파스텔·워시드), 밝기는 유지**. 어둡게가 아니다.
색 조정 요청이 모호하면 채도(연하게/쨍하게)와 명도(밝게/어둡게)를 구분해 확인하거나 채도 조정을 기본으로 한다.
- 확정 톤: `inkwash` 수묵담채(채도 0.45·밝기 1.08). `TonePreset.cs`에 프리셋 5종, `--tone=이름`으로 전환.

### 다음 작업은 항상 `doc/plan-roadmap.md`에서
진행 기준은 살아있는 문서 `doc/plan-roadmap.md`다. "다음 작업" 질문엔 그 문서를 먼저 읽고 최상단
미완료 단계를 제안한다. **끼어드는 새 규칙 아이디어는 구현하지 말고** 해당 `design-*` 문서에 `❓`로
기록만 하고 로드맵 순서를 상기시킨다(사용자가 즉시 구현을 원하면 따른다).

### 커밋
완료 단위마다 커밋·푸시한다. Conventional Commits, 본문 한국어, 트레일러:
`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

## 2. UI 품질이 이 프로젝트의 최우선 리스크

사용자는 과거 여러 게임 프로젝트를 **"구성(아키텍처)은 좋았어도 UI가 나빠서" 포기**한 이력이 있다.
그래서 시각/UI 만족은 이 사용자에게 **시스템보다 우선하는 '계속의 관문'**이다.
- "동작한다"가 아니라 **"보기 좋고 손맛이 있다"**까지가 완료 기준이다.
- **시스템 개발을 이유로 비주얼 개선을 미루자고 제안하지 않는다.** 눈에 보이는 수직 슬라이스를 일찍 당기되,
  게임 로직은 Core에 두어 CLAUDE 규칙 #1(Core 순수성)을 지킨다.

---

## 3. 검증 함정 (조용히 통과해버리는 것들)

### 루트 `dotnet build`는 Game을 포함하지 않는다
저장소 루트에서 `dotnet build`를 돌리면 Core·Tests·Sandbox만 빌드된다. **`SanguoSLG.Game`은 빠진다.**
Game 쪽 `.cs`를 건드렸으면 반드시:
```bash
dotnet build SanguoSLG.Game/SanguoSLG.Game.csproj
```
`--build-solutions`가 실패하면 `An EditorPlugin build callback failed`만 찍고 컴파일 오류는 안 보여주므로,
원인은 위 `dotnet build`로 직접 확인한다.

### 고쳐도 증상이 그대로면 "그 코드가 실행되는지"부터 의심하라
수치를 세 번 고쳐도 증상이 같으면, 원인이 값이 아니라 **디스패치 누락**(함수는 있는데 호출부가 없음)일
수 있다. 일괄 치환 스크립트의 삽입이 조용히 실패한 경우가 대표적. **python 일괄 치환은 반드시 치환 성공을
검사**하고(못 찾으면 실패 종료), 가능하면 자동 검사가 되는 Edit 도구를 쓴다. 반복되면 수치를 더 만지지 말고
**런타임 프로브**(좌표를 파일로 기록)로 실측한다.

### 파이썬으로 C# 파일을 편집할 때 개행 이스케이프
bash heredoc 안 파이썬은 `\n`이 실제 개행으로 들어가 C# 문자열 리터럴을 깨뜨린다. **`\\n`으로 쓰거나**,
스크립트 파일(Write 도구)로 저장해 실행하거나, 편집 후 Edit 도구로 바로잡는다.

### Blender는 스크립트가 죽어도 종료 코드 0
`blender --background --python x.py`는 예외·SyntaxError에도 0을 준다. 셸 `&&`/`if`로 성공을 판정하면
모델이 재생성 안 됐는데 "ok"로 넘어간다. 반드시 `--python-exit-code 1`을 붙인다.
- 헤드리스 프로브 요령: 마커 파일로 켜고(`File.Exists`), 결과는 stdout이 아니라 **파일로** 쓴다
  (`GD.Print`가 헤드리스에서 안 보일 수 있음). 경로는 슬래시(`/`)로.

### 모달 검증 메시지는 모달 안에 띄운다
`CampaignMapScene`의 모달은 전체 화면 백드롭 위에 뜬다. `_log`(하단 상태바)나 `Redraw` 노트에 띄운
검증/실패 메시지는 **모달에 가려 안 보인다.** 모달 안 액션의 실패는 모달 내부 라벨(예: `_depPreview`)에
표시하고, 모달이 닫힌 뒤의 결과만 하단바에 쓴다. 출전 흐름 디버그 로그는 `SanguoSLG.Game/deploy-debug.log`
(`Dbg()`/`res://deploy-debug.log`, `*.log`는 gitignore).

---

## 4. 렌더링·에셋 주의 (작은 스케일 월드)

이 게임의 헥사 타일 외접반경은 **0.577**이라, 미터 스케일을 가정한 Godot 렌더링 기본값이 전부 과하다.
새 렌더링 기능(볼류메트릭·SDFGI·SSR·SSAO·그림자 등)을 켤 때는 **반경·거리·바이어스 성격의 프로퍼티를
전부 찾아 타일 크기 기준으로 다시 잡는다.** 실제로 걸렸던 것: 카메라 Near/Far, `SsaoRadius`(기본 1.0=타일 1.7개),
그림자 Bias/NormalBias/MaxDistance, 얇은 지물의 그림자 acne(`TuneImportedMeshes`가 캐스팅 off).

### Blender 후면 컬링 필수
`bpy.data.materials.new()`는 `use_backface_culling=False`가 기본 → glTF가 `doubleSided:true`로 내보냄 →
Godot에서 z-파이팅 깜빡임. 새 Blender 스크립트의 `make_mat()`에 `m.use_backface_culling = True`를 넣는다.
재생성 불가한 Kenney GLB는 로드 시 `MapView3D.TuneImportedMeshes()`가 `CullMode.Back`을 강제한다.
같은 평면에 법선이 같은 두 면이 겹치면 컬링으로도 안 없어진다 — 높이를 어긋나게 한다(`POST_RISE`).

---

## 5. 머신 의존 경로 (⚠️ 다른 PC에서 클론하면 갱신 필요)

아래는 **이 개발 PC(Windows) 기준** 경로다. 저장소에 담기지 않으므로 다른 환경에선 각자 설치 위치로 바꾼다.

| 도구 | 이 PC 경로 | 용도 |
|---|---|---|
| **Godot 4.7.1 (.NET/mono)** | `D:\godot\Godot_v4.7.1-stable_mono_win64\` (GUI·`_console` 둘 다) | 씬 임포트·헤드리스 빌드·실행. **반드시 mono(.NET) 빌드** — 일반 빌드는 `GodotSharp` 없어 C# 실행 불가 |
| **Blender 5.1.2** | `D:\Blander\blender.exe` (폴더명 오타 "Blander" 주의) | 3D 에셋 제작/편집(헤드리스 `--background --python`) |
| **Fooocus** | `E:/Fooocus_win64_2-5-0` (포터블, 과거 D→E 이동) | 아이콘·초상 이미지 생성 |
| **Kenney 에셋 원본(CC0)** | `D:\dev\assets\kenney\` (Hexagon Kit 3D 72 GLB 등) | 새 모델 통합 시 선별 복사 원본 |

- **`run-*.bat`들이 Godot 경로를 하드코딩**한다(`set "GODOT=D:\godot\..."`). 새 PC에선 각 배치의 그 한 줄을 고친다.
- 이미지 처리는 Fooocus 임베디드 파이썬 대신 **시스템 `python`(Pillow)** 을 쓴다. PIL은 Windows 경로(`C:/`·`D:/`·`E:/`)만 읽고 bash `/tmp`는 못 읽는다.
- 이미 사용 중인 에셋(폰트·아이콘·모델·타일)은 저장소에 커밋돼 있어 **게임 실행에는 위 도구가 불필요**하다.
  위 도구들은 **새 아트를 만들 때만** 필요하다.

---

## 6. 이 문서·메모리 갱신

- 새로 발견한 **작업 방식 함정·선호**는 이 문서에 추가해 커밋한다(다른 PC로 전파되도록).
- 스냅숏성 현황(테스트 개수, 현재 단계 등)은 여기 쓰지 않는다 — 그건 `doc/plan-roadmap.md`가 원본이다.
- 머신 경로가 바뀌면 §5 표를 갱신한다.
