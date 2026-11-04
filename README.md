# 나의 인생 라멘가게

손님이 돌려 말하는 주문을 해석해서 라멘을 만드는 2D 조리 퍼즐 게임.

- 네오위즈 K 게임 아카데미 2팀 프로젝트 · 2026.09 · 2주
- 시연 영상: https://youtu.be/pF59wLZO8Vk

## 팀 구성

8인 팀이 게임 파트와 트레일러 파트로 나눠 작업했다.

| 파트 | 인원 | 맡은 일 |
|---|---|---|
| 게임 · 프로그래밍 | 2명 | 조리 · 연출 · UI · 에디터 툴 / 손님 주문 · 대사 · 채점 · 정산 |
| 게임 · 기획 · 아트 | 2명 | 기획 · 아트 |
| 트레일러 | 4명 | 3D 시네마틱 트레일러 |

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

## 폴더 구조

| 폴더 | 내용 |
|---|---|
| `Assets/Scripts/Cooking/` | 그릇 · 재료 칸 · 커서 · 조리 배속 · 튜토리얼 표시 |
| `Assets/Scripts/Core/` | 게임 흐름 · 주문/결과 화면 · 시식 · 엔딩 연출 · 저장 · 설정 |
| `Assets/Scripts/Customer/` | 주문 생성 · 대사 · 채점 · 하루 정산 |
| `Assets/Scripts/Editor/` | 씬 빌더 · 크레딧 미리보기 · UI 겹침 검사 |
| `Tools/` | 임시 그림을 찍는 파이썬 스크립트 (사용법은 `Tools/README.md`) |

조리 쪽과 손님 쪽이 만나는 곳은 `OrderManager.EvaluateRamen()` 하나다.
조리가 끝난 그릇 상태를 넘기면 채점 결과(판매가)가 돌아온다.

## 처음 볼 파일

| 파일 | 하는 일 |
|---|---|
| `Core/GameManager.cs` | 손님 한 명을 받는 흐름 전체 |
| `Customer/OrderManager.cs` | 주문 관리와 채점 연결 |
| `Cooking/Bowl.cs` | 담는 순서와 개수를 그릇이 직접 확인하는 조리 판정 |
| `Editor/RamenLayoutBuilder.cs` | 좌표 · 크기 상수로 조리 화면 전체를 만드는 빌더 |
| `Cooking/CookTempo.cs` | `Time.timeScale`과 따로 도는 조리대 전용 배속 |
| `Core/CanvasPoint.cs` | Canvas 렌더 모드와 상관없이 마우스 좌표를 바꾸는 곳 |

## 협업 규칙

- 공용 타입은 한 사람이 먼저 만들어 올린다. asmdef가 없어서 같은 이름의 타입이 두 폴더에 생기면 프로젝트 전체가 컴파일에 실패한다.
- 씬 · 프리팹 · 에셋은 자동 병합을 껐다 (`.gitattributes`의 `-merge`). 줄 단위로 섞이면 참조가 조용히 깨지기 때문에, 겹치면 바로 충돌이 나게 했다.
- 코드는 폴더 단위로 담당을 나눴다.

## 알려진 문제

- 손님 소감 뒤 ▶ 버튼을 시식 연출 중에 누르면 결과창이 뜨지 않는다. 마지막 대사에서 부르는 `OrderScreenUI.Close()`가 연출 코루틴이 붙은 오브젝트를 꺼 버리기 때문이다. 제출 빌드에는 고치지 않았다.
