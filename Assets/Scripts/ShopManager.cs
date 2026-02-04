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
    // ★ 아이템 데이터 (신규 10종만 유지)
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

    // ★ [핵심] 신규 아이템 10종 등록
    void InitializeShopItems()
    {
        artifactItems.Clear();

        // 1. Discount Coupon (할인 쿠폰)
        artifactItems.Add(new Item("Discount Coupon", "CouponIcon", "상점 가격 20% 할인", 5, ItemType.Artifact));

        // 2. Mirror of Rank (계급의 거울) - 구현 완료
        artifactItems.Add(new Item("Mirror of Rank", "MirrorIcon", "가장 비싼 아이템 효과 복사", 8, ItemType.Artifact));

        // 3. Magic Paint (마법 페인트)
        artifactItems.Add(new Item("Magic Paint", "PaintIcon", "매 라운드 랜덤 2칸에 +2점 타일 생성", 4, ItemType.Artifact));

        // 4. Chaos Orb (카오스 오브) - 구현 완료
        artifactItems.Add(new Item("Chaos Orb", "OrbIcon", "매 라운드 효과가 랜덤 변경", 6, ItemType.Artifact));

        // 5. Heavy Shackle (무거운 족쇄)
        artifactItems.Add(new Item("Heavy Shackle", "ShackleIcon", "점수 x2배 / 리롤 횟수 -1회", 5, ItemType.Artifact));

        // 6. Underdog's Hope (언더독의 희망)
        artifactItems.Add(new Item("Underdog's Hope", "HopeIcon", "주사위 합 24 이하일 때 점수 x3배", 4, ItemType.Artifact));

        // 7. Devil's Contract (악마의 계약)
        artifactItems.Add(new Item("Devil's Contract", "ContractIcon", "점수 x5배 / 인벤토리 2칸 잠금", 10, ItemType.Artifact));

        // 8. Blackjack (블랙잭)
        artifactItems.Add(new Item("Blackjack", "BlackjackIcon", "주사위 합 21일 때 점수 x20배", 7, ItemType.Artifact));

        // 9. Glitch USB (글리치 USB)
        artifactItems.Add(new Item("Glitch USB", "USBIcon", "스트레이트도 글리치로 인정", 6, ItemType.Artifact));

        // 10. Extra Heart (추가 심장) - 소모성(즉시 적용)
        artifactItems.Add(new Item("Extra Heart", "HeartIcon", "획득 시 목숨 +1 (즉시 적용)", 5, ItemType.Artifact)); // 타입은 Artifact지만 획득 시 처리 다름
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

        // 중복 방지 (이미 가진 유물은 등장하지 않음)
        // 단, Extra Heart는 여러 번 나올 수 있도록 예외 처리 가능 (여기선 중복 안 됨으로 설정)
        HashSet<string> ownedItems = new HashSet<string>();
        if (GameData.Instance != null)
        {
            foreach (var item in GameData.Instance.GetAllActiveUpgrades())
            {
                ownedItems.Add(item.itemName);
            }
        }

        // 상점에 진열할 아이템 필터링
        foreach (var item in artifactItems)
        {
            // 팔리지 않았고 && 내가 안 가진 것만 추가 (Extra Heart는 소모품처럼 계속 나오게 하려면 isSold 체크를 풀어야 함)
            if (!item.isSold && !ownedItems.Contains(item.itemName))
            {
                shopPool.Add(item);
            }
            // Extra Heart 예외 처리: 팔려도 다시 나오게 하려면
            else if (item.itemName == "Extra Heart")
            {
                shopPool.Add(item);
            }
        }

        // 품절 처리
        if (shopPool.Count == 0)
        {
            if (refreshButton != null) refreshButton.interactable = false;
            if (refreshCostText != null) refreshCostText.text = "품절";
            foreach (Transform child in weeklyItemsContainer) Destroy(child.gameObject);
            return;
        }

        if (refreshButton != null) refreshButton.interactable = true;

        // 셔플 및 선택 (최대 3개)
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

        // 리롤 비용 표시 (할인 쿠폰 적용 여부와 관계없이 리롤 비용은 별도 관리)
        int cost = (GameData.Instance != null) ? GameData.Instance.shopRerollCost : 2;
        if (refreshCostText != null) refreshCostText.text = $"🔄 새로고침 [{cost} C]";
    }

    // ★ [신규] 할인 쿠폰 적용 가격 계산
    public int GetAdjustedCost(int originalCost)
    {
        if (GameData.Instance != null && GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Discount Coupon"))
        {
            return Mathf.Max(1, (int)(originalCost * 0.8f)); // 20% 할인
        }
        return originalCost;
    }

    GameObject CreateDefaultShopCard(Item item)
    {
        GameObject card = new GameObject($"ShopCard_{item.itemName}");
        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f);

        Button btn = card.AddComponent<Button>();

        // Extra Heart는 계속 살 수 있으므로 isSold 체크 안함
        if (item.itemName != "Extra Heart" && item.isSold) btn.interactable = false;
        else btn.onClick.AddListener(() => OnItemClicked(item));

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 350);

        // 툴팁 트리거
        ItemHoverTrigger trigger = card.AddComponent<ItemHoverTrigger>();
        trigger.targetItem = item;

        // 아이콘
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        Sprite loadedSprite = Resources.Load<Sprite>(item.itemIcon);

        if (loadedSprite != null)
        {
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = loadedSprite;
            iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 160);
            if (item.isSold && item.itemName != "Extra Heart") iconImg.color = new Color(1, 1, 1, 0.5f);
        }
        else
        {
            TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
            iconTxt.text = item.itemName.Substring(0, 1); // 임시 아이콘 (첫 글자)
            iconTxt.fontSize = 80;
            iconTxt.alignment = TextAlignmentOptions.Center;
        }
        iconObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

        // 이름
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI name = nameObj.AddComponent<TextMeshProUGUI>();
        name.text = item.itemName;
        name.fontSize = 24; // 글자 크기 조정
        name.fontStyle = FontStyles.Bold;
        name.color = Color.black;
        name.alignment = TextAlignmentOptions.Center;
        name.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 130);
        name.enableWordWrapping = true; // 긴 이름 줄바꿈

        // 가격 (할인 적용된 가격 표시)
        GameObject priceObj = new GameObject("Price");
        priceObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI price = priceObj.AddComponent<TextMeshProUGUI>();

        int finalPrice = GetAdjustedCost(item.buyPrice);
        price.text = $"{finalPrice} C";

        // 할인 중이면 색상 변경
        if (finalPrice < item.buyPrice) price.color = new Color(0f, 0.6f, 0f); // 초록색
        else price.color = new Color(0f, 0.2f, 0.8f); // 파란색

        price.fontSize = 32;
        price.alignment = TextAlignmentOptions.Center;
        price.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -110);

        // 품절 텍스트
        if (item.isSold && item.itemName != "Extra Heart")
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

    // ============================================
    // 구매 로직
    // ============================================
    void OnItemClicked(Item item)
    {
        if (item.isSold && item.itemName != "Extra Heart") return;

        // 인벤토리 잠금 확인 (Devil's Contract 등으로 슬롯 부족 시)
        if (GameData.Instance != null && GameData.Instance.artifactRelics.Count >= GameData.Instance.MaxInventorySize && item.itemName != "Extra Heart")
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

        // 할인 적용된 가격으로 결제 시도
        int finalCost = GetAdjustedCost(pendingPurchase.buyPrice);

        if (GameData.Instance.SpendChips(finalCost))
        {
            // ★ [신규] Extra Heart는 즉시 적용 (인벤토리에 안 넣음)
            if (pendingPurchase.itemName == "Extra Heart")
            {
                GameData.Instance.handsLeft++; // 목숨 증가
                Debug.Log("❤️ 목숨 +1 획득!");
            }
            else
            {
                // 일반 아이템은 인벤토리에 추가
                GameData.Instance.AddUpgradeItem(pendingPurchase);
                pendingPurchase.isSold = true;
            }

            GenerateWeeklyItems(); // 목록 갱신
            if (UIManager.Instance != null) UIManager.Instance.UpdateAllUI();

            Debug.Log($"구매 완료: {pendingPurchase.itemName}");
        }
        else
        {
            Debug.Log("칩이 부족합니다!");
        }
        confirmPanel.SetActive(false); pendingPurchase = null;
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

    // 새로고침 함수
    public void RefreshShop()
    {
        if (GameData.Instance.SpendChips(GameData.Instance.shopRerollCost))
            GenerateWeeklyItems();
    }
}