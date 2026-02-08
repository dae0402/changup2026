using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 게임의 모든 데이터를 저장하는 클래스 (Singleton)
/// 20종 아이템 로직 + GameManager 호환성 완벽 적용
/// </summary>
public class GameData : MonoBehaviour
{
    private static GameData _instance;
    public static GameData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameData>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("GameData");
                    _instance = obj.AddComponent<GameData>();
                }
            }
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
        DontDestroyOnLoad(gameObject);
    }

    // ============================================
    // 플레이어 자원
    // ============================================
    [Header("플레이어 자원")]
    public int debt = 500;
    public int wallet = 0;
    public int chips = 10;

    public int handsLeft = 3;    // 현재 목숨
    public int maxHands = 3;     // 최대 목숨 (Overload Gear로 감소 가능)

    public int rerollsLeft = 3;
    public int baseRerolls = 3;  // 기본 리롤 횟수

    [Header("상점 설정")]
    public int shopRerollCost = 2;

    // ============================================
    // 게임 상태 변수
    // ============================================
    [Header("게임 상태")]
    public int currentHandScore = 0;
    public int totalScore = 0;
    public float feverMultiplier = 1f;

    // ★ [복구] GameManager / UIManager 호환용 변수
    public int savedPot = 0;
    public bool isFirstRoll = true;

    // ============================================
    // ★ [핵심] 특수 효과 상태 변수
    // ============================================
    [Header("★ 특수 효과 상태")]
    public HashSet<int> bonusTileIndices = new HashSet<int>(); // [Magic Paint] 타일 위치
    public string currentChaosEffectName = "";                 // [Chaos Orb] 현재 효과
    public float pandoraMultiplier = 1.0f;                     // [Pandora's Box] 배율
    public int lockedInventoryCount = 0;                       // [Pandora's Box] 잠금 칸 수
    public bool hasCreditCard = false;                         // [Credit Card] 보유 여부

    // [Chaos Orb]가 선택할 수 있는 효과 풀
    private List<string> chaosEffectPool = new List<string>
    {
        "Heavy Shackle", "Underdog's Hope", "Devil's Contract", "Blackjack", "Glitch USB"
    };

    // ============================================
    // 주사위 및 인벤토리 데이터
    // ============================================
    [Header("주사위 데이터")]
    public List<DiceData> currentDice = new List<DiceData>();

    // ★ [복구] GameManager 호환용 리스트
    public List<int> availableDiceValues = new List<int> { 1, 2, 3, 4, 5, 6 };

    [Header("인벤토리")]
    public List<Item> inventory = new List<Item>();

    // ★ [수정됨] 악마의 계약 + 판도라의 상자 둘 다 적용
    public int MaxInventorySize
    {
        get
        {
            int baseSize = 8;
            // 1. Devil's Contract (개당 2칸 잠금)
            int contractLock = GetAllActiveUpgrades().FindAll(i => i.itemName == "Devil's Contract").Count * 2;
            // 2. Pandora's Box (랜덤 잠금)
            int totalLock = contractLock + lockedInventoryCount;

            return Mathf.Max(1, baseSize - totalLock);
        }
    }

    [Header("유물 슬롯")]
    public List<Item> artifactRelics = new List<Item>();
    public int maxArtifacts = 8;
    public List<Item> randomBuffs = new List<Item>(); // 레거시 호환용

    public bool isRolling = false;
    public bool canSubmit = false;
    public bool isProcessingTurn = false;
    public bool isStageRewardBlocked = false; // 레거시 호환용

    // ============================================
    // 핵심 메서드
    // ============================================

    public void ResetGame()
    {
        debt = 500; wallet = 0; chips = 10;
        handsLeft = 3; maxHands = 3;
        rerollsLeft = 3; baseRerolls = 3;
        currentHandScore = 0; totalScore = 0;
        feverMultiplier = 1f;
        shopRerollCost = 2;
        savedPot = 0;

        pandoraMultiplier = 1.0f;
        lockedInventoryCount = 0;
        hasCreditCard = false;
        bonusTileIndices.Clear();
        currentChaosEffectName = "";

        currentDice.Clear();
        inventory.Clear();
        artifactRelics.Clear();
        randomBuffs.Clear();

        isRolling = false; canSubmit = false; isProcessingTurn = false;

        if (ShopManager.Instance != null) ShopManager.Instance.ResetShop();
    }

    // 🔄 라운드 시작 (StartNewTurn)
    public void StartNewTurn()
    {
        if (handsLeft <= 0) { Debug.Log("💀 [Game Over]"); return; }

        // 1. [Credit Card] 이자 발생
        if (hasCreditCard && chips < 0)
        {
            int interest = Random.Range(1, 5);
            chips -= interest;
            Debug.Log($"💳 [Credit Card] 빚 이자 {interest}C 발생! 현재 자금: {chips}C");
        }

        // 2. 리롤 횟수 계산
        int currentMaxRerolls = baseRerolls;

        // [Overload Gear] 효과 (+2)
        if (artifactRelics.Exists(i => i.itemName == "Overload Gear"))
        {
            currentMaxRerolls += 2;
        }

        // ★ [수정됨] [Heavy Shackle] 효과 (-1)
        int shackleCount = artifactRelics.FindAll(i => i.itemName == "Heavy Shackle").Count;
        currentMaxRerolls = Mathf.Max(0, currentMaxRerolls - shackleCount);

        // 3. [Time Capsule] 리롤 이월
        if (artifactRelics.Exists(i => i.itemName == "Time Capsule"))
        {
            rerollsLeft += currentMaxRerolls;
            Debug.Log($"⏳ [Time Capsule] 리롤 이월됨! 현재: {rerollsLeft}");
        }
        else
        {
            rerollsLeft = currentMaxRerolls;
        }

        // 4. [Pandora's Box] 랜덤 효과
        if (artifactRelics.Exists(i => i.itemName == "Pandora's Box"))
        {
            pandoraMultiplier = Random.Range(0.5f, 3.0f);
            lockedInventoryCount = Random.Range(1, 4);
            Debug.Log($"📦 [Pandora] 배율: x{pandoraMultiplier:F1}, 잠금: {lockedInventoryCount}칸");
        }
        else
        {
            pandoraMultiplier = 1.0f;
            lockedInventoryCount = 0;
        }

        // 5. [Magic Paint] 타일 설정
        bonusTileIndices.Clear();
        if (artifactRelics.Exists(i => i.itemName == "Magic Paint"))
        {
            List<int> available = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = 0; i < 2; i++)
            {
                int r = Random.Range(0, available.Count);
                bonusTileIndices.Add(available[r]);
                available.RemoveAt(r);
            }
            Debug.Log($"🎨 [Magic Paint] 보너스 타일: {string.Join(", ", bonusTileIndices)}");
        }

        // 6. [Chaos Orb] 효과 설정
        if (artifactRelics.Exists(i => i.itemName == "Chaos Orb"))
        {
            int r = Random.Range(0, chaosEffectPool.Count);
            currentChaosEffectName = chaosEffectPool[r];
            Debug.Log($"🌀 [Chaos Orb] 이번 라운드 효과: {currentChaosEffectName}");
        }
        else
        {
            currentChaosEffectName = "";
        }

        currentHandScore = 0;
        currentDice.Clear();
        isRolling = true;
        canSubmit = false;
        isProcessingTurn = false;
        isFirstRoll = true;
    }

    public void AddScore(int score)
    {
        long tempHand = (long)currentHandScore + score;
        currentHandScore = (tempHand > int.MaxValue) ? int.MaxValue : (int)tempHand;

        long tempTotal = (long)totalScore + score;
        totalScore = (tempTotal > int.MaxValue) ? int.MaxValue : (int)tempTotal;
    }

    // ★ [복구] GameManager 호환용 함수
    public void AddMoney(int amount)
    {
        wallet += amount;
        if (wallet >= debt) Debug.Log("🎉 빚 상환 완료!");
    }

    // 🎲 리롤 로직 (Lucky Coin)
    public bool TryReroll()
    {
        if (isProcessingTurn) return false;

        int cost = 1;
        bool isFree = false;

        if (artifactRelics.Exists(i => i.itemName == "Lucky Coin"))
        {
            if (Random.value < 0.1f)
            {
                isFree = true;
                Debug.Log("🍀 [Lucky Coin] 행운 발동! 리롤 소모 없음.");
            }
        }

        if (rerollsLeft >= cost || isFree)
        {
            if (!isFree) rerollsLeft -= cost;

            isProcessingTurn = true;
            Invoke("UnlockTurn", 0.5f);
            return true;
        }
        return false;
    }

    void UnlockTurn() => isProcessingTurn = false;

    // 💰 칩 사용 로직
    public bool SpendChips(int amount)
    {
        int limit = hasCreditCard ? -20 : 0;
        if (chips - amount >= limit)
        {
            chips -= amount;
            return true;
        }
        return false;
    }

    public void AddChips(int amount)
    {
        chips += amount;
    }

    public bool AddItemToInventory(Item item)
    {
        if (inventory.Count >= MaxInventorySize) return false;
        inventory.Add(item);
        return true;
    }

    public void RemoveItemFromInventory(int index)
    {
        if (index >= 0 && index < inventory.Count)
        {
            inventory.RemoveAt(index);
        }
    }

    public bool AddUpgradeItem(Item item)
    {
        if (item.type == ItemType.Artifact)
        {
            if (item.itemName != "Extra Heart" && artifactRelics.Exists(x => x.itemName == item.itemName)) return false;

            // 인벤토리 잠금 체크 (여기서 MaxInventorySize 속성을 사용하므로 로직 적용됨)
            if (artifactRelics.Count >= MaxInventorySize)
            {
                Debug.Log("🔒 인벤토리가 잠겨있거나 가득 차서 구매 불가");
                return false;
            }

            artifactRelics.Add(item);

            if (item.itemName == "Overload Gear")
            {
                maxHands--;
                if (handsLeft > maxHands) handsLeft = maxHands;
                Debug.Log("⚙️ [Overload Gear] 최대 목숨 -1");
            }

            if (item.itemName == "Credit Card") hasCreditCard = true;

            return true;
        }
        return false;
    }

    public List<Item> GetAllActiveUpgrades()
    {
        List<Item> allItems = new List<Item>();
        allItems.AddRange(artifactRelics);
        allItems.AddRange(randomBuffs);
        return allItems;
    }

    public void RandomizeAllArtifacts(List<Item> shopDatabase)
    {
        if (artifactRelics.Count == 0) return;

        Debug.Log("🌪️ [Chaos Fund] 모든 유물이 랜덤하게 변경됩니다!");
        for (int i = 0; i < artifactRelics.Count; i++)
        {
            Item randomItem = shopDatabase[Random.Range(0, shopDatabase.Count)];
            artifactRelics[i] = new Item(randomItem.itemName, randomItem.itemIcon, randomItem.description, randomItem.buyPrice, ItemType.Artifact);
        }
    }

    public void ExpandInventory(int amount) { /* 더미 */ }
}

[System.Serializable]
public class DiceData
{
    public int slotIndex;
    public int value;
    public bool isSelected;
    public string diceType;
    public int roundsHeld;

    public int finalScore;
    public float finalMult;
    public int bonusScore;

    public DiceData(int index, int val, string type = "Normal")
    {
        slotIndex = index;
        value = val;
        isSelected = false;
        diceType = type;
        roundsHeld = 0;
        finalScore = val;
        finalMult = 1.0f;
        bonusScore = 0;
    }
}

[System.Serializable]
public class Item
{
    public string itemName;
    public string itemIcon;
    public string description;

    public int buyPrice;
    public int cost => buyPrice;

    public int sellPrice;
    public ItemType type;
    public bool isSold = false;

    public Item(string name, string icon, string desc, int buy, ItemType itemType)
    {
        itemName = name;
        itemIcon = icon;
        description = desc;
        buyPrice = buy;
        sellPrice = buy / 2;
        type = itemType;
        isSold = false;
    }
}

public enum ItemType
{
    Dice, Multiplier, Special, RandomBuff, Artifact, Consumable
}