using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [설정] 창. 지금은 화면 필터 세기 하나뿐이다.
///
/// 값은 눈금 0~10 으로 끊는다. 왼쪽·오른쪽 버튼으로 한 칸씩 옮기고, 바꾸는 즉시
/// 뒤쪽 화면에 반영된다 — 창이 시작 화면 위에 떠 있어서 고르는 동안 바로 보인다.
/// 저장은 ScreenGrade 가 한다(PlayerPrefs). 닫기를 눌러야 저장되는 식이면
/// 창을 그냥 닫았을 때 방금 고른 값이 사라져 버린다.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button closeButton;

    /// <summary>눈금 0 일 때 보여 줄 말. 숫자 0 만 있으면 꺼진 것인지 알기 어렵다.</summary>
    private const string OffLabel = "꺼짐";

    private void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(Decrease);
        if (plusButton != null) plusButton.onClick.AddListener(Increase);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (root != null) root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(Decrease);
        if (plusButton != null) plusButton.onClick.RemoveListener(Increase);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (root != null) root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void Decrease()
    {
        Move(-1);
    }

    private void Increase()
    {
        Move(1);
    }

    private void Move(int delta)
    {
        ScreenGrade grade = ScreenGrade.Instance;
        if (grade == null) return;

        grade.SetStep(grade.Step + delta);
        Refresh();
    }

    /// <summary>숫자와 버튼 상태를 지금 값에 맞춘다.</summary>
    private void Refresh()
    {
        ScreenGrade grade = ScreenGrade.Instance;

        if (grade == null)
        {
            if (valueText != null) valueText.text = "-";
            return;
        }

        int step = grade.Step;
        if (valueText != null) valueText.text = step == 0 ? OffLabel : step.ToString();

        // 끝에 닿으면 눌리지 않게 한다. 눌러도 아무 일이 없으면 고장으로 읽힌다.
        if (minusButton != null) minusButton.interactable = step > 0;
        if (plusButton != null) plusButton.interactable = step < ScreenGrade.MaxStep;
    }
}
