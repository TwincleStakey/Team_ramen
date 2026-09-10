using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그림 몇 장을 일정한 속도로 돌린다. 끓는 육수 냄비처럼 늘 움직이고 있어야 하는 것에 붙인다.
///
/// 시간은 실시간으로 잰다. 팝업이 떠서 게임이 멈춰도 냄비는 계속 끓어야 한다.
/// </summary>
[RequireComponent(typeof(Image))]
public class SpriteLoop : MonoBehaviour
{
    /// <summary>돌릴 그림들. RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.</summary>
    public Sprite[] frames;

    /// <summary>초당 몇 장을 넘길지.</summary>
    public float fps = 6f;

    private Image image;
    private float elapsed;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void Update()
    {
        if (image == null || frames == null || frames.Length == 0) return;

        elapsed += Time.unscaledDeltaTime;
        image.sprite = frames[Mathf.Abs(Mathf.FloorToInt(elapsed * fps)) % frames.Length];
    }
}
