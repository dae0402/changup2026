using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임의 모든 데이터를 저장하는 클래스
/// Singleton 패턴: 게임 전체에서 하나만 존재하며 어디서든 접근 가능
/// </summary>
public class GameData : MonoBehaviour
{
    // ============================================
    // 싱글톤(Singleton) 패턴 설정
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
        DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
    }

    // ============================================
    // 설정 상수
    // ============================================
    public const int MAX_REROLLS = 10; // 리롤 최대 보유량 제한 (Time Capsule용)

    // ============================================
    // 플레이어 자원
    // ============================================
    [Header("플레이어 자원")]
    public int debt = 500;        // 갚아야 할 빚
    public int wallet = 0;        // 상환한 금액
    public int chips = 10;        // 현재 보유 칩 (돈)
    public int handsLeft = 3;     // 남은 기회 (목숨)
    public int rerollsLeft = 3;   // 남은 리롤 횟수

    [Header("상점 설정")]
    public int shopRerollCost = 2; // 상점 리롤 비용 (바겐세일 전단지로 감소 가능, 최소 1)

    // ============================================
    // 게임 상태 변수
    // ============================================
    [Header("게임 상태")]
    public int currentHandScore = 0; // 현재 핸드 점수
    public int savedPot = 0;         // (미사용 시 제거 가능)
    public int totalScore = 0;       // 전체 누적 점수 (오버플로우 방지 로직 적용됨)
    public float feverMultiplier = 1f; // 피버 타임 배율

    // 특수 아이템 효과 플래그
    [Header("특수 효과 상태")]
    public bool isStageRewardBlocked = false; // 탐욕의 거래: 스테이지 보상 차단 여부
    public bool hasCreditCard = false;        // 블랙 카드: 외상 가능 여부

    // ★ [신규] 유물 및 주사위 효과를 위한 상태 데이터
    [Header("★ 유물 및 특수 상태")]
    public int soulCollectorStack = 0;      // 영혼 수집가: 삭제된 유물 수 스택
    public DiceLogic.HandType lastHandType; // 데자뷰: 직전 라운드 족보 기억
    public int handStreak = 0;              // 데자뷰: 동일 족보 연속 횟수
    public bool isFirstRoll = true;         // 쌍둥이의 축복: 해당 라운드 첫 리롤 여부 체크

    // ============================================
    // 주사위 및 인벤토리 데이터
    // ============================================
    [Header("주사위 데이터")]
    public List<DiceData> currentDice = new List<DiceData>(); // 현재 굴리는 주사위들
    public List<int> availableDiceValues = new List<int> { 1, 2, 3, 4, 5, 6 }; // 등장 가능한 눈금

    // 주사위 눈금별 기본 배율
    public Dictionary<int, float> diceMultipliers = new Dictionary<int, float>
    {
        {1, 1f}, {2, 1f}, {3, 1f}, {4, 1f}, {5, 1f}, {6, 1f}
    };

    [Header("인벤토리 (왼쪽 주사위 보관함)")]
    public List<Item> inventory = new List<Item>();
    public int maxInventorySize = 8;

    // ============================================
    // 강화 아이템 슬롯 (랜덤 버프 vs 상점 유물)
    // ============================================
    [Header("1. 랜덤 뽑기 능력 (좌측/상단 슬롯)")]
    public List<Item> randomBuffs = new List<Item>();
    public int maxRandomBuffs = 8;

    [Header("2. 상점 유물 (우측/하단 슬롯)")]
    public List<Item> artifactRelics = new List<Item>();
    public int maxArtifacts = 8;

    [Header("시스템 플래그")]
    public bool isRolling = false;        // 주사위가 굴러가는 중인지
    public bool canSubmit = false;        // 점수 제출 가능 여부
    public bool isProcessingTurn = false; // 광클/중복 입력 방지용 잠금

    // ============================================
    // 핵심 메서드
    // ============================================

    // 게임 재시작 (초기화)
    public void ResetGame()
    {
        // 1. 자원 초기화
        debt = 500; wallet = 0; chips = 10;
        handsLeft = 3; rerollsLeft = 3;

        // 2. 점수 및 상태 초기화
        currentHandScore = 0; savedPot = 0; totalScore = 0;
        feverMultiplier = 1f;

        // 3. 특수 플래그 초기화
        shopRerollCost = 2;
        isStageRewardBlocked = false;
        hasCreditCard = false;
        soulCollectorStack = 0;
        handStreak = 0;
        isFirstRoll = true;
        lastHandType = DiceLogic.HandType.HighCard;

        // ★ 인벤토리 및 슬롯 크기 초기화 (무한 확장 버그 방지)
        maxInventorySize = 8;
        maxRandomBuffs = 8;
        maxArtifacts = 8;

        // 4. 리스트 비우기
        currentDice.Clear();
        inventory.Clear();
        randomBuffs.Clear();
        artifactRelics.Clear();

        // 배율 초기화
        foreach (var key in new List<int>(diceMultipliers.Keys)) diceMultipliers[key] = 1f;

        isRolling = false; canSubmit = false; isProcessingTurn = false;

        // ★ 상점 데이터도 초기화 요청 (품절 상태 복구)
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ResetShop();
        }
    }

    // 새 턴(라운드) 시작
    public void StartNewTurn()
    {
        // 게임 오버 체크 (좀비 모드 방지)
        if (handsLeft <= 0)
        {
            Debug.Log("💀 [Game Over] 기회 소진.");
            return;
        }

        // Time Capsule 체크 (리롤 이월 로직: 최대치 제한 적용)
        bool hasTimeCapsule = GetAllActiveUpgrades().Exists(x => x.itemName == "Time Capsule");
        if (hasTimeCapsule) rerollsLeft = Mathf.Min(rerollsLeft + 3, MAX_REROLLS);
        else rerollsLeft = 3;

        // 변수 초기화
        currentHandScore = 0;
        currentDice.Clear();
        isRolling = true;
        canSubmit = false;
        isProcessingTurn = false;
        isFirstRoll = true; // 매 턴 첫 리롤 상태로 초기화

        // ★ [중요] 특수 주사위(시간 주사위 등)의 '보유 라운드' 증가 로직
        // 현재 인벤토리나 장착된 주사위 데이터가 있다면 여기서 roundsHeld를 올려줘야 합니다.
        // (참고: currentDice는 턴마다 비워지므로, 영구적인 주사위 관리는 inventory 리스트나 별도 덱 시스템에서 처리 필요)
    }

    // 점수 안전 합산 (21억점 오버플로우 방지)
    public void AddScore(int score)
    {
        long tempHand = (long)currentHandScore + score;
        currentHandScore = (tempHand > int.MaxValue) ? int.MaxValue : (int)tempHand;

        long tempTotal = (long)totalScore + score;
        totalScore = (tempTotal > int.MaxValue) ? int.MaxValue : (int)tempTotal;
    }

    // 리롤 시도 (광클 방지 + 럭키 코인 구현)
    public bool TryReroll()
    {
        if (isProcessingTurn) return false; // 처리 중이면 무시

        int cost = GetRerollCost();
        bool freeReroll = false;

        // Lucky Coin 로직 (10% 확률 무료)
        if (GetAllActiveUpgrades().Exists(x => x.itemName == "Lucky Coin"))
        {
            if (Random.Range(0, 100) < 10) { freeReroll = true; Debug.Log("🍀 Lucky Coin 발동! 리롤 비용 무료."); }
        }

        // 비용 지불 가능 여부 확인
        if (rerollsLeft >= cost || freeReroll)
        {
            if (!freeReroll) rerollsLeft -= cost;

            isProcessingTurn = true; // 입력 잠금
            Invoke("UnlockTurn", 0.5f); // 0.5초 쿨타임 후 해제
            return true;
        }
        return false;
    }

    // 입력 잠금 해제 헬퍼 함수
    void UnlockTurn() => isProcessingTurn = false;

    // 리롤 비용 계산 (무거운 손 아이템 체크)
    public int GetRerollCost()
    {
        foreach (var item in GetAllActiveUpgrades())
        {
            if (item.itemName == "Heavy Hand") return 2; // 패널티 적용
        }
        return 1; // 기본값
    }

    // 칩 사용 로직 (블랙 카드 외상 기능 포함)
    public bool SpendChips(int amount)
    {
        int limit = hasCreditCard ? -20 : 0; // 한도 설정
        if (chips - amount >= limit)
        {
            chips -= amount;
            return true;
        }
        return false;
    }

    // 칩 획득 (하한선 보호 포함)
    public void AddChips(int amount)
    {
        chips += amount;
        int limit = hasCreditCard ? -20 : 0;
        if (chips < limit) chips = limit; // 빚이 한도보다 더 내려가지 않도록 보호
    }

    // 빚 상환
    public void AddMoney(int amount)
    {
        wallet += amount;
        if (wallet >= debt) Debug.Log("🎉 승리! 빚을 모두 갚았습니다.");
    }

    // 인벤토리에 아이템 추가
    public bool AddItemToInventory(Item item)
    {
        if (inventory.Count >= maxInventorySize) return false;
        inventory.Add(item);
        return true;
    }

    // 인벤토리 아이템 삭제 (악마의 주사위 방어 로직 포함)
    public void RemoveItemFromInventory(int index)
    {
        if (index >= 0 && index < inventory.Count)
        {
            // 악마의 주사위: 삭제 방지 (단, 소모품 사용은 허용)
            bool hasDevilDice = GetAllActiveUpgrades().Exists(i => i.itemName == "Devil Dice");
            if (hasDevilDice && inventory[index].type != ItemType.Consumable)
            {
                Debug.Log("👿 Devil Dice: 악마의 계약으로 아이템을 버릴 수 없습니다!");
                return;
            }

            ItemType removedType = inventory[index].type;
            inventory.RemoveAt(index);

            // 영혼 수집가: 소모품이 아닌 아이템 삭제 시 스택 증가
            if (removedType != ItemType.Consumable)
            {
                OnArtifactSoldOrDestroyed();
            }
        }
    }

    // 유물 삭제/판매 시 호출되는 트리거
    public void OnArtifactSoldOrDestroyed()
    {
        if (GetAllActiveUpgrades().Exists(i => i.itemName == "Soul Collector"))
        {
            soulCollectorStack++;
            Debug.Log($"👻 [Soul Collector] 유물 삭제 감지! 공격력 증가. 현재 스택: {soulCollectorStack}");
        }
    }

    // 인벤토리 랜덤 셔플 (카오스 펀드 등 사용 시)
    public void RandomizeInventory(List<Item> fullItemPool)
    {
        if (inventory.Count == 0 || fullItemPool == null || fullItemPool.Count == 0) return;

        // 악마의 주사위가 있으면 셔플 금지
        if (GetAllActiveUpgrades().Exists(i => i.itemName == "Devil Dice"))
        {
            Debug.Log("👿 Devil Dice: 인벤토리가 고정되어 섞을 수 없습니다!");
            return;
        }

        for (int i = 0; i < inventory.Count; i++)
        {
            Item randomItem = fullItemPool[Random.Range(0, fullItemPool.Count)];
            inventory[i] = randomItem;
        }
        Debug.Log("🌪️ 인벤토리의 아이템이 모두 뒤바뀌었습니다!");
    }

    // 인벤토리 확장 (판도라 상자 등)
    public void ExpandInventory(int amount)
    {
        if (GetAllActiveUpgrades().Exists(i => i.itemName == "Devil Dice"))
        {
            Debug.Log("👿 Devil Dice: 인벤토리를 확장할 수 없습니다!");
            return;
        }
        maxInventorySize += amount;
        maxArtifacts += amount;
        maxRandomBuffs += amount;
        Debug.Log($"📦 인벤토리가 {amount}칸 확장되었습니다.");
    }

    // 강화 아이템(유물/버프) 획득 함수 (중복 방지 포함)
    public bool AddUpgradeItem(Item item)
    {
        if (item.type == ItemType.Artifact)
        {
            // 중복 체크: 이미 가진 유물은 획득 불가
            if (GetAllActiveUpgrades().Exists(x => x.itemName == item.itemName))
            {
                Debug.LogWarning($"🚫 [중복 방지] 이미 {item.itemName}을 가지고 있습니다.");
                return false;
            }

            if (artifactRelics.Count >= maxArtifacts) return false;
            artifactRelics.Add(item);
            return true;
        }
        else if (item.type == ItemType.RandomBuff)
        {
            if (randomBuffs.Count >= maxRandomBuffs) return false;
            randomBuffs.Add(item);
            return true;
        }
        return false;
    }

    // 현재 활성화된 모든 업그레이드 아이템 반환 (랜덤버프 + 유물)
    public List<Item> GetAllActiveUpgrades()
    {
        List<Item> allItems = new List<Item>();
        allItems.AddRange(randomBuffs);
        allItems.AddRange(artifactRelics);
        return allItems;
    }
}

// ============================================
// DiceData 클래스 (라운드 보유 수 포함)
// ============================================
[System.Serializable]
public class DiceData
{
    public int slotIndex;    // 슬롯 위치 (0~14)
    public int value;        // 주사위 눈금
    public bool isSelected;  // 홀드(선택) 여부
    public string diceType;  // 주사위 종류 (Normal, Time, Ice 등)

    // ★ [추가] 이 주사위를 몇 라운드째 보유 중인가 (시간/고대 주사위용)
    public int roundsHeld = 0;

    public int finalScore;   // 계산된 최종 점수
    public float finalMult;  // 계산된 최종 배율
    public int bonusScore;   // 추가 점수

    public DiceData(int index, int val, string type = "Normal")
    {
        slotIndex = index;
        value = val;
        isSelected = false;
        diceType = type;
        roundsHeld = 0; // 0부터 시작
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
    public string itemName;   // 아이템 이름
    public string itemIcon;   // 아이콘 (또는 경로)
    public string description;// 설명
    public int buyPrice;      // 구매가
    public int sellPrice;     // 판매가
    public ItemType type;     // 아이템 타입
    public bool isSold = false; // 상점 품절 여부

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
// ItemType 열거형
// ============================================
public enum ItemType
{
    Dice,        // 주사위 (인벤토리용)
    Multiplier,  // (미사용)
    Special,     // (미사용)
    RandomBuff,  // 랜덤 버프 슬롯 (좌측/상단)
    Artifact,    // 상점 유물 슬롯 (우측/하단)
    Consumable   // 소모성 아이템
}