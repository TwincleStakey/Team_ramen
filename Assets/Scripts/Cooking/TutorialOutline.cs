using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼에서 "지금 이걸 집으세요" 를 알리는 무지개 테두리.
/// 재료통마다 하나씩 붙어 있고, 자기 차례일 때만 켜진다.
///
/// 테두리 그림은 빌더가 통 그림의 실루엣에서 떠서 구워 둔다. 통마다 생김새가 달라도
/// 테두리가 알아서 맞고, 타래통·냄비·병처럼 규격이 다른 것도 같은 방식으로 처리된다.
/// 그림은 흰색 한 장이고 색은 여기서 입힌다.
/// </summary>
public class TutorialOutline : MonoBehaviour
{
    /// <summary>어느 재료의 통인가. 빌더가 넣어 준다.</summary>
    public IngredientType type;

    /// <summary>이 테두리가 무엇을 가리키는가. 켜지는 때가 저마다 다르다.</summary>
    public enum Kind
    {
        Ingredient,   // 재료통. 그 재료가 지금 차례일 때
        Bowl,         // 그릇. 그 재료를 집어 옮기는 중일 때
        Submit,       // 마무리 버튼. 재료를 다 넣었을 때
        Confirm,      // 마무리 확인창의 [넵]. 창이 떠 있는 동안
        TabKey,       // 주문서 아이콘. Tab 을 눌러 보라고 할 때
        BookKey,      // 레시피북 아이콘. B 를 눌러 보라고 할 때
    }

    public Kind kind = Kind.Ingredient;

    /// <summary>무지개 한 바퀴에 걸리는 시간(초). 짧으면 번쩍여서 눈이 아프다.</summary>
    public float cycleSeconds = 1.6f;

    /// <summary>색의 짙기와 밝기. 1,1 은 형광색이라 픽셀아트 위에서 튄다.</summary>
    public float saturation = 0.85f;
    public float value = 1f;

    /// <summary>
    /// 내 차례일 때 어두운 판 위로 끌어올릴 오브젝트의 Canvas. 대개 이 테두리가 얹힌 통 자신이다.
    /// 빌더가 꽂아 주며, 비어 있으면 끌어올리지 않는다.
    ///
    /// 화면을 어둡게 하는 것은 TutorialDim 의 판 한 장이 맡는다. 통마다 색을 낮추는 방식은
    /// 통만 어두워지고 배경·상단바는 그대로라 "이것만" 이 안 읽혔다.
    ///
    /// 면통처럼 그림 하나에 테두리가 둘 붙는 경우에는 한쪽에만 꽂는다.
    /// 둘 다 꽂으면 한쪽이 올리고 다른 쪽이 곧바로 내리기를 매 프레임 되풀이한다.
    /// </summary>
    public Canvas lift;

    /// <summary>
    /// 올라올 때 쓸 그리기 순서. TutorialDim 의 판보다 커야 판 위에 그려진다.
    ///
    /// 켤 때마다 다시 넣는다. overrideSorting 이 꺼져 있는 동안에는 이 값이 남지 않아서,
    /// 빌드할 때 넣어 둔 값이 0 으로 돌아가 있는 경우가 있다. 실제로 그래서 한 번 안 먹었다.
    /// </summary>
    public int liftOrder = 101;

    private Image image;
    private bool lifted;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (image != null) image.raycastTarget = false;

        SetLifted(false);
        Show(false);
    }

    private void Update()
    {
        // 안내가 다 찍힌 뒤에 빛난다. 어두운 판과 같은 조건이라 둘이 함께 켜진다.
        //
        // 그릇만 예외다. 재료를 들고 있는 동안과 그릇이 받아들이는 동안이 곧 "여기에 넣어라"
        // 라서, 글이 다 찍혔는지를 기다릴 이유가 없다. 실제로 기다리게 했더니 재료를 넣은 뒤
        // 안내가 한 박자 비는 동안 Revealed 가 거짓이 되어, 국물이 찰랑이는 내내 그릇이
        // 꺼져 있었다.
        TutorialManager tutorial = TutorialManager.Instance;
        bool mine = tutorial != null
                    && IsMyTurn(tutorial)
                    && (kind == Kind.Bowl || TutorialPrompt.Revealed);

        Show(mine);

        // 내 차례일 때만 어두운 판 위로 올라온다. 나머지는 판 아래에 깔려 어둡다.
        // 튜토리얼이 끝나면 판 자체가 꺼지므로 올릴 일도 없다.
        bool running = tutorial != null && tutorial.IsRunning;
        SetLifted(running && mine);

        if (!mine || image == null) return;

        // 색상만 돌린다. 시간은 실시간으로 잰다 — 폐기 확인창이 떠서 게임이 멈춰도
        // 테두리까지 굳으면 화면이 고장 난 것처럼 보인다.
        float hue = cycleSeconds > 0f
            ? Mathf.Repeat(Time.unscaledTime / cycleSeconds, 1f)
            : 0f;

        image.color = Color.HSVToRGB(hue, saturation, value);
    }

    /// <summary>
    /// 한 프레임에 한 번만 다시 잰 값. 테두리가 여럿이라 각자 재면 재료통이 꺼지는 시각과
    /// 그릇이 켜지는 시각이 한 프레임씩 엇갈린다.
    /// </summary>
    private static bool carrying;
    private static int carryFrame = -1;
    private static Bowl bowl;

    /// <summary>
    /// 그릇을 가리켜야 하는 때인가. 재료를 들고 있거나, 그릇이 아직 받아들이는 중이면 참이다.
    ///
    /// 시간을 재서 늘어뜨리지 않는다. 예전에는 손을 뗀 뒤 0.8초를 더 켜 두었는데,
    /// 그릇에 넣지 않고 바닥에 놓았을 때도 그만큼 그릇이 켜져 있어서 "넣은 건가?" 싶었다.
    /// 지금은 들고 있는 동안과 그림이 도는 동안만 켜진다 — 국자가 다 붓고 국물이 다
    /// 찰랑일 때까지가 한 번이고, 거기서 딱 끊긴다.
    /// </summary>
    private static bool Carrying
    {
        get
        {
            if (carryFrame == Time.frameCount) return carrying;
            carryFrame = Time.frameCount;

            CookingCursor cursor = CookingCursor.Instance;
            if (cursor != null && cursor.IsCarrying) { carrying = true; return true; }

            if (bowl == null) bowl = FindFirstObjectByType<Bowl>();
            carrying = bowl != null && bowl.IsAnimating;

            return carrying;
        }
    }

    private bool IsMyTurn(TutorialManager tutorial)
    {
        switch (kind)
        {
            case Kind.Submit:  return tutorial.ReadyToSubmit;
            case Kind.Confirm: return tutorial.ReadyToSubmit;
            case Kind.TabKey:  return tutorial.WaitingForTab;
            case Kind.BookKey: return tutorial.WaitingForBook;

            // 통과 그릇은 한 짝이라 둘이 동시에 켜지지 않는다. 집어 드는 순간 넘어가고,
            // 그릇이 다 받아들이면 되돌아온다.
            //
            // CurrentStep 을 보지 않는다. 재료를 넣으면 다음 안내까지 한 박자 쉬느라
            // CurrentStep 이 잠깐 null 이 되는데, 그게 국물이 찰랑이는 구간과 겹쳐서
            // 정작 보여 줘야 할 때 그릇이 꺼졌다.
            case Kind.Bowl:    return tutorial.IsRunning && Carrying;
            default:           return tutorial.CurrentStep == type && !Carrying;
        }
    }

    private void Show(bool on)
    {
        if (image != null && image.enabled != on) image.enabled = on;
    }

    private void SetLifted(bool on)
    {
        if (lift == null || lifted == on) return;
        lifted = on;

        lift.overrideSorting = on;
        if (on) lift.sortingOrder = liftOrder;
    }
}
