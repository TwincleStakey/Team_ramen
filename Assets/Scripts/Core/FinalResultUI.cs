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

    public void Open(int totalRevenue, float averageAccuracy, int perfectCount, int servedCount)
    {
        if (popupRoot != null) popupRoot.SetActive(true);
        if (titleText != null) titleText.text = "5일 영업 종료";
        if (revenueText != null) revenueText.text = "누적 매출 : " + totalRevenue.ToString("N0") + "원";
        if (accuracyText != null) accuracyText.text = "평균 정확도 : " + averageAccuracy.ToString("F1") + "%";
        if (perfectText != null) perfectText.text = "완벽한 한 그릇 : " + perfectCount + " / " + servedCount + "건";
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
