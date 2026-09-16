using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정창의 한 줄. 이름 · ◀ · 눈금 막대 · ▶ 로 이루어진다.
///
/// 세 줄(배경음·효과음·화면 필터)이 **같은 눈금 0~10** 을 쓴다.
/// <see cref="Sfx.MaxVolumeStep"/> 과 <see cref="ScreenGrade.MaxStep"/> 이 둘 다 10 이라
/// 위젯 하나로 셋을 다 그린다. 값이 무엇을 뜻하는지는 <see cref="SettingsUI"/> 만 안다 —
/// 이쪽은 「몇 칸 차 있나」만 그린다.
///
/// 숫자 하나로 보여 주지 않는 까닭 — 볼륨은 「7」 보다 「열 칸 중 일곱 칸」 이 한눈에 들어온다.
/// 0 일 때만 숫자 자리에 「꺼짐」 을 띄운다. 빈 막대만 있으면 고장 난 것으로 읽힌다.
/// </summary>
public class SettingsRow : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.
    [SerializeField] private Image[] segments;
    [SerializeField] private TextMeshProUGUI offText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    /// <summary>켜진 칸. 설정판 금테와 같은 색이라 판이 한 덩어리로 읽힌다.</summary>
    private static readonly Color OnColor = new Color32(214, 172, 84, 255);

    /// <summary>꺼진 칸. 우묵한 틀보다 조금만 밝게 둔다 — 아예 비우면 칸 수가 안 보인다.</summary>
    private static readonly Color OffColor = new Color32(62, 46, 32, 255);

    public Button Minus { get { return minusButton; } }

    public Button Plus { get { return plusButton; } }

    /// <summary>칸 수. 빌더가 몇 칸을 만들었든 그대로 따른다.</summary>
    public int Steps { get { return segments != null ? segments.Length : 0; } }

    /// <summary>지금 값에 맞춰 막대와 버튼을 그린다.</summary>
    public void Show(int value)
    {
        // 0 이면 눈금을 아예 감춘다. 「꺼짐」 글자가 막대 위에 겹쳐 있어서, 빈 칸 열 개를
        // 깔아 둔 채로 글자를 얹으면 글자가 칸에 걸려 읽기 나쁘다.
        bool off = value <= 0;

        if (segments != null)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;

                segments[i].enabled = !off;
                segments[i].color = i < value ? OnColor : OffColor;
            }
        }

        if (offText != null) offText.enabled = off;

        // 끝에 닿으면 눌리지 않게 한다. 눌러도 아무 일이 없으면 고장으로 읽힌다.
        if (minusButton != null) minusButton.interactable = value > 0;
        if (plusButton != null) plusButton.interactable = value < Steps;
    }
}
