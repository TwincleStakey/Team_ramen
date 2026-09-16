using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화면 위 UI 가 서로 겹치는지 검사한다. `Tools/Ramen/Check UI Overlap`.
///
/// 2026-09-16 에 상단바 「금일 수익」 판을 넓히다가 「마무리」 버튼을 14칸 파고들었다.
/// 컴파일도 배선도 멀쩡했고 콘솔도 조용해서, 사용자가 화면을 보고 알려 줄 때까지 몰랐다.
/// 눈으로 보는 것을 사람에게만 맡기지 않으려고 둔다.
///
/// **글상자가 아니라 실제로 그려진 글자 범위로 잰다.** 왼쪽 정렬 라벨은 상자만 넓고 글자는
/// 안 닿는 일이 흔해서, 상자로 재면 멀쩡한 것까지 겹쳤다고 잡는다. 처음에 그렇게 재서
/// 「Tab」·「B」 라벨이 셋이나 잘못 걸렸다.
///
/// 가로만 본다. 상단바처럼 한 줄에 늘어선 것이 지금까지 사고가 난 자리 전부다.
/// 세로까지 보면 층이 다른 판들이 서로 겹쳤다고 나와 목록이 쓸모없어진다.
/// </summary>
public static class UiOverlapCheck
{
    /// <summary>이만큼 넘게 겹쳐야 알린다. 1칸 스치는 것은 9-슬라이스 테두리라 정상이다.</summary>
    private const float Threshold = 1f;

    /// <summary>
    /// 이 이름 밑은 통째로 건너뛴다.
    ///
    /// 서로 겹치는 것이 정상인 것들이다 — 팝업은 화면을 덮으라고 있고, 연출 판도 마찬가지다.
    /// </summary>
    private static readonly string[] Skip =
    {
        "Backdrop", "Dim", "Flash", "Cosmos", "Aura", "Sparkles", "ScreenGrain",
        "DragLayer", "Cursor", "TutorialOutline", "Outline",
    };

    private class Box
    {
        public string Path;
        public float Left;
        public float Right;
    }

    [MenuItem("Tools/Ramen/Check UI Overlap")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();

        // 검사할 묶음. 한 줄에 늘어서는 곳만 본다.
        string[] groups = { "TopBar", "OrderScreen" };
        int total = 0;

        foreach (string groupName in groups)
        {
            Transform group = Find(scene, groupName);
            if (group == null)
            {
                Debug.LogWarning("[UiOverlapCheck] " + groupName + " 을 찾지 못했습니다.");
                continue;
            }

            total += Report(groupName, Collect(group, group));
        }

        // 상단바 왼쪽 구석은 키 안내가 같이 쓴다. 부모가 달라 따로 모아 붙인다.
        Transform bar = Find(scene, "TopBar");
        if (bar != null)
        {
            List<Box> mixed = Collect(bar, bar.parent);
            foreach (string hint in new[] { "KeyHint_Tab", "KeyHint_Book" })
            {
                Transform t = Find(scene, hint);
                if (t != null) mixed.AddRange(Collect(t, bar.parent));
            }
            total += Report("TopBar + KeyHint", mixed);
        }

        if (total == 0) Debug.Log("[UiOverlapCheck] 겹치는 UI 없음.");
        else Debug.LogWarning("[UiOverlapCheck] 겹침 " + total + "건. 위 목록을 확인하세요.");
    }

    /// <summary>group 밑의 그릴 것들을 space 기준 가로 범위로 모은다.</summary>
    private static List<Box> Collect(Transform group, Transform space)
    {
        var found = new List<Box>();

        foreach (Graphic g in group.GetComponentsInChildren<Graphic>(true))
        {
            if (!g.gameObject.activeInHierarchy) continue;
            if (IsSkipped(g.transform)) continue;

            var rect = g.rectTransform;
            float left, right;

            var text = g as TextMeshProUGUI;
            if (text != null)
            {
                if (string.IsNullOrEmpty(text.text)) continue;

                // 글상자가 아니라 실제로 찍힌 글자 범위.
                text.ForceMeshUpdate();
                if (text.textBounds.size.x <= 0f) continue;

                float center = space.InverseTransformPoint(rect.TransformPoint(text.textBounds.center)).x;
                float half = text.textBounds.size.x * 0.5f * rect.lossyScale.x / space.lossyScale.x;
                left = center - half;
                right = center + half;
            }
            else
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                left = space.InverseTransformPoint(corners[0]).x;
                right = space.InverseTransformPoint(corners[2]).x;
            }

            found.Add(new Box
            {
                Path = PathOf(g.transform, group),
                Left = Mathf.Min(left, right),
                Right = Mathf.Max(left, right),
            });
        }

        return found;
    }

    private static int Report(string title, List<Box> boxes)
    {
        int hits = 0;

        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                // 같은 판의 아이콘·글자끼리는 겹쳐도 된다. 한쪽 경로가 다른 쪽으로 시작하면 부모·자식이다.
                if (boxes[i].Path.StartsWith(boxes[j].Path) || boxes[j].Path.StartsWith(boxes[i].Path)) continue;

                float over = Mathf.Min(boxes[i].Right, boxes[j].Right)
                           - Mathf.Max(boxes[i].Left, boxes[j].Left);
                if (over <= Threshold) continue;

                Debug.LogWarning(string.Format("[UiOverlapCheck] {0} — {1:F0}칸 겹침\n    {2}  ({3:F0} ~ {4:F0})\n    {5}  ({6:F0} ~ {7:F0})",
                    title, over,
                    boxes[i].Path, boxes[i].Left, boxes[i].Right,
                    boxes[j].Path, boxes[j].Left, boxes[j].Right));
                hits++;
            }
        }

        return hits;
    }

    private static bool IsSkipped(Transform t)
    {
        for (Transform c = t; c != null; c = c.parent)
        {
            foreach (string s in Skip)
            {
                if (c.name.StartsWith(s)) return true;
            }
        }
        return false;
    }

    private static string PathOf(Transform t, Transform stop)
    {
        string path = t.name;
        for (Transform c = t.parent; c != null && c != stop; c = c.parent) path = c.name + "/" + path;
        return path;
    }

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeep(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindDeep(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
