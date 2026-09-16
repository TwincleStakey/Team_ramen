#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Play 모드 화면을 일정 간격으로 찍어 파일로 남긴다. <b>확인용이고 빌드에는 안 들어간다.</b>
///
/// 크레딧의 무대(손님·컷신·아이리스)는 코루틴이라 Play 에서만 돈다. 에디트 모드 렌더로는
/// 정지 화면밖에 못 봐서, 쓰러지는 높이나 까마귀 자리가 맞는지 눈으로 확인할 수가 없었다.
/// 그래서 실제로 돌려 놓고 찍는다.
///
/// 화면을 그대로 뜨려면 <see cref="ScreenCapture.CaptureScreenshotAsTexture"/> 를 써야 하고,
/// 그건 그 프레임의 그리기가 다 끝난 뒤에만 쓸 수 있다(WaitForEndOfFrame).
/// </summary>
public class CreditsRecorder : MonoBehaviour
{
    private string dir;
    private float step;
    private float length;
    private int index;

    /// <summary>찍기 시작한다. 씬을 다시 열어도 살아남는다 — 크레딧이 끝나면 씬이 바뀐다.</summary>
    public static CreditsRecorder Begin(string folder, float fps, float seconds)
    {
        Directory.CreateDirectory(folder);

        var go = new GameObject("CreditsRecorder");
        DontDestroyOnLoad(go);

        var rec = go.AddComponent<CreditsRecorder>();
        rec.dir = folder;
        rec.step = 1f / Mathf.Max(1f, fps);
        rec.length = seconds;
        return rec;
    }

    /// <summary>지금까지 찍은 장수. 밖에서 진행을 보는 데 쓴다.</summary>
    public int Count { get { return index; } }

    private void Start()
    {
        StartCoroutine(Record());
    }

    private IEnumerator Record()
    {
        var wait = new WaitForEndOfFrame();
        float next = 0f;
        float elapsed = 0f;

        while (elapsed < length)
        {
            yield return wait;

            elapsed += Time.unscaledDeltaTime;
            if (elapsed < next) continue;

            next += step;

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, "f" + index.ToString("D5") + ".png"), shot.EncodeToPNG());
            Destroy(shot);
            index++;
        }

        Debug.Log("[크레딧 녹화] " + index + "장 찍었습니다: " + dir);
    }
}
#endif
