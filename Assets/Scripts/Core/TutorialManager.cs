using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시작 튜토리얼. 기획서 v1.2 9장.
///
/// 시오 기본 레시피 한 그릇을 안내대로 따라 만들게 한다. 안내된 재료 말고는 아예 집히지 않고,
/// 다 넣기 전에는 제출도 막힌다. 그래서 여기서는 오답이 나올 수 없다.
/// (기획 9.2 의 "오답 제출 시 이유를 알려준 뒤 재시도"는 이 방식에서는 지나갈 일이 없다.)
///
/// 기획 9.1 은 시오·쇼유·돈코츠 세 건이지만 시오 한 건만 한다.
/// 늘릴 일이 생기면 <see cref="Steps"/> 표만 손보면 된다.
///
/// 튜토리얼 손님은 먹고 그냥 간다. 그래서 지급액도 통계도 남지 않는다
/// (기획 9.2 — 손님 수·운영시간·누적 매출·평균 정확도에서 모두 제외).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [SerializeField]
    [Tooltip("끄면 튜토리얼을 건너뛰고 곧장 1일차로 간다. 조리 화면을 고칠 때 켜 두면 매번 한 판을 해야 한다.")]
    private bool enableTutorial = true;

    /// <summary>튜토리얼에서 만들 라멘.</summary>
    private const RamenType Menu = RamenType.Shio;

    /// <summary>
    /// 넣는 차례. 시오 기본 레시피를 그대로 편 것이고, 같은 재료가 여러 개면 그만큼 줄이 반복된다.
    ///
    /// 앞의 셋은 순서를 바꿀 수 없다. Bowl 이 타래 → 육수 → 면 말고는 받지 않는다.
    /// 뒤의 토핑은 아무 순서나 되지만 레시피에 적힌 차례를 그대로 쓴다.
    /// </summary>
    private static readonly IngredientType[] Steps =
    {
        IngredientType.ShioTare,
        IngredientType.Broth,
        IngredientType.ThinNoodles,
        IngredientType.Chashu,
        IngredientType.Menma,
        IngredientType.Menma,
        IngredientType.GreenOnion,
        IngredientType.FlavorOil,
    };

    /// <summary>
    /// 튜토리얼이 지금 무엇을 기다리는가.
    ///
    /// 재료를 넣기 전에 주문서와 레시피북을 한 번씩 열어 보게 한다. 이 둘은 게임 내내 쓰는
    /// 기능인데 화면 어디에도 단서가 없어서, 안 가르치면 있는 줄도 모르고 끝난다.
    /// </summary>
    private enum Phase
    {
        OrderNote,    // Tab 으로 주문서 열어 보기
        RecipeBook,   // B 로 레시피북 열어 보기
        Ready,        // "이제 만들어 봅시다" 한 박자
        Cooking,      // 재료 8 단계
        Submit,       // 제출
    }

    private Phase phase = Phase.OrderNote;

    /// <summary>
    /// Ready 를 몇 초 더 보여 주고 넘어가는가.
    /// 글이 다 찍힌 뒤부터 센다. 찍히는 동안을 포함해 재면 긴 문장일수록 읽을 틈이 줄어든다.
    /// </summary>
    private const float ReadySeconds = 2f;

    /// <summary>안내가 안 떠도 이만큼 지나면 넘어간다.</summary>
    private const float ReadyMaxWait = 12f;

    private float readyUntil;

    /// <summary>안내가 안 떠도 이 시각이 지나면 그냥 넘어간다. 갇히지 않게 하는 마지막 빗장이다.</summary>
    private float readyDeadline;

    /// <summary>주문서·레시피북을 한 번이라도 열었는가. 열었다가 닫아야 다음으로 넘어간다.</summary>
    private bool noteOpened;
    private bool bookOpened;

    private OrderNoteUI note;
    private RecipeBookUI book;

    /// <summary>지금 몇 번째 재료를 기다리는가. Steps.Length 가 되면 남은 일은 제출뿐이다.</summary>
    private int step;

    /// <summary>튜토리얼이 끝났는가. 끝나야 본편 1일차가 열리고 저장도 시작된다.</summary>
    public bool IsDone { get; private set; }

    /// <summary>지금 튜토리얼을 진행 중인가.</summary>
    public bool IsRunning => enableTutorial && !IsDone;

    /// <summary>
    /// 지금 안내 중인 재료. 재료를 받을 차례가 아니면 null 이다.
    ///
    /// 주문서·레시피북 단계에서 null 이 되는 것이 그대로 잠금이 된다. CanPick 이
    /// "지금 차례인 재료인가" 를 묻는데, 차례가 없으면 어떤 재료도 통과하지 못한다.
    /// </summary>
    public IngredientType? CurrentStep
        => IsRunning && phase == Phase.Cooking && step < Steps.Length
            ? Steps[step]
            : (IngredientType?)null;

    /// <summary>재료를 다 넣어 제출만 남았는가.</summary>
    public bool ReadyToSubmit => IsRunning && phase == Phase.Submit;

    /// <summary>
    /// 지금 화면에서 가리키고 있는 것이 있는가. 어두운 판을 켤지 정하는 기준이다.
    ///
    /// "이제 만들어 봅시다" 한 박자(Ready)에는 가리킬 것이 없다. 그때도 어둡게 하면
    /// 아무것도 빛나지 않는 캄캄한 화면이 되어 무엇을 하라는 것인지 알 수 없다.
    /// </summary>
    public bool HasTarget => IsRunning && phase != Phase.Ready;

    /// <summary>Tab 으로 주문서를 열어 보라고 하는 중인가.</summary>
    public bool WaitingForTab => IsRunning && phase == Phase.OrderNote;

    /// <summary>B 로 레시피북을 열어 보라고 하는 중인가.</summary>
    public bool WaitingForBook => IsRunning && phase == Phase.RecipeBook;

    /// <summary>
    /// 지금 차례가 바로 앞과 같은 재료인가. 멘마처럼 기본이 2개인 재료에서 참이 된다.
    /// 안내 문구를 "한 번 더" 로 바꾸는 데 쓴다. 같은 통을 두 번 가리키면
    /// 넣은 것인지 아닌지 헷갈리기 때문이다.
    /// </summary>
    public bool IsRepeatStep
        => CurrentStep != null && step > 0 && Steps[step] == Steps[step - 1];

    /// <summary>
    /// 그릇 위에 띄울 안내. 줄바꿈이 들어 있고, 띄울 것이 없으면 null 이다.
    /// 문구를 여기 모아 둔다 — 단계를 아는 쪽이 여기라서, 나누면 두 곳이 어긋난다.
    /// </summary>
    public string PromptText
    {
        get
        {
            if (!IsRunning) return null;

            switch (phase)
            {
                case Phase.OrderNote:
                    return "시오라멘 주문이 들어왔네요.\nTab 키를 꾹 눌러 주문서를 확인해보세요.";

                case Phase.RecipeBook:
                    return "기본 레시피를 확인해볼까요?\nB 키를 꾹 눌러 레시피북을 확인하세요.";

                case Phase.Ready:
                    return "레시피북을 참고해서 만들어볼까요?\n빛나는 재료를 그릇에 넣어주세요.";

                case Phase.Submit:
                    return "완성됐어요.\n위쪽 마무리 버튼을 눌러 손님에게 내주세요.";
            }

            IngredientType? ingredient = CurrentStep;
            if (ingredient == null) return null;

            string name = OrderManager.GetKoreanIngredientName(ingredient.Value);
            string josa = ObjectJosa(name);

            return IsRepeatStep
                ? name + josa + " 한 번 더 넣어주세요."
                : name + josa + " 넣어주세요.";
        }
    }

    /// <summary>
    /// 목적격 조사. 받침이 있으면 "을", 없으면 "를" 이다.
    ///
    /// 재료 이름을 그대로 붙이면 "얇은면를" 처럼 어긋난다. 열셋 중 받침이 있는 것은
    /// 얇은면·굵은면·김·계란·목이버섯 다섯이고 나머지는 없어서, 한 벌로 적으면 반드시 틀린다.
    ///
    /// 한글 음절은 유니코드에서 (초성, 중성, 종성) 순서로 늘어서 있어 28 로 나눈 나머지가 종성이다.
    /// 0 이면 받침이 없다.
    /// </summary>
    private static string ObjectJosa(string word)
    {
        if (string.IsNullOrEmpty(word)) return "를";

        char last = word[word.Length - 1];
        if (last < '가' || last > '힣') return "를";

        return (last - '가') % 28 == 0 ? "를" : "을";
    }

    /// <summary>
    /// 주문서·레시피북을 열었다 닫았는지 지켜본다.
    ///
    /// 열린 것만으로는 넘기지 않는다. Tab 은 누르고 있는 동안만 열려 있어서, 열린 순간
    /// 넘겨 버리면 안내가 바뀌는 것을 손이 키에서 떨어지기 전에는 못 본다.
    /// </summary>
    private void Update()
    {
        if (!IsRunning) return;

        switch (phase)
        {
            case Phase.OrderNote:
                if (note == null) note = FindFirstObjectByType<OrderNoteUI>();
                if (note == null) return;

                if (note.IsOpen) noteOpened = true;
                else if (noteOpened) phase = Phase.RecipeBook;
                return;

            case Phase.RecipeBook:
                if (book == null) book = FindFirstObjectByType<RecipeBookUI>();
                if (book == null) return;

                if (book.IsOpen) bookOpened = true;
                else if (bookOpened)
                {
                    phase = Phase.Ready;
                    readyUntil = Time.unscaledTime + ReadySeconds;
                    readyDeadline = Time.unscaledTime + ReadyMaxWait;
                }
                return;

            case Phase.Ready:
                // 글이 다 찍히기 전에는 시간을 세지 않는다. 다 찍힌 순간부터 읽을 틈을 준다.
                //
                // 다만 무한정 기다리지는 않는다. 안내 판이 어떤 이유로든 안 뜨면 Revealed 가
                // 영영 거짓이라 여기서 갇히고, 그러면 재료를 하나도 집을 수 없게 된다.
                if (!TutorialPrompt.Revealed && Time.unscaledTime < readyDeadline)
                {
                    readyUntil = Time.unscaledTime + ReadySeconds;
                    return;
                }

                if (Time.unscaledTime >= readyUntil) phase = Phase.Cooking;
                return;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 이 재료를 지금 집을 수 있는가. 재료통 셋(고체·액체·면)이 집기 전에 물어본다.
    /// 튜토리얼이 아니면 늘 참이다.
    /// </summary>
    public static bool CanPick(IngredientType type)
    {
        TutorialManager t = Instance;
        if (t == null || !t.IsRunning) return true;

        return t.CurrentStep == type;
    }

    /// <summary>그릇을 제출할 수 있는가. 튜토리얼에서는 차례를 다 밟아야 열린다.</summary>
    public static bool CanSubmit()
    {
        TutorialManager t = Instance;
        if (t == null || !t.IsRunning) return true;

        return t.ReadyToSubmit;
    }

    /// <summary>저장해도 되는가. 튜토리얼 중에는 남기지 않는다 — 껐다 켜면 처음부터다.</summary>
    public static bool CanSave()
    {
        TutorialManager t = Instance;
        return t == null || !t.IsRunning;
    }

    /// <summary>
    /// 재료가 그릇에 들어갔다. Bowl 이 실제로 받아들인 뒤에만 부른다.
    /// 안내한 것과 다르면 아무 일도 하지 않는다 — 차례가 밀리면 안내가 어긋난다.
    /// </summary>
    public void NotifyAdded(IngredientType type)
    {
        if (!IsRunning || phase != Phase.Cooking) return;
        if (step >= Steps.Length || Steps[step] != type) return;

        step++;
        if (step >= Steps.Length) phase = Phase.Submit;
    }

    /// <summary>그릇을 비웠다. 재료 차례를 처음으로 되돌린다.</summary>
    public void NotifyBowlCleared()
    {
        if (!IsRunning) return;
        if (phase != Phase.Cooking && phase != Phase.Submit) return;

        step = 0;
        phase = Phase.Cooking;
    }

    /// <summary>제출까지 끝났다. 이 뒤로는 본편이다.</summary>
    public void Finish()
    {
        IsDone = true;
        step = 0;
    }

    /// <summary>
    /// 튜토리얼 주문 하나를 만든다. 무작위 생성을 거치지 않고 기본 레시피를 그대로 정답으로 쓴다.
    /// 변경 요청이 없으므로 기획 9.2 의 "추가·감소·제외·면 교체를 적용하지 않는다"가 저절로 지켜진다.
    /// </summary>
    public static DialogueScenario BuildScenario()
    {
        Dictionary<IngredientType, int> recipe = RecipeGenerator.GetBaseRecipe(Menu);

        var scenario = new DialogueScenario
        {
            personaId = "Polite",
            personaName = "첫 손님",
            difficulty = 1,
            order = new CustomerOrder { ramenType = Menu },
            baseRecipe = recipe,
            targetRecipe = new Dictionary<IngredientType, int>(recipe),
        };

        scenario.lines.Add("안녕하세요.");
        scenario.lines.Add("시오라멘 하나 주세요.");
        scenario.lines.Add("기본으로 부탁드려요.");

        return scenario;
    }
}
