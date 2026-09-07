using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 씬에 하나만 존재하는 진입점.
/// 그릇이 제출되면 B의 OrderManager로 넘겨 채점시킨다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 채점을 맡는 B의 컴포넌트. 인스펙터에서 꽂아 두면 그걸 쓰고,
    // 비어 있으면 제출 시점에 씬에서 한 번 찾는다.
    [SerializeField] private OrderManager orderManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 그릇을 제출 영역에 놓으면 호출된다.
    /// 지금은 로그만 남기고, 통합 후에는 B의 정산 코드로 넘긴다.
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

        if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();

        if (orderManager == null)
        {
            // 주문 화면(B)이 아직 씬에 없으면 채점할 상대가 없다. 조리만 확인하는 단계.
            Debug.LogWarning("[GameManager] 씬에 OrderManager가 없어 채점을 건너뜁니다.");

            return;
        }

        int price = orderManager.EvaluateRamen(ramenState);
        Debug.Log("[정산] 판매 금액 " + price.ToString("N0") + "원");
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
