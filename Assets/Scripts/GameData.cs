using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

    [Header("플레이어 자원")]
    public int debt = 500;
    public int wallet = 0;
    public int chips = 10;
    public int handsLeft = 3;
    public int maxHands = 3;
    public int rerollsLeft = 3;
    public int baseRerolls = 3;
    public int shopRerollCost = 2;

    [Header("게임 상태")]
    public int currentHandScore = 0;
    public int totalScore = 0;
    public float feverMultiplier = 1f;
    public int savedPot = 0;
    public bool isFirstRoll = true;
    public string currentHandName = "";

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

    [Header("보유 주사위 목록")]
    public List<DiceData> currentDice = new List<DiceData>();
    public List<int> availableDiceValues = new List<int> { 1, 2, 3, 4, 5, 6 };

    // ============================================
    // ★ [추가] 주사위 주머니(Deck) 시스템
    // ============================================
    [Header("나의 주사위 주머니")]
    public List<string> ownedSpecialDice = new List<string>(); // 상점에서 산 주사위들
    public List<string> currentDrawPile = new List<string>();  // 현재 턴에 뽑을 수 있는 주사위들

    // 주사위 섞기
    public void ShuffleDeck(int dicePerRoll)
    {
        currentDrawPile.Clear();
        currentDrawPile.AddRange(ownedSpecialDice);

        // 최소 굴리는 개수(5개)만큼은 보장되도록 빈자리를 '일반(Normal)'으로 채움
        while (currentDrawPile.Count < dicePerRoll)
        {
            currentDrawPile.Add("Normal");
        }

        // 무작위로 섞기
        currentDrawPile = currentDrawPile.OrderBy(x => Random.value).ToList();
        Debug.Log("🃏 주사위 주머니를 섞었습니다!");
    }

    // 주머니에서 하나 꺼내기
    public string DrawDiceFromDeck(int dicePerRoll)
    {
        // 리롤을 많이 해서 주머니가 비었으면 다시 섞음
        if (currentDrawPile.Count == 0)
        {
            ShuffleDeck(dicePerRoll);
        }

        string drawnDice = currentDrawPile[0];
        currentDrawPile.RemoveAt(0); // 뽑은 건 주머니에서 뺌
        return drawnDice;
    }
    // ============================================

    [Header("유물(아티팩트) 슬롯")]
    public List<Item> inventory = new List<Item>();
    public List<Item> artifactRelics = new List<Item>();
    public int maxArtifacts = 8;
    public List<Item> randomBuffs = new List<Item>();

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

    public bool isRolling = false;
    public bool canSubmit = false;
    public bool isProcessingTurn = false;
    public bool isStageRewardBlocked = false;

    public void ResetGame()
    {
        debt = 500; wallet = 0; chips = 10;
        handsLeft = 3; maxHands = 3;
        rerollsLeft = 3; baseRerolls = 3;
        currentHandScore = 0; totalScore = 0;
        feverMultiplier = 1f; shopRerollCost = 2;
        savedPot = 0; currentHandName = "";
        pandoraMultiplier = 1.0f; lockedInventoryCount = 0;
        hasCreditCard = false; bonusTileIndices.Clear();
        currentChaosEffectName = "";

        currentDice.Clear();
        inventory.Clear();
        artifactRelics.Clear();
        randomBuffs.Clear();

        // ★ 주머니 초기화
        ownedSpecialDice.Clear();
        currentDrawPile.Clear();

        isRolling = false; canSubmit = false; isProcessingTurn = false;

        var shop = FindObjectOfType<ShopManager>();
        if (shop != null) shop.ResetShop();
    }

    public void StartNewTurn()
    {
        if (handsLeft <= 0) return;

        if (hasCreditCard && chips < 0) chips -= Random.Range(1, 5);

        int currentMaxRerolls = baseRerolls;
        if (artifactRelics.Exists(i => i.itemName == "Overload Gear")) currentMaxRerolls += 2;
        int shackleCount = artifactRelics.FindAll(i => i.itemName == "Heavy Shackle").Count;
        currentMaxRerolls = Mathf.Max(0, currentMaxRerolls - shackleCount);

        rerollsLeft = artifactRelics.Exists(i => i.itemName == "Time Capsule") ? rerollsLeft + currentMaxRerolls : currentMaxRerolls;

        if (artifactRelics.Exists(i => i.itemName == "Pandora's Box"))
        {
            pandoraMultiplier = Random.Range(0.5f, 3.0f);
            lockedInventoryCount = Random.Range(1, 4);
        }
        else
        {
            pandoraMultiplier = 1.0f; lockedInventoryCount = 0;
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
            currentChaosEffectName = chaosEffectPool[Random.Range(0, chaosEffectPool.Count)];
        else
            currentChaosEffectName = "";

        currentHandScore = 0;
        currentDice.Clear();
        isRolling = true; canSubmit = false; isProcessingTurn = false; isFirstRoll = true;
    }

    public void AddScore(int score)
    {
        long tempHand = (long)currentHandScore + score;
        currentHandScore = (tempHand > int.MaxValue) ? int.MaxValue : (int)tempHand;
        long tempTotal = (long)totalScore + score;
        totalScore = (tempTotal > int.MaxValue) ? int.MaxValue : (int)tempTotal;
    }

    public void AddMoney(int amount) { wallet += amount; }
    public bool SpendChips(int amount)
    {
        int limit = hasCreditCard ? -20 : 0;
        if (chips - amount >= limit) { chips -= amount; return true; }
        return false;
    }
    public void AddChips(int amount) { chips += amount; }

    public bool HasItem(string searchName) => artifactRelics.Exists(x => x.itemName == searchName);

    // ★ [핵심 변경] 구매 시 수량 제한 및 오류 로그 강화
    public bool AddUpgradeItem(Item item)
    {
        if (item.type == ItemType.Dice)
        {
            // 주사위는 최대 5개까지만 보유 가능
            if (ownedSpecialDice.Count >= 5)
            {
                Debug.LogWarning("🎲 주사위 주머니가 꽉 찼습니다! (최대 5개)");
                return false;
            }

            ownedSpecialDice.Add(item.itemName);
            Debug.Log($"🎲 [GameData] 주머니에 특수 주사위 추가됨! : {item.itemName} (현재: {ownedSpecialDice.Count}/5)");
            return true;
        }

        if (item.type == ItemType.Artifact)
        {
            if (item.itemName != "Extra Heart" && HasItem(item.itemName)) return false;

            // 유물은 MaxInventorySize (기본 8개)까지만 보유 가능
            if (artifactRelics.Count >= MaxInventorySize)
            {
                Debug.LogWarning("🎒 유물 인벤토리가 꽉 찼습니다! (최대 8개)");
                return false;
            }

            artifactRelics.Add(item);
            if (item.itemName == "Overload Gear") { maxHands--; if (handsLeft > maxHands) handsLeft = maxHands; }
            if (item.itemName == "Credit Card") hasCreditCard = true;
            Debug.Log($"🎒 [GameData] 유물 획득! : {item.itemName} (현재: {artifactRelics.Count}/{MaxInventorySize})");
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
            DiceData existingDice = currentDice.Find(d => d.slotIndex == newSlotIndex && d != dice);
            if (existingDice != null) existingDice.slotIndex = dice.slotIndex;
            dice.slotIndex = newSlotIndex;
        }
    }
}

public enum DiceEffectState { None, Buff, Nerf }

[System.Serializable]
public class DiceData
{
    public int slotIndex;
    public int value;
    public bool isSelected;
    public string diceType;
    public int roundsHeld;
    public DiceEffectState effectState = DiceEffectState.None;

    public int finalScore;
    public int bonusScore;
    public float finalMult;
    public float externalBuffMult;
    public float externalNerfMult;
    public bool isImmuneToNerf;
    public int totalScoreCalculated;

    // ============================================
    // ★ [새로 추가된 부분] 비주얼 연출을 위한 상태 변수들
    // ============================================
    public bool isBuffed;
    public bool isNerfed;
    public string effectPopupText;

    public DiceData(int index, int val, string type = "Normal")
    {
        slotIndex = index;
        value = val;
        isSelected = false;
        diceType = type;
        roundsHeld = 0;
        effectState = DiceEffectState.None;
        finalScore = val;
        finalMult = 1.0f;
        bonusScore = 0;
        externalBuffMult = 1.0f;
        externalNerfMult = 1.0f;
        isImmuneToNerf = (type == "Steel Dice");
        totalScoreCalculated = 0;

        // ★ [추가] 생성 시 초기화
        isBuffed = false;
        isNerfed = false;
        effectPopupText = "";
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
        itemName = name; itemIcon = icon; description = desc;
        buyPrice = buy; sellPrice = buy / 2; type = itemType; isSold = false;
    }
}

public enum ItemType { Dice, Multiplier, Special, RandomBuff, Artifact, Consumable }