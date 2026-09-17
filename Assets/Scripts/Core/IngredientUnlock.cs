using UnityEngine;

/// <summary>
/// 일차별 해금. <b>주문을 만드는 쪽과 조리 화면이 같은 표를 본다.</b>
///
/// 표가 두 벌이면 언젠가 어긋나고, 그때 손님은 조리대에서 잠겨 있는 재료를 시킨다 —
/// 100% 가 나올 수 없는 주문이다. 그래서 여기 한 곳에만 둔다.
/// 읽는 곳은 둘이다: B 의 <c>DialogueScenarioGenerator</c>(후보에서 뺀다)와
/// 조리 화면의 재료통(집히지 않게 하고 자물쇠를 얹는다).
///
///   1일차  시오만. 김·계란·숙주·목이 잠김
///   2일차  쇼유 열림. 김·계란 열림
///   3일차  돈코츠 열림. 숙주·목이 열림 — 이때부터 전부 열려 있다
///
/// 기본 레시피와 맞물려 있다. 1·2일차에 나오는 시오·쇼유의 기본에는 잠긴 넷이 하나도 없고,
/// 넷이 다 들어 있는 돈코츠 기본은 3일차에야 열린다. <see cref="RecipeGenerator.GetBaseRecipe"/>
/// 를 고칠 때 이 관계가 깨지면 역시 만들 수 없는 주문이 된다.
/// </summary>
public static class IngredientUnlock
{
    /// <summary>
    /// 지금 몇 일차인가. 재료통이 집기 전에 물어보는데, 그때마다 DayManager 를 찾으면 아깝다.
    /// 하루가 열릴 때 <see cref="IngredientLocks"/> 가 한 번 적어 둔다.
    /// </summary>
    public static int CurrentDay = 1;

    /// <summary>열리는 순서. 앞에서부터 잘라 쓰므로 이 순서가 곧 해금 순서다.</summary>
    public static readonly RamenType[] RamenOrder =
    {
        RamenType.Shio,     // 1일차
        RamenType.Shoyu,    // 2일차
        RamenType.Tonkotsu  // 3일차
    };

    /// <summary>그 날까지 열린 라멘 가짓수. 1일차 1종 · 2일차 2종 · 3일차부터 3종.</summary>
    public static int UnlockedRamenCount(int day)
    {
        return Mathf.Clamp(day, 1, RamenOrder.Length);
    }

    /// <summary>그 일차에 아직 안 들어온 재료인가.</summary>
    public static bool IsLocked(IngredientType type, int day)
    {
        if (day >= 3) return false;

        // 3일차 — 돈코츠와 같은 날 들어온다.
        if (type == IngredientType.BeanSprout || type == IngredientType.WoodEar) return true;

        // 타래는 라멘이 열려야 쓴다. 육수·면은 첫날부터 쓴다.
        if (type == IngredientType.TonkotsuBase) return true;
        if (type == IngredientType.ShoyuTare && day < 2) return true;

        if (day >= 2) return false;

        // 2일차
        return type == IngredientType.Nori || type == IngredientType.Egg;
    }

    /// <summary>조리 화면이 묻는 자리. 지금 일차로 판단한다.</summary>
    public static bool IsLocked(IngredientType type)
    {
        return IsLocked(type, CurrentDay);
    }
}
