using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 마우스 커서를 대신하는 조리 도구. 시스템 커서는 숨기고 이 그림이 마우스를 따라다닌다.
/// 평소에는 젓가락, 타래·육수·향미유를 뜨면 국자, 시치미를 들면 병 자체가 된다.
/// </summary>
public class CookingCursor : MonoBehaviour
{
    public static CookingCursor Instance { get; private set; }

    // 아래 그림들은 RamenLayoutBuilder가 넣어 준다.
    public Sprite[] chopstickFrames;   // 0=벌림, 1=중간, 2=집음

    /// <summary>
    /// 국자가 기울어 다 붓기까지의 한 벌(35칸). 국물 색이 달라 재료 갈래마다 시트가 따로다.
    ///
    ///   0~4     세운 채, 국물 담김      → 들고 다닐 때 쓰는 그림
    ///   5~26    기울여 붓는 중
    ///   27~34   다시 세움, 빈 국자      → 뜨러 내려갈 때 쓰는 그림
    ///
    /// 기울기를 그림으로 갖고 있는 이유는, 커서를 코드로 돌리면 컵 테두리의 검은 윤곽이
    /// 회전으로 부서지기 때문이다.
    /// </summary>
    /// <summary>
    /// 면 소쿠리. 두 벌씩 있다.
    ///   Drain  통 위에서 물기를 터는 대기 모션 40칸. 0번은 안 흔들린 자세라 들고 다닐 때도 쓴다.
    ///   Pour   그릇에 면을 쏟는 40칸. 마지막 장까지 다 돌아야 그릇에 들어간다.
    /// </summary>
    public Sprite[] noodleThinDrainFrames;
    public Sprite[] noodleThickDrainFrames;
    public Sprite[] noodleThinPourFrames;
    public Sprite[] noodleThickPourFrames;

    public Sprite[] ladleShioFrames;      // 시오 · 향미유 · 육수 (금색)
    public Sprite[] ladleShoyuFrames;     // 쇼유 (갈색)
    public Sprite[] ladleTonkotsuFrames;  // 돈코츠 (크림색)

    /// <summary>
    /// 시치미 병 시트 5칸. 0 = 세운 채, 3 = 붓는 자세, 4 = 터는 끝이다.
    /// 국자와 같은 이유로 그림에 기울기를 구웠다. 코드로 돌리면 병 테두리와 뚜껑이 부서진다.
    /// </summary>
    public Sprite[] bottleFrames;

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
    private static readonly Vector2 BottleTipArt = new Vector2(0f, -26f);

    // 국자만 그림 한 칸이 80이다. 손잡이를 기울인 그림을 담으려면 64칸으로는 모자란다.
    // 컵-손잡이 이음매에서 손잡이 끝까지가 56픽셀이라, 지금 각도에서 3도만 돌려도 칸 밖으로 나간다.
    // 그림 자체는 그대로고 투명 여백만 늘렸으므로, 상자도 같은 비율로 키워야 화면에서 크기가 안 변한다.
    private const float LadleArtFrameSize = 128f;
    // 새 시트에서 국자 컵 바닥은 칸 안 (84, 85)에 있다. 칸 한가운데(64,64) 기준으로 잰 값이다.
    private static readonly Vector2 LadleTipArt = new Vector2(20f, 21f);

    // 면 소쿠리는 칸 한가운데를 그대로 쥔다. 손잡이 끝을 잡게 하려면 여기를 (34,-42)쯤으로 옮긴다.
    private static readonly Vector2 NoodleTipArt = Vector2.zero;

    /// <summary>국자 시트에서 국물이 담긴 채 세워진 칸.</summary>
    private const int LadleFullFrame = 0;

    /// <summary>
    /// 국자를 기울여 다 붓는 데 걸리는 시간(초).
    /// 그릇이 차오르는 길이도 여기에 맞춘다(Bowl.PlayPour). 두 곳에 따로 적으면 어긋난다.
    /// </summary>
    public const float LadlePourSeconds = 0.7f;

    /// <summary>면 소쿠리를 터는 속도(초당 장). 40칸이라 24면 한 바퀴가 1.7초쯤이다.</summary>
    private const float NoodleDrainFps = 24f;

    /// <summary>면을 그릇에 다 쏟는 데 걸리는 시간(초).</summary>
    private const float NoodlePourSeconds = 1.2f;

    /// <summary>면통 위에 마우스가 있는가. 터는 대기 모션은 통 위에서만 돈다.</summary>
    private bool overNoodlePot;

    /// <summary>터는 모션이 얼마나 돌았는지. 통을 벗어나면 0번으로 굳는다.</summary>
    private float drainTime;

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

    // ── 병으로 뿌리는 동작 ───────────────────────────────────────
    private const float PourTiltSeconds = 0.14f;
    private const float PourHoldSeconds = 0.10f;
    private const int ShakeCount = 3;
    private const float ShakeSeconds = 0.07f;

    // 병 시트의 칸. 각도가 0·15·30·45·58도라 한 칸이 대략 15도다.
    // 흔들기는 붓는 자세에서 한 칸 위아래로 오가는 것이고, 예전 ±13도와 같은 폭이다.
    private const int BottleUprightStep = 0;
    private const int BottlePourStep = 3;

    private enum Mode { Chopsticks, Ladle, Bottle, Noodle }

    private RectTransform rect;
    private Image image;
    private Canvas canvas;

    private Mode mode = Mode.Chopsticks;
    private bool gripping;    // 젓가락으로 고체를 집고 있는 중

    /// <summary>젓가락으로 재료를 집고 있는 중인가. 집은 채로는 다른 통을 건드리면 안 된다.</summary>
    public bool IsGripping { get { return gripping; } }
    private float pinch;      // 0 = 벌림, 1 = 다뭄. 재료통 위에서는 벌린 채로 기다린다.
    private float dip;        // 국자가 아래로 내려간 정도
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
        baseSize = rect.sizeDelta;
    }

    /// <summary>빌더가 정해 준 상자 크기. 젓가락·병이 쓰는 값이고, 국자는 여기서 키워 쓴다.</summary>
    private Vector2 baseSize;

    /// <summary>
    /// 모드를 바꾸면서 상자 크기도 같이 맞춘다.
    /// 국자와 면 소쿠리는 그림 칸이 128이라, 64짜리 상자에 넣으면 다른 도구보다 작아진다.
    /// </summary>
    private void SetMode(Mode value)
    {
        mode = value;

        float ratio = (value == Mode.Ladle || value == Mode.Noodle)
            ? LadleArtFrameSize / ArtFrameSize
            : 1f;
        rect.sizeDelta = baseSize * ratio;
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
        TickNoodleDrain();
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
    }

    private float PixelScale()
    {
        float art = (mode == Mode.Ladle || mode == Mode.Noodle) ? LadleArtFrameSize : ArtFrameSize;
        return rect.sizeDelta.x / art;
    }

    /// <summary>그림의 끝이 마우스에 오도록 이미지 중심을 밀어 주는 양.</summary>
    private Vector2 HotspotOffset()
    {
        Vector2 tip;
        switch (mode)
        {
            case Mode.Ladle: tip = LadleTipArt; break;
            case Mode.Noodle: tip = NoodleTipArt; break;
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
    /// 지금 도구를 바꾸면 안 되는 상태인가.
    ///
    /// 통에서 무언가를 들었거나 뜨는 중이면, 마우스가 다른 통 위를 지나가도 도구를 그대로 둔다.
    /// 들고 그릇까지 가는 길에 다른 통을 스치는 일이 흔한데, 거기서 도구가 바뀌면
    /// 들고 있던 것이 사라진 것처럼 보인다.
    ///
    /// frozen  뿌리는 동작 중
    /// IsHolding  이미 들었다 (병은 누르는 순간부터, 국자는 다 뜬 뒤부터)
    /// motion  뜨거나 뿌리는 코루틴이 도는 중 (국자를 뜨는 도중이 여기에 걸린다)
    /// </summary>
    private bool Busy
    {
        get { return frozen || IsHolding || motion != null; }
    }

    /// <summary>
    /// 통 위에 마우스가 올라왔을 때. 내용물 없이 도구 모양만 그 통에 맞춘다.
    /// 실제로 뜨는 것은 통에서 끌기 시작할 때(PickUp)다.
    /// </summary>
    public void PreviewTool(IngredientType type)
    {
        // 젓가락으로 재료를 집고 있는 동안에는 도구를 바꾸지 않는다.
        // 집은 채로는 어차피 뜨거나 부을 수 없는데, 커서만 국자로 바뀌면
        // 집은 것이 사라진 것처럼 보인다.
        //
        // SlotHover 에도 같은 검사가 있지만 그것은 고체 재료통에만 붙는다.
        // 액체·조미료통은 LiquidSlot 이 직접 이 함수를 부르므로 여기서도 막아야 한다.
        if (Busy || gripping) return;

        if (IsBottle(type))
        {
            if (mode == Mode.Bottle && Held == type) return;   // 이미 그 병이면 그대로 둔다
            StopMotion();
            SetMode(Mode.Bottle);
            IsHolding = false;
            Held = type;
            image.sprite = BottleSprite(BottleUprightStep);
            dip = 0f;
            return;
        }

        if (IsNoodle(type))
        {
            if (mode == Mode.Noodle && Held == type) return;   // 이미 그 면이면 그대로 둔다
            StopMotion();
            SetMode(Mode.Noodle);
            IsHolding = false;
            Held = type;
            drainTime = 0f;
            image.sprite = Frame(DrainSheet(type), 0);
            dip = 0f;
            return;
        }

        if (!IsLadleType(type)) return;

        // 국자는 비어 있는 모양으로 보여 준다. 담긴 그림은 실제로 펐을 때만 쓴다.
        if (mode == Mode.Ladle && !IsHolding && Held == type) return;
        StopMotion();
        SetMode(Mode.Ladle);
        IsHolding = false;
        Held = type;
        image.sprite = EmptyLadle(type);
        dip = 0f;
    }

    /// <summary>
    /// 고체 재료 위에 마우스가 올라왔을 때. 젓가락으로 되돌린다.
    /// 국자나 병에 담아 둔 것은 여기서 버려진다.
    /// </summary>
    public void UseChopsticks()
    {
        if (Busy || mode == Mode.Chopsticks) return;
        Drop();
    }

    /// <summary>통에서 끌기 시작했을 때. 액체는 국자로 뜨고, 조미료는 병째로 든다.</summary>
    public void PickUp(IngredientType type)
    {
        if (IsBottle(type))
        {
            StopMotion();
            SetMode(Mode.Bottle);
            IsHolding = true;
            Held = type;
            image.sprite = BottleSprite(BottleUprightStep);
            dip = 0f;
            Debug.Log("[커서] " + type + " 병을 들었습니다.");
            return;
        }

        if (IsNoodle(type))
        {
            StopMotion();
            SetMode(Mode.Noodle);
            IsHolding = true;
            Held = type;
            image.sprite = Frame(DrainSheet(type), 0);
            dip = 0f;
            Debug.Log("[면] " + type + " 소쿠리를 들었습니다.");
            return;
        }

        if (!IsLadleType(type))
        {
            Debug.LogWarning("[CookingCursor] 들 수 없는 재료입니다: " + type);
            return;
        }

        StopMotion();
        motion = StartCoroutine(ScoopRoutine(type));
    }

    private IEnumerator ScoopRoutine(IngredientType type)
    {
        SetMode(Mode.Ladle);
        IsHolding = false;
        Held = type;

        // 새 시트는 붓는 동작 한 벌이라 "뜨는" 그림이 따로 없다.
        // 끝 칸이 빈 국자, 첫 칸이 담긴 국자라 그 둘만 바꿔 끼우면 뜨는 것처럼 보인다.
        image.sprite = EmptyLadle(type);

        for (float t = 0f; t < DipDownSeconds; t += Time.unscaledDeltaTime)
        {
            dip = Mathf.Lerp(0f, DipDepth, t / DipDownSeconds);
            yield return null;
        }

        // 바닥에서 국물이 담긴다
        dip = DipDepth;
        image.sprite = FullLadle(type);
        IsHolding = true;

        for (float t = 0f; t < DipUpSeconds; t += Time.unscaledDeltaTime)
        {
            dip = Mathf.Lerp(DipDepth, 0f, t / DipUpSeconds);
            yield return null;
        }

        dip = 0f;
        motion = null;
        Debug.Log("[국자] " + type + "을(를) 펐습니다.");
    }

    // ── 그릇에 넣기 ──────────────────────────────────────────────

    /// <summary>
    /// 그릇에 놓았을 때 부른다. 병은 기울여 뿌리는 동작을 보여 준다.
    ///
    /// 병과 소쿠리는 동작이 다 끝나야 onDone 을 부른다. 넣어지는 판정을 그때 해야 하기
    /// 때문이다 — 국자를 다 퍼야 담기는 것과 같은 규칙이다.
    /// 국자만 예외로 기울이기 시작할 때 부른다. 그릇이 차오르는 것이 붓는 동작과 겹쳐
    /// 보여야 한다.
    /// </summary>
    public void Deliver(System.Action onDone = null)
    {
        if (mode == Mode.Bottle)
        {
            StopMotion();
            motion = StartCoroutine(PourRoutine(onDone));
            return;
        }

        if (mode == Mode.Ladle)
        {
            StopMotion();
            motion = StartCoroutine(LadlePourRoutine(onDone));
            return;
        }

        if (mode == Mode.Noodle)
        {
            StopMotion();
            motion = StartCoroutine(NoodlePourRoutine(onDone));
            return;
        }

        Drop();
        if (onDone != null) onDone();
    }

    private IEnumerator PourRoutine(System.Action onDone)
    {
        IsHolding = false;   // 붓는 동안 또 넣지 못하게
        frozen = true;       // 병이 마우스를 따라다니면 뿌리는 동작이 읽히지 않는다

        // 세운 자세에서 붓는 자세까지 기울인다
        for (float t = 0f; t < PourTiltSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / PourTiltSeconds;
            image.sprite = BottleSprite(Mathf.RoundToInt(k * BottlePourStep));
            yield return null;
        }
        image.sprite = BottleSprite(BottlePourStep);

        // 탈탈 턴다. 사인 한 바퀴가 붓는 자세 → 한 칸 위 → 붓는 자세 → 한 칸 아래 → 붓는 자세다.
        for (int i = 0; i < ShakeCount; i++)
        {
            for (float t = 0f; t < ShakeSeconds; t += Time.unscaledDeltaTime)
            {
                float swing = Mathf.Sin(t / ShakeSeconds * Mathf.PI * 2f);
                image.sprite = BottleSprite(BottlePourStep + Mathf.RoundToInt(swing));
                yield return null;
            }
        }

        image.sprite = BottleSprite(BottlePourStep);
        yield return new WaitForSecondsRealtime(PourHoldSeconds);

        for (float t = 0f; t < PourTiltSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / PourTiltSeconds;
            image.sprite = BottleSprite(Mathf.RoundToInt((1f - k) * BottlePourStep));
            yield return null;
        }

        motion = null;
        frozen = false;
        Drop();

        // 다 뿌리고 나서야 그릇에 들어간다.
        if (onDone != null) onDone();
    }

    /// <summary>들고 있던 것을 놓고 젓가락으로 돌아간다.</summary>
    public void Drop()
    {
        StopMotion();

        IsHolding = false;
        SetMode(Mode.Chopsticks);
        dip = 0f;
        pinch = 0f;
    }

    private void StopMotion()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;

        // 뿌리는 도중에 다른 동작이 끼어들어도 커서가 얼어붙은 채 남지 않게 한다.
        frozen = false;
    }

    /// <summary>
    /// 국자를 기울여 다 붓는다. 병·소쿠리와 달리 기울이기 시작하는 순간 그릇에 들어간다.
    /// 국물이 차오르는 것이 붓는 동작과 같이 보여야 하기 때문이다 — 다 붓고 나서 넣으면
    /// 국자가 원위치한 뒤에야 그릇이 차서 따로 노는 것처럼 보인다.
    ///
    /// 그릇 쪽 차오름도 LadlePourSeconds 에 맞춰져 있어 둘이 같이 끝난다.
    /// 못 넣는 재료였다면 붓는 도중에 그릇이 붉게 깜빡인다.
    /// </summary>
    private IEnumerator LadlePourRoutine(System.Action onDone)
    {
        IsHolding = false;   // 붓는 동안 또 넣지 못하게
        frozen = true;       // 국자가 마우스를 따라다니면 붓는 동작이 읽히지 않는다

        // 기울이기 시작하는 순간 그릇이 차오르기 시작한다.
        if (onDone != null) onDone();

        Sprite[] sheet = LadleSheet(Held);
        int last = sheet != null ? sheet.Length - 1 : 0;

        for (float t = 0f; t < LadlePourSeconds; t += Time.unscaledDeltaTime)
        {
            int step = Mathf.Clamp(Mathf.FloorToInt(t / LadlePourSeconds * (last + 1)), 0, last);
            image.sprite = Frame(sheet, step);
            yield return null;
        }

        image.sprite = Frame(sheet, last);
        motion = null;
        frozen = false;
        Drop();
    }

    /// <summary>
    /// 면통 위에 마우스가 있는지 NoodleSlot 이 알려 준다.
    /// 통 위에서는 소쿠리를 털고, 벗어나면 0번에서 굳는다.
    /// </summary>
    public void SetOverNoodlePot(bool value)
    {
        overNoodlePot = value;
        if (!value) drainTime = 0f;
    }

    /// <summary>
    /// 소쿠리를 터는 대기 모션. Follow 가 매 프레임 부른다.
    /// 붓는 중(frozen)에는 손대지 않는다 — 그때는 쏟는 그림이 돌고 있다.
    /// </summary>
    private void TickNoodleDrain()
    {
        if (mode != Mode.Noodle || frozen) return;

        Sprite[] sheet = DrainSheet(Held);
        if (sheet == null || sheet.Length == 0) return;

        if (!overNoodlePot)
        {
            image.sprite = sheet[0];
            return;
        }

        drainTime += Time.unscaledDeltaTime;
        image.sprite = sheet[Mathf.Abs(Mathf.FloorToInt(drainTime * NoodleDrainFps)) % sheet.Length];
    }

    /// <summary>
    /// 면을 그릇에 쏟는다. 마지막 장까지 다 돌아야 그릇에 들어간다.
    /// 국자·시치미와 같은 규칙이다.
    /// </summary>
    private IEnumerator NoodlePourRoutine(System.Action onDone)
    {
        IsHolding = false;   // 붓는 동안 또 넣지 못하게
        frozen = true;       // 소쿠리가 마우스를 따라다니면 쏟는 동작이 읽히지 않는다

        Sprite[] sheet = PourSheet(Held);
        int last = sheet != null ? sheet.Length - 1 : 0;

        for (float t = 0f; t < NoodlePourSeconds; t += Time.unscaledDeltaTime)
        {
            int step = Mathf.Clamp(Mathf.FloorToInt(t / NoodlePourSeconds * (last + 1)), 0, last);
            image.sprite = Frame(sheet, step);
            yield return null;
        }

        image.sprite = Frame(sheet, last);
        motion = null;
        frozen = false;
        Drop();

        // 다 쏟고 나서야 그릇에 들어간다.
        if (onDone != null) onDone();
    }

    private static bool IsNoodle(IngredientType type)
    {
        return type == IngredientType.ThinNoodles || type == IngredientType.ThickNoodles;
    }

    private Sprite[] DrainSheet(IngredientType type)
    {
        return type == IngredientType.ThickNoodles ? noodleThickDrainFrames : noodleThinDrainFrames;
    }

    private Sprite[] PourSheet(IngredientType type)
    {
        return type == IngredientType.ThickNoodles ? noodleThickPourFrames : noodleThinPourFrames;
    }

    /// <summary>국자로 뜰 수 있는 재료인가.</summary>
    private static bool IsLadleType(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.ShioTare:
            case IngredientType.ShoyuTare:
            case IngredientType.TonkotsuBase:
            case IngredientType.Broth:
            case IngredientType.FlavorOil:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 이 재료가 쓰는 국자 시트. 국물 색이 같은 것끼리 한 장을 나눠 쓴다.
    ///
    /// 육수는 제 시트가 아직 없어서 금색(시오)을 빌려 쓴다. 냄비 안 육수가 주황색이라
    /// 셋 중에서는 이쪽이 그나마 가깝다. 육수용 그림이 들어오면 여기만 바꾸면 된다.
    /// </summary>
    private Sprite[] LadleSheet(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.ShoyuTare: return ladleShoyuFrames;
            case IngredientType.TonkotsuBase: return ladleTonkotsuFrames;
            default: return ladleShioFrames;   // 시오 · 향미유 · 육수
        }
    }

    /// <summary>국물이 담긴 채 세워진 국자.</summary>
    private Sprite FullLadle(IngredientType type)
    {
        return Frame(LadleSheet(type), LadleFullFrame);
    }

    /// <summary>다 붓고 난 빈 국자. 시트 맨 끝 칸이다.</summary>
    private Sprite EmptyLadle(IngredientType type)
    {
        Sprite[] sheet = LadleSheet(type);
        return Frame(sheet, sheet != null ? sheet.Length - 1 : 0);
    }

    private static Sprite Frame(Sprite[] sheet, int index)
    {
        if (sheet == null || sheet.Length == 0) return null;
        return sheet[Mathf.Clamp(index, 0, sheet.Length - 1)];
    }

    /// <summary>병으로 드는 재료인가. 지금은 시치미뿐이고, 향미유는 국자로 옮겼다.</summary>
    private static bool IsBottle(IngredientType type)
    {
        return type == IngredientType.ChiliPowder;
    }

    /// <summary>병 시트에서 한 장을 꺼낸다. 시트가 없거나 짧으면 null이다.</summary>
    private Sprite BottleSprite(int step)
    {
        if (bottleFrames == null || bottleFrames.Length == 0) return null;
        return bottleFrames[Mathf.Clamp(step, 0, bottleFrames.Length - 1)];
    }
}
