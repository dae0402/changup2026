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
    public TextMeshProUGUI totalScoreText; // 노란색 큰 박스

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
    public List<DropSlot> allSlots = new List<DropSlot>();

    private Color buffColor = new Color(1f, 1f, 0f, 0.7f); // 노란색 (Buff)
    private Color nerfColor = new Color(1f, 0f, 0f, 0.7f); // 빨간색 (Nerf)

    void Start()
    {
        if (rollButton) rollButton.onClick.AddListener(OnRollButtonClicked);
        if (rerollButton) rerollButton.onClick.AddListener(OnRerollButtonClicked);
        if (submitButton) submitButton.onClick.AddListener(OnSubmitButtonClicked);

        UpdateAllUI();
        ShowTitleScreen();
    }

    public void UpdateAllUI()
    {
        UpdateStats();
        UpdateGamePanel();
        UpdateButtons();
        UpdateInventory();
        UpdateDiceUI();

        UpdateSlotColors();
    }

    void UpdateDiceUI()
    {
        if (GameData.Instance == null || allSlots.Count == 0) return;

        foreach (DiceData dice in GameData.Instance.currentDice)
        {
            if (dice.slotIndex >= 0 && dice.slotIndex < allSlots.Count)
            {
                allSlots[dice.slotIndex].SetDice(dice);
            }
        }
    }

    void UpdateSlotColors()
    {
        if (GameData.Instance == null || allSlots.Count == 0) return;

        foreach (var slot in allSlots)
        {
            if (slot != null) slot.ResetHighlight();
        }

        foreach (DiceData dice in GameData.Instance.currentDice)
        {
            string typeName = dice.diceType.Trim().ToLower();

            if (typeName == "buff dice" || typeName == "mirror dice" || typeName == "chameleon dice")
            {
                PaintNeighbors(dice.slotIndex, "cross", buffColor);
            }
            else if (typeName == "spring dice")
            {
                PaintNeighbors(dice.slotIndex, "cross", buffColor);
                PaintNeighbors(dice.slotIndex, "3x3_outer", nerfColor);
            }
        }
    }

    void PaintNeighbors(int centerIndex, string shape, Color color)
    {
        int columns = 5;
        int row = centerIndex / columns;
        int col = centerIndex % columns;

        for (int i = 0; i < allSlots.Count; i++)
        {
            if (allSlots[i] == null) continue;

            int r = i / columns;
            int c = i % columns;
            bool isTarget = false;

            if (i == centerIndex) continue;

            if (shape == "cross")
            {
                if ((r == row && Mathf.Abs(c - col) == 1) || (c == col && Mathf.Abs(r - row) == 1))
                    isTarget = true;
            }
            else if (shape == "3x3_outer")
            {
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

    public void UpdateStats()
    {
        if (GameData.Instance == null) return;
        if (debtText) debtText.text = $"${GameData.Instance.debt}";
        if (walletText) walletText.text = $"${GameData.Instance.wallet}";
        if (chipsText) chipsText.text = GameData.Instance.chips.ToString();
        if (handsLeftText) handsLeftText.text = GameData.Instance.handsLeft.ToString();
        if (shopChipsText != null) shopChipsText.text = GameData.Instance.chips.ToString();
    }

    // ★ [핵심 수정됨] 점수 이중 곱셈 버그 해결 및 표시 로직 정상화
    public void UpdateGamePanel()
    {
        if (GameData.Instance == null) return;

        if (savedPotText) savedPotText.text = GameData.Instance.savedPot.ToString();

        float mult = GameData.Instance.feverMultiplier > 0 ? GameData.Instance.feverMultiplier : 1f;

        // 1. 배율 표시 (FeverMult)
        if (feverMultText) feverMultText.text = $"x {mult:F1}";

        // 2. 기본 점수 표시 (Current) 
        // -> 최종 점수에서 배율을 다시 나눠서 '곱해지기 전의 순수 주사위 합'을 보여줍니다.
        int rawScore = Mathf.RoundToInt(GameData.Instance.currentHandScore / mult);
        if (currentScoreText) currentScoreText.text = rawScore.ToString();

        // 3. 노란색 큰 박스 (Total) 
        // -> 배율을 또 곱하지 않고, 이미 계산이 완벽하게 끝난 최종 점수(currentHandScore)를 그대로 사용합니다!
        int estimatedWin = GameData.Instance.currentHandScore + GameData.Instance.savedPot;
        if (totalScoreText) totalScoreText.text = estimatedWin.ToString();
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
    }

    public void ShowScorePopup(int score) => Debug.Log($"💰 {score}점 획득!");

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