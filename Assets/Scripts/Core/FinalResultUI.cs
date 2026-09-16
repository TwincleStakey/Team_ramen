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

    private void Awake()
    {
        if (restartButton != null) restartButton.onClick.AddListener(Restart);

        // 이 스크립트는 popupRoot 바깥에 붙어 있어야 한다.
        // 안에 있으면 여기서 자기 자신을 꺼 버려 다시 켤 수 없다.
        Close();
    }

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
    }

    /// <summary>
    /// 뜨자마자 [확인]이 눌리지 않게 잠가 두는 시간(초).
    ///
    /// 하루 정산표의 [확인]과 이 창의 [확인]이 세로로 19칸 겹친다. 5일차 정산표를 누른
    /// 그 자리에 이 버튼이 그대로 올라오므로, 손이 한 번 더 움직이면 성적표를 못 보고
    /// 타이틀로 나가 버린다(Restart 는 씬을 다시 연다).
    /// </summary>
    private const float ArmDelay = 0.6f;

    public void Open(int totalRevenue, float averageAccuracy, int perfectCount, int servedCount)
    {
        if (popupRoot != null) popupRoot.SetActive(true);

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
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

    /// <summary>저장 기능이 없으므로 씬을 다시 불러 처음부터 시작한다. (기획서 13장)</summary>
    private void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
