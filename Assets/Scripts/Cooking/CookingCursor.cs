using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 마우스 커서를 대신하는 조리 도구. 시스템 커서는 숨기고 이 그림이 마우스를 따라다닌다.
/// 평소에는 젓가락, 타래·육수를 뜨면 국자, 조미료를 들면 병 자체가 된다.
/// </summary>
public class CookingCursor : MonoBehaviour
{
    public static CookingCursor Instance { get; private set; }

    // 아래 그림들은 RamenLayoutBuilder가 넣어 준다.
    public Sprite[] chopstickFrames;   // 0=벌림, 1=중간, 2=집음
    public Sprite ladleEmpty;
    public Sprite ladleShio;
    public Sprite ladleShoyu;
    public Sprite ladleTonkotsu;
    public Sprite ladleBroth;
    public Sprite flavorOilBottle;
    public Sprite chiliPowderBottle;

    // 메뉴 화면에서는 조리 도구 대신 시스템 화살표를 쓴다.
    // 버튼을 누르는 화면에 젓가락이 떠 있으면 어디를 가리키는지 읽히지 않는다.
    public GameObject[] uiScreens;   // 하나라도 켜져 있으면 젓가락을 감춘다
    public Texture2D arrowCursor;         // 그동안 띄울 픽셀 화살표
    public Texture2D arrowCursorPressed;  // 누르고 있는 동안 바꿔 끼울 그림
    public Vector2 arrowHotspot;          // 화살표 끝이 가리키는 점 (그림 좌표)

    /// <summary>국자나 병에 무언가 들려 있는가. 들려 있어야 그릇에 부을 수 있다.</summary>
    public bool IsHolding { get; private set; }
    public IngredientType Held { get; private set; }

    // ── 커서 끝이 마우스에 오도록 맞추는 값 ────────────────────────
    // 원본 64px 그림에서 잰 "실제로 가리키는 점"의 중심 기준 좌표. 이미지 좌표라 y는 아래가 +.
    // 젓가락은 집는 끝(세 프레임 모두 10,55), 국자는 국자 컵(2,57), 병은 주둥이.
    // 그림 한 변 길이로 나눠 쓰므로 커서 크기를 바꿔도 끝이 계속 마우스에 붙는다.
    private const float ArtFrameSize = 64f;
    private static readonly Vector2 ChopstickTipArt = new Vector2(-22f, 23f);
    private static readonly Vector2 LadleTipArt = new Vector2(-30f, 25f);
    private static readonly Vector2 BottleTipArt = new Vector2(0f, -26f);

    // ── 젓가락 ───────────────────────────────────────────────────
    private const float PinchSpeed = 12f;   // 0(벌림)에서 1(집음)까지 가는 속도

    // 재료를 집으면 젓가락이 자기 축을 따라 뒤로 물러난다.
    // 안 그러면 끝이 재료 한가운데를 찔러 얹혀 있는 것처럼 보인다.
    private static readonly Vector2 GripDirection = new Vector2(0.77f, 0.63f);   // 끝 → 손잡이 방향
    private const float GripBackArt = 11f;

    // ── 국자로 뜨는 동작 ─────────────────────────────────────────
    private const float DipDownSeconds = 0.12f;
    private const float DipUpSeconds = 0.18f;
    private const float DipDepth = 12f;
    private const float DipTilt = -22f;

    // ── 병으로 뿌리는 동작 ───────────────────────────────────────
    private const float PourTiltSeconds = 0.14f;
    private const float PourHoldSeconds = 0.10f;
    private const float PourTilt = 45f;        // 왼쪽으로 기울인다 (반시계)
    private const int ShakeCount = 3;
    private const float ShakeSeconds = 0.07f;
    private const float ShakeAngle = 13f;

    private enum Mode { Chopsticks, Ladle, Bottle }

    private RectTransform rect;
    private Image image;
    private Canvas canvas;

    private Mode mode = Mode.Chopsticks;
    private bool gripping;    // 젓가락으로 고체를 집고 있는 중

    /// <summary>젓가락으로 재료를 집고 있는 중인가. 집은 채로는 다른 통을 건드리면 안 된다.</summary>
    public bool IsGripping { get { return gripping; } }
    private float pinch;      // 0 = 벌림, 1 = 다뭄. 재료통 위에서는 벌린 채로 기다린다.
    private float dip;        // 국자가 아래로 내려간 정도
    private float tilt;
    private Coroutine motion;

    /// <summary>참이면 마우스를 따라가지 않는다. 뿌리는 동작 중에만 켠다.</summary>
    private bool frozen;

    /// <summary>지금 시스템 화살표를 쓰고 있는가. 도구를 감춘 상태다.</summary>
    private bool arrowMode = true;

    /// <summary>마우스가 올라와 있는 재료통. 없으면 도구를 감추고 화살표로 돌아간다.</summary>
    private GameObject hoveredSlot;

    /// <summary>화살표가 눌린 그림으로 바뀌어 있는가.</summary>
    private bool arrowPressed;

    /// <summary>드래그 고스트가 커서에 비례한 크기로 나오도록 알려 준다.</summary>
    public float Size
    {
        get { return rect != null ? rect.sizeDelta.x : 0f; }
    }

    private void Awake()
    {
        Instance = this;
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        arrowMode = AnyUiOpen() || !ToolVisible();
        ApplyCursorMode();
    }

    private void OnDisable()
    {
        // 플레이를 멈춰도 화살표가 남지 않도록 시스템 기본값으로 돌려 놓는다.
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        if (Instance == this) Instance = null;
    }

    /// <summary>주문 화면·팝업 중 하나라도 열려 있는가.</summary>
    private bool AnyUiOpen()
    {
        if (uiScreens == null) return false;

        for (int i = 0; i < uiScreens.Length; i++)
        {
            if (uiScreens[i] != null && uiScreens[i].activeInHierarchy) return true;
        }
        return false;
    }

    /// <summary>재료통 위에 있는지 SlotHover가 알려 준다.</summary>
    public void EnterSlot(GameObject slot)
    {
        hoveredSlot = slot;
    }

    public void ExitSlot(GameObject slot)
    {
        // 통에서 통으로 바로 넘어가면 나간 통의 이탈이 늦게 올 수 있다. 그때 새 통을 지우면 안 된다.
        if (hoveredSlot == slot) hoveredSlot = null;
    }

    /// <summary>조리 도구를 띄울 때인가. 아니면 시스템 화살표를 쓴다.</summary>
    private bool ToolVisible()
    {
        // 통 위에 있거나, 무언가를 들고 그릇으로 가는 중이거나, 뜨고 뿌리는 동작 중일 때.
        return hoveredSlot != null || gripping || IsHolding || motion != null;
    }

    private void ApplyCursorMode()
    {
        if (image != null) image.enabled = !arrowMode;
        Cursor.visible = arrowMode;
        arrowPressed = false;
        if (arrowMode && arrowCursor != null) Cursor.SetCursor(arrowCursor, arrowHotspot, CursorMode.Auto);
    }

    /// <summary>누르고 있는 동안 화살표를 눌린 그림으로 바꾼다.</summary>
    private void UpdateArrowPress()
    {
        bool down = Mouse.current.leftButton.isPressed;

        // 바뀌는 순간에만 갈아 끼운다. 매 프레임 부르면 OS 커서를 계속 새로 만들어 깜빡인다.
        if (down == arrowPressed) return;

        arrowPressed = down;
        Texture2D tex = (down && arrowCursorPressed != null) ? arrowCursorPressed : arrowCursor;
        if (tex != null) Cursor.SetCursor(tex, arrowHotspot, CursorMode.Auto);
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        bool arrow = AnyUiOpen() || !ToolVisible();
        if (arrow != arrowMode)
        {
            arrowMode = arrow;
            ApplyCursorMode();
        }
        if (arrowMode)
        {
            UpdateArrowPress();
            return;   // 화살표가 도는 동안 젓가락은 멈춰 둔다
        }

        UpdateSprite();
        Follow();
    }

    private void UpdateSprite()
    {
        if (mode != Mode.Chopsticks) return;   // 국자·병 그림은 동작 코루틴이 정한다

        // 재료통 위에서는 벌린 채로 기다리고, 누르는 동안만 다문다.
        // 젓가락은 통 위에 있을 때만 뜨므로, 벌린 모양이 곧 "여기서 집을 수 있다"는 표시가 된다.
        float target = Mouse.current.leftButton.isPressed ? 1f : 0f;

        // 집는 순간은 보간하지 않는다. 천천히 다물면 재료를 든 뒤에 한 번 더 움직이는 것처럼 보인다.
        if (gripping) pinch = 1f;
        else pinch = Mathf.MoveTowards(pinch, target, PinchSpeed * Time.unscaledDeltaTime);

        if (chopstickFrames == null || chopstickFrames.Length == 0) return;

        int index = Mathf.Clamp(Mathf.RoundToInt(pinch * (chopstickFrames.Length - 1)),
                                0, chopstickFrames.Length - 1);
        image.sprite = chopstickFrames[index];
    }

    private void Follow()
    {
        // 뿌리는 동안에는 병이 그 자리에 머물러야 한다. 끝나면 다시 마우스를 따라간다.
        if (frozen) return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 screen = Mouse.current.position.ReadValue();

        Vector2 offset = HotspotOffset() + new Vector2(0f, -dip);

        rect.position = screen + offset * scale;
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    private float PixelScale()
    {
        return rect.sizeDelta.x / ArtFrameSize;
    }

    /// <summary>그림의 끝이 마우스에 오도록 이미지 중심을 밀어 주는 양.</summary>
    private Vector2 HotspotOffset()
    {
        Vector2 tip;
        switch (mode)
        {
            case Mode.Ladle: tip = LadleTipArt; break;
            case Mode.Bottle: tip = BottleTipArt; break;
            default: tip = ChopstickTipArt; break;
        }

        // 이미지 좌표(y 아래가 +)를 UI 좌표(y 위가 +)로 뒤집고 반대 방향으로 민다.
        return new Vector2(-tip.x, tip.y) * PixelScale();
    }

    // ── 집기 ─────────────────────────────────────────────────────

    /// <summary>재료통에서 고체를 끌기 시작·끝냈을 때 IngredientSlot이 알려 준다.</summary>
    public void SetGripping(bool value)
    {
        gripping = value;
        if (value) Drop();   // 국자나 병을 들고 있었다면 내려놓고 젓가락으로 돌아간다
    }

    // ── 액체·조미료 들기 ─────────────────────────────────────────

    /// <summary>
    /// 통 위에 마우스가 올라왔을 때. 내용물 없이 도구 모양만 그 통에 맞춘다.
    /// 실제로 뜨는 것은 클릭(PickUp)이다.
    /// </summary>
    public void PreviewTool(IngredientType type)
    {
        if (frozen) return;

        Sprite bottle = BottleFor(type);
        if (bottle != null)
        {
            if (mode == Mode.Bottle && Held == type) return;   // 이미 그 병이면 그대로 둔다
            StopMotion();
            mode = Mode.Bottle;
            IsHolding = false;
            Held = type;
            image.sprite = bottle;
            tilt = 0f;
            dip = 0f;
            return;
        }

        Sprite ladle = LadleFor(type);
        if (ladle == null) return;

        // 국자는 비어 있는 모양으로 보여 준다. 담긴 그림은 실제로 펐을 때만 쓴다.
        if (mode == Mode.Ladle && !IsHolding && Held == type) return;
        StopMotion();
        mode = Mode.Ladle;
        IsHolding = false;
        Held = type;
        image.sprite = ladleEmpty;
        tilt = 0f;
        dip = 0f;
    }

    /// <summary>
    /// 고체 재료 위에 마우스가 올라왔을 때. 젓가락으로 되돌린다.
    /// 국자나 병에 담아 둔 것은 여기서 버려진다.
    /// </summary>
    public void UseChopsticks()
    {
        if (frozen || mode == Mode.Chopsticks) return;
        Drop();
    }

    /// <summary>통을 클릭했을 때. 액체는 국자로 뜨고, 조미료는 병째로 든다.</summary>
    public void PickUp(IngredientType type)
    {
        Sprite bottle = BottleFor(type);
        if (bottle != null)
        {
            StopMotion();
            mode = Mode.Bottle;
            IsHolding = true;
            Held = type;
            image.sprite = bottle;
            tilt = 0f;
            dip = 0f;
            Debug.Log("[커서] " + type + " 병을 들었습니다.");
            return;
        }

        if (LadleFor(type) == null)
        {
            Debug.LogWarning("[CookingCursor] 들 수 없는 재료입니다: " + type);
            return;
        }

        StopMotion();
        motion = StartCoroutine(ScoopRoutine(type));
    }

    private IEnumerator ScoopRoutine(IngredientType type)
    {
        mode = Mode.Ladle;
        IsHolding = false;
        Held = type;
        image.sprite = ladleEmpty;

        for (float t = 0f; t < DipDownSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / DipDownSeconds;
            dip = Mathf.Lerp(0f, DipDepth, k);
            tilt = Mathf.Lerp(0f, DipTilt, k);
            yield return null;
        }

        // 바닥에서 국물이 담긴다
        dip = DipDepth;
        tilt = DipTilt;
        image.sprite = LadleFor(type);
        IsHolding = true;

        for (float t = 0f; t < DipUpSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / DipUpSeconds;
            dip = Mathf.Lerp(DipDepth, 0f, k);
            tilt = Mathf.Lerp(DipTilt, 0f, k);
            yield return null;
        }

        dip = 0f;
        tilt = 0f;
        motion = null;
        Debug.Log("[국자] " + type + "을(를) 펐습니다.");
    }

    // ── 그릇에 넣기 ──────────────────────────────────────────────

    /// <summary>그릇이 재료를 받아들인 뒤 부른다. 병은 기울여 뿌리는 동작을 보여 준다.</summary>
    public void Deliver()
    {
        if (mode == Mode.Bottle)
        {
            StopMotion();
            motion = StartCoroutine(PourRoutine());
            return;
        }

        Drop();
    }

    private IEnumerator PourRoutine()
    {
        IsHolding = false;   // 붓는 동안 또 넣지 못하게
        frozen = true;       // 병이 마우스를 따라다니면 뿌리는 동작이 읽히지 않는다

        for (float t = 0f; t < PourTiltSeconds; t += Time.unscaledDeltaTime)
        {
            tilt = Mathf.Lerp(0f, PourTilt, t / PourTiltSeconds);
            yield return null;
        }
        tilt = PourTilt;

        // 탈탈 턴다
        for (int i = 0; i < ShakeCount; i++)
        {
            for (float t = 0f; t < ShakeSeconds; t += Time.unscaledDeltaTime)
            {
                tilt = PourTilt + Mathf.Sin(t / ShakeSeconds * Mathf.PI * 2f) * ShakeAngle;
                yield return null;
            }
        }

        tilt = PourTilt;
        yield return new WaitForSecondsRealtime(PourHoldSeconds);

        for (float t = 0f; t < PourTiltSeconds; t += Time.unscaledDeltaTime)
        {
            tilt = Mathf.Lerp(PourTilt, 0f, t / PourTiltSeconds);
            yield return null;
        }

        motion = null;
        frozen = false;
        Drop();
    }

    /// <summary>들고 있던 것을 놓고 젓가락으로 돌아간다.</summary>
    public void Drop()
    {
        StopMotion();

        IsHolding = false;
        mode = Mode.Chopsticks;
        dip = 0f;
        tilt = 0f;
        pinch = 0f;
    }

    private void StopMotion()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;

        // 뿌리는 도중에 다른 동작이 끼어들어도 커서가 얼어붙은 채 남지 않게 한다.
        frozen = false;
    }

    private Sprite LadleFor(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.ShioTare: return ladleShio;
            case IngredientType.ShoyuTare: return ladleShoyu;
            case IngredientType.TonkotsuBase: return ladleTonkotsu;
            case IngredientType.Broth: return ladleBroth;
            default: return null;
        }
    }

    private Sprite BottleFor(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.FlavorOil: return flavorOilBottle;
            case IngredientType.ChiliPowder: return chiliPowderBottle;
            default: return null;
        }
    }
}
