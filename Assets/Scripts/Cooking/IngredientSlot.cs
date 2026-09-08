using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 고체 재료가 담긴 재료통. 여기서 그릇으로 드래그해 재료를 투입한다.
/// 액체(타래·육수)는 국자를 거쳐야 하므로 LiquidSlot이 따로 맡는다.
/// 실제 투입 판정은 Bowl.OnDrop이 한다. 슬롯은 고스트를 따라다니게 하는 것까지만 책임진다.
/// </summary>
public class IngredientSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 아래 셋 다 RamenLayoutBuilder가 생성 시 넣어 준다.
    public IngredientType type;

    /// <summary>그릇 안에 얹히는 재료 그림. 조미료는 그릇용 그림이 없어 비어 있다.</summary>
    public Sprite bowlSprite;

    /// <summary>드래그할 때 마우스를 따라다니는 그림. 테두리가 강조된 판이 있으면 그걸 쓴다.</summary>
    public Sprite dragSprite;

    /// <summary>
    /// 고스트 크기 배율. 재료통이 표준(192)보다 크면 원본 그림도 그만큼 크게 그려져 있다는 뜻이라
    /// 같은 비율로 키워야 다른 재료와 눈에 보이는 크기가 맞는다.
    /// 면은 원본이 128px(통 384)이고 나머지는 64px(통 192)라, 안 키우면 면만 절반으로 보인다.
    /// </summary>
    public float ghostScale = 1f;

    private const string DragLayerName = "DragLayer";

    // 젓가락에 집힌 것처럼 보이도록 커서 크기의 절반으로 띄운다.
    // 그릇에 놓이면 Bowl이 훨씬 큰 크기로 다시 그린다.
    private const float GhostToCursorRatio = 0.5f;
    private const float FallbackGhostSide = 128f;

    private GameObject ghost;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Transform dragLayer = transform.root.Find(DragLayerName);
        if (dragLayer == null)
        {
            Debug.LogWarning("[IngredientSlot] DragLayer를 찾지 못했습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
            return;
        }

        // 국자나 병을 들고 있었다면 내려놓고 젓가락으로 돌아간다. 집는 모양도 여기서 켠다.
        if (CookingCursor.Instance != null) CookingCursor.Instance.SetGripping(true);

        ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rt = ghost.GetComponent<RectTransform>();
        rt.SetParent(dragLayer, false);
        float side = CookingCursor.Instance != null
            ? CookingCursor.Instance.Size * GhostToCursorRatio
            : FallbackGhostSide;
        if (ghostScale > 0f) side *= ghostScale;
        rt.sizeDelta = new Vector2(side, side);

        var img = ghost.GetComponent<UnityEngine.UI.Image>();
        img.sprite = dragSprite != null ? dragSprite : bowlSprite;
        img.preserveAspect = true;

        // 고스트가 레이캐스트를 먹으면 마우스 밑이 항상 고스트라 그릇의 OnDrop이 영영 안 불린다.
        img.raycastTarget = false;

        rt.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghost != null) ghost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;

        if (CookingCursor.Instance != null) CookingCursor.Instance.SetGripping(false);
    }

    // TODO(해금단계): SetLocked(bool) — 미해금 재료를 회색 처리하고 드래그를 막는다.
}
