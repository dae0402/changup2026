using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임의 모든 데이터를 저장하는 클래스 (Singleton)
/// 10종 아이템 로직 + GameManager 호환성 완벽 적용
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
    public int handsLeft = 3;
    public int rerollsLeft = 3;

    [Header("상점 설정")]
    public int shopRerollCost = 2;

    // ============================================
    // 게임 상태 변수
    // ============================================
    [Header("게임 상태")]
    public int currentHandScore = 0;
    public int totalScore = 0;
    public float feverMultiplier = 1f;

    // ★ [복구] GameManager / UIManager 호환용 변수 (에러 해결)
    public int savedPot = 0;
    public bool isFirstRoll = true;

    // ★ [신규] 특수 효과 상태 (마법 페인트, 카오스 오브)
    [Header("★ 특수 효과 상태")]
    public HashSet<int> bonusTileIndices = new HashSet<int>();
    public string currentChaosEffectName = "";

    private List<string> chaosEffectPool = new List<string>
    {
        "Heavy Shackle", "Underdog's Hope", "Devil's Contract", "Blackjack", "Glitch USB"
    };

    // ============================================
    // 주사위 및 인벤토리 데이터
    // ============================================
    [Header("주사위 데이터")]
    public List<DiceData> currentDice = new List<DiceData>();

    // ★ [복구] GameManager 호환용 리스트 (에러 해결)
    public List<int> availableDiceValues = new List<int> { 1, 2, 3, 4, 5, 6 };

    [Header("인벤토리")]
    public List<Item> inventory = new List<Item>();

    // ★ [신규] 인벤토리 크기 (악마의 계약 아이템 적용)
    public int MaxInventorySize
    {
        get
        {
            int baseSize = 8;
            // 'Devil's Contract' 1개당 2칸 잠금
            int lockedCount = GetAllActiveUpgrades().FindAll(i => i.itemName == "Devil's Contract").Count * 2;
            return Mathf.Max(1, baseSize - lockedCount);
        }
    }

    [Header("유물 슬롯")]
    public List<Item> artifactRelics = new List<Item>();
    public int maxArtifacts = 8;
    public List<Item> randomBuffs = new List<Item>(); // 레거시 호환용

    public bool isRolling = false;
    public bool canSubmit = false;
    public bool isProcessingTurn = false;
    public bool hasCreditCard = false; // 레거시 호환용
    public bool isStageRewardBlocked = false; // 레거시 호환용

    // ============================================
    // 핵심 메서드
    // ============================================

    public void ResetGame()
    {
        debt = 500; wallet = 0; chips = 10;
        handsLeft = 3; rerollsLeft = 3;
        currentHandScore = 0; totalScore = 0;
        feverMultiplier = 1f;
        shopRerollCost = 2;
        savedPot = 0; // 초기화

        bonusTileIndices.Clear();
        currentChaosEffectName = "";

        currentDice.Clear();
        inventory.Clear();
        artifactRelics.Clear();
        randomBuffs.Clear();

        isRolling = false; canSubmit = false; isProcessingTurn = false;

        if (ShopManager.Instance != null) ShopManager.Instance.ResetShop();
    }

    public void StartNewTurn()
    {
        if (handsLeft <= 0) { Debug.Log("💀 [Game Over]"); return; }

        // ★ [신규] 'Heavy Shackle' 보유 시 리롤 횟수 차감
        int maxRerolls = 3;
        int penalty = GetAllActiveUpgrades().FindAll(i => i.itemName == "Heavy Shackle").Count;
        rerollsLeft = Mathf.Max(0, maxRerolls - penalty);

        currentHandScore = 0;
        currentDice.Clear();
        isRolling = true;
        canSubmit = false;
        isProcessingTurn = false;
        isFirstRoll = true;

        // ★ [신규] 'Magic Paint' 타일 설정 (랜덤 2칸)
        bonusTileIndices.Clear();
        if (GetAllActiveUpgrades().Exists(i => i.itemName == "Magic Paint"))
        {
            List<int> available = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = 0; i < 2; i++)
            {
                int r = Random.Range(0, available.Count);
                bonusTileIndices.Add(available[r]);
                available.RemoveAt(r);
            }
        }

        // ★ [신규] 'Chaos Orb' 매 라운드 효과 변경
        if (GetAllActiveUpgrades().Exists(i => i.itemName == "Chaos Orb"))
        {
            int r = Random.Range(0, chaosEffectPool.Count);
            currentChaosEffectName = chaosEffectPool[r];
        }
        else
        {
            currentChaosEffectName = "";
        }
    }

    public void AddScore(int score)
    {
        long tempHand = (long)currentHandScore + score;
        currentHandScore = (tempHand > int.MaxValue) ? int.MaxValue : (int)tempHand;

        long tempTotal = (long)totalScore + score;
        totalScore = (tempTotal > int.MaxValue) ? int.MaxValue : (int)tempTotal;
    }

    // ★ [복구] GameManager 호환용 함수 (AddMoney 에러 해결)
    public void AddMoney(int amount)
    {
        wallet += amount;
        if (wallet >= debt) Debug.Log("🎉 빚 상환 완료!");
    }

    public bool TryReroll()
    {
        if (isProcessingTurn) return false;
        int cost = 1;
        if (rerollsLeft >= cost)
        {
            rerollsLeft -= cost;
            isProcessingTurn = true;
            Invoke("UnlockTurn", 0.5f);
            return true;
        }
        return false;
    }

    void UnlockTurn() => isProcessingTurn = false;

    public bool SpendChips(int amount)
    {
        if (chips >= amount)
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
            // Extra Heart는 중복 획득 가능, 나머지는 중복 불가
            if (item.itemName != "Extra Heart" && GetAllActiveUpgrades().Exists(x => x.itemName == item.itemName))
            {
                return false;
            }
            if (artifactRelics.Count >= MaxInventorySize) return false;

            artifactRelics.Add(item);
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

    public void RandomizeInventory(List<Item> fullPool) { /* 보류 */ }
    public void ExpandInventory(int amount) { /* 더미 */ }
}

[System.Serializable]
public class DiceData
{
    public int slotIndex;
    public int value;
    public bool isSelected;
    public string diceType;
    public int roundsHeld; // 호환성용

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

    // ★ [핵심 수정] DiceLogic에서 'item.cost'를 호출할 때 에러가 나지 않도록 연결
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