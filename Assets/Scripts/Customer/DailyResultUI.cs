using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyResultUI : MonoBehaviour
{
    [Header("매니저 참조")]
    [SerializeField]
    private DayManager dayManager;

    [Header("UI 요소 연결")]
    [Tooltip("정산 팝업 패널 최상위 오브젝트 (열고 닫을 때 활성화/비활성화)")]
    [SerializeField]
    private GameObject popupRoot;

    [Tooltip("몇 일차인지 표시하는 텍스트 (예: Day 1 정산)")]
    [SerializeField]
    private TextMeshProUGUI titleText;

    [Tooltip("당일 목표 금액 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI targetProfitText;

    [Tooltip("당일 총 수익 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI profitText;

    [Tooltip("누적 총 매출 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI totalProfitText;

    [Tooltip("당일 평균 정확도 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI averageAccuracyText;

    [Tooltip("당일 100% 그릇 수 표시 텍스트")]
    [SerializeField]
    private TextMeshProUGUI perfectCountText;

    [Tooltip("다음 날로 넘어가기 위한 [확인] 버튼 (목표 달성 시 활성화)")]
    [SerializeField]
    private Button confirmButton;

    [Tooltip("목표 미달 시 1일차부터 다시 시작하기 위한 [다시하기] 버튼 (목표 미달 시 활성화)")]
    [SerializeField]
    private Button retryButton;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryButtonClicked);
        }

        // 시작 시에는 팝업을 숨겨둠
        ClosePopup();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryButtonClicked);
        }
    }

    /// <summary>
    /// 정산 팝업을 열고 일차, 당일 총 수익, 당일 목표 금액, 누적 총 매출, 평균 정확도, 완벽한 한 그릇 수 및 목표 달성 여부를 표시합니다.
    /// </summary>
    /// <param name="day">현재 일차</param>
    /// <param name="todayProfit">당일 총 누적 수익</param>
    /// <param name="totalProfit">게임 전체 누적 총 매출</param>
    /// <param name="averageAccuracy">당일 평균 정확도 (%)</param>
    /// <param name="perfectCount">당일 100% 그릇 수</param>
    /// <param name="isSuccess">목표 금액 달성 여부 (true: [확인] 버튼 활성화, false: [다시하기] 버튼 활성화)</param>
    /// <param name="targetProfit">당일 목표 금액</param>
    public void OpenPopup(int day, int todayProfit, int totalProfit, float averageAccuracy, int perfectCount, bool isSuccess, int targetProfit = 0)
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }

        Sfx.Play("sfx_ui_dayend", 0.7f);

        if (titleText != null)
        {
            // 영수증 그림에 「당일 정산표」가 이미 찍혀 있다. 여기까지 "정산" 이라고 쓰면 두 번이다.
            titleText.text = $"{day} 일차";
        }

        // 목표 금액은 오늘 장사를 시작하기 전부터 정해져 있던 값이다. 굴려 올릴 것이 없다.
        if (targetProfitText != null)
        {
            targetProfitText.text = $"목표 금액 : {targetProfit:N0}원";
        }

        // 나머지 넉 줄은 0 부터 한 줄씩 올라간다. 버튼은 다 오른 뒤에 나온다.
        if (rollRoutine != null) StopCoroutine(rollRoutine);
        rollRoutine = StartCoroutine(RollNumbers(todayProfit, totalProfit, averageAccuracy, perfectCount, isSuccess));
    }

    // ── 숫자 굴리기 ──────────────────────────────────────────────

    /// <summary>
    /// 한 줄이 0 에서 제 값까지 오르는 데 걸리는 시간(초).
    ///
    /// 0.45 는 숫자가 올라가는 것이 안 보일 만큼 빨랐다. 하루를 닫고 성적을 읽는 자리라
    /// 한 줄씩 눈으로 따라갈 수 있어야 한다.
    /// </summary>
    private const float RollSeconds = 1.1f;

    /// <summary>「띡」 소리 간격(초). 이보다 촘촘하면 소리가 서로 꼬리를 물어 뭉개진다.</summary>
    private const float TickInterval = 0.06f;

    /// <summary>한 줄이 다 오른 뒤 다음 줄로 넘어가기까지의 틈(초). 줄과 줄이 붙으면 한 덩이로 읽힌다.</summary>
    private const float LineGap = 0.35f;

    /// <summary>
    /// 한 프레임에 흘려보낼 수 있는 최대 시간(초).
    ///
    /// unscaledDeltaTime 은 유니티가 안 잘라 준다. 무거운 프레임이 한 번 끼면 그 한 프레임에
    /// 연출이 통째로 끝나 「올라가는 것이 안 보인다」로 나타난다.
    /// </summary>
    private const float MaxStep = 0.05f;

    private Coroutine rollRoutine;

    private IEnumerator RollNumbers(int todayProfit, int totalProfit, float averageAccuracy,
                                   int perfectCount, bool isSuccess)
    {
        // 다 오르기 전에 눌러 넘기지 못하게 감춘다.
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (retryButton != null) retryButton.gameObject.SetActive(false);

        System.Func<float, string> profit = p => $"당일 총 수익 : {Mathf.RoundToInt(todayProfit * p):N0}원";
        System.Func<float, string> total = p => $"누적 총 매출 : {Mathf.RoundToInt(totalProfit * p):N0}원";
        System.Func<float, string> accuracy = p => $"평균 정확도 : {averageAccuracy * p:F1}%";
        System.Func<float, string> perfect = p => $"완벽한 한 그릇 : {Mathf.RoundToInt(perfectCount * p)}건";

        // 넉 줄을 **한꺼번에 0 으로 세워 두고** 시작한다.
        //
        // 줄은 제 차례가 와야 글이 바뀐다. 그 전까지는 프리팹에 박혀 있던 자리글이 그대로
        // 보이는데, 그게 영문(「Total Profit」·「Today Accuracy」·「Perfect Ramen」)이라
        // 「누적 총 매출」은 1.5초, 「완벽한 한 그릇」은 4초 넘게 영어로 떠 있었다.
        //
        // 프리팹 자리글도 한글로 바꿔 두었지만 그건 에디터에서 보기 위한 것이고,
        // **실제로 뜨는 문구는 여기 네 줄이 전부 만든다.** 프리팹에 숫자까지 적지 않는다 —
        // 적으면 형식이 두 곳에 생겨 한쪽만 고쳤을 때 어긋난다.
        Set(profitText, profit);
        Set(totalProfitText, total);
        Set(averageAccuracyText, accuracy);
        Set(perfectCountText, perfect);

        yield return Roll(profitText, profit);
        yield return Roll(totalProfitText, total);
        yield return Roll(averageAccuracyText, accuracy);

        // 건수는 많아야 여덟이라 한 칸 오를 때마다 「띡」이 붙는다. 띡, 띡, 띡 하고 세어 준다.
        // 금액 줄에 같은 걸 하면 3만 번 울리므로 그쪽은 시간 기준 그대로다.
        yield return Roll(perfectCountText, perfect, p => Mathf.RoundToInt(perfectCount * p));

        // 목표 달성 여부에 따라 버튼 표시 분기
        if (confirmButton != null) confirmButton.gameObject.SetActive(isSuccess);
        if (retryButton != null) retryButton.gameObject.SetActive(!isSuccess);

        // 숫자가 다 오른 뒤에 도장이 박힌다(잘한 날) / 임대 딱지가 붙는다(못 채운 날).
        // 어느 도장인지는 ResultStamp 가 평균 정확도로 고른다 — 여기서는 값만 넘긴다.
        if (ResultStamp.Instance != null)
            yield return ResultStamp.Instance.Play(isSuccess, averageAccuracy);

        rollRoutine = null;
    }

    /// <summary>줄 하나를 굴리기 전 0 상태로 세운다. 프리팹 자리글이 비치지 않게 하는 자리다.</summary>
    private static void Set(TextMeshProUGUI label, System.Func<float, string> draw)
    {
        if (label != null) label.text = draw(0f);
    }

    /// <summary>
    /// 한 줄을 0 에서 제 값까지 올린다. draw 는 0~1 을 받아 그 시점의 글을 만든다.
    ///
    /// 끝에서 느려지게(ease-out) 둔 것은 마지막 숫자가 눈에 걸려야 읽히기 때문이다.
    ///
    /// 소리는 기본이 **시간 기준**이다. 숫자 변화에 맞추면 3만 원짜리 줄에서 3만 번 울린다.
    /// 다만 <paramref name="countOf"/> 를 주면 그 값이 한 칸 바뀔 때만 울린다 —
    /// 「완벽한 한 그릇」처럼 많아야 여덟인 줄은 그쪽이 띡, 띡, 띡 하고 세어 주는 맛이 난다.
    /// </summary>
    private IEnumerator Roll(TextMeshProUGUI label, System.Func<float, string> draw,
                             System.Func<float, int> countOf = null)
    {
        if (label == null) yield break;

        float elapsed = 0f;
        float nextTick = 0f;
        int lastCount = countOf != null ? countOf(0f) : 0;

        while (elapsed < RollSeconds)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);

            float x = Mathf.Clamp01(elapsed / RollSeconds);
            float p = 1f - (1f - x) * (1f - x);
            label.text = draw(p);

            if (countOf != null)
            {
                int now = countOf(p);
                if (now != lastCount)
                {
                    lastCount = now;
                    Sfx.Play("sfx_ui_count", 0.45f);
                }
            }
            else if (elapsed >= nextTick)
            {
                Sfx.Play("sfx_ui_count", 0.35f);
                nextTick += TickInterval;
            }

            yield return null;
        }

        // 마지막은 반올림이 아니라 제 값으로 확실히 맞춘다.
        label.text = draw(1f);
        yield return new WaitForSecondsRealtime(LineGap);
    }

    // 기본 호출 오버로드 (isSuccess 및 totalProfit 생략 시 기본 성공 처리)
    public void OpenPopup(int day, int todayProfit, float averageAccuracy, int perfectCount)
    {
        OpenPopup(day, todayProfit, todayProfit, averageAccuracy, perfectCount, true);
    }

    // 기본 호출 오버로드 (완벽 그릇 수 생략 시)
    public void OpenPopup(int day, int todayProfit, float averageAccuracy)
    {
        OpenPopup(day, todayProfit, todayProfit, averageAccuracy, 0, true);
    }

    // 기본 호출 오버로드 (평균 정확도 생략 시)
    public void OpenPopup(int day, int todayProfit)
    {
        OpenPopup(day, todayProfit, todayProfit, 0f, 0, true);
    }

    // 정산 팝업을 닫습니다.
    public void ClosePopup()
    {
        // 굴러가던 숫자를 세운다. 안 세우면 닫힌 팝업의 글자를 계속 고치면서 소리가 난다.
        if (rollRoutine != null)
        {
            StopCoroutine(rollRoutine);
            rollRoutine = null;
        }

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    // 확인 버튼 클릭 시 DayManager에 다음 날 진행을 알립니다.
    private void OnConfirmButtonClicked()
    {
        if (dayManager != null)
        {
            dayManager.NextDay();
        }
        else
        {
            ClosePopup();
        }
    }

    // 다시하기 버튼 클릭 시 DayManager에 1일차 재시작을 알립니다.
    private void OnRetryButtonClicked()
    {
        if (dayManager != null)
        {
            dayManager.RestartGame();
        }
        else
        {
            ClosePopup();
        }
    }
}