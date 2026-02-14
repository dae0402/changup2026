using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DiceLogic : MonoBehaviour
{
    public struct HandResult
    {
        public string handName;
        public float multiplier;
        public int totalScore;
        public bool isGlitch;
    }

    public enum HandType
    {
        HighCard, OnePair, TwoPair, ThreeOfAKind, Straight, FullHouse, FourOfAKind, FiveOfAKind
    }

    private Dictionary<HandType, float> handMultipliers = new Dictionary<HandType, float>
    {
        { HandType.HighCard, 1.0f }, { HandType.OnePair, 2.0f }, { HandType.TwoPair, 3.0f },
        { HandType.ThreeOfAKind, 4.0f }, { HandType.Straight, 6.0f }, { HandType.FullHouse, 8.0f },
        { HandType.FourOfAKind, 12.0f }, { HandType.FiveOfAKind, 20.0f }
    };

    private Dictionary<HandType, string> handNames = new Dictionary<HandType, string>
    {
        { HandType.HighCard, "하이 카드" }, { HandType.OnePair, "원 페어" }, { HandType.TwoPair, "투 페어" },
        { HandType.ThreeOfAKind, "트리플" }, { HandType.Straight, "스트레이트" },
        { HandType.FullHouse, "풀 하우스" }, { HandType.FourOfAKind, "포 카드" },
        { HandType.FiveOfAKind, "파이브 오브 어 카인드" }
    };

    public static HandResult AnalyzeDice(List<DiceData> dice)
    {
        if (dice == null || dice.Count == 0) return new HandResult { handName = "없음", multiplier = 0, totalScore = 0, isGlitch = false };

        DiceEffectManager.ApplyAllDiceEffects();
        ApplyGlobalArtifacts(dice);

        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);
        string handName = new DiceLogic().handNames[hand];
        float handMult = new DiceLogic().handMultipliers[hand];

        // ★★★ [여기 수정됨] 글리치 발동 조건 확장! ★★★
        bool isGlitch = false;

        // 1. 같은 주사위가 3개 이상 들어간 족보면 무조건 글리치!
        if (hand == HandType.ThreeOfAKind || // 트리플
            hand == HandType.FullHouse ||    // 풀 하우스 (3개+2개)
            hand == HandType.FourOfAKind ||  // 포 카드
            hand == HandType.FiveOfAKind)    // 파이브 카드
        {
            isGlitch = true;
            Debug.Log($"👾 [{handName}] 같은 주사위 3개 이상! -> 글리치 발동!");
        }

        // 2. 아이템 체크 (기존 유지)
        List<Item> activeItems = (GameData.Instance != null) ? GameData.Instance.GetAllActiveUpgrades() : new List<Item>();

        if (hand == HandType.Straight)
        {
            if (activeItems.Exists(i => i.itemName == "Glitch USB"))
            {
                handName = "글리치 스트레이트";
                isGlitch = true;
            }
            if (activeItems.Exists(i => i.itemName == "Order Emblem"))
            {
                handMult += 7.0f;
            }
        }

        // 점수 합산 로직 (기존 동일)
        long sumOfDice = 0;
        foreach (var die in dice) sumOfDice += die.totalScoreCalculated;

        if (activeItems.Exists(i => i.itemName == "Skill Scanner"))
        {
            int specialCount = dice.Count(d => d.diceType != "Normal");
            if (specialCount > 0) sumOfDice += specialCount * 30;
        }

        return new HandResult { handName = handName, multiplier = handMult, totalScore = (int)sumOfDice, isGlitch = isGlitch };
    }

    // --- 아래는 기존과 동일한 헬퍼 함수들 ---
    private static void ApplyGlobalArtifacts(List<DiceData> diceList)
    {
        if (GameData.Instance == null) return;
        List<Item> items = GameData.Instance.GetAllActiveUpgrades();
        int activatedCount = 0;
        foreach (var item in items) if (CheckAndApplyArtifact(item.itemName, diceList)) activatedCount++;
        if (items.Exists(i => i.itemName == "Ancient Battery") && activatedCount > 0)
        {
            int bonus = activatedCount * 50;
            foreach (var d in diceList) d.totalScoreCalculated += (bonus / diceList.Count);
        }
    }

    private static bool CheckAndApplyArtifact(string itemName, List<DiceData> diceList)
    {
        bool triggered = false;
        long sum = diceList.Sum(d => d.totalScoreCalculated);
        switch (itemName)
        {
            case "Underdog's Hope":
                if (sum <= 24) { foreach (var d in diceList) d.totalScoreCalculated *= 3; triggered = true; }
                break;
            case "Blackjack":
                if (sum == 21) { foreach (var d in diceList) d.totalScoreCalculated *= 20; triggered = true; }
                break;
        }
        return triggered;
    }

    private static Dictionary<int, int> CountDiceValues(List<DiceData> dice)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (var d in dice) { if (counts.ContainsKey(d.value)) counts[d.value]++; else counts[d.value] = 1; }
        return counts;
    }

    private static HandType DetermineHandType(List<DiceData> dice, Dictionary<int, int> counts)
    {
        if (counts.ContainsValue(5)) return HandType.FiveOfAKind;
        if (counts.ContainsValue(4)) return HandType.FourOfAKind;
        if (counts.ContainsValue(3) && counts.ContainsValue(2)) return HandType.FullHouse;
        if (IsStraight(dice)) return HandType.Straight;
        if (counts.ContainsValue(3)) return HandType.ThreeOfAKind;
        if (counts.Values.Count(c => c == 2) >= 2) return HandType.TwoPair;
        if (counts.ContainsValue(2)) return HandType.OnePair;
        return HandType.HighCard;
    }

    private static bool IsStraight(List<DiceData> dice)
    {
        if (dice.Count < 5) return false;
        var values = dice.Select(d => d.value).Distinct().OrderBy(v => v).ToList();
        int consecutive = 0;
        for (int i = 0; i < values.Count - 1; i++)
        {
            if (values[i + 1] == values[i] + 1) consecutive++;
            else consecutive = 0;
            if (consecutive >= 4) return true;
        }
        return false;
    }
}