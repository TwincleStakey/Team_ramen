using TMPro;
using UnityEngine;

/// <summary>
/// 개발 키가 열려 있다는 것을 화면 구석에 작게 알린다.
///
/// 표시가 없으면 열렸는지 잠겼는지 알 길이 없어, 눌리지 않는 F5 를 몇 번이고 다시 누르게 된다.
/// 제출한 빌드에서 이 글자가 보이면 「지금은 평범한 플레이가 아니다」라는 뜻이기도 하다.
/// </summary>
public class DevKeyBadge : MonoBehaviour
{
    /// <summary>빌더가 꽂아 준다.</summary>
    [SerializeField] private TextMeshProUGUI label;

    private void OnEnable()
    {
        DevKeys.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        DevKeys.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (label != null) label.enabled = DevKeys.Enabled;
    }
}
