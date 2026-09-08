using TMPro;
using UnityEngine;

/// <summary>
/// 손님 주문 내역. Tab을 누르고 있는 동안 뜨고, 떼면 사라진다.
/// 조리 화면 상단의 ? 버튼으로도 열 수 있으며 그때는 토글이다.
///
/// 기본 레시피와 재료 속성은 여기 없다. 그쪽은 B 키로 여는 레시피 책이 맡는다.
/// </summary>
public class OrderNoteUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI dialogueText;

    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        Hide();
    }

    public void Show(string dialogue)
    {
        if (root != null) root.SetActive(true);
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrEmpty(dialogue) ? "(받은 주문이 없습니다)" : dialogue;
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    public void Toggle(string dialogue)
    {
        if (IsOpen) Hide();
        else Show(dialogue);
    }
}
