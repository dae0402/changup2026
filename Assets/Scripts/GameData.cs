using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 게임의 모든 데이터를 저장하는 싱글톤 클래스
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
    // 1. 플레이어 자원
    // ============================================
    [Header("플레이어 자원")]
    public int debt = 500;
    public int wallet = 0;
    public int chips = 10;

    public int handsLeft = 3;
    public int maxHands = 3;

    public int rerollsLeft = 3;
    public int baseRerolls = 3;

    [Header("상점 설정")]
    public int shopRerollCost = 2;

    // ============================================
    // 2. 게임 상태
    // ============================================
    [Header("게임 상태")]
    public int currentHandScore = 0;
    public int totalScore = 0;
    public float feverMultiplier = 1f;

    public int savedPot = 0;
    public bool isFirstRoll = true;
    public string currentHandName = ""; // ★ UI 표시용 족보 이름 추가

    // ============================================
    // 3. 특수 효과 상태
    // ============================================
    [Header("★ 특수 효과 상태")]
    public HashSet<int> bonusTileIndices = new HashSet<int>();
    public string currentChaosEffectName = "";
    public float pandoraMultiplier = 1.0f;
    public int lockedInventoryCount = 0;
    public bool hasCreditCard = false;

    private List<string> chaosEffectPool = new List<string>
    {
        "Heavy Shackle", "Underdog's Hope", "Devil's Contract", "Blackjack", "Glitch USB"
    };

    // ============================================
    // 4. 주사위 및 인벤토리 데이터
    // ============================================
    [Header("보유 주사위 목록")]
    public List<DiceData> currentDice = new List<DiceData>();

    public List<int> availableDiceValues = new List<int> { 1, 2, 3, 4, 5, 6 };

    [Header("일반 인벤토리 (사용 안 함)")]
    public List<Item> inventory = new List<Item>();

    public int MaxInventorySize
    {
        get
        {
            int baseSize = 8;
            int contractLock = GetAllActiveUpgrades().FindAll(i => i.itemName == "Devil's Contract").Count * 2;
            int totalLock = contractLock + lockedInventoryCount;
            return Mathf.Max(1, baseSize - totalLock);
        }
    }

    [Header("유물(아티팩트) 슬롯")]
    public List<Item> artifactRelics = new List<Item>();
    public int maxArtifacts = 8;
    public List<Item> randomBuffs = new List<Item>();

    public bool isRolling = false;
    public bool canSubmit = false;
    public bool isProcessingTurn = false;
    public bool isStageRewardBlocked = false;

    // ============================================
    // 핵심 기능 메서드
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
        currentHandName = "";

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

        // ShopManager 리셋 호출 (ShopManager가 있을 때만)
        var shop = FindObjectOfType<ShopManager>(); // 순환 참조 방지
        if (shop != null) shop.ResetShop();
    }

    public void StartNewTurn()
    {
        if (handsLeft <= 0) { Debug.Log("💀 [Game Over]"); return; }

        if (hasCreditCard && chips < 0)
        {
            int interest = Random.Range(1, 5);
            chips -= interest;
        }

        int currentMaxRerolls = baseRerolls;
        if (artifactRelics.Exists(i => i.itemName == "Overload Gear")) currentMaxRerolls += 2;
        int shackleCount = artifactRelics.FindAll(i => i.itemName == "Heavy Shackle").Count;
        currentMaxRerolls = Mathf.Max(0, currentMaxRerolls - shackleCount);

        if (artifactRelics.Exists(i => i.itemName == "Time Capsule"))
        {
            rerollsLeft += currentMaxRerolls;
        }
        else
        {
            rerollsLeft = currentMaxRerolls;
        }

        if (artifactRelics.Exists(i => i.itemName == "Pandora's Box"))
        {
            pandoraMultiplier = Random.Range(0.5f, 3.0f);
            lockedInventoryCount = Random.Range(1, 4);
        }
        else
        {
            pandoraMultiplier = 1.0f;
            lockedInventoryCount = 0;
        }

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
        }

        if (artifactRelics.Exists(i => i.itemName == "Chaos Orb"))
        {
            int r = Random.Range(0, chaosEffectPool.Count);
            currentChaosEffectName = chaosEffectPool[r];
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

    public void AddMoney(int amount)
    {
        wallet += amount;
        if (wallet >= debt) Debug.Log("🎉 빚 상환 완료!");
    }

    public bool TryReroll()
    {
        if (isProcessingTurn) return false;

        int cost = 1;
        bool isFree = false;

        if (artifactRelics.Exists(i => i.itemName == "Lucky Coin"))
        {
            if (Random.value < 0.1f) isFree = true;
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

    // ★ [핵심] ShopManager에서 사용하는 함수 추가
    public bool HasItem(string searchName)
    {
        // 1. 아티팩트 목록 확인
        if (artifactRelics.Exists(x => x.itemName == searchName)) return true;

        // 2. 주사위 목록 확인 (선택 사항: 같은 종류 주사위 중복 허용하려면 이 부분 제거)
        // if (currentDice.Exists(d => d.diceType == searchName)) return true;

        return false;
    }

    public bool AddUpgradeItem(Item item)
    {
        if (item.type == ItemType.Dice)
        {
            // 주사위 획득 시: 현재 보유한 주사위 개수를 slotIndex로 사용하여 추가
            // (실제 게임에서는 DiceSpawner가 빈 슬롯을 찾아 배치해 줄 것입니다)
            int newSlotIndex = -1;
            // 빈 슬롯 찾기 로직이 필요하다면 여기에 추가 (지금은 단순히 목록에만 추가)

            DiceData newDice = new DiceData(-1, Random.Range(1, 7), item.itemName);
            currentDice.Add(newDice);
            Debug.Log($"🎲 [GameData] 주사위 획득 성공! : {item.itemName}");
            return true;
        }

        if (item.type == ItemType.Artifact)
        {
            if (item.itemName != "Extra Heart" && HasItem(item.itemName)) return false;
            if (artifactRelics.Count >= MaxInventorySize)
            {
                Debug.Log("🔒 인벤토리 가득 참");
                return false;
            }
            artifactRelics.Add(item);
            if (item.itemName == "Overload Gear") { maxHands--; if (handsLeft > maxHands) handsLeft = maxHands; }
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
        for (int i = 0; i < artifactRelics.Count; i++)
        {
            Item randomItem = shopDatabase[Random.Range(0, shopDatabase.Count)];
            artifactRelics[i] = new Item(randomItem.itemName, randomItem.itemIcon, randomItem.description, randomItem.buyPrice, ItemType.Artifact);
        }
    }

    public void AddItemToInventory(Item item) { if (inventory.Count < 20) inventory.Add(item); }
    public void RemoveItemFromInventory(int index) { if (index >= 0 && index < inventory.Count) inventory.RemoveAt(index); }
    public void ExpandInventory(int amount) { }

    public void MoveDice(DiceData dice, int newSlotIndex)
    {
        if (dice != null)
        {
            // 기존 위치에 있던 주사위 찾기 (교환 로직)
            DiceData existingDice = currentDice.Find(d => d.slotIndex == newSlotIndex && d != dice);

            if (existingDice != null)
            {
                // 자리 바꾸기
                existingDice.slotIndex = dice.slotIndex;
            }

            dice.slotIndex = newSlotIndex;
            Debug.Log($"🎲 주사위 이동 완료: {dice.diceType} -> 슬롯 {newSlotIndex}");
        }
    }

} // End of GameData class


// ============================================
// 데이터 클래스 (기존 유지)
// ============================================
[System.Serializable]
public class DiceData
{
    public int slotIndex;
    public int value;
    public bool isSelected;
    public string diceType;
    public int roundsHeld;

    // 실시간 계산 변수
    public int finalScore;
    public int bonusScore;
    public float finalMult;
    public float externalBuffMult;
    public float externalNerfMult;
    public bool isImmuneToNerf;
    public int totalScoreCalculated;

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
        externalBuffMult = 1.0f;
        externalNerfMult = 1.0f;
        isImmuneToNerf = (type == "Steel Dice");
        totalScoreCalculated = 0;
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