using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 5일 영업이 모두 끝났을 때 뜨는 최종 성적표.
/// 하루 단위 정산은 B의 DailyResultUI가 맡고, 이쪽은 전체 누계만 보여 준다.
/// 기획서 13장에 따라 게임오버는 없다. 끝까지 가면 무조건 이 화면이다.
/// </summary>
public class FinalResultUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI perfectText;
    [SerializeField] private Button restartButton;

    /// <summary>
    /// [확인]을 눌렀을 때 할 일. 비어 있으면 씬을 다시 연다.
    ///
    /// 5일 완주에서는 GameManager 가 여기에 「엔딩 글 → 암전 → 크레딧」을 꽂는다.
    /// 이 창은 무엇이 다음에 오는지 몰라도 된다 — 아는 쪽이 꽂아 준다.
    /// </summary>
    private System.Action onConfirm;

    private void Awake()
    {
        if (restartButton != null) restartButton.onClick.AddListener(Confirm);

        // 이 스크립트는 popupRoot 바깥에 붙어 있어야 한다.
        // 안에 있으면 여기서 자기 자신을 꺼 버려 다시 켤 수 없다.
        Close();
    }

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(Confirm);
    }

    /// <summary>
    /// 뜨자마자 [확인]이 눌리지 않게 잠가 두는 시간(초).
    ///
    /// 하루 정산표의 [확인]과 이 창의 [확인]이 세로로 19칸 겹친다. 5일차 정산표를 누른
    /// 그 자리에 이 버튼이 그대로 올라오므로, 손이 한 번 더 움직이면 성적표를 못 보고
    /// 다음으로 넘어가 버린다(<see cref="Confirm"/> 은 엔딩 글로 넘기거나 씬을 다시 연다).
    /// </summary>
    private const float ArmDelay = 0.6f;

    /// <param name="onConfirm">
    /// [확인]을 눌렀을 때 할 일. null 이면 예전처럼 씬을 다시 연다.
    /// </param>
    public void Open(int totalRevenue, float averageAccuracy, int perfectCount, int servedCount,
                     System.Action onConfirm = null)
    {
        this.onConfirm = onConfirm;

        if (popupRoot != null) popupRoot.SetActive(true);

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
            restartButton.interactable = true;      // Confirm 이 잠가 둔 것을 되돌린다
            StartCoroutine(ArmRestart());
        }
        Sfx.Play("sfx_ui_final", 0.8f);
        if (titleText != null) titleText.text = "5일 영업 종료";
        if (revenueText != null) revenueText.text = "누적 매출 : " + totalRevenue.ToString("N0") + "원";
        if (accuracyText != null) accuracyText.text = "평균 정확도 : " + averageAccuracy.ToString("F1") + "%";
        if (perfectText != null) perfectText.text = "완벽한 한 그릇 : " + perfectCount + " / " + servedCount + "건";
    }

    /// <summary>잠깐 두었다 [확인]을 켠다. 그 사이 눌린 것은 아무 데도 안 닿는다.</summary>
    private IEnumerator ArmRestart()
    {
        yield return new WaitForSecondsRealtime(ArmDelay);
        if (restartButton != null) restartButton.gameObject.SetActive(true);
    }

    public void Close()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    /// <summary>
    /// [확인]. 넘겨받은 일이 있으면 그것을 하고, 없으면 씬을 다시 불러 처음부터 시작한다.
    /// (기획서 13장 — 저장 기능이 없다.)
    /// </summary>
    private void Confirm()
    {
        // 한 번만 듣는다. 엔딩 글은 코루틴이라 바로 안 덮이는데, 그사이 한 번 더 눌리면
        // 같은 연출이 두 벌 돌아 글자가 겹쳐 찍힌다.
        if (restartButton != null) restartButton.interactable = false;

        System.Action go = onConfirm;
        onConfirm = null;

        if (go != null)
        {
            go();
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
