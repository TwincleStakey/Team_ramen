using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼이 도는 동안 화면 전체를 덮는 어두운 판.
///
/// 지금 만져야 할 것과 그릇만 이 판 위로 올라오고 나머지는 아래에 깔려 어두워진다.
/// "이것만 누르세요" 를 글이 아니라 화면으로 말하는 장치다.
///
/// 클릭은 통과시킨다. 막으면 정작 눌러야 할 통까지 안 눌린다.
/// 위로 올리는 일은 각 통에 붙은 TutorialOutline 이 자기 차례에 스스로 한다.
/// </summary>
public class TutorialDim : MonoBehaviour
{
    /// <summary>어두운 판. 빌더가 꽂아 준다.</summary>
    [SerializeField] private Image cover;

    /// <summary>
    /// 손님 대사 화면. 이게 떠 있는 동안에는 덮지 않는다.
    ///
    /// 빌더가 꽂아 주지 않고 여기서 직접 찾는다. 어두운 판이 주문 화면보다 먼저 만들어져서
    /// 빌드 시점에는 아직 씬에 없다. 처음 쓸 때 한 번만 찾아 둔다.
    /// </summary>
    private OrderScreenUI orderScreen;

    private void Update()
    {
        if (orderScreen == null) orderScreen = FindFirstObjectByType<OrderScreenUI>();

        TutorialManager tutorial = TutorialManager.Instance;

        // 가리킬 것이 있을 때만 덮는다.
        //
        // 예전에는 튜토리얼이 도는 내내 덮었다. 그러면 손님 대사를 읽는 동안에도 화면이
        // 캄캄한데 빛나는 것은 하나도 없어서, 무엇을 하라는 것인지 알 수 없었다.
        // 조리 화면에서 키를 누르라고 하거나 재료를 가리킬 때만 덮는다.
        // 첫 안내를 다 읽힌 뒤에 어두워진다. 글이 찍히는 중에 화면이 어두워지면
        // 읽으라는 글과 보라는 통이 한꺼번에 들어와 어느 쪽도 안 읽힌다.
        //
        // Revealed 가 아니라 RevealedOnce 를 본다. Revealed 는 안내가 넘어갈 때마다 잠깐
        // 거짓이 되는데, 그때마다 판을 걷으면 화면이 밝아졌다 어두워져 리셋된 것처럼 깜빡인다.
        bool on = tutorial != null
                  && tutorial.HasTarget
                  && (orderScreen == null || !orderScreen.IsOpen)
                  && TutorialPrompt.RevealedOnce;

        if (cover != null && cover.enabled != on) cover.enabled = on;
    }
}
