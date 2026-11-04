# 나의 인생 라멘가게

손님이 돌려 말하는 주문을 해석해서 라멘을 만드는 2D 조리 퍼즐 게임이다.

- 네오위즈 K 게임 아카데미 팀 프로젝트 · 2026.09 · 2주
- 8인 팀 중 게임 파트 4명 (프로그래밍 2명) · 나머지 4명은 3D 트레일러 제작
- 메인 개발: 김기백 — 조리 판정, 연출, UI 전체, 씬 빌더
- 포트폴리오 상세: https://twinclestakey.github.io/#ramen
- 시연 영상: https://youtu.be/pF59wLZO8Vk

## 실행

| 항목 | 내용 |
|---|---|
| 엔진 | Unity 6000.3.18f1 · URP |
| 패키지 | Input System · uGUI · TextMesh Pro |
| 씬 | `Assets/Scenes/MainScene.unity` 하나로 전부 돌아간다 |

1. Unity Hub에서 이 폴더를 연다.
2. `MainScene`을 열고 Play.

조리 화면 UI는 손으로 배치하지 않고 에디터 스크립트로 만든다.
화면 배치를 바꿨다면 메뉴의 `RamenLayoutBuilder`를 다시 실행해야 씬에 반영된다.

## 폴더와 담당

| 폴더 | 내용 | 담당 |
|---|---|---|
| `Assets/Scripts/Cooking/` | 그릇 · 재료 칸 · 커서 · 조리 배속 · 튜토리얼 표시 | 김기백 |
| `Assets/Scripts/Core/` | 게임 흐름 · 주문/결과 화면 · 시식 · 엔딩 연출 · 저장 · 설정 | 김기백 |
| `Assets/Scripts/Editor/` | 씬 빌더 · 크레딧 미리보기 · UI 겹침 검사 | 김기백 |
| `Assets/Scripts/Customer/` | 주문 생성 · 대사 · 채점 · 하루 정산 | 팀원 |
| `Tools/` | 임시 그림을 찍는 파이썬 스크립트 | 김기백 |

두 사람이 만나는 곳은 `OrderManager.EvaluateRamen()` 하나다.
조리가 끝난 그릇 상태를 넘기면 채점 결과(판매가)가 돌아온다.

## 먼저 볼 파일

| 파일 | 보면 알 수 있는 것 |
|---|---|
| `Cooking/Bowl.cs` | 담는 순서와 개수를 그릇이 직접 확인하는 조리 판정 |
| `Editor/RamenLayoutBuilder.cs` | 좌표 · 크기 상수 한 곳에서 조리 화면 전체를 만드는 빌더 |
| `Cooking/CookTempo.cs` | `Time.timeScale`과 따로 도는 조리대 전용 배속 |
| `Core/GameManager.cs` | 손님 한 명을 받는 흐름, 채점 연결 |
| `Core/CanvasPoint.cs` | Canvas 렌더 모드와 상관없이 마우스 좌표를 바꾸는 한 곳 |
| `Core/CreditsSequence.cs` · `Core/IrisFade.cs` | 엔딩 크레딧과 아이리스 연출 |
| `Core/OrderScreenUI.cs` | 주문 대사와 ▶ 버튼 처리 |

## 고친 문제

자세한 과정은 포트폴리오에 정리했다.

| 문제 | 상태 | 관련 파일 |
|---|---|---|
| 끝까지 재생되지 않는 엔딩 크레딧 | 적용 · 녹화로 확인 | `CreditsSequence.cs` · `IrisFade.cs` |
| 커서 실종, 둘째 손님부터 조작 불가 | 적용 · 확인 완료 | `CanvasPoint.cs` · `CookingCursor.cs` |
| 한 번의 클릭, 두 번의 처리 | ① 타자기 적용 · ② ▶ 버튼은 원인 확인 · 수정안만 (제출 빌드 미반영) | `OpeningNarration.cs` · `OrderScreenUI.cs` · `EatingCutscene.cs` |

## 협업 규칙

- 공용 타입은 한 사람이 먼저 만들어 올린다. asmdef가 없어서 같은 이름의 타입이 두 폴더에 생기면 프로젝트 전체가 컴파일에 실패한다.
- 씬 · 프리팹 · 에셋은 자동 병합을 껐다 (`.gitattributes`의 `-merge`). 줄 단위로 섞이면 참조가 조용히 깨지기 때문에, 겹치면 바로 충돌이 나게 했다.
