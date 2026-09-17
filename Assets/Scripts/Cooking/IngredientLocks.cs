using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일차가 바뀔 때 재료통 자물쇠를 한꺼번에 다시 칠한다.
///
/// <see cref="IngredientUnlock.CurrentDay"/> 를 적어 두는 곳도 여기다. 재료통은 집기 직전마다
/// 잠겼는지 물어보는데, 그때마다 DayManager 를 찾아 다니면 아깝다.
/// </summary>
public class IngredientLocks : MonoBehaviour
{
    // 빌더가 꽂아 준다.
    [SerializeField] private DayManager dayManager;

    /// <summary>자물쇠가 풀리는 연출.</summary>
    [SerializeField] private UnlockSequence sequence;

    /// <summary>주문 화면. 이게 닫히는 때가 곧 조리가 시작되는 때다.</summary>
    [SerializeField] private OrderScreenUI orderScreen;

    private SlotLock[] locks;

    /// <summary>오늘 열어 줄 통들. 조리 화면이 뜨기를 기다리는 중이면 들어 있다.</summary>
    private SlotLock[] pending;

    /// <summary>그 통들을 열어 줄 일차.</summary>
    private int pendingDay;

    private void OnEnable()
    {
        // 구독은 Start 가 아니라 여기서 한다. DayManager 가 저보다 먼저 Start 를 돌면서
        // 1일차를 열어 버리면 그 신호를 놓친다.
        if (dayManager != null) dayManager.OnDayStarted += HandleDayStarted;
    }

    private void OnDisable()
    {
        if (dayManager != null) dayManager.OnDayStarted -= HandleDayStarted;
    }

    /// <summary>
    /// 첫 칠은 Start 에서 한다. SlotHover 가 Awake 에서 통의 원래 색을 캐시하는데,
    /// 그 전에 칠하면 어둡게 한 색이 "원래 색"으로 굳어 해금된 뒤에도 어둡게 남는다.
    /// </summary>
    private void Start()
    {
        Refresh(dayManager != null ? dayManager.CurrentDay : 1);
    }

    /// <summary>
    /// 하루가 열렸다. 오늘 새로 열리는 통이 있으면 <b>바로 풀지 않고</b> 잠긴 채로 둔다.
    ///
    /// 지금은 손님이 주문을 말하는 중이라 조리대가 안 보인다. 여기서 풀어 버리면 자물쇠가
    /// 언제 없어졌는지 아무도 못 본다. 조리 화면이 뜰 때까지 기다렸다가 거기서 연출로 푼다.
    /// 기다리는 동안은 어제 상태 그대로라, 잠긴 통은 잠긴 모습이고 집히지도 않는다.
    /// </summary>
    private void HandleDayStarted(int day)
    {
        SlotLock[] opening = FindOpening(day);

        if (opening.Length == 0 || sequence == null)
        {
            Refresh(day);
            return;
        }

        Refresh(day - 1);
        pending = opening;
        pendingDay = day;
    }

    /// <summary>어제까지 잠겨 있다가 오늘 열리는 통. 순서는 재료 차례(타래가 먼저)를 따른다.</summary>
    private SlotLock[] FindOpening(int day)
    {
        EnsureLocks();

        var found = new List<SlotLock>();
        foreach (SlotLock slot in locks)
        {
            if (slot == null) continue;

            if (IngredientUnlock.IsLocked(slot.Type, day - 1) && !IngredientUnlock.IsLocked(slot.Type, day))
            {
                found.Add(slot);
            }
        }

        found.Sort((a, b) => ((int)a.Type).CompareTo((int)b.Type));
        return found.ToArray();
    }

    /// <summary>
    /// 조리 화면이 떴는지 지켜본다. 주문 화면이 닫히는 순간이 곧 조리가 시작되는 순간이다.
    ///
    /// 주문 화면이 닫히는 자리를 직접 부르지 않고 여기서 본다. 그 자리는 손님이 [넵] 을 누르는
    /// 평범한 길이라, 거기에 해금을 끼우면 5일 내내 도는 흐름에 이틀치 예외가 박힌다.
    /// </summary>
    private void Update()
    {
        if (pending == null) return;
        if (orderScreen != null && orderScreen.IsOpen) return;

        SlotLock[] opening = pending;
        pending = null;

        StartCoroutine(OpenAfterPause(opening, pendingDay));
    }

    /// <summary>
    /// 조리 화면이 뜨고 한 박자 쉰 뒤에 자물쇠를 푼다.
    ///
    /// 화면이 나타나자마자 터뜨렸더니 어색했다. 눈이 조리대에 앉기도 전에 무언가 끝나 있다.
    /// 쉬는 동안은 어제 상태 그대로다 — 오늘 열릴 통도 아직 잠겨 있어서 집히지 않는다.
    /// 여기서 미리 열어 두면 연출을 보기도 전에 쓸 수 있게 되어 앞뒤가 안 맞는다.
    /// </summary>
    private IEnumerator OpenAfterPause(SlotLock[] opening, int day)
    {
        yield return new WaitForSecondsRealtime(EnterPause);

        // 이제 오늘이다. 여는 통은 연출이 직접 그리므로 나머지만 여기서 다시 칠한다.
        IngredientUnlock.CurrentDay = day;
        foreach (SlotLock slot in locks)
        {
            if (slot != null && System.Array.IndexOf(opening, slot) < 0) slot.Refresh();
        }

        yield return sequence.Play(opening);
    }

    /// <summary>조리 화면이 뜨고 자물쇠가 덜컹거리기까지 쉬는 시간.</summary>
    private const float EnterPause = 0.8f;

    private void Refresh(int day)
    {
        IngredientUnlock.CurrentDay = day;
        EnsureLocks();

        foreach (SlotLock slot in locks)
        {
            if (slot != null) slot.Refresh();
        }
    }

    /// <summary>씬에 있는 자물쇠를 한 번만 모아 둔다. 통은 씬이 만들어질 때 다 생긴다.</summary>
    private void EnsureLocks()
    {
        if (locks != null && locks.Length > 0) return;

        locks = FindObjectsByType<SlotLock>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
