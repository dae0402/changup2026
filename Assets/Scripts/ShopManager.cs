using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

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

    [Header("★ 왼쪽 아래 주사위 뽑기권 UI")]
    public Transform dicePackContainer;

    [Header("★ [중요] 3지선다 선택 팝업 UI")]
    public GameObject selectionPanel;
    public Transform selectionContainer;
    public TextMeshProUGUI selectionTitle;

    [Header("구매 확인 팝업")]
    public GameObject confirmPanel;
    public TextMeshProUGUI confirmText;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("툴팁 UI")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipName;
    public TextMeshProUGUI tooltipDesc;
    public TextMeshProUGUI tooltipPrice;

    private List<Item> weeklyItems = new List<Item>();
    private Item pendingPurchase;

    private List<Item> artifactItems = new List<Item>();
    private List<Item> diceItems = new List<Item>();

    void Start()
    {
        InitializeShopItems();
        InitializeDiceItems();

        if (refreshButton != null) refreshButton.onClick.AddListener(RefreshShop);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmPurchase);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CancelPurchase);

        GenerateWeeklyItems();
        GenerateDicePack();

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        if (selectionPanel != null) selectionPanel.SetActive(false);
    }

    void InitializeShopItems()
    {
        artifactItems.Clear();
        artifactItems.Add(new Item("Discount Coupon", "CouponIcon", "상점 가격 20% 할인", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Mirror of Rank", "MirrorIcon", "가장 비싼 아이템 효과 복사", 8, ItemType.Artifact));
        artifactItems.Add(new Item("Magic Paint", "PaintIcon", "랜덤 타일 2칸에 보너스 점수 부여", 4, ItemType.Artifact));
        artifactItems.Add(new Item("Chaos Orb", "OrbIcon", "매 라운드 랜덤한 유물 효과 발동", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Heavy Shackle", "ShackleIcon", "점수 2배 / 리롤 횟수 -1", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Underdog's Hope", "HopeIcon", "주사위 합 24 이하일 때 점수 3배", 4, ItemType.Artifact));
        artifactItems.Add(new Item("Devil's Contract", "ContractIcon", "점수 5배 / 인벤토리 2칸 잠금", 10, ItemType.Artifact));
        artifactItems.Add(new Item("Blackjack", "BlackjackIcon", "주사위 합 21일 때 점수 20배", 7, ItemType.Artifact));
        artifactItems.Add(new Item("Extra Heart", "HeartIcon", "[즉시] 목숨 +1 증가", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Lucky Coin", "CoinIcon", "리롤 시 10% 확률로 횟수 차감 X", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Time Capsule", "TimeIcon", "남은 리롤 횟수 다음 라운드로 이월", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Overload Gear", "GearIcon", "매 라운드 리롤 +2회 / 최대 목숨 -1", 8, ItemType.Artifact));
        artifactItems.Add(new Item("Recharge Pack", "PackIcon", "[소모품] 사용 즉시 리롤 +2회 충전", 3, ItemType.Consumable));
        artifactItems.Add(new Item("Chaos Fund", "ChaosIcon", "[소모품] +20칩 획득 & 모든 유물 랜덤 변경", 4, ItemType.Consumable));
        artifactItems.Add(new Item("Pandora's Box", "BoxIcon", "랜덤 배율(0.5~3.0) & 인벤토리 랜덤 잠금", 7, ItemType.Artifact));
        artifactItems.Add(new Item("Artifact Collector", "ArtIcon", "보유 유물 수만큼 배율 증가", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Dice Collector", "DiceIcon", "사용 주사위 수만큼 배율 증가", 5, ItemType.Artifact));
        artifactItems.Add(new Item("Credit Card", "CardIcon", "-20칩까지 외상 가능 (이자 발생)", 0, ItemType.Artifact));
        artifactItems.Add(new Item("Glitch USB", "USBIcon", "스트레이트도 글리치 판정 인정", 6, ItemType.Artifact));
        artifactItems.Add(new Item("Order Emblem", "OrderIcon", "스트레이트 달성 시 배율 +7.0 추가", 7, ItemType.Artifact));
        artifactItems.Add(new Item("Ancient Battery", "BatteryIcon", "유물 효과가 발동될 때마다 +50점", 7, ItemType.Artifact));
        artifactItems.Add(new Item("Skill Scanner", "ScannerIcon", "특수 주사위 1개당 +30점", 6, ItemType.Artifact));
    }

    void InitializeDiceItems()
    {
        diceItems.Clear();
        diceItems.Add(new Item("Time Dice", "TimeIcon", "보유 라운드만큼 배율 증가", 10, ItemType.Dice));
        diceItems.Add(new Item("Ice Dice", "IceIcon", "타일 색에 따라 점수 증감", 8, ItemType.Dice));
        diceItems.Add(new Item("Rubber Dice", "RubberIcon", "버프/디버프 절반 적용", 7, ItemType.Dice));
        diceItems.Add(new Item("Buff Dice", "BuffIcon", "범위 내 버프 효과 3배", 15, ItemType.Dice));
        diceItems.Add(new Item("Comeback Dice", "ComebackIcon", "목숨이 적을수록 배율 증가", 10, ItemType.Dice));
        diceItems.Add(new Item("Spring Dice", "SpringIcon", "안쪽 버프2배 / 바깥 너프2배", 12, ItemType.Dice));
        diceItems.Add(new Item("Mirror Dice", "MirrorIcon", "가장 높은 등급 효과 복사", 18, ItemType.Dice));
        diceItems.Add(new Item("Reflect Dice", "ReflectIcon", "버프↔디버프 반전", 14, ItemType.Dice));
        diceItems.Add(new Item("Steel Dice", "SteelIcon", "디버프 면역", 13, ItemType.Dice));
        diceItems.Add(new Item("Chameleon Dice", "ChameleonIcon", "가장 높은 눈금 복사", 14, ItemType.Dice));
        diceItems.Add(new Item("Splash Dice", "SplashIcon", "범위 내 룰렛 효과", 11, ItemType.Dice));
        diceItems.Add(new Item("Absorb Dice", "AbsorbIcon", "주변 버프 흡수", 16, ItemType.Dice));
        diceItems.Add(new Item("Ancient Dice", "AncientIcon", "일정 라운드 후 진화", 20, ItemType.Dice));
    }

    public void ResetShop()
    {
        foreach (var item in artifactItems) item.isSold = false;
        GenerateWeeklyItems();
        GenerateDicePack();
        if (refreshButton != null) refreshButton.interactable = true;
    }

    void GenerateWeeklyItems()
    {
        weeklyItems.Clear();
        List<Item> shopPool = artifactItems.Where(item => !GameData.Instance.HasItem(item.itemName)).ToList();

        int countToSpawn = Mathf.Min(3, shopPool.Count);

        HashSet<int> selectedIndices = new HashSet<int>();
        while (selectedIndices.Count < countToSpawn)
        {
            selectedIndices.Add(Random.Range(0, shopPool.Count));
        }

        foreach (int index in selectedIndices)
        {
            weeklyItems.Add(shopPool[index]);
        }

        DisplayWeeklyItems();
    }

    void GenerateDicePack()
    {
        if (dicePackContainer == null) return;
        foreach (Transform child in dicePackContainer) Destroy(child.gameObject);

        Item packItem = new Item("Random Dice Pack", "PackIcon", "구매 시 3개의 주사위 중 하나를 선택합니다.", 5, ItemType.Consumable);

        GameObject packUI = CreateDefaultShopCard(packItem);

        RectTransform rt = packUI.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 250);

        packUI.transform.SetParent(dicePackContainer, false);
    }

    void DisplayWeeklyItems()
    {
        foreach (Transform child in weeklyItemsContainer) Destroy(child.gameObject);
        foreach (Item item in weeklyItems)
        {
            GameObject itemUI = CreateDefaultShopCard(item);
            itemUI.transform.SetParent(weeklyItemsContainer, false);
        }
        if (refreshCostText != null) refreshCostText.text = "새로고침 [2 C]";
    }

    public void ShowDiceSelectionUI()
    {
        if (selectionPanel == null) return;
        selectionPanel.SetActive(true);

        foreach (Transform child in selectionContainer) Destroy(child.gameObject);

        List<Item> availableDice = new List<Item>(diceItems);

        availableDice = availableDice.Where(d => !GameData.Instance.ownedSpecialDice.Contains(d.itemName)).ToList();

        if (availableDice.Count == 0)
        {
            if (selectionTitle != null) selectionTitle.text = "모든 주사위를 획득했습니다!";
            return;
        }

        if (selectionTitle != null) selectionTitle.text = "주사위 선택";

        int count = Mathf.Min(3, availableDice.Count);
        HashSet<int> pickedIndices = new HashSet<int>();

        while (pickedIndices.Count < count)
        {
            pickedIndices.Add(Random.Range(0, availableDice.Count));
        }

        List<Item> selectedOptions = new List<Item>();
        foreach (int index in pickedIndices)
        {
            selectedOptions.Add(availableDice[index]);
        }

        foreach (Item diceChoice in selectedOptions)
        {
            GameObject card = CreateDefaultShopCard(diceChoice);

            RectTransform rt = card.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220, 320);

            Transform priceObj = card.transform.Find("Price");
            if (priceObj != null) priceObj.GetComponent<TextMeshProUGUI>().text = "선택";

            Button btn = card.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnDiceSelected(diceChoice));

            card.transform.SetParent(selectionContainer, false);
        }
    }

    void OnDiceSelected(Item selectedDice)
    {
        bool success = GameData.Instance.AddUpgradeItem(selectedDice);

        if (success)
        {
            Debug.Log($"🎉 주사위 선택 완료: {selectedDice.itemName}");

            HideTooltip();
            selectionPanel.SetActive(false);

            if (UIManager.Instance != null) UIManager.Instance.UpdateAllUI();
        }
    }

    void OnItemClicked(Item item)
    {
        pendingPurchase = item;
        confirmPanel.SetActive(true);
        confirmText.text = $"{item.itemName}\n구매하시겠습니까?";
    }

    void ConfirmPurchase()
    {
        HideTooltip();
        if (pendingPurchase == null) return;

        int cost = (pendingPurchase.itemName == "Random Dice Pack") ? 5 : GetAdjustedCost(pendingPurchase.buyPrice);

        if (GameData.Instance.SpendChips(cost))
        {
            if (pendingPurchase.itemName == "Random Dice Pack")
            {
                Debug.Log("📦 주사위 팩 개봉!");
                ShowDiceSelectionUI();
            }
            else if (pendingPurchase.type == ItemType.Consumable)
            {
                ApplyConsumableEffect(pendingPurchase);
            }
            else
            {
                if (pendingPurchase.itemName == "Extra Heart")
                {
                    GameData.Instance.handsLeft++;
                }
                else
                {
                    bool added = GameData.Instance.AddUpgradeItem(pendingPurchase);
                    if (added && pendingPurchase.type == ItemType.Artifact)
                    {
                        pendingPurchase.isSold = true;
                    }
                    else if (!added)
                    {
                        GameData.Instance.AddChips(cost);
                    }
                }
            }

            DisplayWeeklyItems();
            if (UIManager.Instance != null) UIManager.Instance.UpdateAllUI();
        }
        confirmPanel.SetActive(false); pendingPurchase = null;
    }

    void ApplyConsumableEffect(Item item)
    {
        if (item.itemName == "Recharge Pack") GameData.Instance.rerollsLeft += 2;
        if (item.itemName == "Chaos Fund") { GameData.Instance.AddChips(20); GameData.Instance.RandomizeAllArtifacts(artifactItems); }
    }

    void CancelPurchase() { HideTooltip(); confirmPanel.SetActive(false); pendingPurchase = null; }

    public void RefreshShop()
    {
        if (GameData.Instance.SpendChips(2)) GenerateWeeklyItems();
    }

    public int GetAdjustedCost(int price)
    {
        if (GameData.Instance.HasItem("Discount Coupon")) return (int)(price * 0.8f);
        return price;
    }

    GameObject CreateDefaultShopCard(Item item)
    {
        GameObject card = new GameObject($"Card_{item.itemName}");
        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f);

        Button btn = card.AddComponent<Button>();

        bool isRebuyable = (item.itemName == "Random Dice Pack" || item.type == ItemType.Consumable || item.itemName == "Extra Heart");

        // ★ [핵심 추가] 가방이 꽉 찼는지, 이미 구매했는지 모두 검사합니다!
        bool isAlreadyOwned = (!isRebuyable && (item.isSold || GameData.Instance.HasItem(item.itemName)));
        bool isArtifactFull = (item.type == ItemType.Artifact && item.itemName != "Extra Heart" && GameData.Instance.artifactRelics.Count >= GameData.Instance.MaxInventorySize);
        bool isDiceFull = ((item.type == ItemType.Dice || item.itemName == "Random Dice Pack") && GameData.Instance.ownedSpecialDice.Count >= 5);

        // 이미 샀거나 인벤토리가 꽉 찼으면 클릭 비활성화
        if (isAlreadyOwned || isArtifactFull || isDiceFull)
        {
            btn.interactable = false;
        }
        else
        {
            btn.onClick.AddListener(() => OnItemClicked(item));
        }

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140, 200);

        ItemHoverTrigger trigger = card.AddComponent<ItemHoverTrigger>();
        trigger.targetItem = item;

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        Sprite loadedSprite = Resources.Load<Sprite>(item.itemIcon);
        if (loadedSprite != null)
        {
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = loadedSprite;
            iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 120);
        }
        else
        {
            TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
            iconTxt.text = item.itemName.Substring(0, 1);
            iconTxt.fontSize = 60; iconTxt.alignment = TextAlignmentOptions.Center;
            iconTxt.color = Color.black;
        }
        iconObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI name = nameObj.AddComponent<TextMeshProUGUI>();
        name.text = item.itemName;
        name.fontSize = 20; name.color = Color.black;
        name.alignment = TextAlignmentOptions.Center;
        name.enableWordWrapping = true;
        name.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 110);

        GameObject priceObj = new GameObject("Price");
        priceObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI price = priceObj.AddComponent<TextMeshProUGUI>();
        price.text = $"{GetAdjustedCost(item.buyPrice)} C";
        price.fontSize = 24; price.color = Color.blue;
        price.alignment = TextAlignmentOptions.Center;
        price.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -110);

        // ★ [수정] 샀으면 "SOLD", 꽉 찼으면 "FULL"이라고 표시합니다.
        if (isAlreadyOwned || isArtifactFull || isDiceFull)
        {
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(card.transform, false);
            TextMeshProUGUI statusTxt = statusObj.AddComponent<TextMeshProUGUI>();

            if (isArtifactFull || isDiceFull)
                statusTxt.text = "FULL";
            else
                statusTxt.text = "SOLD";

            statusTxt.color = Color.red;
            statusTxt.fontSize = 40;
            statusTxt.alignment = TextAlignmentOptions.Center;
        }

        return card;
    }

    public void ShowTooltip(Item item, Vector3 pos)
    {
        tooltipPanel.SetActive(true);
        tooltipName.text = item.itemName;
        tooltipDesc.text = item.description;
        tooltipPrice.text = $"{GetAdjustedCost(item.buyPrice)} C";
    }
    public void HideTooltip() { tooltipPanel.SetActive(false); }
}