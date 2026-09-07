using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬에 하나만 존재하는 진입점.
/// 손님을 한 명씩 받아 주문을 띄우고, 그릇이 제출되면 B의 OrderManager로 넘겨 채점한다.
/// 아직 주문·정산 화면이 없어서 조리 화면 위에 글자로만 보여 준다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 아래 셋은 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private Text orderText;
    [SerializeField] private Text revenueText;

    /// <summary>지금까지 판 금액의 합. 재료비가 없어져서 매출이 곧 성적표다. (기획 확정)</summary>
    private int totalRevenue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        RefreshRevenue();
        NextCustomer();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 그릇을 제출 영역에 놓으면 호출된다.
    /// 채점하고 매출에 더한 뒤 바로 다음 손님을 받는다.
    /// </summary>
    public void SubmitRamen(RamenState ramenState)
    {
        if (ramenState == null)
        {
            Debug.LogWarning("[GameManager] SubmitRamen에 null이 들어왔습니다.");
            return;
        }

        // B의 RamenState는 그릇 딕셔너리(selectedIngredients)만 보관한다.
        // 폐기 딕셔너리는 생성자에서 인자로 받기만 하고 저장하지 않으므로 여기서 읽을 수 없다.
        // 그래서 폐기분 로그는 Bowl이 제출 직전에 직접 남긴다.
        Debug.Log("[제출] 그릇: " + Describe(ramenState.selectedIngredients));

        if (!EnsureOrderManager())
        {
            Debug.LogWarning("[GameManager] 씬에 OrderManager가 없어 채점을 건너뜁니다.");
            return;
        }

        int price = orderManager.EvaluateRamen(ramenState);
        totalRevenue += price;
        RefreshRevenue();

        Debug.Log("[정산] 판매 금액 " + price.ToString("N0") + "원 / 누적 매출 " + totalRevenue.ToString("N0") + "원");

        NextCustomer();
    }

    /// <summary>다음 손님의 주문을 만들어 화면에 띄운다.</summary>
    private void NextCustomer()
    {
        if (!EnsureOrderManager())
        {
            SetOrderText("(주문 시스템이 씬에 없습니다)");
            return;
        }

        orderManager.CreateOrder();
        SetOrderText(orderManager.CurrentDialogue);
    }

    /// <summary>인스펙터가 비어 있으면 씬에서 한 번 찾아 둔다.</summary>
    private bool EnsureOrderManager()
    {
        if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();
        return orderManager != null;
    }

    private void SetOrderText(string text)
    {
        if (orderText != null) orderText.text = text;
    }

    private void RefreshRevenue()
    {
        if (revenueText != null) revenueText.text = "누적 매출 " + totalRevenue.ToString("N0") + "원";
    }

    private static string Describe(Dictionary<IngredientType, int> dict)
    {
        if (dict == null || dict.Count == 0) return "(비어 있음)";

        var sb = new StringBuilder();
        foreach (var pair in dict)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(pair.Key).Append('=').Append(pair.Value);
        }
        return sb.ToString();
    }
}
