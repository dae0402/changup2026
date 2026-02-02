using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임의 모든 데이터를 저장하는 클래스
/// Singleton 패턴: 게임 전체에서 하나만 존재
/// </summary>
public class GameData : MonoBehaviour
{
    // ============================================
    // Singleton 패턴
    // ============================================
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

    // 상점 리롤 비용 (기본값 2)
    [Header("상점 설정")]
    public int shopRerollCost = 2;

    // ============================================
    // 게임 상태
    // ============================================
    [Header("게임 상태")]
    public int currentHandScore = 0;
    public int savedPot = 0;
    public int totalScore = 0;
    public float feverMultiplier = 1f;

    // 특수 아이템 효과 플래그
    [Header("특수 효과 상태")]
    public bool isStageRewardBlocked = false; // 탐욕의 거래
    public bool hasCreditCard = false;        // 블랙 카드

    // ★ [신규] 새로운 유물 효과를 위한 변수들
    [Header("★ 신규 유물 상태 데이터")]
    public int soulCollectorStack = 0;      // 영혼 수집가: 삭제된 유물 수
    public DiceLogic.HandType lastHandType; // 데자뷰: 직전 족보 기억
    public int handStreak = 0;              // 데자뷰: 연속 횟수
    public bool isFirstRoll = true;         // 쌍둥이의 축복: 해당 라운드 첫 리롤 여부

    // ============================================
    // 주사위 및 인벤토리 (기본)
    // ============================================
    [Header("주사위")]
    public List<DiceData> currentDice = new List<DiceData>();
    public List<int> availableDiceValues = new List<int> { 1, 2, 3, 4, 5, 6 };

    public Dictionary<int, float> diceMultipliers = new Dictionary<int, float>
    {
        {1, 1f}, {2, 1f}, {3, 1f}, {4, 1f}, {5, 1f}, {6, 1f}
    };

    [Header("인벤토리 (왼쪽 주사위 보관함)")]
    public List<Item> inventory = new List<Item>();
    public int maxInventorySize = 8;

    // ============================================
    // 강화 능력 분리 (랜덤 vs 유물)
    // ============================================

    [Header("1. 랜덤 뽑기 능력 (좌측/상단 슬롯)")]
    public List<Item> randomBuffs = new List<Item>();
    public int maxRandomBuffs = 8;

    [Header("2. 상점 유물 (우측/하단 슬롯)")]
    public List<Item> artifactRelics = new List<Item>();
    public int maxArtifacts = 8;

    [Header("플래그")]
    public bool isRolling = false;
    public bool canSubmit = false;

    // ============================================
    // 메서드들
    // ============================================
    public void ResetGame()
    {
        debt = 500;
        wallet = 0;
        chips = 10;
        handsLeft = 3;
        rerollsLeft = 3;
        currentHandScore = 0;
        savedPot = 0;
        totalScore = 0;
        feverMultiplier = 1f;

        // 상점 비용 및 특수 효과 초기화
        shopRerollCost = 2;
        isStageRewardBlocked = false;
        hasCreditCard = false;

        // ★ 신규 변수 초기화
        soulCollectorStack = 0;
        handStreak = 0;
        isFirstRoll = true;
        // lastHandType은 Enum 기본값(0)으로 자동 초기화됨

        currentDice.Clear();
        inventory.Clear();

        // 두 리스트 모두 초기화
        randomBuffs.Clear();
        artifactRelics.Clear();

        foreach (var key in new List<int>(diceMultipliers.Keys)) diceMultipliers[key] = 1f;

        isRolling = false;
        canSubmit = false;
    }

    public void StartNewTurn()
    {
        rerollsLeft = 3;
        currentHandScore = 0;
        totalScore = 0;
        currentDice.Clear();
        isRolling = true;
        canSubmit = false;

        // ★ 라운드 시작 시 초기화
        isFirstRoll = true; // 쌍둥이의 축복을 위해 true로 설정
        // handStreak = 0;  // 데자뷰 연속 횟수를 라운드마다 초기화할지 여부는 기획에 따라 결정 (여기선 유지)
    }

    // 칩 사용 로직 (외상 기능 포함)
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

    public void AddChips(int amount) => chips += amount;

    public void AddMoney(int amount)
    {
        wallet += amount;
        if (wallet >= debt) Debug.Log("🎉 승리!");
    }

    public bool AddItemToInventory(Item item)
    {
        if (inventory.Count >= maxInventorySize) return false;
        inventory.Add(item);
        return true;
    }

    // ★ [수정됨] 아이템 삭제/판매 시 호출 (영혼 수집가 체크)
    public void RemoveItemFromInventory(int index)
    {
        if (index >= 0 && index < inventory.Count)
        {
            inventory.RemoveAt(index);
            // 일반 주사위 삭제도 포함할지, 유물만 포함할지는 기획에 따름. 여기선 일단 호출.
            OnArtifactSoldOrDestroyed();
        }
    }

    // ★ [신규] 유물 판매/삭제 시 스택 증가 함수
    public void OnArtifactSoldOrDestroyed()
    {
        bool hasSoulCollector = false;
        foreach (var item in GetAllActiveUpgrades())
        {
            if (item.itemName == "Soul Collector")
            {
                hasSoulCollector = true;
                break;
            }
        }

        if (hasSoulCollector)
        {
            soulCollectorStack++;
            Debug.Log($"👻 [Soul Collector] 유물 삭제 감지! 현재 스택: {soulCollectorStack}");
        }
    }

    // ★ [신규] 리롤 비용 계산 함수 (무거운 손 체크)
    public int GetRerollCost()
    {
        foreach (var item in GetAllActiveUpgrades())
        {
            if (item.itemName == "Heavy Hand") return 2; // 무거운 손 있으면 2 소모
        }
        return 1; // 기본 1 소모
    }

    // 인벤토리 랜덤 셔플
    public void RandomizeInventory(List<Item> fullItemPool)
    {
        if (inventory.Count == 0 || fullItemPool == null || fullItemPool.Count == 0) return;

        for (int i = 0; i < inventory.Count; i++)
        {
            Item randomItem = fullItemPool[Random.Range(0, fullItemPool.Count)];
            inventory[i] = randomItem;
        }
        Debug.Log("🌪️ 인벤토리의 아이템이 모두 뒤바뀌었습니다!");
    }

    // 강화 아이템 추가 함수
    public bool AddUpgradeItem(Item item)
    {
        if (item.type == ItemType.RandomBuff)
        {
            if (randomBuffs.Count >= maxRandomBuffs) return false;
            randomBuffs.Add(item);
            return true;
        }
        else if (item.type == ItemType.Artifact)
        {
            if (artifactRelics.Count >= maxArtifacts) return false;
            artifactRelics.Add(item);
            return true;
        }
        return false;
    }

    // 모든 활성 효과 아이템 반환
    public List<Item> GetAllActiveUpgrades()
    {
        List<Item> allItems = new List<Item>();
        allItems.AddRange(randomBuffs);
        allItems.AddRange(artifactRelics);
        return allItems;
    }
}

// ============================================
// DiceData
// ============================================
[System.Serializable]
public class DiceData
{
    public int slotIndex;
    public int value;
    public bool isSelected;
    public string diceType;
    public int finalScore;
    public float finalMult;
    public int bonusScore;

    public DiceData(int index, int val, string type = "Normal")
    {
        slotIndex = index;
        value = val;
        isSelected = false;
        diceType = type;
        finalScore = val;
        finalMult = 1.0f;
        bonusScore = 0;
    }
}

// ============================================
// Item 클래스
// ============================================
[System.Serializable]
public class Item
{
    public string itemName;
    public string itemIcon;
    public string description;
    public int buyPrice;
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

// ============================================
// ItemType
// ============================================
public enum ItemType
{
    Dice,        // 주사위
    Multiplier,  // (미사용)
    Special,     // (미사용)
    RandomBuff,  // 랜덤 버프 슬롯
    Artifact,    // 상점 유물 슬롯
    Consumable   // 소모성
}