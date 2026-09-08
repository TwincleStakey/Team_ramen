# 엑셀 매크로 규칙 기반 주문 시스템

## Unity 배치

- `DialogueScenarioModels.cs`, `DialogueWorkbookDatabase.cs`,
  `DialogueScenarioGenerator.cs`, `DialogueScenarioDebugTester.cs`를 Scripts 폴더에 넣습니다.
- `Resources/DialogueDB.json`은 Unity 프로젝트의
  `Assets/Resources/DialogueDB.json`에 넣습니다.
- 빈 GameObject에 `DialogueScenarioGenerator`를 추가합니다.
- 테스트용 GameObject에 `DialogueScenarioDebugTester`를 추가하고 Generator를 연결합니다.

## 생성 진입점

```csharp
DialogueScenario scenario = generator.GenerateScenario(currentDay);

string customerDialogue = scenario.Dialogue;
CustomerOrder order = scenario.order;
Dictionary<IngredientType, int> answerRecipe = scenario.targetRecipe;
```

`CustomerOrder.requests`에는 실제 레시피 변화량이 들어갑니다.

- 추가: `+1 ~ +3`
- 완전 제거: `-기본 수량`이므로 최종 수량은 `0`
- 감소: 기본 수량이 `2` 이상일 때만 `-1`

대사에는 정확한 횟수나 단위를 쓰지 않습니다. 내부 수량은 그대로 유지하면서
페르소나별 `amt1`, `amt2`, `amt3` 표현과 제거·감소 어미를 사용합니다.

## 반영한 엑셀 규칙

- 15개 페르소나별 시작·마무리·종결형·연결형 말투
- 난이도 1 직설 / 2 설명 / 3 간접
- 난이도 2 이상에서 라멘 힌트 사용
- 힌트 문장과 충돌하는 재료 변경 차단
- 주문당 재료 변경 1~3종
- 중간 요청 문장 연결형 약 55%, 마지막 요청 종결형
- filler 문장 50%
- 현재 `RamenType`에 존재하는 Shio, Shoyu, Tonkotsu만 생성

Inspector의 Context Menu에서 `주문 시나리오 생성 테스트` 또는
`주문 시나리오 100회 자동 검증`을 실행할 수 있습니다.
