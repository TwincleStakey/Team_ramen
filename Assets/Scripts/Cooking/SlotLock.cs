using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아직 안 들어온 재료통에 자물쇠를 얹고 어둡게 깔아 둔다. 통 하나에 하나씩 붙는다.
///
/// 어느 날 열리는지는 <see cref="IngredientUnlock"/> 한 곳이 정한다. 그 표를 주문 만드는
/// 쪽도 같이 보기 때문에, 잠긴 통을 시키는 주문은 애초에 만들어지지 않는다.
///
/// 잠긴 동안에는 <see cref="SlotHover"/> 를 재운다. 그쪽이 통 색을 캐시해 두었다가
/// 마우스가 스칠 때마다 되돌리기 때문에, 색만 어둡게 하면 한 번 스치고 원래 색으로 돌아온다.
/// 끄면 그쪽 OnDisable 이 제 손으로 원래 모습을 돌려놓으므로, 어둡게 하는 것은 그다음이다.
/// </summary>
public class SlotLock : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private IngredientType type;

    /// <summary>통 그림. 잠기면 어두워진다.</summary>
    [SerializeField] private Image bin;

    /// <summary>통 위에 뜨는 자물쇠. 잠긴 동안에만 켜진다.</summary>
    [SerializeField] private Image padlock;

    /// <summary>잠긴 동안 재워 둘 호버.</summary>
    [SerializeField] private SlotHover hover;

    /// <summary>집으려 했을 때 안내를 띄울 자리. 그릇이 쓰는 것과 같은 토스트다.</summary>
    [SerializeField] private IngredientToast toast;

    /// <summary>
    /// 잠긴 통 색. 곱해서 쓰므로 0.45 면 절반 조금 넘게 어두워진다.
    /// 더 내리면 무슨 재료인지 못 알아보고, 더 올리면 잠긴 티가 안 난다.
    /// </summary>
    public static readonly Color LockedTint = new Color(0.45f, 0.45f, 0.5f, 1f);

    public IngredientType Type { get { return type; } }

    /// <summary>해금 연출(<see cref="UnlockSequence"/>)이 흔들고 열고 지우는 대상.</summary>
    public Image Padlock { get { return padlock; } }

    /// <summary>같은 연출이 어두운 색에서 흰색으로 되돌리는 통 그림.</summary>
    public Image Bin { get { return bin; } }

    /// <summary>지금 잠겨 있는가.</summary>
    public bool IsLocked { get { return IngredientUnlock.IsLocked(type); } }

    /// <summary>오늘 일차에 맞춰 잠금을 다시 칠한다. <see cref="IngredientLocks"/> 가 부른다.</summary>
    public void Refresh()
    {
        bool locked = IsLocked;

        // 호버를 먼저 끈다. 그쪽 OnDisable 이 원래 색과 그림을 돌려놓은 뒤에 어둡게 해야
        // 우리 색이 남는다. 순서가 바뀌면 어둡게 칠한 것을 그쪽이 곧바로 지운다.
        if (hover != null) hover.enabled = !locked;

        if (bin != null) bin.color = locked ? LockedTint : Color.white;
        if (padlock != null) padlock.enabled = locked;
    }

    /// <summary>
    /// 잠긴 통을 집으려 했다. 안내를 띄우고 참을 돌려준다 — 부르는 쪽은 거기서 멈춘다.
    /// 재료통 둘(고체·액체)이 집기 직전에 물어본다.
    /// </summary>
    public bool RejectIfLocked()
    {
        if (!IsLocked) return false;

        if (toast != null) toast.Show("아직 안 들어온 재료입니다");
        return true;
    }
}
