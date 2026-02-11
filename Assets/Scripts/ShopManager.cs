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

    [Header("★ 아이템 설명 툴팁 UI")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipName;
    public TextMeshProUGUI tooltipDesc;
    public TextMeshProUGUI tooltipPrice;

    private List<Item> weeklyItems = new List<Item>();
    private Item pendingPurchase;

    // ============================================
    // ★ 아이템 데이터 (통합 22종)
    // ============================================
    private List<Item> artifactItems = new List<Item>();

    void Start()
    {
        InitializeShopItems(); // 아이템 DB 초기화

        if (refreshButton != null) refreshButton.onClick.AddListener(RefreshShop);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmPurchase);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CancelPurchase);

        GenerateWeeklyItems();

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // ★ [핵심] 모든 아이템 22종 등록
    void InitializeShopItems()
    {
        artifactItems.Clear();

        // --- Group A. 기존 오리지널 (9종) ---
        artifactItems.Add(new Item("Discount Coupon", "CouponIcon", "상점 가격 20% 할인", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Mirror of Rank", "MirrorIcon", "가장 비싼 아이템 효과 복사", 8, ItemType.Artifact));
        artifactItems.Add(new Item("Magic Paint", "PaintIcon", "랜덤 타일 2칸에 보너스 점수 부여", 4, ItemType.Artifact));
        artifactItems.Add(new Item("Chaos Orb", "OrbIcon", "매 라운드 랜덤한 유물 효과 발동", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Heavy Shackle", "ShackleIcon", "점수 2배 / 리롤 횟수 -1", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Underdog's Hope", "HopeIcon", "주사위 합 24 이하일 때 점수 3배", 4, ItemType.Artifact));
        artifactItems.Add(new Item("Devil's Contract", "ContractIcon", "점수 5배 / 인벤토리 2칸 잠금", 10, ItemType.Artifact));
        artifactItems.Add(new Item("Blackjack", "BlackjackIcon", "주사위 합 21일 때 점수 20배", 7, ItemType.Artifact));
        artifactItems.Add(new Item("Extra Heart", "HeartIcon", "[즉시] 목숨 +1 증가", 5, ItemType.Artifact));

        // --- Group B. 리롤 & 기회 (4종) ---
        artifactItems.Add(new Item("Lucky Coin", "CoinIcon", "리롤 시 10% 확률로 횟수 차감 X", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Time Capsule", "TimeIcon", "남은 리롤 횟수 다음 라운드로 이월", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Overload Gear", "GearIcon", "매 라운드 리롤 +2회 / 최대 목숨 -1", 8, ItemType.Artifact));
        artifactItems.Add(new Item("Recharge Pack", "PackIcon", "[소모품] 사용 즉시 리롤 +2회 충전", 3, ItemType.Consumable));

        // --- Group C. 변경 & 도박 (2종) ---
        artifactItems.Add(new Item("Chaos Fund", "ChaosIcon", "[소모품] +20칩 획득 & 모든 유물 랜덤 변경", 4, ItemType.Consumable));
        artifactItems.Add(new Item("Pandora's Box", "BoxIcon", "랜덤 배율(0.5~3.0) & 인벤토리 랜덤 잠금", 7, ItemType.Artifact));

        // --- Group D. 성장 & 경제 (3종) ---
        artifactItems.Add(new Item("Artifact Collector", "ArtIcon", "보유 유물 수만큼 배율 증가", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Dice Collector", "DiceIcon", "사용 주사위 수만큼 배율 증가", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Credit Card", "CardIcon", "-20칩까지 외상 가능 (이자 발생)", 0, ItemType.Artifact));

        // --- Group E. 스트레이트 특화 (2종) ---
        artifactItems.Add(new Item("Glitch USB", "USBIcon", "스트레이트도 글리치 판정 인정", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Order Emblem", "OrderIcon", "스트레이트 달성 시 배율 +7.0 추가", 7, ItemType.Artifact));

        // --- Group F. 조건부 추가 점수 (신규 2종) ---
        artifactItems.Add(new Item("Ancient Battery", "BatteryIcon", "유물 효과가 발동될 때마다 +50점", 7, ItemType.Artifact));
        artifactItems.Add(new Item("Skill Scanner", "ScannerIcon", "특수 주사위 1개당 +30점", 6, ItemType.Artifact));
    }

    public void ResetShop()
    {
        foreach (var item in artifactItems) item.isSold = false;
        GenerateWeeklyItems();
        if (refreshButton != null) refreshButton.interactable = true;
    }

    void GenerateWeeklyItems()
    {
        weeklyItems.Clear();
        List<Item> shopPool = new List<Item>();
        HashSet<string> ownedItems = new HashSet<string>();

        if (GameData.Instance != null)
        {
            foreach (var item in GameData.Instance.GetAllActiveUpgrades()) ownedItems.Add(item.itemName);
        }

        foreach (var item in artifactItems)
        {
            // Extra Heart와 소모품은 중복 등장 가능
            bool isRebuyable = (item.type == ItemType.Consumable || item.itemName == "Extra Heart");

            if (isRebuyable || (!item.isSold && !ownedItems.Contains(item.itemName)))
            {
                shopPool.Add(item);
            }
        }

        if (shopPool.Count == 0)
        {
            if (refreshButton != null) refreshButton.interactable = false;
            if (refreshCostText != null) refreshCostText.text = "품절";
            foreach (Transform child in weeklyItemsContainer) Destroy(child.gameObject);
            return;
        }

        if (refreshButton != null) refreshButton.interactable = true;

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
            GameObject itemUI = CreateDefaultShopCard(item);
            itemUI.transform.SetParent(weeklyItemsContainer, false);
        }

        int cost = (GameData.Instance != null) ? GameData.Instance.shopRerollCost : 2;
        if (refreshCostText != null) refreshCostText.text = $"🔄 새로고침 [{cost} C]";
    }

    public int GetAdjustedCost(int originalCost)
    {
        // [Discount Coupon] 할인 적용
        if (GameData.Instance != null && GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Discount Coupon"))
        {
            return Mathf.Max(1, (int)(originalCost * 0.8f));
        }
        return originalCost;
    }

    GameObject CreateDefaultShopCard(Item item)
    {
        GameObject card = new GameObject($"ShopCard_{item.itemName}");
        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f);

        Button btn = card.AddComponent<Button>();
        // 소모품이거나 Extra Heart면 계속 구매 가능
        bool isRebuyable = (item.type == ItemType.Consumable || item.itemName == "Extra Heart");
        if (!isRebuyable && item.isSold) btn.interactable = false;
        else btn.onClick.AddListener(() => OnItemClicked(item));

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 350);

        ItemHoverTrigger trigger = card.AddComponent<ItemHoverTrigger>();
        trigger.targetItem = item;

        // 아이콘 표시
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        Sprite loadedSprite = Resources.Load<Sprite>(item.itemIcon);
        if (loadedSprite != null)
        {
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = loadedSprite;
            iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 160);
            if (item.isSold && !isRebuyable) iconImg.color = new Color(1, 1, 1, 0.5f);
        }
        else
        {
            TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
            iconTxt.text = item.itemName.Substring(0, 1);
            iconTxt.fontSize = 80;
            iconTxt.alignment = TextAlignmentOptions.Center;
        }
        iconObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

        // 이름
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI name = nameObj.AddComponent<TextMeshProUGUI>();
        name.text = item.itemName;
        name.fontSize = 24;
        name.fontStyle = FontStyles.Bold;
        name.color = Color.black;
        name.alignment = TextAlignmentOptions.Center;
        name.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 130);
        name.enableWordWrapping = true;

        // 가격
        GameObject priceObj = new GameObject("Price");
        priceObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI price = priceObj.AddComponent<TextMeshProUGUI>();
        int finalPrice = GetAdjustedCost(item.buyPrice);
        price.text = $"{finalPrice} C";
        if (finalPrice < item.buyPrice) price.color = new Color(0f, 0.6f, 0f);
        else price.color = new Color(0f, 0.2f, 0.8f);
        price.fontSize = 32;
        price.alignment = TextAlignmentOptions.Center;
        price.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -110);

        // 품절 텍스트
        if (item.isSold && !isRebuyable)
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

        int finalPrice = GetAdjustedCost(item.buyPrice);
        if (tooltipPrice != null)
        {
            tooltipPrice.text = (finalPrice < item.buyPrice)
                ? $"가격: <s color=red>{item.buyPrice}</s> -> <color=green>{finalPrice} C</color>"
                : $"가격: {finalPrice} C";
        }

        tooltipPanel.SetActive(true);
        tooltipPanel.transform.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    void OnItemClicked(Item item)
    {
        bool isRebuyable = (item.type == ItemType.Consumable || item.itemName == "Extra Heart");
        if (!isRebuyable && item.isSold) return;

        if (!isRebuyable && GameData.Instance != null && GameData.Instance.artifactRelics.Count >= GameData.Instance.MaxInventorySize)
        {
            Debug.Log("🔒 인벤토리가 꽉 찼거나 잠겨 있습니다!");
            return;
        }
        ShowPurchaseConfirmation(item);
    }

    void ConfirmPurchase()
    {
        HideTooltip();
        if (pendingPurchase == null) return;

        int finalCost = GetAdjustedCost(pendingPurchase.buyPrice);

        if (GameData.Instance.SpendChips(finalCost))
        {
            if (pendingPurchase.type == ItemType.Consumable)
            {
                ApplyConsumableEffect(pendingPurchase);
                Debug.Log($"💊 소모품 사용: {pendingPurchase.itemName}");
            }
            else if (pendingPurchase.itemName == "Extra Heart")
            {
                GameData.Instance.handsLeft++;
                Debug.Log("❤️ 목숨 +1 획득!");
            }
            else
            {
                bool added = GameData.Instance.AddUpgradeItem(pendingPurchase);
                if (added)
                {
                    pendingPurchase.isSold = true;
                    Debug.Log($"🎒 유물 획득: {pendingPurchase.itemName}");
                }
                else
                {
                    GameData.Instance.AddChips(finalCost);
                    Debug.Log("❌ 구매 실패: 인벤토리 오류 (환불됨)");
                }
            }
            GenerateWeeklyItems();
            if (UIManager.Instance != null) UIManager.Instance.UpdateAllUI();
        }
        else
        {
            Debug.Log("💸 칩이 부족합니다!");
        }
        confirmPanel.SetActive(false); pendingPurchase = null;
    }

    void ApplyConsumableEffect(Item item)
    {
        switch (item.itemName)
        {
            case "Recharge Pack":
                GameData.Instance.rerollsLeft += 2;
                Debug.Log("🔋 리롤 충전 완료!");
                break;
            case "Chaos Fund":
                GameData.Instance.AddChips(20);
                GameData.Instance.RandomizeAllArtifacts(artifactItems);
                Debug.Log("🌪️ 아이템 전체 교체!");
                break;
        }
    }

    void CancelPurchase()
    {
        HideTooltip();
        confirmPanel.SetActive(false);
        pendingPurchase = null;
    }

    void ShowPurchaseConfirmation(Item item)
    {
        pendingPurchase = item;
        confirmPanel.SetActive(true);
        int cost = GetAdjustedCost(item.buyPrice);
        confirmText.text = $"{item.itemName}\n{cost} C\n구매하시겠습니까?";
    }

    public void RefreshShop()
    {
        if (GameData.Instance.SpendChips(GameData.Instance.shopRerollCost))
            GenerateWeeklyItems();
    }
}