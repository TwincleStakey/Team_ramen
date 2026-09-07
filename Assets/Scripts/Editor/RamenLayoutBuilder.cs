using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 조리 화면 UI 계층을 코드로 생성한다.
/// 손으로 배치하면 재현이 안 되므로 좌표를 전부 여기에 상수로 둔다.
/// 메뉴를 다시 누르면 기존 CookingCanvas를 지우고 새로 만든다.
/// 배치 기준은 기획서 14-3 와이어프레임, 그림은 Assets/Art의 45도 픽셀아트.
///
/// 크기는 전부 원본 픽셀의 정수 배율로만 잡는다. 픽셀아트를 1.5배 같은 배율로 늘리면
/// 픽셀 크기가 들쭉날쭉해져 그림이 지저분해진다.
/// </summary>
public static class RamenLayoutBuilder
{
    private const string CanvasName = "CookingCanvas";
    private const string FontPath = "Assets/Art/Fonts/malgun.ttf";
    private const string UndoLabel = "Build Cooking Layout";

    private const string BowlDir = "Assets/Art/그릇/";
    private const string EtcDir = "Assets/Art/나머지/";
    private const string IngredientDir = "Assets/Art/재료/";

    private static readonly Vector2 RefResolution = new Vector2(1920f, 1080f);

    // 앵커 프리셋. anchorMin과 anchorMax를 같은 값으로 두면 그 지점이 좌표의 원점이 된다.
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 MidLeft = new Vector2(0f, 0.5f);
    private static readonly Vector2 MidRight = new Vector2(1f, 0.5f);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

    // 원본 픽셀의 몇 배로 띄울지를 실제 물건 크기 느낌에 맞춰 정한 것.
    // 냄비 > 재료통·타래통 > 조미료병 순서고, 면 튀김기가 제일 큰 장비다.
    private static readonly Vector2 BinSize = new Vector2(192f, 192f);        // 64px x 3
    private static readonly Vector2 PotSize = new Vector2(320f, 320f);        // 64px x 5 (육수 냄비)
    private static readonly Vector2 NoodleBinSize = new Vector2(384f, 384f);  // 128px x 3
    private static readonly Vector2 BowlSize = new Vector2(768f, 768f);       // 128px x 6
    private static readonly Vector2 CursorSize = new Vector2(256f, 256f);     // 64px x 4

    private static readonly Color InkColor = new Color(0.16f, 0.16f, 0.16f);   // 베이지 배경 위 글자색
    private const int SlotLabelSize = 22;

    /// <summary>슬롯 하나의 정의. 좌표표를 그대로 코드로 옮긴 것.</summary>
    private class SlotDef
    {
        public readonly IngredientType Type;
        public readonly string Label;
        public readonly Vector2 Anchor;
        public readonly Vector2 Pos;
        public readonly Vector2 Size;

        /// <summary>화면에 놓이는 통 그림.</summary>
        public readonly string BinPath;

        /// <summary>그릇 안에 얹히는 그림. 액체와 조미료는 없어서 비어 있다.</summary>
        public readonly string BowlPath;

        /// <summary>드래그할 때 따라다니는 그림. 없으면 BowlPath를 쓴다.</summary>
        public readonly string DragPath;

        /// <summary>참이면 끌 수 없고, 클릭해서 국자에 담는다.</summary>
        public readonly bool Liquid;

        /// <summary>이름표를 통 그림 아래가 아닌 다른 곳에 둘 때 쓴다. (0,0)이면 기본 위치.</summary>
        public readonly Vector2 LabelOffset;

        public SlotDef(IngredientType type, string label, Vector2 anchor, float x, float y, Vector2 size,
                       string binPath, string bowlPath = null, string dragPath = null, bool liquid = false,
                       float labelX = 0f, float labelY = 0f)
        {
            Type = type;
            Label = label;
            Anchor = anchor;
            Pos = new Vector2(x, y);
            Size = size;
            BinPath = binPath;
            BowlPath = bowlPath;
            DragPath = dragPath;
            Liquid = liquid;
            LabelOffset = new Vector2(labelX, labelY);
        }
    }

    private static readonly SlotDef[] Slots =
    {
        // 좌상단: 타래 3통. 클릭하면 커서가 국자로 바뀌며 뜬다. (미소는 프로토타입 미구현)
        // 실제로도 육수 냄비보다 훨씬 작은 통이라 배율을 낮게 잡았다.
        new SlotDef(IngredientType.ShioTare,     "시오",     TopLeft, 130f, -215f, BinSize,
                    EtcDir + "시오.png", liquid: true),
        new SlotDef(IngredientType.ShoyuTare,    "쇼유",     TopLeft, 290f, -215f, BinSize,
                    EtcDir + "쇼유.png", liquid: true),
        new SlotDef(IngredientType.TonkotsuBase, "돈코츠",   TopLeft, 450f, -215f, BinSize,
                    EtcDir + "돈코츠.png", liquid: true),

        // 좌측 중앙: 육수 냄비. 화면에서 두 번째로 큰 물건.
        new SlotDef(IngredientType.Broth,        "육수",     MidLeft, 200f, 30f, PotSize,
                    EtcDir + "육수.png", liquid: true),

        // 좌하단: 면 튀김기. 그릇이 커져서 아래 가운데에 두면 이름표가 그릇 것처럼 보인다.
        // 굵은면 아트도 있지만 프로토타입은 얇은면 하나로 간다.
        new SlotDef(IngredientType.Noodles,      "공용 면",  MidLeft, 300f, -310f, NoodleBinSize,
                    EtcDir + "얇은면.png", EtcDir + "얇은면 그릇용.png", labelY: -205f),

        // 그릇 아래: 조미료 2종. 클릭하면 젓가락 대신 병 자체를 들고, 그릇에 대면 기울여 뿌린다.
        // 그릇용 그림이 없어 수량만 세고 그릇에는 안 나온다.
        new SlotDef(IngredientType.FlavorOil,    "향미유",   Center, -150f, -410f, BinSize,
                    EtcDir + "향미유.png", liquid: true),
        new SlotDef(IngredientType.ChiliPowder,  "시치미",   Center, 150f, -410f, BinSize,
                    EtcDir + "시치미.png", liquid: true),

        // 우측: 재료통 7개를 세로 한 줄로. 이름표는 통 왼쪽에 붙여 줄 간격을 아낀다.
        new SlotDef(IngredientType.Chashu,       "차슈",     MidRight, -110f,  364f, BinSize,
                    IngredientDir + "차슈 재료통.png", IngredientDir + "차슈.png", IngredientDir + "차슈 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.Menma,        "멘마",     MidRight, -110f,  226f, BinSize,
                    IngredientDir + "멘마 재료통.png", IngredientDir + "멘마.png", IngredientDir + "멘마 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.GreenOnion,   "파",       MidRight, -110f,   88f, BinSize,
                    IngredientDir + "파 재료통.png", IngredientDir + "파.png", IngredientDir + "파 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.Egg,          "계란",     MidRight, -110f,  -50f, BinSize,
                    IngredientDir + "계란 재료통.png", IngredientDir + "계란.png", IngredientDir + "계란 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.Nori,         "김",       MidRight, -110f, -188f, BinSize,
                    IngredientDir + "김 재료통.png", IngredientDir + "김.png", IngredientDir + "김 테두리 강조.png",
                    labelX: -190f),
        // 숙주만 파일 이름이 "테두리 강조"가 아니라 "테두리"다.
        new SlotDef(IngredientType.BeanSprout,   "숙주",     MidRight, -110f, -326f, BinSize,
                    IngredientDir + "숙주 재료통.png", IngredientDir + "숙주.png", IngredientDir + "숙주 테두리.png",
                    labelX: -190f),
        new SlotDef(IngredientType.WoodEar,      "목이버섯", MidRight, -110f, -464f, BinSize,
                    IngredientDir + "목이버섯 재료통.png", IngredientDir + "목이버섯.png", IngredientDir + "목이버섯 테두리 강조.png",
                    labelX: -190f),
    };

    [MenuItem("Tools/Ramen/Build Cooking Layout")]
    public static void Build()
    {
        Font font = LoadFont();

        // 기존 것을 지우고 새로 만든다. 반복 실행해도 결과가 같아야 하므로.
        GameObject existing = GameObject.Find(CanvasName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        Transform canvas = CreateCanvas();
        EnsureEventSystem();
        EnsureGameManager();

        // 순서가 곧 그리기 순서다. 배경이 맨 처음, DragLayer가 맨 마지막.
        BuildBackground(canvas);
        Image discardButton = BuildTopBar(canvas, font);
        BuildSlots(CreateGroup("Slots", canvas), font);
        Bowl bowl = BuildBowl(canvas);

        // 폐기 버튼은 그릇보다 먼저 만들어지므로 둘이 다 생긴 뒤에 연결한다.
        WireDiscardButton(discardButton, bowl);

        // DragLayer는 반드시 마지막. 그래야 드래그 고스트와 커서가 항상 모든 UI 위에 그려진다.
        // 이 그룹 자체에는 Image를 붙이지 않는다. 붙이면 화면 전체를 덮어 모든 클릭을 삼킨다.
        BuildCursor(CreateGroup("DragLayer", canvas));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("[RamenLayoutBuilder] " + SceneManager.GetActiveScene().name +
                  " 씬에 조리 UI를 생성했습니다. 슬롯 " + Slots.Length + "개.");
    }

    // ── 큰 덩어리 ────────────────────────────────────────────────

    private static Transform CreateCanvas()
    {
        var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = RefResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;   // 가로·세로 변화를 반반씩 반영

        return go.transform;
    }

    /// <summary>와이어프레임의 베이지 판. 이게 없으면 요소들이 허공에 뜬 것처럼 보인다.</summary>
    private static void BuildBackground(Transform canvas)
    {
        var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = Hex("#F5E9D0");
        img.raycastTarget = false;   // 배경이 클릭을 먹지 않도록
    }

    /// <summary>폐기 버튼 이미지를 돌려준다. 그릇이 생긴 뒤 onClick을 연결해야 하기 때문.</summary>
    private static Image BuildTopBar(Transform canvas, Font font)
    {
        Transform bar = CreateGroup("TopBar", canvas);
        Sprite panel = PanelSprite();

        // 주문 확인 ? 버튼 (자리만)
        Image help = CreateImage("OrderCheckButton", bar, TopLeft, new Vector2(70f, -55f), new Vector2(60f, 60f), Hex("#FFFFFF"), panel);
        CreateLabel(help.transform, "?", 32, InkColor, font);

        // 날짜 (자리만)
        Image day = CreateImage("DayPanel", bar, TopLeft, new Vector2(215f, -55f), new Vector2(190f, 60f), Hex("#FFFFFF"), panel);
        CreateLabel(day.transform, "1일차", 28, InkColor, font);

        // 제출 영역. 와이어프레임의 회색 가로 바.
        Image submit = CreateImage("SubmitZone", bar, TopCenter, new Vector2(0f, -55f), new Vector2(560f, 80f), Hex("#C9C9C9"), panel);
        Undo.AddComponent<SubmitZone>(submit.gameObject);
        CreateLabel(submit.transform, "제출하기", 30, InkColor, font);

        // 누적 매출 (자리만). 재료비와 자본은 기획 확정으로 제거되어 누적 매출만 표시한다.
        Image revenue = CreateImage("RevenuePanel", bar, TopRight, new Vector2(-250f, -55f), new Vector2(280f, 60f), Hex("#FFFFFF"), panel);
        CreateLabel(revenue.transform, "누적 매출 0원", 26, InkColor, font);

        // 폐기 버튼. onClick은 그릇이 생긴 뒤 WireDiscardButton에서 붙인다.
        Image discard = CreateImage("DiscardButton", bar, TopRight, new Vector2(-60f, -55f), new Vector2(90f, 70f), Hex("#7BB661"), panel);
        CreateLabel(discard.transform, "폐기", 26, InkColor, font);

        return discard;
    }

    /// <summary>
    /// 슬롯 하나 = 통 그림 + 아래에 이름.
    /// 고체는 IngredientSlot(드래그), 액체는 LiquidSlot(클릭해서 국자에 담기)이 붙는다.
    /// </summary>
    private static void BuildSlots(Transform parent, Font font)
    {
        foreach (SlotDef def in Slots)
        {
            Image bin = CreateImage("Slot_" + def.Type, parent, def.Anchor, def.Pos, def.Size,
                                    Color.white, LoadSprite(def.BinPath));
            bin.preserveAspect = true;

            if (def.Liquid)
            {
                LiquidSlot slot = Undo.AddComponent<LiquidSlot>(bin.gameObject);
                slot.type = def.Type;
            }
            else
            {
                IngredientSlot slot = Undo.AddComponent<IngredientSlot>(bin.gameObject);
                slot.type = def.Type;
                slot.bowlSprite = LoadSprite(def.BowlPath);
                slot.dragSprite = LoadSprite(def.DragPath);
            }

            CreateSlotLabel(bin.transform, def, font);
        }
    }

    /// <summary>시스템 커서를 대신하는 조리 도구. 평소 젓가락, 액체를 뜨면 국자.</summary>
    private static void BuildCursor(Transform dragLayer)
    {
        Sprite[] frames = LoadChopstickFrames();

        Image img = CreateImage("Cursor", dragLayer, Center, Vector2.zero, CursorSize,
                                Color.white, frames != null && frames.Length > 0 ? frames[0] : null);
        img.preserveAspect = true;

        // 커서가 레이캐스트를 먹으면 마우스 밑이 늘 커서라 아무것도 클릭할 수 없다.
        img.raycastTarget = false;

        var cursor = Undo.AddComponent<CookingCursor>(img.gameObject);
        cursor.chopstickFrames = frames;
        cursor.ladleEmpty = LoadSprite(EtcDir + "국자.png");
        cursor.ladleShio = LoadSprite(EtcDir + "국자 시오.png");
        cursor.ladleShoyu = LoadSprite(EtcDir + "국자 쇼유.png");
        cursor.ladleTonkotsu = LoadSprite(EtcDir + "국자 돈코츠.png");
        cursor.ladleBroth = LoadSprite(EtcDir + "국자 육수.png");
        cursor.flavorOilBottle = LoadSprite(EtcDir + "향미유.png");
        cursor.chiliPowderBottle = LoadSprite(EtcDir + "시치미.png");
    }

    /// <summary>
    /// 젓가락은 한 장에 64px 프레임 3개(벌림·중간·집음)가 가로로 붙어 있다.
    /// Multiple로 잘라 세 조각을 만든 뒤 왼쪽부터 순서대로 돌려준다.
    /// </summary>
    private static Sprite[] LoadChopstickFrames()
    {
        const int frameCount = 3;
        const float frameSize = 64f;
        string path = EtcDir + "젓가락.png";

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 젓가락 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple || importer.spritesheet.Length != frameCount)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var sheet = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                sheet[i].name = "Chopstick_" + i;
                sheet[i].rect = new Rect(i * frameSize, 0f, frameSize, frameSize);
                sheet[i].pivot = new Vector2(0.5f, 0.5f);
                sheet[i].alignment = (int)SpriteAlignment.Center;
            }
            importer.spritesheet = sheet;
            importer.SaveAndReimport();
        }

        var frames = new Sprite[frameCount];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite == null) continue;

            int index = Mathf.RoundToInt(sprite.rect.x / frameSize);
            if (index >= 0 && index < frameCount) frames[index] = sprite;
        }

        if (frames[0] == null) Debug.LogWarning("[RamenLayoutBuilder] 젓가락 프레임을 잘라내지 못했습니다.");
        return frames;
    }

    private static Bowl BuildBowl(Transform canvas)
    {
        Image bowl = CreateImage("Bowl", canvas, Center, new Vector2(0f, 90f), BowlSize,
                                 Color.white, LoadSprite(BowlDir + "빈그릇.png"));
        bowl.preserveAspect = true;

        Bowl component = Undo.AddComponent<Bowl>(bowl.gameObject);
        component.emptyBowlSprite = LoadSprite(BowlDir + "빈그릇.png");
        component.brothBowlSprite = LoadSprite(BowlDir + "육수그릇.png");
        component.shioBowlSprite = LoadSprite(BowlDir + "시오그릇.png");
        component.shoyuBowlSprite = LoadSprite(BowlDir + "쇼유그릇.png");
        component.tonkotsuBowlSprite = LoadSprite(BowlDir + "돈코츠그릇.png");

        // 드래그 중에 레이캐스트를 통과시키려면 CanvasGroup이 필요하다.
        // 없으면 그릇 자신이 SubmitZone을 가려서 제출이 영영 안 된다.
        Undo.AddComponent<CanvasGroup>(bowl.gameObject);

        // 그릇 안 재료 그림이 들어갈 자리. Bowl이 런타임에 여기로 넣는다.
        CreateGroup("Contents", bowl.transform);

        return component;
    }

    /// <summary>폐기 버튼 클릭을 Bowl.Discard에 붙인다. 인스펙터에 남는 연결이라 씬을 저장하면 유지된다.</summary>
    private static void WireDiscardButton(Image discard, Bowl bowl)
    {
        var button = Undo.AddComponent<Button>(discard.gameObject);
        button.targetGraphic = discard;
        UnityEventTools.AddPersistentListener(button.onClick, bowl.Discard);
    }

    // ── 씬에 하나만 있어야 하는 것들 ───────────────────────────────

    private static void EnsureEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            es = go.GetComponent<EventSystem>();
        }

        // 이 프로젝트는 Input System 전용(activeInputHandler=1)이라 구 모듈은 이벤트를 하나도 못 보낸다.
        StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Undo.DestroyObjectImmediate(legacy);
        }

        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
        }
    }

    private static void EnsureGameManager()
    {
        // 이미 있으면 인스펙터 연결이 날아가지 않도록 그대로 둔다.
        if (Object.FindFirstObjectByType<GameManager>() != null) return;

        var go = new GameObject("GameManager", typeof(GameManager));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
    }

    // ── 작은 도우미들 ─────────────────────────────────────────────

    /// <summary>자식들이 화면 가장자리를 기준으로 자리를 잡도록 부모를 화면 전체로 늘린다.</summary>
    private static Transform CreateGroup(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        }
        return img;
    }

/// <summary>
    /// 통 그림에 붙는 이름표. 기본은 바로 아래고, LabelOffset을 주면 그 자리로 간다.
    /// 우측 재료통처럼 세로로 빽빽하게 세운 줄은 이름표를 옆에 붙여야 자리가 나온다.
    /// </summary>
    private static void CreateSlotLabel(Transform parent, SlotDef def, Font font)
    {
        bool beside = !Mathf.Approximately(def.LabelOffset.x, 0f);

        Vector2 pos = def.LabelOffset == Vector2.zero
            ? new Vector2(0f, -def.Size.y * 0.5f - 14f)
            : def.LabelOffset;

        Vector2 size = beside ? new Vector2(175f, 40f) : new Vector2(def.Size.x + 80f, 30f);
        TextAnchor align = beside ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;

        CreateText("Label", parent, Center, pos, size, def.Label, SlotLabelSize, InkColor, font, align);
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                                   string content, int fontSize, Color color, Font font, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        return ConfigureText(go.GetComponent<Text>(), content, fontSize, color, font, align);
    }

    /// <summary>부모를 꽉 채우는 라벨. 버튼 위에 글자를 얹을 때 쓴다.</summary>
    private static Text CreateLabel(Transform parent, string content, int fontSize, Color color, Font font)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return ConfigureText(go.GetComponent<Text>(), content, fontSize, color, font, TextAnchor.MiddleCenter);
    }

    private static Text ConfigureText(Text text, string content, int fontSize, Color color, Font font, TextAnchor align)
    {
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // 라벨이 레이캐스트를 먹으면 슬롯의 드래그가 시작되지 않는다.
        text.raycastTarget = false;
        return text;
    }

    /// <summary>내장 둥근 사각형. 9슬라이스라 어느 크기로 늘려도 모서리가 뭉개지지 않는다.</summary>
    private static Sprite PanelSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    /// <summary>
    /// 그림 하나를 불러온다. 이 프로젝트의 PNG 기본 임포트는 Multiple + 압축 + Bilinear라
    /// 그대로 두면 픽셀아트가 흐려지고, 그림을 갈아 끼울 때 씬의 참조가 끊긴다.
    /// 그래서 불러오기 전에 설정을 확인하고 어긋나 있으면 바로잡는다.
    /// </summary>
    private static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;   // 픽셀아트라 보간하면 안 된다
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 스프라이트를 만들지 못했습니다: " + path);
        }
        return sprite;
    }

    private static Font LoadFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font != null) return font;

        Debug.LogWarning("[RamenLayoutBuilder] 폰트를 찾지 못했습니다: " + FontPath +
                         "\n내장 LegacyRuntime.ttf로 대체합니다. 한글이 네모로 보이면 맑은고딕을 이 경로에 넣어 주세요.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
    }
}
