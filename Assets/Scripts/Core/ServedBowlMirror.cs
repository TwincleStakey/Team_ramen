using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 앞에 놓이는 그릇을 **조리 화면에서 실제로 만든 그릇**으로 바꾼다. (시험용)
///
/// 원래는 김 나는 그릇 그림 한 장(손님그릇.png)을 돌렸다. 무엇을 말아 줬든 늘 같은 그릇이라,
/// 공들여 올린 토핑이 손님 앞에서는 사라졌다. 여기서는 조리 화면 그릇 가지를 **통째로 복제해**
/// 그 자리에 놓는다. 올린 토핑이 그대로 따라온다.
///
/// 원본을 옮겨 오지 않고 복제하는 이유 — 조리 화면 그릇은 제출·초기화 흐름이 걸려 있고
/// 자기 Canvas 까지 얹고 있다. 옮기면 그 흐름이 통째로 끌려온다. 복제본은 그림만 남긴다.
///
/// 크기는 조리 화면과 **같은 2배**로 둔다. 원본 그릇 그림이 128칸이라 2배면 256칸이다.
/// 예전 그림(220칸)에 맞추려면 1.72배가 되는데, 픽셀아트를 정수 아닌 배로 늘리면
/// 획이 반 칸에 걸려 가장자리가 흐려진다.
/// </summary>
public class ServedBowlMirror : MonoBehaviour
{
    /// <summary>조리 화면 그릇. 이 가지를 복제한다.</summary>
    [SerializeField] private RectTransform source;

    /// <summary>예전 그릇 그림. 복제가 되면 꺼 두고, 안 되면 그대로 쓴다.</summary>
    [SerializeField] private Image stockBowl;

    /// <summary>복제본을 놓을 자리와 크기.</summary>
    [SerializeField] private Vector2 position = Vector2.zero;
    [SerializeField] private float scale = 1f;

    /// <summary>
    /// 복제한 그릇을 쓸 것인가. 꺼 두면 예전 그림(손님그릇.png) 그대로다.
    ///
    /// 2026-09-15 에 껐다. 복제는 제대로 도는 것을 확인했고(토핑까지 따라온다) 코드도 그대로
    /// 남겨 두었다. 인스펙터에서 이 칸만 켜면 되살아난다.
    /// </summary>
    [SerializeField] private bool useCookedBowl = false;

    private GameObject clone;

    /// <summary>라멘을 낼 때마다(자리가 켜질 때마다) 그 시점의 그릇을 다시 뜬다.</summary>
    private void OnEnable()
    {
        if (!useCookedBowl)
        {
            if (stockBowl != null) stockBowl.enabled = true;
            return;
        }

        Rebuild();
    }

    private void OnDisable()
    {
        Clear();
    }

    private void Clear()
    {
        if (clone == null) return;

        Destroy(clone);
        clone = null;
    }

    private void Rebuild()
    {
        Clear();

        if (source == null)
        {
            if (stockBowl != null) stockBowl.enabled = true;
            return;
        }

        // 지금 조리 화면에 떠 있는 그릇 그림. 복제본을 켜는 순간 복제본의 Bowl.Awake 가
        // 돌면서 빈 딕셔너리 기준으로 RefreshBowlSprite() 를 불러 이 그림을 빈그릇으로
        // 덮어쓴다. 켜기 전에 받아 두었다가 부품을 떼어 낸 뒤 도로 얹는다.
        Image sourceImage = source.GetComponent<Image>();
        Sprite cooked = sourceImage != null ? sourceImage.sprite : null;

        clone = Instantiate(source.gameObject, transform);
        clone.name = "CookedBowl";

        // 조리 화면에서는 주문 화면이 떠 있는 동안 그릇을 꺼 둔다. 꺼진 것을 복제하면
        // 복제본도 꺼진 채로 나온다.
        clone.SetActive(true);

        Strip(clone);

        // Bowl.Awake 가 덮어쓴 그릇 그림을 되돌린다.
        Image cloneImage = clone.GetComponent<Image>();
        if (cloneImage != null && cooked != null) cloneImage.sprite = cooked;

        var rect = (RectTransform)clone.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = new Vector3(scale, scale, 1f);

        if (stockBowl != null) stockBowl.enabled = false;
    }

    /// <summary>
    /// 복제본에서 그림 말고 다 떼어 낸다.
    ///
    /// 조리 화면 그릇에는 <see cref="Bowl"/>(제출·재료 받기), 자기 <see cref="Canvas"/>(앞으로
    /// 끌어올리기), 튜토리얼 테두리 따위가 붙어 있다. 그대로 두면 복제본이 재료를 받으려 들고
    /// 자기 Canvas 로 주문 화면 위에 떠오른다.
    /// </summary>
    private static void Strip(GameObject root)
    {
        foreach (Component c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            if (c is RectTransform || c is CanvasRenderer) continue;

            if (c is Image image)
            {
                image.raycastTarget = false;   // 눌러도 아무 일 없어야 한다
                continue;
            }

            // 마스크는 남긴다. 떼면 국물 수면 그림이 마스크가 아니라 평범한 그림이 되어
            // 그릇 속을 통째로 덮는다. 남겨 두면 잠긴 재료가 그대로 잘린다.
            if (c is Mask) continue;

            // Canvas 는 뗄 수 없다 — GraphicRaycaster 가 물고 있어 유니티가 거부한다
            // ("Can't remove Canvas because GraphicRaycaster depends on it").
            // 대신 무력화한다. overrideSorting 이 켜진 채로 남으면 복제본이 제 층에 그려져
            // 주문 화면 뒤로 숨는다. 레이캐스터 쪽은 아래 Destroy 가 가져간다.
            if (c is Canvas nested)
            {
                nested.overrideSorting = false;
                continue;
            }

            Destroy(c);
        }
    }
}
