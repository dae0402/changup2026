using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<UIManager>();
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    [Header("스탯 UI (오른쪽 패널)")]
    public TextMeshProUGUI debtText;
    public TextMeshProUGUI walletText;
    public TextMeshProUGUI chipsText;
    public TextMeshProUGUI handsLeftText;

    [Header("상점 UI")]
    public TextMeshProUGUI shopChipsText;

    [Header("게임 패널 UI")]
    public TextMeshProUGUI handNameText;
    public TextMeshProUGUI handMultiplierText;
    public TextMeshProUGUI savedPotText;
    public TextMeshProUGUI currentScoreText;
    public TextMeshProUGUI feverMultText;
    public TextMeshProUGUI totalScoreText;

    [Header("버튼들")]
    public Button rollButton;
    public Button rerollButton;
    public Button submitButton;
    public TextMeshProUGUI rerollButtonText;

    [Header("인벤토리 슬롯들")]
    public GameObject[] inventorySlots;

    [Header("화면들")]
    public GameObject titleScreen;
    public GameObject gameScreen;
    public GameObject shopScreen;
    public GameObject rightPanel;

    [Header("★ 주사위 슬롯 데이터")]
    // DiceSpawner에서 생성된 슬롯들이 여기에 담깁니다.
    public List<DropSlot> allSlots = new List<DropSlot>();

    // ★ 색상 설정: 알파값 0.7f로 설정하여 검은 배경에서도 선명하게 보이게 함
    private Color buffColor = new Color(1f, 1f, 0f, 0.7f); // 선명한 노랑 (Buff)
    private Color nerfColor = new Color(1f, 0f, 0f, 0.7f); // 선명한 빨강 (Nerf)

    void Start()
    {
        if (rollButton) rollButton.onClick.AddListener(OnRollButtonClicked);
        if (rerollButton) rerollButton.onClick.AddListener(OnRerollButtonClicked);
        if (submitButton) submitButton.onClick.AddListener(OnSubmitButtonClicked);

        UpdateAllUI();
        ShowTitleScreen();
    }

    // GameManager 등에서 호출하는 메인 UI 업데이트 함수
    public void UpdateAllUI()
    {
        UpdateStats();
        UpdateGamePanel();
        UpdateButtons();
        UpdateInventory();
        UpdateDiceUI();

        // ★ 주사위 위치에 따라 주변 칸 색칠하기
        UpdateSlotColors();
    }

    // 슬롯에 주사위 데이터를 매칭시키는 함수
    void UpdateDiceUI()
    {
        if (GameData.Instance == null || allSlots.Count == 0) return;

        // 모든 슬롯 비우기 (시각적 초기화)
        foreach (var slot in allSlots)
        {
            if (slot != null) slot.ClearSlot();
        }

        // 현재 소지한 주사위 데이터를 슬롯 번호에 맞춰 배치
        foreach (DiceData dice in GameData.Instance.currentDice)
        {
            if (dice.slotIndex >= 0 && dice.slotIndex < allSlots.Count)
            {
                allSlots[dice.slotIndex].SetDice(dice);
            }
        }
    }

    // 주사위 타입별 영향 범위를 찾아 색칠하는 핵심 로직
    void UpdateSlotColors()
    {
        if (GameData.Instance == null || allSlots.Count == 0) return;

        // 1. 모든 슬롯의 하이라이트 초기화 (투명하게)
        foreach (var slot in allSlots)
        {
            if (slot != null) slot.ResetHighlight();
        }

        // 2. 현재 배치된 주사위들을 검사하여 색칠
        foreach (DiceData dice in GameData.Instance.currentDice)
        {
            string typeName = dice.diceType.Trim().ToLower();

            // 버프 계열: 십자(+) 범위 노란색
            if (typeName == "buff dice" || typeName == "mirror dice" || typeName == "chameleon dice")
            {
                PaintNeighbors(dice.slotIndex, "cross", buffColor);
            }
            // 스프링 주사위: 십자(+) 노랑색 + 대각선 4칸 빨간색
            else if (typeName == "spring dice")
            {
                PaintNeighbors(dice.slotIndex, "cross", buffColor);
                PaintNeighbors(dice.slotIndex, "3x3_outer", nerfColor);
            }
        }
    }

    // ★ 가로 5칸 그리드 기준으로 주변 인덱스를 찾는 수학 함수
    void PaintNeighbors(int centerIndex, string shape, Color color)
    {
        int columns = 5; // 가로 5칸 기준
        int row = centerIndex / columns;
        int col = centerIndex % columns;

        for (int i = 0; i < allSlots.Count; i++)
        {
            if (allSlots[i] == null) continue;

            int r = i / columns;
            int c = i % columns;
            bool isTarget = false;

            // 자기 자신 슬롯은 색칠에서 제외
            if (i == centerIndex) continue;

            if (shape == "cross")
            {
                // 상하좌우 1칸 거리 체크
                if ((r == row && Mathf.Abs(c - col) == 1) || (c == col && Mathf.Abs(r - row) == 1))
                    isTarget = true;
            }
            else if (shape == "3x3")
            {
                // 주변 8칸 모두 체크
                if (Mathf.Abs(r - row) <= 1 && Mathf.Abs(c - col) <= 1)
                    isTarget = true;
            }
            else if (shape == "3x3_outer")
            {
                // 3x3 범위(주변 8칸) 중 상하좌우를 제외한 대각선 4칸만 체크
                bool in3x3 = Mathf.Abs(r - row) <= 1 && Mathf.Abs(c - col) <= 1;
                bool isCross = (r == row || c == col);
                if (in3x3 && !isCross) isTarget = true;
            }

            if (isTarget)
            {
                allSlots[i].SetSlotColor(color);
            }
        }
    }

    // --- GameManager와의 연결을 위한 필수 함수들 ---

    public void UpdateStats()
    {
        if (GameData.Instance == null) return;
        if (debtText) debtText.text = $"${GameData.Instance.debt}";
        if (walletText) walletText.text = $"${GameData.Instance.wallet}";
        if (chipsText) chipsText.text = GameData.Instance.chips.ToString();
        if (handsLeftText) handsLeftText.text = GameData.Instance.handsLeft.ToString();
        if (shopChipsText != null) shopChipsText.text = GameData.Instance.chips.ToString();
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
            else if (slotText) slotText.text = "";
        }
    }

    public void DisplayHandResult(string handName, float multiplier)
    {
        if (handNameText) handNameText.text = handName;
        if (handMultiplierText) handMultiplierText.text = $"x{multiplier:F1}";
    }

    public void ShowScorePopup(int score) => Debug.Log($"💰 {score}점 획득!");

    // --- 화면 전환 및 내비게이션 ---

    public void ShowTitleScreen()
    {
        if (titleScreen) titleScreen.SetActive(true);
        if (gameScreen) gameScreen.SetActive(false);
        if (shopScreen) shopScreen.SetActive(false);
        if (rightPanel != null) rightPanel.SetActive(false);
    }

    public void ShowGameScreen()
    {
        if (titleScreen) titleScreen.SetActive(false);
        if (gameScreen) gameScreen.SetActive(true);
        if (shopScreen) shopScreen.SetActive(false);
        if (rightPanel != null) rightPanel.SetActive(true);
        UpdateNavigationButtons("game");
    }

    public void ShowShopScreen()
    {
        if (titleScreen) titleScreen.SetActive(false);
        if (gameScreen) gameScreen.SetActive(false);
        if (shopScreen) shopScreen.SetActive(true);
        if (rightPanel != null) rightPanel.SetActive(true);
        UpdateNavigationButtons("shop");
        UpdateStats();
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

    // --- 이벤트 리스너 ---

    void OnRollButtonClicked() { if (GameManager.Instance != null) GameManager.Instance.RollDice(); }
    void OnRerollButtonClicked() { if (GameManager.Instance != null) GameManager.Instance.RerollSelectedDice(); }
    void OnSubmitButtonClicked() { if (GameManager.Instance != null) GameManager.Instance.SubmitHand(); }

    void OnInventorySlotClicked(int index)
    {
        if (GameData.Instance == null || index >= GameData.Instance.inventory.Count) return;
        Item item = GameData.Instance.inventory[index];
        GameData.Instance.AddChips(item.sellPrice);
        GameData.Instance.RemoveItemFromInventory(index);
        UpdateAllUI();
    }

    public void ShowHandCompletedEffect(string handName) => Debug.Log($"🎰 {handName} 완성!");
    public void ShowMessage(string message) => Debug.Log($"💬 {message}");
}