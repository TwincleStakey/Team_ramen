using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 마우스를 누른 자리에 픽셀 링이 퍼졌다 사라진다.
/// 무엇을 눌렀든 반응이 있어야 화면이 먹통이 아니라는 것이 전달된다.
/// RamenLayoutBuilder가 DragLayer에 만들어 준다.
/// </summary>
[RequireComponent(typeof(Image))]
public class ClickRipple : MonoBehaviour
{
    /// <summary>퍼지는 그림. 작은 링부터 큰 링 순서다.</summary>
    public Sprite[] frames;

    /// <summary>한 프레임을 보여 주는 시간. 다 합쳐 0.2초를 넘기면 굼떠 보인다.</summary>
    public float frameSeconds = 0.05f;

    /// <summary>사라지기 직전의 진하기. 퍼질수록 옅어져야 물결처럼 보인다.</summary>
    public float fadeTo = 0.08f;

    private RectTransform rect;
    private Image image;
    private Coroutine playing;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        // 링이 레이캐스트를 먹으면 방금 누른 버튼이 다음 클릭을 받지 못한다.
        image.raycastTarget = false;
        image.enabled = false;
    }

    private void Update()
    {
        if (Mouse.current == null || frames == null || frames.Length == 0) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // 링은 누른 자리에 머문다. 마우스를 따라가면 무엇을 눌렀는지가 흐려진다.
        rect.position = Mouse.current.position.ReadValue();

        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        image.enabled = true;

        // 그림은 프레임 단위로 커지지만 진하기는 매 프레임 조금씩 옅어진다.
        // 크기만 커지고 진하기가 그대로면 물결이 아니라 도장을 찍은 것처럼 보인다.
        float total = frames.Length * frameSeconds;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            int index = Mathf.Min((int)(t / frameSeconds), frames.Length - 1);
            image.sprite = frames[index];

            Color c = image.color;
            c.a = Mathf.Lerp(1f, fadeTo, t / total);
            image.color = c;

            // 팝업이 시간을 멈춰도 링은 돌아야 한다.
            yield return null;
        }

        image.enabled = false;
        playing = null;
    }
}
