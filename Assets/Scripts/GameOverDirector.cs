using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameOverDirector : MonoBehaviour
{
    [Header("1. 꺼질 게임 화면 (GameScreen)")]
    public RectTransform gameScreenRect;
    public GameObject gameOverText;
    public Image bloodImage;
    public GameObject terminalPanel;
    public Text terminalText;

    [Header("2. 최종 결과창")]
    public GameOverResultUI resultUI;

    [Header("3. 효과음")]
    public AudioSource audioSource;
    public AudioClip footstepClip;
    public AudioClip gunshotClip;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void TriggerGameOver()
    {
        StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        // [1] 발소리 & 화면 흔들림
        if (gameOverText != null) gameOverText.SetActive(true);
        if (audioSource != null && footstepClip != null)
        {
            audioSource.clip = footstepClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        float timer = 0f;
        Vector3 originPos = gameScreenRect.anchoredPosition;

        while (timer < 3.0f)
        {
            timer += Time.unscaledDeltaTime;
            float x = Random.Range(-5f, 5f);
            float y = Random.Range(-5f, 5f);
            gameScreenRect.anchoredPosition = originPos + new Vector3(x, y, 0);
            if (audioSource != null) audioSource.volume = timer / 3.0f;
            yield return null;
        }

        gameScreenRect.anchoredPosition = originPos;
        if (audioSource != null) audioSource.Stop();

        // [2] 탕! & 피 튀김
        if (audioSource != null && gunshotClip != null) audioSource.PlayOneShot(gunshotClip);
        if (bloodImage != null) bloodImage.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(1.5f);

        // ==================================================
        // [3] 터미널 연출 (속도 조절됨)
        // ==================================================
        if (terminalPanel != null)
        {
            terminalPanel.SetActive(true);

            Image panelImg = terminalPanel.GetComponent<Image>();
            if (panelImg != null) panelImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            if (terminalText != null)
            {
                terminalText.gameObject.SetActive(true);

                terminalText.color = Color.white;
                terminalText.fontSize = 60;
                terminalText.alignment = TextAnchor.UpperLeft;
                terminalText.horizontalOverflow = HorizontalWrapMode.Overflow;
                terminalText.verticalOverflow = VerticalWrapMode.Overflow;

                terminalText.text = "";

                // ★ [수정] 타이핑 속도: 0.6초 -> 1.0초 (천천히)
                string[] steps = { ".", ". .", ". . .", ". . . |" };
                foreach (var step in steps)
                {
                    terminalText.text = step;
                    yield return new WaitForSecondsRealtime(1.0f); // 1초마다 찍힘
                }

                // ★ [수정] 깜빡임 속도: 0.3초 -> 0.5초 (천천히 깜빡)
                for (int i = 0; i < 3; i++)
                {
                    terminalText.text = ". . .";
                    yield return new WaitForSecondsRealtime(0.5f);
                    terminalText.text = ". . . |";
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // [4] CRT 전원 꺼짐
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            gameScreenRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(1f, 0.01f, 1f), t / 0.2f);
            yield return null;
        }

        t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            gameScreenRect.localScale = Vector3.Lerp(new Vector3(1f, 0.01f, 1f), Vector3.zero, t / 0.2f);
            yield return null;
        }

        gameScreenRect.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(1.0f);

        // [5] 결과창
        if (resultUI != null) resultUI.ShowResult();
    }
}