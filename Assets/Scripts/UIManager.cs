using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 사용

/// <summary>
/// 모든 UI 요소를 업데이트하는 매니저
/// GameData의 값이 바뀌면 화면에 표시
/// </summary>
public class UIManager : MonoBehaviour
{
    // ============================================
    // Singleton 패턴
    // ============================================
    private static UIManager _instance;

    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UIManager>();
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("UIManager 중복! 기존 것을 사용합니다.");
            Destroy(gameObject);
            return;
        }
        _instance = this;

        Debug.Log("UIManager Awake 완료!");
    }

    // ============================================
    // UI 요소 연결 (인스펙터에서 드래그 앤 드롭)
    // ============================================

    [Header("스탯 UI (오른쪽 패널)")]
    public TextMeshProUGUI debtText;        // 빚 표시
    public TextMeshProUGUI walletText;      // 지갑 표시
    public TextMeshProUGUI chipsText;       // 칩 표시 (게임 화면)
    public TextMeshProUGUI handsLeftText;   // 남은 손 횟수

    [Header("상점 UI")]
    public TextMeshProUGUI shopChipsText;   // 칩 표시 (상점 화면)

    [Header("게임 패널 UI")]
    public TextMeshProUGUI handNameText;         // 현재 패 이름
    public TextMeshProUGUI handMultiplierText;   // 배율 표시
    public TextMeshProUGUI savedPotText;         // 저장된 점수
    public TextMeshProUGUI currentScoreText;     // 현재 점수
    public TextMeshProUGUI feverMultText;        // 피버 배율
    public TextMeshProUGUI totalScoreText;       // 총 점수

    [Header("버튼들")]
    public Button rollButton;       // 주사위 굴리기 버튼
    public Button rerollButton;     // 리롤 버튼
    public Button submitButton;     // 제출 버튼
    public TextMeshProUGUI rerollButtonText; // 리롤 남은 횟수 표시

    [Header("인벤토리 슬롯들")]
    public GameObject[] inventorySlots; // 8개 슬롯 배열

    [Header("화면들")]
    public GameObject titleScreen;  // 타이틀 화면
    public GameObject gameScreen;   // 게임 화면
    public GameObject shopScreen;   // 상점 화면

    // ★ RightPanel은 Hierarchy에서 GameScreen 밖으로 빼내서 연결해야 합니다!
    public GameObject rightPanel;   // 통계 패널 (게임/상점 공통)

    // ============================================
    // 초기화
    // ============================================
    void Start()
    {
        // 버튼 클릭 이벤트 연결
        if (rollButton) rollButton.onClick.AddListener(OnRollButtonClicked);
        if (rerollButton) rerollButton.onClick.AddListener(OnRerollButtonClicked);
        if (submitButton) submitButton.onClick.AddListener(OnSubmitButtonClicked);

        // 초기 UI 업데이트
        UpdateAllUI();

        // 시작 시 타이틀 화면 보여주기 (필요시)
        ShowTitleScreen();

        Debug.Log("UIManager 초기화 완료!");
    }

    // ============================================
    // 전체 UI 업데이트
    // ============================================

    public void UpdateAllUI()
    {
        UpdateStats();
        UpdateGamePanel();
        UpdateButtons();
        UpdateInventory();
    }

    public void UpdateStats()
    {
        if (GameData.Instance == null) return;

        if (debtText) debtText.text = $"${GameData.Instance.debt}";
        if (walletText) walletText.text = $"${GameData.Instance.wallet}";
        if (chipsText) chipsText.text = GameData.Instance.chips.ToString();
        if (handsLeftText) handsLeftText.text = GameData.Instance.handsLeft.ToString();

        if (shopChipsText != null)
        {
            shopChipsText.text = GameData.Instance.chips.ToString();
        }
    }

    public void UpdateGamePanel()
    {
        if (GameData.Instance == null) return;

        if (savedPotText) savedPotText.text = GameData.Instance.savedPot.ToString();
        if (currentScoreText) currentScoreText.text = GameData.Instance.currentHandScore.ToString();
        if (feverMultText) feverMultText.text = $"x {GameData.Instance.feverMultiplier:F1}";
        if (totalScoreText) totalScoreText.text = GameData.Instance.totalScore.ToString();
    }

    public void UpdateButtons()
    {
        if (GameData.Instance == null) return;

        if (rollButton) rollButton.interactable = GameData.Instance.handsLeft > 0 && !GameData.Instance.isRolling;

        if (rerollButton)
        {
            rerollButton.interactable = GameData.Instance.rerollsLeft > 0 &&
                                        GameData.Instance.currentDice.Count > 0 &&
                                        !GameData.Instance.isRolling;
        }
        if (rerollButtonText) rerollButtonText.text = $"CHEAT [{GameData.Instance.rerollsLeft}]";

        if (submitButton) submitButton.interactable = GameData.Instance.canSubmit && !GameData.Instance.isRolling;
    }

    public void UpdateInventory()
    {
        if (GameData.Instance == null || inventorySlots == null) return;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            TextMeshProUGUI slotText = inventorySlots[i].GetComponentInChildren<TextMeshProUGUI>();

            if (i < GameData.Instance.inventory.Count)
            {
                Item item = GameData.Instance.inventory[i];
                if (slotText) slotText.text = item.itemIcon;

                int index = i;
                Button slotButton = inventorySlots[i].GetComponent<Button>();
                if (slotButton != null)
                {
                    slotButton.onClick.RemoveAllListeners();
                    slotButton.onClick.AddListener(() => OnInventorySlotClicked(index));
                }
            }
            else
            {
                if (slotText) slotText.text = "";
            }
        }
    }

    public void DisplayHandResult(string handName, float multiplier)
    {
        if (handNameText) handNameText.text = handName;
        if (handMultiplierText) handMultiplierText.text = $"x{multiplier:F1}";
        Debug.Log($"패 결과: {handName} (x{multiplier})");
    }

    // ============================================
    // 버튼 클릭 이벤트
    // ============================================

    void OnRollButtonClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.RollDice();
    }

    void OnRerollButtonClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.RerollSelectedDice();
    }

    void OnSubmitButtonClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.SubmitHand();
    }

    void OnInventorySlotClicked(int index)
    {
        if (GameData.Instance == null || index >= GameData.Instance.inventory.Count) return;

        Item item = GameData.Instance.inventory[index];
        Debug.Log($"아이템 '{item.itemName}' 판매: {item.sellPrice}칩");

        GameData.Instance.AddChips(item.sellPrice);
        GameData.Instance.RemoveItemFromInventory(index);
        UpdateAllUI();
    }

    // ============================================
    // 화면 전환 (여기가 핵심!)
    // ============================================

    public void ShowTitleScreen()
    {
        if (titleScreen) titleScreen.SetActive(true);
        if (gameScreen) gameScreen.SetActive(false);
        if (shopScreen) shopScreen.SetActive(false);

        // ★ 타이틀에서는 안 보임
        if (rightPanel != null) rightPanel.SetActive(false);

        Debug.Log("타이틀 화면으로 전환");
    }

    public void ShowGameScreen()
    {
        if (titleScreen) titleScreen.SetActive(false);
        if (gameScreen) gameScreen.SetActive(true);
        if (shopScreen) shopScreen.SetActive(false);

        // ★ 게임 화면: 보임
        if (rightPanel != null) rightPanel.SetActive(true);

        UpdateNavigationButtons("game");
        Debug.Log("게임 화면으로 전환");
    }

    public void ShowShopScreen()
    {
        if (titleScreen) titleScreen.SetActive(false);
        if (gameScreen) gameScreen.SetActive(false);
        if (shopScreen) shopScreen.SetActive(true);

        // ★ 상점 화면: 보임 (여기가 중요!)
        if (rightPanel != null) rightPanel.SetActive(true);

        UpdateNavigationButtons("shop");
        UpdateStats(); // 상점 진입 시 스탯 갱신
        Debug.Log("상점 화면으로 전환");
    }

    void UpdateNavigationButtons(string currentScreen)
    {
        Button[] allNavButtons = FindObjectsOfType<Button>();
        foreach (Button btn in allNavButtons)
        {
            if (btn.name.Contains("GameButton"))
            {
                var colors = btn.colors;
                colors.normalColor = (currentScreen == "game") ? new Color(0f, 0.95f, 1f) : new Color(0.2f, 0.2f, 0.2f);
                btn.colors = colors;
            }
            else if (btn.name.Contains("ShopButton"))
            {
                var colors = btn.colors;
                colors.normalColor = (currentScreen == "shop") ? new Color(0f, 0.95f, 1f) : new Color(0.2f, 0.2f, 0.2f);
                btn.colors = colors;
            }
        }
    }

    // ============================================
    // 피드백 (임시)
    // ============================================
    public void ShowScorePopup(int score) => Debug.Log($"💰 {score}점 획득!");
    public void ShowHandCompletedEffect(string handName) => Debug.Log($"🎰 {handName} 완성!");
    public void ShowMessage(string message) => Debug.Log($"💬 {message}");
}