using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("기본 상점 UI")]
    public Transform weeklyItemsContainer;
    public GameObject shopItemPrefab;
    public Button refreshButton;
    public TextMeshProUGUI refreshCostText;

    [Header("구매 확인 UI")]
    public GameObject confirmPanel;
    public TextMeshProUGUI confirmText;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("랜덤 뽑기 시스템")]
    public Button randomPackButton;
    public int randomPackCost = 0; // 랜덤 뽑기 비용 0원
    public GameObject selectionPanel;
    public Transform selectionContainer;
    public GameObject selectionCardPrefab;

    [Header("★ 인벤토리 UI 연결")]
    public Transform randomSlotContainer;
    public Transform artifactSlotContainer;
    public GameObject upgradeIconPrefab;

    [Header("★ 아이템 설명 툴팁 UI")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipName;
    public TextMeshProUGUI tooltipDesc;
    public TextMeshProUGUI tooltipPrice;

    private List<Item> weeklyItems = new List<Item>();
    private Item pendingPurchase;

    // ============================================
    // 아이템 데이터
    // ============================================

    // 1. 상점 판매용 유물 목록
    private List<Item> artifactItems = new List<Item>
    {
        // --- 기존 유물 ---
        new Item("Magic Dice", "MagicDice", "라운드 시작 시 첫 주사위는 6 고정", 0, ItemType.Artifact),
        new Item("Payback", "PaybackIcon", "라운드 종료 시 남은 리롤 x 2칩 획득", 0, ItemType.Artifact),
        new Item("Lucky Coin", "LuckyIcon", "리롤 시 10% 확률로 횟수 소모 안 함", 0, ItemType.Artifact),
        new Item("Blackjack", "BlackjackIcon", "주사위 합이 21이면 점수 x7", 0, ItemType.Artifact),
        new Item("Time Capsule", "TimeIcon", "남은 리롤 이월 (최대 10개)", 0, ItemType.Artifact),
        new Item("Devil Dice", "DevilIcon", "점수 x5, 인벤토리 잠금 (판매/확장 불가)", 0, ItemType.Artifact),
        new Item("Sniper Scope", "ScopeIcon", "원 페어일 때 배율 2배 추가 증가", 0, ItemType.Artifact),
        new Item("Heavy Weight", "WeightIcon", "눈금 4, 5, 6 주사위는 점수 +3", 0, ItemType.Artifact),
        new Item("Odd Eye", "EyeIcon", "모든 주사위가 홀수(1,3,5)면 점수 x3", 0, ItemType.Artifact),

        // --- 특수 아이템 ---
        new Item("Greed's Bargain", "GreedIcon", "즉시 +30칩 / 이후 스테이지 보상 0", 0, ItemType.Artifact),
        new Item("Golden Scale", "ScaleIcon", "보유 칩 10개당 점수 +20%", 0, ItemType.Artifact),
        new Item("Coin Sack", "CoinIcon", "구매 시 10칩 획득 (소모성)", 0, ItemType.Consumable),
        new Item("Chaos Fund", "ChaosIcon", "+50칩 / 인벤토리 랜덤 변경", 0, ItemType.Consumable),
        new Item("Pandora's Box", "BoxIcon", "인벤토리 +2칸 / 인벤토리 랜덤 변경", 0, ItemType.Consumable),
        new Item("Black Card", "CardIcon", "-20칩까지 외상 가능 (매턴 이자 발생)", 0, ItemType.Artifact),

        // --- 이전 추가된 유물 ---
        new Item("Bargain Flyer", "FlyerIcon", "상점 리롤 비용 -1 (최소 1)", 0, ItemType.Artifact),
        new Item("Artisan Whetstone", "WhetstoneIcon", "노말 등급 주사위 배율 2배", 0, ItemType.Artifact),
        new Item("Golden Piggy Bank", "BankIcon", "라운드 종료 시 10칩당 +1칩 (최대 10)", 0, ItemType.Artifact),
        new Item("Fate Die", "FateIcon", "라운드 종료 시 주사위 도박 (-10 ~ +30)", 0, ItemType.Artifact),
        new Item("Eco Bin", "RecycleIcon", "남은 리롤 횟수만큼 추가 칩 획득", 0, ItemType.Artifact),

        // --- ★ [신규 추가] 요청하신 4가지 유물 ---
        new Item("Soul Collector", "SoulIcon", "유물 삭제 시마다 배율 +0.5배 중첩", 0, ItemType.Artifact),
        new Item("Heavy Hand", "HandIcon", "최종 점수 2배 / 리롤 비용 +1 증가", 0, ItemType.Artifact),
        new Item("Deja Vu", "DejaIcon", "같은 족보 2연속 시 리롤 +1 회복", 0, ItemType.Artifact),
        new Item("Twin's Blessing", "TwinIcon", "라운드 첫 리롤은 무조건 투 페어 (홀드 없을 시)", 0, ItemType.Artifact)
    };

    // 2. 랜덤 뽑기 전용 풀 (특수 주사위 17종 포함)
    private List<Item> randomPool = new List<Item>
    {
        // ★ [신규] 특수 주사위 17종 추가
        new Item("Time Dice", "⏳", "보유 라운드만큼 배율 증가", 0, ItemType.Dice),
        new Item("Ice Dice", "❄️", "십자가 +5 / 대각선 -4", 0, ItemType.Dice),
        new Item("Rubber Dice", "🎈", "주변 효과 절반(1/2) 감소", 0, ItemType.Dice),
        new Item("TimeAttack(R)", "⚔️", "십자가 범위: 잃은 목숨당 +2점", 0, ItemType.Dice),
        new Item("Buff Dice", "🔰", "십자가 범위: 버프 효과 3배 증폭", 0, ItemType.Dice),
        new Item("TimeAttack(S)", "⏱️", "자신: 잃은 목숨당 배율 1.5배", 0, ItemType.Dice),
        new Item("Spring Dice", "🌀", "3x3 내부 2배 / 외부 절반", 0, ItemType.Dice),
        new Item("Laser Dice", "🔫", "가로/세로 같은 줄 주사위 수 비례 점수", 0, ItemType.Dice),
        new Item("Mirror Dice", "🪞", "십자가 중 가장 높은 등급(배율) 복사", 0, ItemType.Dice),
        new Item("Reflection Dice", "🔁", "가로 범위: 버프/디버프 반전", 0, ItemType.Dice),
        new Item("Steel Dice", "🛡️", "자신: 모든 디버프 무시", 0, ItemType.Dice),
        new Item("Offer Dice", "🩸", "자신 0점 / 주변 3x3 배율 3배", 0, ItemType.Dice),
        new Item("Chameleon Dice", "🦎", "ㄱ자 범위 중 가장 큰 수 복사", 0, ItemType.Dice),
        new Item("Stone Dice", "🪨", "십자가 범위: 돌(무효화) 상태 적용", 0, ItemType.Dice),
        new Item("Absorb Dice", "🧽", "주변 3x3 버프를 모두 흡수", 0, ItemType.Dice),
        new Item("Glass Dice", "🍷", "1,2면 0점 / 3~6면 2배", 0, ItemType.Dice),
        new Item("Ancient Dice", "🗿", "5라운드 후 전설(6/x5)로 진화", 0, ItemType.Dice)
    };

    void Start()
    {
        if (refreshButton != null) refreshButton.onClick.AddListener(RefreshShop);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmPurchase);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CancelPurchase);
        if (randomPackButton != null) randomPackButton.onClick.AddListener(OnRandomPackClicked);

        GenerateWeeklyItems();
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);

        UpdateAllUpgradeUI();
    }

    // ★ 게임 재시작 시 호출되는 초기화 함수
    public void ResetShop()
    {
        foreach (var item in artifactItems) item.isSold = false;
        GenerateWeeklyItems();
        if (refreshButton != null) refreshButton.interactable = true;
    }

    // ============================================
    // 상점 진열 로직
    // ============================================
    void GenerateWeeklyItems()
    {
        weeklyItems.Clear();
        List<Item> shopPool = new List<Item>();

        // 1. 소모품 재입고 (부활) 로직
        // 소모품은 매주 다시 살 수 있게 초기화
        foreach (var item in artifactItems)
        {
            if (item.type == ItemType.Consumable) item.isSold = false;
        }

        // 2. 중복 방지 (이미 가진 유물은 등장하지 않음)
        HashSet<string> ownedItems = new HashSet<string>();
        foreach (var item in GameData.Instance.GetAllActiveUpgrades())
        {
            ownedItems.Add(item.itemName);
        }

        // 3. 상점에 진열할 아이템 필터링
        foreach (var item in artifactItems)
        {
            // 팔리지 않았고(소모품은 위에서 부활) && 내가 안 가진 것만 추가
            if (!item.isSold && !ownedItems.Contains(item.itemName))
            {
                shopPool.Add(item);
            }
        }

        // 4. 품절 처리
        if (shopPool.Count == 0)
        {
            if (refreshButton != null) refreshButton.interactable = false;
            if (refreshCostText != null) refreshCostText.text = "품절";
            foreach (Transform child in weeklyItemsContainer) Destroy(child.gameObject);
            return;
        }

        if (refreshButton != null) refreshButton.interactable = true;

        // 5. 셔플 및 선택 (최대 3개)
        List<Item> shuffled = new List<Item>(shopPool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            Item temp = shuffled[i];
            int r = Random.Range(i, shuffled.Count);
            shuffled[i] = shuffled[r];
            shuffled[r] = temp;
        }

        int count = Mathf.Min(3, shuffled.Count);
        for (int i = 0; i < count; i++) weeklyItems.Add(shuffled[i]);

        DisplayWeeklyItems();
    }

    void DisplayWeeklyItems()
    {
        if (weeklyItemsContainer == null) return;
        foreach (Transform child in weeklyItemsContainer) Destroy(child.gameObject);

        foreach (Item item in weeklyItems)
        {
            GameObject itemUI = CreateShopItemUI(item);
            itemUI.transform.SetParent(weeklyItemsContainer, false);
        }

        // GameData의 shopRerollCost를 가져와서 표시
        int cost = (GameData.Instance != null) ? GameData.Instance.shopRerollCost : 2;
        if (refreshCostText != null) refreshCostText.text = $"🔄 새로고침 [{cost} C]";
    }

    GameObject CreateShopItemUI(Item item) => CreateDefaultShopCard(item);

    GameObject CreateDefaultShopCard(Item item)
    {
        GameObject card = new GameObject($"ShopCard_{item.itemName}");
        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f);

        Button btn = card.AddComponent<Button>();
        if (item.isSold) btn.interactable = false;
        else btn.onClick.AddListener(() => OnItemClicked(item));

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 350);

        ItemHoverTrigger trigger = card.AddComponent<ItemHoverTrigger>();
        trigger.targetItem = item;

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        Sprite loadedSprite = Resources.Load<Sprite>(item.itemIcon);

        if (loadedSprite != null)
        {
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = loadedSprite;
            iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 160);
            if (item.isSold) iconImg.color = new Color(1, 1, 1, 0.5f);
        }
        else
        {
            TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
            iconTxt.text = item.itemIcon;
            iconTxt.fontSize = 80;
            iconTxt.alignment = TextAlignmentOptions.Center;
        }
        iconObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI name = nameObj.AddComponent<TextMeshProUGUI>();
        name.text = item.itemName;
        name.fontSize = 28;
        name.fontStyle = FontStyles.Bold;
        name.color = Color.black;
        name.alignment = TextAlignmentOptions.Center;
        name.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 130);

        GameObject priceObj = new GameObject("Price");
        priceObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI price = priceObj.AddComponent<TextMeshProUGUI>();
        price.text = $"{item.buyPrice} C";
        price.fontSize = 32;
        price.color = new Color(0f, 0.2f, 0.8f);
        price.alignment = TextAlignmentOptions.Center;
        price.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -110);

        if (item.isSold)
        {
            GameObject soldObj = new GameObject("SoldOutText");
            soldObj.transform.SetParent(card.transform, false);
            TextMeshProUGUI soldTxt = soldObj.AddComponent<TextMeshProUGUI>();
            soldTxt.text = "SOLD OUT";
            soldTxt.fontSize = 50;
            soldTxt.fontStyle = FontStyles.Bold;
            soldTxt.color = Color.red;
            soldTxt.alignment = TextAlignmentOptions.Center;
            soldObj.transform.localRotation = Quaternion.Euler(0, 0, 15);
        }
        return card;
    }

    public void ShowTooltip(Item item, Vector3 position)
    {
        if (tooltipPanel == null) return;
        if (tooltipName != null) tooltipName.text = item.itemName;
        if (tooltipDesc != null) tooltipDesc.text = item.description;
        if (tooltipPrice != null) tooltipPrice.text = $"가격: {item.buyPrice} C";

        tooltipPanel.SetActive(true);
        RectTransform rect = tooltipPanel.GetComponent<RectTransform>();
        if (rect != null) rect.anchoredPosition = Vector2.zero;
        tooltipPanel.transform.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // ============================================
    // 구매 로직
    // ============================================
    void OnItemClicked(Item item)
    {
        if (item.isSold) return;

        // Devil Dice 구매 방지 (주사위)
        if (item.type == ItemType.Dice && GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Devil Dice"))
        {
            ShowMessage("👿 악마의 계약으로 주사위 교체 불가!");
            return;
        }

        // 상점 유물은 슬롯 제한 체크
        if (item.type != ItemType.Consumable)
        {
            if (item.type == ItemType.Artifact && GameData.Instance.artifactRelics.Count >= GameData.Instance.maxArtifacts) { ShowMessage("유물 슬롯 꽉참"); return; }
        }

        ShowPurchaseConfirmation(item);
    }

    void ConfirmPurchase()
    {
        HideTooltip();

        if (pendingPurchase == null) return;

        if (GameData.Instance.SpendChips(pendingPurchase.buyPrice))
        {
            ProcessItemEffect(pendingPurchase);

            if (pendingPurchase.type != ItemType.Consumable)
            {
                if (pendingPurchase.type == ItemType.Dice) GameData.Instance.AddItemToInventory(pendingPurchase);
                else GameData.Instance.AddUpgradeItem(pendingPurchase);
            }

            pendingPurchase.isSold = true;
            GenerateWeeklyItems(); // 목록 갱신 (중복 방지를 위해 재생성)
            UpdateAllUpgradeUI();
            if (UIManager.Instance != null) UIManager.Instance.UpdateAllUI();
            ShowMessage("구매 완료");
        }
        else
        {
            ShowMessage("칩이 부족합니다!");
        }
        confirmPanel.SetActive(false); pendingPurchase = null;
    }

    void CancelPurchase()
    {
        HideTooltip();
        confirmPanel.SetActive(false);
        pendingPurchase = null;
    }

    // ============================================
    // 랜덤 뽑기
    // ============================================
    void OnRandomPackClicked()
    {
        if (GameData.Instance.SpendChips(randomPackCost))
        {
            ShowSelectionPopup();
            if (UIManager.Instance) UIManager.Instance.UpdateAllUI();
        }
        else
        {
            ShowMessage($"칩이 부족합니다! ({randomPackCost} C 필요)");
        }
    }

    void ShowSelectionPopup()
    {
        if (selectionPanel == null) return;
        selectionPanel.SetActive(true);
        foreach (Transform child in selectionContainer) Destroy(child.gameObject);

        List<Item> options = GetRandomUpgrades(3);

        foreach (Item item in options)
        {
            GameObject card = Instantiate(selectionCardPrefab, selectionContainer);
            Transform t = card.transform.Find("Name"); if (t) t.GetComponent<TextMeshProUGUI>().text = item.itemName;

            Button btn = card.GetComponent<Button>();
            if (btn == null) btn = card.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnUpgradeSelected(item));

            ItemHoverTrigger trigger = card.AddComponent<ItemHoverTrigger>();
            trigger.targetItem = item;
        }
    }

    List<Item> GetRandomUpgrades(int count)
    {
        List<Item> result = new List<Item>();
        List<Item> temp = new List<Item>(randomPool);
        for (int i = 0; i < count && temp.Count > 0; i++)
        {
            int r = Random.Range(0, temp.Count);
            result.Add(temp[r]);
            temp.RemoveAt(r);
        }
        return result;
    }

    void OnUpgradeSelected(Item item)
    {
        HideTooltip();

        ProcessItemEffect(item);

        if (item.type == ItemType.Dice)
        {
            if (GameData.Instance.AddItemToInventory(item))
            {
                ShowMessage($"[주사위] {item.itemName} 획득!");
            }
            else
            {
                ShowMessage("주사위 인벤토리가 꽉 찼습니다!");
            }
        }
        else if (item.type != ItemType.Consumable)
        {
            if (GameData.Instance.AddUpgradeItem(item))
            {
                ShowMessage($"[능력] {item.itemName} 획득!");
            }
            else
            {
                ShowMessage("능력 슬롯이 꽉 찼습니다!");
            }
        }
        else
        {
            ShowMessage($"{item.itemName} 사용됨!");
        }

        selectionPanel.SetActive(false);
        UpdateAllUpgradeUI();
        if (UIManager.Instance) UIManager.Instance.UpdateAllUI();
    }

    // ★ 효과 처리 함수 (구매 즉시 발동되는 효과들)
    void ProcessItemEffect(Item item)
    {
        switch (item.itemName)
        {
            // 바겐세일 전단지: 상점 리롤 비용 감소 (최소 1원)
            case "Bargain Flyer":
                GameData.Instance.shopRerollCost = Mathf.Max(1, GameData.Instance.shopRerollCost - 1);
                Debug.Log($"📉 바겐세일! 리롤 비용이 {GameData.Instance.shopRerollCost}원으로 감소했습니다.");
                if (refreshCostText != null) refreshCostText.text = $"🔄 새로고침 [{GameData.Instance.shopRerollCost} C]";
                break;

            case "Greed's Bargain":
                GameData.Instance.AddChips(30);
                GameData.Instance.isStageRewardBlocked = true;
                Debug.Log("💰 탐욕의 거래: 30칩 획득, 보상 차단됨.");
                break;

            case "Coin Sack":
                GameData.Instance.AddChips(10);
                Debug.Log("💰 동전 주머니: 10칩 획득");
                break;

            case "Chaos Fund":
                GameData.Instance.AddChips(50);
                GameData.Instance.RandomizeInventory(artifactItems);
                Debug.Log("🌪️ 혼돈의 자금: 50칩 획득 & 인벤토리 셔플");
                break;

            case "Pandora's Box":
                // GameData의 ExpandInventory 호출 (Devil Dice 체크 포함)
                GameData.Instance.ExpandInventory(2);
                GameData.Instance.RandomizeInventory(artifactItems);
                Debug.Log("📦 판도라의 상자: 슬롯 확장 시도 & 셔플");
                break;

            case "Black Card":
                GameData.Instance.hasCreditCard = true;
                Debug.Log("💳 블랙 카드: 외상 한도 -20까지 확장");
                break;
        }
    }

    void ShowMessage(string m) { Debug.Log(m); }

    public void UpdateAllUpgradeUI()
    {
        UpdateSingleGrid(randomSlotContainer, GameData.Instance.randomBuffs);
        UpdateSingleGrid(artifactSlotContainer, GameData.Instance.artifactRelics);
    }

    void UpdateSingleGrid(Transform container, List<Item> dataList)
    {
        if (container == null) return;
        for (int i = 0; i < container.childCount; i++)
        {
            Transform slot = container.GetChild(i);
            foreach (Transform child in slot) Destroy(child.gameObject);

            if (i < dataList.Count)
            {
                Item data = dataList[i];
                GameObject iconObj = Instantiate(upgradeIconPrefab, slot);
                Image img = iconObj.GetComponent<Image>();
                Sprite loaded = Resources.Load<Sprite>(data.itemIcon);
                if (img != null && loaded != null) { img.sprite = loaded; var t = iconObj.GetComponentInChildren<TextMeshProUGUI>(); if (t) t.text = ""; }
                else { var t = iconObj.GetComponentInChildren<TextMeshProUGUI>(); if (t) t.text = data.itemIcon; }
                iconObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }
        }
    }

    // 새로고침 함수
    public void RefreshShop()
    {
        if (GameData.Instance.SpendChips(GameData.Instance.shopRerollCost))
            GenerateWeeklyItems();
    }

    void ShowPurchaseConfirmation(Item item) { pendingPurchase = item; confirmPanel.SetActive(true); confirmText.text = $"{item.itemName}\n{item.buyPrice} C\n구매?"; }
}