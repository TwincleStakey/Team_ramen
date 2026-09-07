using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 씬에 하나만 존재하는 진입점. 지금은 제출 내용을 로그로만 확인한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── B 연결 자리 ───────────────────────────────────────────────
    // 9/8 통합 때 B의 OrderManager를 여기에 꽂는다.
    // [SerializeField] private OrderManager orderManager;
    // ─────────────────────────────────────────────────────────────

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

        // TODO(통합): B가 EvaluateRamen을 만들면 여기서 호출한다.
        //             현재 OrderManager에는 CreateOrder만 있고 평가 진입점이 없다.
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
