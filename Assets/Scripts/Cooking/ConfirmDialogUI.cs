using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 되돌릴 수 없는 조작에 한 번 더 묻는 창. 폐기와 마무리가 같은 것을 쓴다.
///
/// 기획서 v1.2 3.2 는 폐기에만 확인창을 두라고 하지만, 마무리도 누르는 순간 손님에게 나가
/// 되돌릴 수 없다. 같은 무게의 조작이라 같은 창을 쓴다.
///
/// 묻는 동안 <see cref="Time.timeScale"/>을 0 으로 내려 뒤 화면을 세운다. 뒷판만 깔면
/// 클릭은 막히지만 육수 냄비·국물 찰랑임·그레인이 계속 돌아 "멈춘" 것처럼 보이지 않는다.
/// UI 클릭은 timeScale 과 무관하게 먹으므로 창의 버튼은 그대로 눌린다.
///
/// 빈 그릇이면 열지 않는다. 폐기든 마무리든 빈 그릇에는 아무 일도 일어나지 않아서,
/// 물어봐야 할 것이 없다.
/// </summary>
public class ConfirmDialogUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder 가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Bowl bowl;

    /// <summary>[넵] 을 눌렀을 때 할 일. 빌더가 Bowl.Discard 나 Bowl.Submit 을 걸어 준다.</summary>
    [SerializeField] private UnityEvent onConfirm;

    /// <summary>
    /// 마무리 창인가. 튜토리얼에서 차례를 다 밟기 전에는 열리지 않아야 한다(기획서 v1.2 9장).
    ///
    /// 예전에는 이 잠금이 Bowl 의 끌기 시작 지점에 있었다. 제출을 버튼으로 바꾸면서
    /// 그 함수가 통째로 사라졌고, 잠금도 같이 사라져 중간에 마무리가 눌렸다.
    /// 폐기는 반대다 — 잘못 넣었을 때 되돌릴 유일한 길이라 튜토리얼에서도 늘 열려 있어야 한다.
    /// </summary>
    [SerializeField] private bool isSubmit;

    /// <summary>창을 열면서 내려 둔 timeScale. 닫을 때 이 값으로 되돌린다.</summary>
    private float savedTimeScale = 1f;

    private bool isOpen;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);

        if (popupRoot != null) popupRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
    }

    /// <summary>
    /// 창이 열린 채로 이 오브젝트가 꺼지면 timeScale 이 0 에 묶인 채 게임 전체가 멈춘다.
    /// 씬을 갈아 끼우거나 에디터에서 Play 를 끊을 때가 그렇다.
    /// </summary>
    private void OnDisable()
    {
        if (isOpen) Restore();
    }

    /// <summary>폐기·마무리 버튼이 부른다. 실제 일은 [넵] 을 눌러야 일어난다.</summary>
    public void Open()
    {
        if (bowl != null && bowl.IsEmpty) return;
        if (isSubmit && !TutorialManager.CanSubmit()) return;
        if (isOpen) return;

        isOpen = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (popupRoot != null) popupRoot.SetActive(true);
    }

    private void Confirm()
    {
        Restore();
        if (onConfirm != null) onConfirm.Invoke();
    }

    private void Cancel()
    {
        Restore();
    }

    /// <summary>창을 닫고 시간을 되돌린다. 눌렀든 말았든 거쳐야 하는 자리다.</summary>
    private void Restore()
    {
        isOpen = false;
        Time.timeScale = savedTimeScale;
        if (popupRoot != null) popupRoot.SetActive(false);
    }
}
