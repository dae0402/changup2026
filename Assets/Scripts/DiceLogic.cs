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

    // ★ [수정됨] 유니티 경고(new MonoBehaviour 금지)를 해결하기 위해 static readonly로 변경
    private static readonly Dictionary<HandType, float> handMultipliers = new Dictionary<HandType, float>
    {
        { HandType.HighCard, 1.0f }, { HandType.OnePair, 2.0f }, { HandType.TwoPair, 3.0f },
        { HandType.ThreeOfAKind, 4.0f }, { HandType.Straight, 6.0f }, { HandType.FullHouse, 8.0f },
        { HandType.FourOfAKind, 12.0f }, { HandType.FiveOfAKind, 20.0f }
    };

    private static readonly Dictionary<HandType, string> handNames = new Dictionary<HandType, string>
    {
        { HandType.HighCard, "하이 카드" }, { HandType.OnePair, "원 페어" }, { HandType.TwoPair, "투 페어" },
        { HandType.ThreeOfAKind, "트리플" }, { HandType.Straight, "스트레이트" },
        { HandType.FullHouse, "풀 하우스" }, { HandType.FourOfAKind, "포 카드" },
        { HandType.FiveOfAKind, "파이브 오브 어 카인드" }
    };

    public static HandResult AnalyzeDice(List<DiceData> dice)
    {
        if (dice == null || dice.Count == 0) return new HandResult { handName = "없음", multiplier = 0, totalScore = 0, isGlitch = false };

        // 1. 주사위 개별 이펙트 적용 (버프/너프 등)
        DiceEffectManager.ApplyAllDiceEffects();

        // 2. 글로벌 아티팩트(유물) 효과 먼저 적용
        ApplyGlobalArtifacts(dice);

        // 3. 족보 판별 (★ 원래 눈금이 아니라, 카멜레온 등으로 변신한 눈금 기준으로 검사!)
        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);

        // static 딕셔너리로 안전하게 접근
        string currentHandName = handNames[hand];
        float handMult = handMultipliers[hand];

        bool isGlitch = false;

        // 글리치 발동 조건 (트리플 이상)
        if (hand == HandType.ThreeOfAKind ||
            hand == HandType.FullHouse ||
            hand == HandType.FourOfAKind ||
            hand == HandType.FiveOfAKind)
        {
            isGlitch = true;
            Debug.Log($"👾 [{currentHandName}] 같은 주사위 3개 이상! -> 글리치 발동!");
        }

        // 아이템 체크
        List<Item> activeItems = (GameData.Instance != null) ? GameData.Instance.GetAllActiveUpgrades() : new List<Item>();

        if (hand == HandType.Straight)
        {
            if (activeItems.Exists(i => i.itemName == "Glitch USB"))
            {
                currentHandName = "글리치 스트레이트";
                isGlitch = true;
            }
            if (activeItems.Exists(i => i.itemName == "Order Emblem"))
            {
                handMult += 7.0f;
            }
        }

        // ★ [추가됨] 상점에 있는 배율 증가 유물 로직 연동
        if (activeItems.Exists(i => i.itemName == "Artifact Collector"))
        {
            handMult += activeItems.Count * 0.5f; // 보유 유물 수만큼 배율 0.5씩 추가
        }
        if (activeItems.Exists(i => i.itemName == "Dice Collector"))
        {
            handMult += dice.Count * 0.2f; // 사용 주사위 수만큼 배율 추가
        }

        // 4. 점수 합산 로직
        long sumOfDice = 0;
        foreach (var die in dice)
        {
            sumOfDice += die.totalScoreCalculated;
        }

        if (activeItems.Exists(i => i.itemName == "Skill Scanner"))
        {
            int specialCount = dice.Count(d => d.diceType != "Normal");
            if (specialCount > 0) sumOfDice += specialCount * 30;
        }

        return new HandResult { handName = currentHandName, multiplier = handMult, totalScore = (int)sumOfDice, isGlitch = isGlitch };
    }

    private static void ApplyGlobalArtifacts(List<DiceData> diceList)
    {
        if (GameData.Instance == null) return;
        List<Item> items = GameData.Instance.GetAllActiveUpgrades();
        int activatedCount = 0;

        foreach (var item in items)
        {
            if (CheckAndApplyArtifact(item.itemName, diceList)) activatedCount++;
        }

        if (items.Exists(i => i.itemName == "Ancient Battery") && activatedCount > 0)
        {
            int bonus = activatedCount * 50;
            foreach (var d in diceList) d.totalScoreCalculated += (bonus / diceList.Count);
        }
    }

    private static bool CheckAndApplyArtifact(string itemName, List<DiceData> diceList)
    {
        bool triggered = false;

        // ★ [수정됨] 점수가 뻥튀기된 totalScore가 아니라, 순수한 눈금의 합(finalScore)을 기준으로 21, 24를 검사합니다!
        long faceSum = diceList.Sum(d => d.finalScore);

        switch (itemName)
        {
            case "Underdog's Hope":
                if (faceSum <= 24)
                {
                    foreach (var d in diceList) d.totalScoreCalculated *= 3;
                    triggered = true;
                    Debug.Log("🛡️ Underdog's Hope 발동! (합 24 이하)");
                }
                break;
            case "Blackjack":
                if (faceSum == 21)
                {
                    foreach (var d in diceList) d.totalScoreCalculated *= 20;
                    triggered = true;
                    Debug.Log("🃏 Blackjack 발동! (합 21)");
                }
                break;
        }
        return triggered;
    }

    private static Dictionary<int, int> CountDiceValues(List<DiceData> dice)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (var d in dice)
        {
            // ★ [수정됨] d.value(초기 눈금)가 아니라, 이펙트로 변한 눈금(finalScore)을 기준으로 족보를 판별합니다.
            int effectiveValue = d.finalScore;
            if (counts.ContainsKey(effectiveValue)) counts[effectiveValue]++;
            else counts[effectiveValue] = 1;
        }
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

        // ★ [수정됨] 스트레이트 검사 시에도 변신한 눈금(finalScore)을 사용합니다.
        var values = dice.Select(d => d.finalScore).Distinct().OrderBy(v => v).ToList();

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