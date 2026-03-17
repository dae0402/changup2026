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

    // ★ [기존 배열 삭제하고 새롭게 추가된 인벤토리 컨테이너] ★
    [Header("★ 인벤토리 UI (보유 아이템 표시)")]
    public Transform artifactContainer; // 유물(Artifact) 8칸이 들어갈 부모 오브젝트
    public Transform diceContainer;     // 주사위(Dice) 5칸이 들어갈 부모 오브젝트

    [Header("화면들")]
    public GameObject titleScreen;
    public GameObject gameScreen;
    public GameObject shopScreen;
    public GameObject rightPanel;

    [Header("주사위 슬롯 데이터")]
    public List<DropSlot> allSlots = new List<DropSlot>();

    private Color buffColor = new Color(1f, 1f, 0f, 0.7f);
    private Color nerfColor = new Color(1f, 0f, 0f, 0.7f);

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
        UpdateInventory(); // ★ 인벤토리 UI 갱신 함수 호출
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

    public void UpdateGamePanel()
    {
        if (GameData.Instance == null) return;

        if (savedPotText) savedPotText.text = GameData.Instance.savedPot.ToString();

        float mult = GameData.Instance.feverMultiplier > 0 ? GameData.Instance.feverMultiplier : 1f;

        if (feverMultText) feverMultText.text = $"x {mult:F1}";

        int rawScore = Mathf.RoundToInt(GameData.Instance.currentHandScore / mult);
        if (currentScoreText) currentScoreText.text = rawScore.ToString();

        int estimatedWin = GameData.Instance.currentHandScore + GameData.Instance.savedPot;
        if (totalScoreText) totalScoreText.text = estimatedWin.ToString();
    }

    public void UpdateButtons()
    {
        if (GameData.Instance == null) return;

        // 바닥에 주사위가 깔려있는지(진행 중인 턴이 있는지) 확인
        bool hasActiveDice = GameData.Instance.currentDice.Count > 0;

        // 1. ROLL DICE 버튼: 주사위가 '없을 때만(새 턴)' 누를 수 있게 잠금!
        if (rollButton)
        {
            rollButton.interactable = GameData.Instance.handsLeft > 0 &&
                                      !GameData.Instance.isRolling &&
                                      !hasActiveDice;
        }

        // 2. CHEAT 버튼: 주사위가 '있을 때만' 누를 수 있게 설정
        if (rerollButton)
        {
            rerollButton.interactable = GameData.Instance.rerollsLeft > 0 &&
                                        hasActiveDice &&
                                        !GameData.Instance.isRolling;
        }
        if (rerollButtonText) rerollButtonText.text = $"CHEAT [{GameData.Instance.rerollsLeft}]";

        // 3. CASH OUT 버튼: 제출 가능한 상태일 때 활성화
        if (submitButton)
        {
            submitButton.interactable = GameData.Instance.canSubmit && !GameData.Instance.isRolling;
        }
    }

    // =========================================================
    // ★ [새로 추가됨] 인벤토리 시각화 및 판매 로직
    // =========================================================
    public void UpdateInventory()
    {
        if (GameData.Instance == null) return;

        // 1. 유물(Artifact) UI 업데이트
        if (artifactContainer != null)
        {
            foreach (Transform child in artifactContainer) Destroy(child.gameObject);

            for (int i = 0; i < GameData.Instance.artifactRelics.Count; i++)
            {
                Item item = GameData.Instance.artifactRelics[i];
                int index = i;

                GameObject slotObj = CreateInventorySlotUI(item.itemName, item.sellPrice, artifactContainer);

                // ★ 슬롯 전체가 아니라, 우측 상단의 'SellBadge' 버튼에만 판매 기능 연결!
                Button sellBtn = slotObj.transform.Find("SellBadge").GetComponent<Button>();
                sellBtn.onClick.AddListener(() => SellArtifact(index, item));
            }
        }

        // 2. 특수 주사위(Dice) UI 업데이트
        if (diceContainer != null)
        {
            foreach (Transform child in diceContainer) Destroy(child.gameObject);

            for (int i = 0; i < GameData.Instance.ownedSpecialDice.Count; i++)
            {
                string diceName = GameData.Instance.ownedSpecialDice[i];
                int index = i;
                int diceSellPrice = 5;

                GameObject slotObj = CreateInventorySlotUI(diceName, diceSellPrice, diceContainer);

                Button sellBtn = slotObj.transform.Find("SellBadge").GetComponent<Button>();
                sellBtn.onClick.AddListener(() => SellDice(index, diceSellPrice, diceName));
            }
        }
    }

    // ★ [핵심] 기획하신 이미지처럼 우측 상단에 빨간색 '-' 버튼을 달아주는 함수
    // ★ [수정됨] 글자가 안 겹치도록 깔끔한 세로형 '카드' 디자인으로 변경
    GameObject CreateInventorySlotUI(string nameText, int sellPrice, Transform parent)
    {
        // 1. 카드 배경
        GameObject slotObj = new GameObject($"InvSlot_{nameText}");
        slotObj.transform.SetParent(parent, false);

        Image bg = slotObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f); // 고급스러운 다크 그레이

        Outline outline = slotObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f); // 밝은 테두리
        outline.effectDistance = new Vector2(2, -2);

        // 2. 아이템 이름 텍스트 (자동 크기 조절 기능 켬)
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(slotObj.transform, false);
        TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
        nameTxt.text = nameText;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.white;

        // ★ 글자가 길면 알아서 작아지도록 AutoSizing 켜기
        nameTxt.enableAutoSizing = true;
        nameTxt.fontSizeMin = 12;
        nameTxt.fontSizeMax = 22;

        RectTransform nameRt = nameTxt.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0.35f);
        nameRt.anchorMax = new Vector2(1, 0.85f);
        nameRt.offsetMin = new Vector2(5, 0); // 좌우 여백 5px
        nameRt.offsetMax = new Vector2(-5, 0);

        // 3. 우측 상단 판매(-) 버튼
        GameObject badgeObj = new GameObject("SellBadge");
        badgeObj.transform.SetParent(slotObj.transform, false);

        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.9f, 0.2f, 0.2f, 1f);
        Button badgeBtn = badgeObj.AddComponent<Button>();

        RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
        badgeRt.sizeDelta = new Vector2(30, 30);
        badgeRt.anchorMin = new Vector2(1, 1);
        badgeRt.anchorMax = new Vector2(1, 1);
        badgeRt.pivot = new Vector2(1, 1);
        badgeRt.anchoredPosition = new Vector2(10, 10);

        // 4. 마이너스(-) 기호 텍스트
        GameObject minusObj = new GameObject("MinusText");
        minusObj.transform.SetParent(badgeObj.transform, false);
        TextMeshProUGUI minusTxt = minusObj.AddComponent<TextMeshProUGUI>();
        minusTxt.text = "-";
        minusTxt.fontSize = 28;
        minusTxt.alignment = TextAlignmentOptions.Center;
        minusTxt.color = Color.white;
        minusTxt.fontStyle = FontStyles.Bold;

        RectTransform minusRt = minusTxt.GetComponent<RectTransform>();
        minusRt.anchorMin = Vector2.zero;
        minusRt.anchorMax = Vector2.one;
        minusRt.offsetMin = Vector2.zero;
        minusRt.offsetMax = new Vector2(0, 2);

        // 5. 판매 가격 표시
        GameObject priceObj = new GameObject("PriceText");
        priceObj.transform.SetParent(slotObj.transform, false);
        TextMeshProUGUI priceTxt = priceObj.AddComponent<TextMeshProUGUI>();
        priceTxt.text = $"팔기 +{sellPrice}C";
        priceTxt.fontSize = 14;
        priceTxt.alignment = TextAlignmentOptions.Center;
        priceTxt.color = new Color(1f, 0.8f, 0.2f, 1f);
        priceTxt.fontStyle = FontStyles.Bold;

        RectTransform priceRt = priceTxt.GetComponent<RectTransform>();
        priceRt.anchorMin = new Vector2(0, 0);
        priceRt.anchorMax = new Vector2(1, 0.3f);
        priceRt.offsetMin = Vector2.zero;
        priceRt.offsetMax = Vector2.zero;

        return slotObj;
    }

    void SellArtifact(int index, Item item)
    {
        GameData.Instance.AddChips(item.sellPrice);

        // 아이템 효과 롤백 (예: 오버로드 기어 팔면 최대 목숨 다시 복구)
        if (item.itemName == "Overload Gear") GameData.Instance.maxHands++;

        GameData.Instance.artifactRelics.RemoveAt(index);
        Debug.Log($"🎒 유물 판매 완료: {item.itemName} (+{item.sellPrice}칩)");
        UpdateAllUI();
    }

    void SellDice(int index, int sellPrice, string diceName)
    {
        GameData.Instance.AddChips(sellPrice);
        GameData.Instance.ownedSpecialDice.RemoveAt(index);
        Debug.Log($"🎲 주사위 판매 완료: {diceName} (+{sellPrice}칩)");
        UpdateAllUI();
    }
    // =========================================================

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

    public void ShowHandCompletedEffect(string handName) => Debug.Log($"🎰 {handName} 완성!");
    public void ShowMessage(string message) => Debug.Log($"💬 {message}");
}