using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 시치미와 향미유를 그릇 오른쪽 위에 "아이콘 X N" 으로 따로 보여 준다.
///
/// 이 둘만 따로 두는 이유는 그릇에 그림이 안 올라가기 때문이다. Bowl.AddIcon 은
/// 그릇용 그림이 없는 재료를 조미료로 보고 일찍 빠져나간다(Bowl.cs 의 icon == null 관문).
/// 그래서 넣어도 그릇이 그대로라 넣었는지 확인할 길이 없다.
/// 나머지 재료는 그릇에 쌓이는 게 보이므로 여기서 세지 않는다.
///
/// 0개면 줄을 통째로 감춘다. "X 0" 을 남겨 두면 안 넣은 것과 0을 넣은 것이 같아 보인다.
/// </summary>
public class SeasoningBadges : MonoBehaviour
{
    /// <summary>한 줄. 아이콘이 줄의 뿌리이고 수량 글자가 그 자식이다.</summary>
    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public TextMeshProUGUI count;
    }

    public Row flavorOil;
    public Row chiliPowder;

    /// <summary>그릇이 바뀔 때마다 Bowl 이 부른다.</summary>
    public void Refresh(int oil, int chili)
    {
        Show(flavorOil, oil);
        Show(chiliPowder, chili);
    }

    private static void Show(Row row, int count)
    {
        if (row == null || row.root == null) return;

        row.root.SetActive(count > 0);
        if (count > 0 && row.count != null) row.count.text = "X " + count;
    }
}
