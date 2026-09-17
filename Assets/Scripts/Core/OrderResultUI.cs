using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 한 명에게 라멘을 낸 직후 뜨는 결과창.
/// 하루 마감 정산(B의 DailyResultUI)과는 다르다. 이건 매 주문마다 뜬다.
///
/// 이 창이 떠 있는 동안은 다음 손님으로 넘어가지 않는다.
/// [확인]을 눌러야 GameManager가 진행을 이어받는다.
/// </summary>
public class OrderResultUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI customerLine;
    [SerializeField] private Image emoji;
    [SerializeField] private Sprite emojiHappy;
    [SerializeField] private Sprite emojiNeutral;
    [SerializeField] private Sprite emojiAngry;
    [SerializeField] private Button confirmButton;
    [SerializeField] private GameManager gameManager;

    /// <summary>대사 왼쪽에 붙는 손님 초상.</summary>
    [SerializeField] private Image customerFace;

    /// <summary>판 왼쪽에 놓이는 "내가 만든 그릇". 제출 순간에 <see cref="CaptureBowl"/> 가 채운다.</summary>
    [SerializeField] private Image servedBowl;

    /// <summary>판 가운데에 서는 주문서. 그릇과 견주어 보라고 같이 띄운다.</summary>
    [SerializeField] private ResultOrderNote note;

    /// <summary>
    /// 지금 손님이 누구인지 아는 쪽. 초상은 말투마다 다르니 여기서 받아 온다.
    ///
    /// 정확도 창이 스스로 말투를 알 길은 없다 — <see cref="Open"/> 이 받는 것은 점수와 돈뿐이고,
    /// 말투를 인자로 더 받게 하면 부르는 쪽(GameManager)이 손님 그림까지 알아야 한다.
    /// 이미 손님을 세우고 있는 쪽에 물어보는 편이 좁게 끝난다.
    /// </summary>
    [SerializeField] private CustomerAppearance customer;

    /// <summary>결과창이 떠 있는가. 떠 있으면 이미 제출한 뒤라 조리 단축키가 막힌다.</summary>
    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        Close();
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
    }

    public void Open(float accuracy, int reward, int totalRevenue)
    {
        if (root != null) root.SetActive(true);
        Sfx.Play("sfx_ui_result", 0.6f);

        // 튜토리얼에서 감춰 두었을 수 있다. 평범한 손님은 늘 돈을 보여 준다.
        ShowMoney(true);

        if (accuracyText != null) accuracyText.text = "정확도 : " + accuracy.ToString("F0") + "%";
        if (rewardText != null) rewardText.text = "+ " + reward.ToString("N0") + "₩";
        // 콜론을 뺐다. 12 칸으로 작아진 곁다리 줄이라 기호가 하나 더 붙으면 그만큼 성기다.
        if (revenueText != null) revenueText.text = "금일 수익 " + totalRevenue.ToString("N0") + "₩";

        // 주문서를 옆에 세운다. 그릇은 제출 순간에 이미 찍어 두었다(CaptureBowl).
        if (note != null) note.Show();

        // 한마디. 정확도 구간이 아니라 **어긋난 방향**을 말한다 — 그릇과 주문서를 나란히
        // 놓고 보는 자리라, 소감보다 "어느 쪽으로 틀렸나"가 쓸모 있다.
        if (customerLine != null) customerLine.text = ReactionLines.Feedback(accuracy);

        // 누가 한 말인지. 그림이 없는 말투면 판을 아예 감춘다 — 스프라이트를 비운 채
        // 켜 두면 유니티가 그 자리에 흰 사각형을 그린다.
        if (customerFace != null)
        {
            Sprite face = customer != null ? customer.Face : null;
            customerFace.sprite = face;
            customerFace.enabled = face != null;
        }

        // 기획서 7.5 — 90% 이상 웃음 / 70~89% 무표정 / 69% 이하 화남
        if (emoji != null)
        {
            Sprite face = accuracy >= 90f ? emojiHappy
                        : accuracy >= 70f ? emojiNeutral
                        : emojiAngry;
            if (face != null) emoji.sprite = face;
        }
    }

    /// <summary>
    /// 튜토리얼 마무리용. 정확도 창을 띄우되 한마디 자리에 안내를 넣고,
    /// [확인] 뒤에 할 일을 따로 받는다.
    ///
    /// 안내대로만 넣게 해 둬서 그릇은 늘 정답이라 정확도는 100 이다.
    /// 값은 받지 않는다(기획 9.2 — 튜토리얼은 매출·정확도 집계에서 모두 제외).
    /// </summary>
    public void OpenTutorial(int totalRevenue, string message, System.Action confirmed)
    {
        Open(100f, 0, totalRevenue);

        // 보상과 누적 수익 칸은 아예 감춘다. 튜토리얼은 값을 받지도 쌓지도 않아서
        // 0원이 두 줄로 뜨면 뭔가 잘못된 것처럼 보인다. 정확도와 안내만 남긴다.
        ShowMoney(false);
        if (customerLine != null) customerLine.text = message;

        onConfirm = confirmed;
    }

    /// <summary>보상·누적 수익 칸을 여닫는다. 튜토리얼에서만 닫는다.</summary>
    private void ShowMoney(bool visible)
    {
        if (rewardText != null) rewardText.gameObject.SetActive(visible);
        if (revenueText != null) revenueText.gameObject.SetActive(visible);
    }

    /// <summary>[확인] 을 눌렀을 때 대신 할 일. 비어 있으면 평소대로 다음 손님으로 간다.</summary>
    private System.Action onConfirm;

    /// <summary>
    /// 지금 조리대에 있는 그릇을 그대로 결과창에 옮겨 놓는다.
    ///
    /// **제출하는 그 순간에 불러야 한다.** Bowl.Submit 은 GameManager 에 넘기고 돌아오자마자
    /// ClearBowl 로 안을 비운다.
    ///
    /// 재료 자리를 다시 계산하지 않고 그릇 안(Contents)을 통째로 복제한다. 배치표는
    /// Bowl 안에만 있고(Layouts), 그걸 베껴 오면 그릇을 고칠 때마다 두 곳이 어긋난다.
    /// 크기도 조리대 그릇 그대로다 — 줄이면 정수배가 깨져 픽셀에 회색이 낀다.
    /// </summary>
    public void CaptureBowl()
    {
        if (servedBowl == null) return;

        ClearBowlShot();

        Bowl bowl = FindFirstObjectByType<Bowl>();
        Image source = bowl != null ? bowl.GetComponent<Image>() : null;

        // 조리대가 없는 자리(빌더를 안 돌렸거나 컷신만 돌릴 때)면 그릇 자리를 비워 둔다.
        servedBowl.enabled = source != null && source.sprite != null;
        if (!servedBowl.enabled) return;

        servedBowl.sprite = source.sprite;

        Transform contents = bowl.transform.Find("Contents");
        if (contents == null) return;

        // worldPositionStays 를 꺼야 원래 자리 그대로 온다. 켜 두면 화면 좌표를 맞추려고
        // 새 부모 기준으로 다시 계산해서, 재료만 조리대 자리에 남는다.
        Instantiate(contents.gameObject, servedBowl.transform, false);
    }

    /// <summary>찍어 둔 그릇 속을 비운다. 다음 손님 것이 앞 손님 위에 얹히지 않게.</summary>
    private void ClearBowlShot()
    {
        if (servedBowl == null) return;

        for (int i = servedBowl.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(servedBowl.transform.GetChild(i).gameObject);
        }
    }

    public void Close()
    {
        // 손님이 갔다. 다음 손님은 반응을 새로 뽑는다.
        ReactionLines.Clear();
        ClearBowlShot();

        if (root != null) root.SetActive(false);
    }

    private void Confirm()
    {
        Close();

        // 한 번 쓰고 비운다. 남겨 두면 다음 손님의 [확인] 까지 여기로 빠진다.
        if (onConfirm != null)
        {
            System.Action done = onConfirm;
            onConfirm = null;
            done();
            return;
        }

        if (gameManager != null) gameManager.AdvanceCustomer();
    }
}
