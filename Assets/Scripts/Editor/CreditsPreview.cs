using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 크레딧을 Play 를 안 켜고 **연속된 그림 여러 장**으로 뽑는다. 밖에서 ffmpeg 로 이으면 영상이 된다.
///
/// 캔버스가 ScreenSpaceCamera 라 그 카메라를 잠깐 RenderTexture 로 돌려서 그대로 받아 낸다.
/// 씬에는 아무것도 남지 않는다 — 세운 판에는 DontSaveInEditor 가 붙어 있고, 찍자마자 지운다.
///
/// Play 를 안 켜는 것이 중요하다. 에디터 하나를 여러 세션이 같이 쓰므로, 다른 세션이 빌더를
/// 돌리는 중이어도 이쪽은 방해가 되지 않는다.
///
/// 글자가 어느 시각에 어떤 모습인지는 <see cref="CreditsSequence.Evaluate"/> 하나만 본다.
/// 실제 게임이 보는 것과 같은 함수라, 여기서 맞으면 게임에서도 맞는다.
/// </summary>
public static class CreditsPreview
{
    /// <summary>찍는 크기. 캔버스 960x540 의 2배 — 1920x1080 창에서 보이는 그대로다.</summary>
    private const int ShotWidth = 1920;
    private const int ShotHeight = 1080;

    /// <summary>
    /// 찍는 동안 고정할 캔버스 배율.
    ///
    /// <see cref="PixelPerfectCanvas"/> 는 배율을 Game 뷰 크기에서 뽑는다(floor(w/960)).
    /// Game 뷰가 1920 보다 작으면 배율이 1 로 내려가서, 1920x1080 으로 찍으면 화면이
    /// 한가운데 960x540 만큼만 그려지고 둘레가 새까맣게 남는다. 실제로 한 번 그렇게 나왔다.
    /// </summary>
    private const int ShotScale = 2;

    /// <summary>
    /// <paramref name="from"/> 초부터 <paramref name="to"/> 초까지 <paramref name="fps"/> 장/초로 찍는다.
    /// 파일 이름은 <c>f00000.png</c> 꼴이고 번호는 0 초를 기준으로 매긴다 — 나눠 찍어도 이어진다.
    /// </summary>
    public static string CaptureRange(string dir, float from, float to, int fps, int width, int height)
    {
        Directory.CreateDirectory(dir);

        Camera camera = FindCanvasCamera();
        if (camera == null) return "캔버스 카메라를 못 찾았습니다.";

        var backdrop = new Backdrop();
        backdrop.Stage();

        CreditsSequence credits = CreditsSequence.BuildPreview();

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        var shot = new Texture2D(width, height, TextureFormat.RGB24, false);

        int first = Mathf.RoundToInt(from * fps);
        int last = Mathf.RoundToInt(to * fps);
        int written = 0;

        try
        {
            camera.targetTexture = rt;

            for (int n = first; n < last; n++)
            {
                credits.SeekPreview((float)n / fps);
                backdrop.PinScale();

                camera.Render();
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                shot.Apply(false);

                File.WriteAllBytes(Path.Combine(dir, "f" + n.ToString("D5") + ".png"), shot.EncodeToPNG());
                written++;
            }
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(shot);

            // DestroyPreview 가 아니라 이쪽을 부른다. 찍는 도중에 에디터가 꺼지거나 예외가 나면
            // 판이 씬에 남는데, 그게 쌓이면 어느 판이 그려지는지 알 수 없게 된다.
            // 이름으로 싹 쓸어내는 편이 확실하다.
            CreditsSequence.ClearLeftovers();
            backdrop.Restore();
        }

        return "찍음 " + written + "장  (" + first + "~" + (last - 1) + ")  " + width + "x" + height;
    }

    /// <summary>
    /// 크레딧이 도는 동안 보일 화면으로 잠깐 갈아 끼웠다가 되돌린다.
    ///
    /// 켜고 끄는 것은 씬을 더럽힌다(isDirty). 에디터 하나를 여러 세션이 같이 쓰므로,
    /// <b>들어올 때 깨끗했으면</b> 나갈 때 그 표시를 도로 지운다. 더러운 채로 들어왔으면
    /// 남의 편집이 올라가 있는 것이므로 손대지 않는다.
    /// </summary>
    private class Backdrop
    {
        /// <summary>크레딧 무대. 켜 둘 것.</summary>
        private static readonly string[] On = { "OrderScreen" };

        /// <summary>
        /// 크레딧이 실제로 치우는 것 + 편집 중에만 보이는 것.
        ///
        /// 앞쪽은 <see cref="CreditsSequence.FrameHidden"/> 을 그대로 쓴다 — 두 벌로 적으면
        /// 미리보기와 실제 화면이 갈려서 미리보기가 거짓말이 된다.
        /// 뒤쪽은 Play 를 안 켜서 그대로 남는 조리 화면 물건들이다. 실제로는 주문 화면이 덮는다.
        /// </summary>
        private static string[] Off
        {
            get
            {
                var list = new System.Collections.Generic.List<string>(CreditsSequence.FrameHidden);
                list.AddRange(new[]
                {
                    "TopBar", "Slots", "NoodlePot", "Bowl", "SeasoningBadges", "SlotNameplate",
                    "IngredientToast", "DiscardConfirm", "SubmitConfirm",
                });
                return list.ToArray();
            }
        }

        /// <summary>
        /// 주문 화면 안에서 켜 둘 것. 나머지는 전부 끈다.
        ///
        /// 1단계(글자만)는 빈 포장마차만 있으면 된다. Play 를 안 켜니 손님 그림도 연출 판도
        /// 편집 중 모습 그대로라 — 흰 네모, 빈 말풍선 — 그냥 두면 글자를 볼 수가 없다.
        /// 2단계에서 손님을 세울 때 CustomerSlot 을 여기로 옮긴다.
        /// </summary>
        private static readonly string[] StageOn =
        {
            "Night", "Scenery", "Counter",
        };

        private readonly System.Collections.Generic.Dictionary<GameObject, bool> was =
            new System.Collections.Generic.Dictionary<GameObject, bool>();

        private bool wasClean;

        public void Stage()
        {
            wasClean = !UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty;

            GameObject frame = GameObject.Find("Frame");
            if (frame == null) return;

            Transform orderScreen = null;
            string[] off_ = Off;

            foreach (Transform t in frame.transform)
            {
                if (t.name == "OrderScreen") orderScreen = t;

                bool on = System.Array.IndexOf(On, t.name) >= 0;
                bool off = System.Array.IndexOf(off_, t.name) >= 0;
                if (!on && !off) continue;

                Set(t.gameObject, on);
            }

            if (orderScreen == null) return;

            foreach (Transform t in orderScreen)
            {
                Set(t.gameObject, System.Array.IndexOf(StageOn, t.name) >= 0);
            }

            // 주문 화면이 미끄러지다 만 자리에 서 있을 수 있다(Play 를 돌린 뒤 등).
            // 그대로 찍으면 창 안에 조리 화면이 반쯤 드러난다. 열린 자리(0)로 박아 둔다.
            //
            // SnapOpen() 을 부르면 안 된다. 그쪽이 쓰는 「열린 자리」는 Awake 에서 재 두는 값이라,
            // Play 를 안 켠 에디터에서는 0 이 들어가 있어 화면을 엉뚱한 데로 옮긴다.
            slid = (RectTransform)orderScreen;
            slidWas = slid.anchoredPosition;
            slid.anchoredPosition = Vector2.zero;
        }

        private void Set(GameObject go, bool on)
        {
            if (!was.ContainsKey(go)) was[go] = go.activeSelf;
            go.SetActive(on);
        }

        private RectTransform slid;
        private Vector2 slidWas;

        private UnityEngine.UI.CanvasScaler scaler;
        private float scaleWas = -1f;

        /// <summary>
        /// 찍는 동안 캔버스 배율을 <see cref="ShotScale"/> 로 고정한다.
        ///
        /// 매 장마다 다시 박는다. PixelPerfectCanvas 가 Game 뷰 크기를 보고 언제든 되돌려 놓는다.
        /// </summary>
        public void PinScale()
        {
            if (scaler == null)
            {
                foreach (var s in Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (s.GetComponent<PixelPerfectCanvas>() == null) continue;
                    scaler = s;
                    scaleWas = s.scaleFactor;
                    break;
                }
            }

            if (scaler != null) scaler.scaleFactor = ShotScale;
            Canvas.ForceUpdateCanvases();
        }

        public void Restore()
        {
            if (scaler != null && scaleWas > 0f) scaler.scaleFactor = scaleWas;
            if (slid != null) slid.anchoredPosition = slidWas;

            foreach (var pair in was) if (pair.Key != null) pair.Key.SetActive(pair.Value);

            if (!wasClean) return;

            // ClearSceneDirtiness 는 공개되어 있지 않다. 없으면 「저장하지 말라」고 알리는 것이 최선이다.
            var method = typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);

            if (method != null)
            {
                method.Invoke(null, new object[] { UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene() });
            }
            else
            {
                Debug.LogWarning("[크레딧 미리보기] 씬에 「고침」 표시가 남았습니다. 내용은 그대로이니 저장하지 말고 두세요.");
            }
        }
    }

    /// <summary>
    /// 씬을 「방금 빌더가 만든 상태」로 되돌린다.
    ///
    /// 크레딧 미리보기는 찍는 동안 씬의 물건을 껐다 켜고 자리를 옮긴다. 정상 경로에서는
    /// 전부 되돌리지만, 중간에 에디터가 꺼지거나 손으로 돌린 진단이 끼면 어긋난 채로 굳는다.
    /// <b>실제로 그렇게 굳어서 시작 화면이 안 뜬 적이 있다</b> — TitleScreen 이 꺼지고
    /// OrderScreen 이 켜진 채로 남았다. 그때 이걸 누르면 된다.
    ///
    /// 씬을 다시 여는 것과 달리 <b>다른 세션의 저장 안 한 작업을 날리지 않는다.</b>
    /// 크레딧이 만지는 것만 골라서 제자리에 놓는다.
    /// </summary>
    [MenuItem("Tools/Ramen/Reset Credits Preview")]
    public static void ResetScene()
    {
        CreditsSequence.ClearLeftovers();

        GameObject frame = GameObject.Find("Frame");
        if (frame == null) { Debug.LogWarning("[크레딧] Frame 을 못 찾았습니다."); return; }

        int fixedCount = 0;

        foreach (Transform t in frame.transform)
        {
            int want = System.Array.IndexOf(FrameOn, t.name) >= 0 ? 1
                     : System.Array.IndexOf(FrameOff, t.name) >= 0 ? 0 : -1;
            if (want < 0 || t.gameObject.activeSelf == (want == 1)) continue;

            t.gameObject.SetActive(want == 1);
            fixedCount++;
        }

        Transform screen = frame.transform.Find("OrderScreen");
        if (screen != null)
        {
            foreach (Transform t in screen)
            {
                bool want = System.Array.IndexOf(StageStart, t.name) >= 0;
                if (t.gameObject.activeSelf == want) continue;

                t.gameObject.SetActive(want);
                fixedCount++;
            }

            // 까마귀는 손님보다 **뒤** 층이다. 앉는 연출이 앞으로 냈다가 못 돌린 적이 있다.
            Transform crow = screen.Find("Crow");
            Transform slot = screen.Find("CustomerSlot");
            if (crow != null && slot != null && crow.GetSiblingIndex() > slot.GetSiblingIndex())
            {
                crow.SetSiblingIndex(slot.GetSiblingIndex());
                fixedCount++;
            }
        }

        Bowl bowl = Object.FindFirstObjectByType<Bowl>();
        if (bowl != null)
        {
            if (bowl.transform.localScale != Vector3.one) { bowl.transform.localScale = Vector3.one; fixedCount++; }
            if (!bowl.IsEmpty) { bowl.Discard(); fixedCount++; }
        }

        var look = Object.FindFirstObjectByType<CustomerAppearance>(FindObjectsInactive.Include);
        if (look != null) { look.SetOffset(Vector2.zero); look.ReleaseFrame(); }

        Debug.Log("[크레딧] 씬을 제자리로 돌렸습니다. 고친 것 " + fixedCount + "개.");
    }

    /// <summary>씬이 처음 열렸을 때 켜져 있어야 하는 것(캔버스 Frame 바로 밑).</summary>
    private static readonly string[] FrameOn =
    {
        "TopBar", "Slots", "NoodlePot", "Bowl", "SeasoningBadges", "SlotNameplate",
        "KeyHint_Tab", "KeyHint_Book", "HoldToSkip", "PerfectSign", "ClosedSign",
        "ScreenFade", "TitleScreen", "Settings", "OpeningNarration", "DayTitle",
        "IrisFade", "DragLayer", "ScreenGrain", "ScreenVignette",
    };

    /// <summary>씬이 처음 열렸을 때 꺼져 있어야 하는 것.</summary>
    private static readonly string[] FrameOff =
    {
        "TutorialPrompt", "IngredientToast", "TutorialDim", "DiscardConfirm", "SubmitConfirm",
        "TodayReciept", "FinalResultPopup", "OrderScreen", "RecipeBook", "OrderNote", "OrderResult",
    };

    /// <summary>주문 화면 안에서 처음에 켜져 있어야 하는 것. 나머지는 연출이 켠다.</summary>
    private static readonly string[] StageStart =
    {
        "Night", "Scenery", "Counter", "CustomerSlot", "Bubble", "DayTimePanel", "RevenuePanel",
    };

    /// <summary>
    /// 마지막 장면(조리 화면 + 라멘 한 그릇)을 편집 모드에서 세워 놓고 한 장 찍는다.
    ///
    /// 이 장면은 코루틴이 아니라 <b>정지 화면</b>이라 Play 없이도 그대로 볼 수 있다.
    /// 세운 것은 다 되돌린다 — 그릇도 비우고 껐던 것도 도로 켠다.
    /// </summary>
    public static string CaptureLastScene(string path)
    {
        Camera camera = FindCanvasCamera();
        GameObject frame = GameObject.Find("Frame");
        if (camera == null || frame == null) return "카메라나 Frame 을 못 찾았습니다.";

        var backdrop = new Backdrop();
        backdrop.Stage();

        // 크레딧이 어둠 뒤에서 하는 것과 같은 차림. 다만 창으로 줄이지 않고 온 화면이다.
        var was = new System.Collections.Generic.Dictionary<GameObject, bool>();
        Transform order = frame.transform.Find("OrderScreen");
        if (order != null) { was[order.gameObject] = order.gameObject.activeSelf; order.gameObject.SetActive(false); }

        foreach (string name in CreditsSequence.LastSceneHidden)
        {
            Transform t = frame.transform.Find(name);
            if (t == null) continue;

            if (!was.ContainsKey(t.gameObject)) was[t.gameObject] = t.gameObject.activeSelf;
            t.gameObject.SetActive(false);
        }

        // 그릇만 도로 켠다. 미리보기 차림이 꺼 두었는데 마지막 장면의 주인공이다.
        // Slots(재료통·냄비·조미료)는 켜지 않는다 — LastSceneHidden 이 치우는 쪽이다.
        foreach (string name in new[] { "Bowl" })
        {
            Transform t = frame.transform.Find(name);
            if (t == null) continue;

            if (!was.ContainsKey(t.gameObject)) was[t.gameObject] = t.gameObject.activeSelf;
            t.gameObject.SetActive(true);
        }

        Bowl bowl = Object.FindFirstObjectByType<Bowl>();
        Vector3 bowlScaleWas = Vector3.one;
        if (bowl != null)
        {
            bowlScaleWas = bowl.transform.localScale;
            bowl.transform.localScale = new Vector3(2f, 2f, 1f);   // CreditsSequence.LastBowlScale
            if (!bowl.IsEmpty) bowl.Discard();
            Fill(bowl);
        }

        // 마지막 한 줄까지 올려서 찍는다. 글 없이 조리대만 보면 자리가 맞는지 알 수 없다.
        CreditsSequence credits = CreditsSequence.BuildPreview();
        credits.ShowClosingPreview();

        string result = Shoot(camera, path, ShotWidth, ShotHeight, backdrop);

        CreditsSequence.ClearLeftovers();
        if (bowl != null)
        {
            if (!bowl.IsEmpty) bowl.Discard();
            bowl.transform.localScale = bowlScaleWas;
        }
        foreach (var pair in was) if (pair.Key != null) pair.Key.SetActive(pair.Value);
        backdrop.Restore();

        return result;
    }

    /// <summary>그릇에 마지막 장면용 레시피를 담는다. 코루틴 없이 한 번에 넣는다.</summary>
    private static void Fill(Bowl bowl)
    {
        // Play 를 안 켜면 Awake 가 안 돌아 그릇 내부 참조(Image 등)가 비어 있다.
        // 그대로 담으면 RefreshBowlSprite 에서 널로 터진다. 미리보기에서만 한 번 깨워 준다.
        var awake = typeof(Bowl).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (awake != null) awake.Invoke(bowl, null);

        var icons = new System.Collections.Generic.Dictionary<IngredientType, Sprite>();
        foreach (IngredientSlot slot in Object.FindObjectsByType<IngredientSlot>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            icons[slot.type] = slot.bowlSprite;
        }

        // enum 순서가 곧 담는 순서다(타래 → 육수 → 면 → 토핑 → 조미료).
        var order = new System.Collections.Generic.List<IngredientType>(CreditsSequence.LastBowl.Keys);
        order.Sort((a, b) => ((int)a).CompareTo((int)b));

        foreach (IngredientType type in order)
        {
            Sprite icon;
            icons.TryGetValue(type, out icon);
            for (int n = 0; n < CreditsSequence.LastBowl[type]; n++) bowl.TryAdd(type, icon);
        }
    }

    private static string Shoot(Camera camera, string path, int width, int height, Backdrop backdrop)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        var shot = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            camera.targetTexture = rt;
            backdrop.PinScale();
            camera.Render();

            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            shot.Apply(false);
            File.WriteAllBytes(path, shot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(shot);
        }

        return "찍음 " + path;
    }

    /// <summary>크레딧 전체 길이(초). 엔딩곡 + 여운.</summary>
    public static float TotalSeconds()
    {
        CreditsSequence credits = CreditsSequence.BuildPreview();
        float total = credits.TotalSeconds;
        credits.DestroyPreview();
        return total;
    }

    /// <summary>화면 캔버스가 쓰는 카메라. 빌더가 ScreenSpaceCamera 로 세워 둔다.</summary>
    private static Camera FindCanvasCamera()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.worldCamera != null) return canvas.worldCamera;
        }
        return Camera.main;
    }
}
