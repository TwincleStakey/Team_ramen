using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 고체 재료가 담긴 재료통. 여기서 그릇으로 드래그해 재료를 투입한다.
/// 액체(타래·육수)는 국자를 거쳐야 하므로 LiquidSlot이 따로 맡는다.
/// 실제 투입 판정은 Bowl.OnDrop이 한다. 슬롯은 고스트를 따라다니게 하는 것까지만 책임진다.
/// </summary>
public class IngredientSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                              IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler
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
    private const float FallbackGhostSide = 64f;

    private GameObject ghost;

    /// <summary>한 번 찾으면 계속 쓴다. 집을 때마다 계층을 훑으면 아깝다.</summary>
    private Transform cachedDragLayer;

    /// <summary>
    /// 끌고 다니는 그림을 올릴 판을 찾는다.
    ///
    /// 예전에는 root 바로 밑에 있어서 root.Find로 충분했다. 화면을 640x360으로 옮기면서
    /// 모든 UI가 Frame 아래로 한 단 내려갔고, Find는 바로 밑 자식만 보기 때문에 못 찾게 됐다.
    /// 못 찾으면 재료가 젓가락에 안 달리고, 그 뒤에 있는 SetGripping도 건너뛰어져
    /// 끄는 도중에 커서가 화살표로 돌아가 버린다. 그래서 깊이와 무관하게 찾는다.
    /// </summary>
    private Transform DragLayer
    {
        get
        {
            if (cachedDragLayer != null) return cachedDragLayer;

            Transform root = transform.root;
            cachedDragLayer = root.Find(DragLayerName);
            if (cachedDragLayer != null) return cachedDragLayer;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == DragLayerName) { cachedDragLayer = t; break; }
            }
            return cachedDragLayer;
        }
    }

    /// <summary>
    /// 고체 재료 위에서는 젓가락이어야 한다. 국자나 병을 들고 있었다면 여기서 내려놓는다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CookingCursor.Instance != null) CookingCursor.Instance.UseChopsticks();
    }

    /// <summary>
    /// 젓가락이 다무는 순간에 재료가 손에 들려야 한다.
    /// OnBeginDrag는 마우스가 몇 픽셀 움직여야 오므로, 그때까지 집었는데 아무것도 없어 보인다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 튜토리얼 중에는 안내한 재료 말고는 아예 집히지 않는다(기획서 v1.2 9장).
        if (!TutorialManager.CanPick(type)) return;

        CreateGhost(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 끌지 않고 눌렀다 뗀 경우에는 OnEndDrag가 오지 않는다. 여기서 치워야 남지 않는다.
        ClearGhost();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // OnPointerDown 에서 막아도 여기로 다시 들어온다. 그쪽에서 거부하면 ghost 가 null 로
        // 남아 있어서 아래 줄이 그대로 만들어 버린다. 두 곳 다 막아야 한다.
        if (!TutorialManager.CanPick(type)) return;

        // 누를 때 이미 만들어 뒀다. 눌림을 놓친 경우에만 여기서 만든다.
        if (ghost == null) CreateGhost(eventData.position);
    }

    private void Update()
    {
        // 드래그로 인정되기 전 몇 픽셀 동안에도 재료가 마우스를 따라와야 한다.
        if (ghost != null && Mouse.current != null) ghost.transform.position = Mouse.current.position.ReadValue();
    }

    private void CreateGhost(Vector2 position)
    {
        if (ghost != null) return;

        // 국자나 병을 들고 있었다면 내려놓고 젓가락으로 돌아간다. 집는 모양도 여기서 켠다.
        // 판을 못 찾아도 이건 먼저 해야 한다. 뒤에 두면 판을 못 찾은 날 커서까지 같이 망가진다.
        if (CookingCursor.Instance != null) CookingCursor.Instance.SetGripping(true);

        Transform dragLayer = DragLayer;
        if (dragLayer == null)
        {
            Debug.LogWarning("[IngredientSlot] DragLayer를 찾지 못했습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
            return;
        }

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

        rt.position = position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghost != null) ghost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ClearGhost();
    }

    private void ClearGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;

        if (CookingCursor.Instance != null) CookingCursor.Instance.SetGripping(false);
    }

    // TODO(해금단계): SetLocked(bool) — 미해금 재료를 회색 처리하고 드래그를 막는다.
}
