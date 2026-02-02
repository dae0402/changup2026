using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverResultUI : MonoBehaviour
{
    [Header("UI 텍스트")]
    public Text playTimeText;       // 플레이 시간 표시용
    public Text earnedMoneyText;    // 획득 돈 표시용
    public Text highScoreText;      // 점수 표시용

    [Header("버튼")]
    public Button replayButton;
    public Button mainMenuButton;

    void Start()
    {
        // 버튼 기능 연결 (씬 이름은 본인 프로젝트에 맞게 수정하세요)
        replayButton.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
        mainMenuButton.onClick.AddListener(() => SceneManager.LoadScene("TitleScene"));
    }

    public void ShowResult()
    {
        gameObject.SetActive(true); // 패널 켜기

        // 데이터 표시 (GameData가 있다면)
        if (GameData.Instance != null)
        {
            float time = Time.timeSinceLevelLoad;
            playTimeText.text = $"플레이 시간 : {Mathf.Floor(time / 60):00}:{Mathf.Floor(time % 60):00}";
            earnedMoneyText.text = $"획득한 재화 : ${GameData.Instance.wallet}";
            highScoreText.text = $"최고 점수 : {GameData.Instance.totalScore}";
        }
    }
}