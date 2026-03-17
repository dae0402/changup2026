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

        // =======================================================
        // ★ [보스 2] 조용한 그림자: 특수 주사위 이펙트(버프/너프) 봉인
        // =======================================================
        bool skipEffects = GameData.Instance != null && GameData.Instance.isBossStage && GameData.Instance.currentBossName == "조용한 그림자 (Silent Shadow)";
        if (!skipEffects)
        {
            // 평소에는 이펙트 정상 발동
            DiceEffectManager.ApplyAllDiceEffects();
        }
        else
        {
            Debug.Log("🌑 [조용한 그림자] 보스 기믹 발동! 주사위 이펙트가 무효화됩니다.");
            // 이펙트가 무효화되므로 현재 눈금을 최종 점수로 확정지어줌
            foreach (var d in dice) d.finalScore = d.value;
        }

        // 2. 글로벌 아티팩트(유물) 효과 먼저 적용
        ApplyGlobalArtifacts(dice);

        // 3. 족보 판별 (원래 눈금이 아니라 변신한 눈금 기준)
        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);

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

        // 아이템 체크 리스트 가져오기
        List<Item> activeItems = (GameData.Instance != null) ? GameData.Instance.GetAllActiveUpgrades() : new List<Item>();

        if (hand == HandType.Straight)
        {
            if (activeItems.Exists(i => i.itemName == "Glitch USB"))
            {
                currentHandName = "글리치 스트레이트";
                isGlitch = true;
            }

            int orderCount = activeItems.Count(i => i.itemName == "Order Emblem");
            if (orderCount > 0)
            {
                handMult += (7.0f * orderCount);
            }
        }

        int artColCount = activeItems.Count(i => i.itemName == "Artifact Collector");
        if (artColCount > 0) handMult += (activeItems.Count * 0.5f * artColCount);

        int diceColCount = activeItems.Count(i => i.itemName == "Dice Collector");
        if (diceColCount > 0) handMult += (dice.Count * 0.2f * diceColCount);

        int scaleCount = activeItems.Count(i => i.itemName == "Golden Scale");
        if (scaleCount > 0)
        {
            int currentChips = GameData.Instance.chips;
            if (currentChips >= 10)
            {
                float bonusMult = (currentChips / 10) * 0.5f * scaleCount;
                handMult += bonusMult;
            }
        }

        int shackleCount = activeItems.Count(i => i.itemName == "Heavy Shackle");
        if (shackleCount > 0)
        {
            handMult *= Mathf.Pow(2.0f, shackleCount);
        }

        // =======================================================
        // ★ [보스 1] 변덕쟁이 심판: 홀/짝 통일성 판별 후 배율 조정
        // =======================================================
        if (GameData.Instance != null && GameData.Instance.isBossStage && GameData.Instance.currentBossName == "변덕쟁이 심판 (The Fickle)")
        {
            bool hasOdd = dice.Exists(d => d.finalScore % 2 != 0); // 홀수가 하나라도 있는지
            bool hasEven = dice.Exists(d => d.finalScore % 2 == 0); // 짝수가 하나라도 있는지

            if (hasOdd && !hasEven) // 모두 홀수!
            {
                handMult += 0.5f;
                currentHandName += " (홀수 통일!)";
                Debug.Log("⚖️ [변덕쟁이 심판] 모두 홀수! 배율 +0.5 보너스!");
            }
            else if (!hasOdd && hasEven) // 모두 짝수!
            {
                handMult += 0.5f;
                currentHandName += " (짝수 통일!)";
                Debug.Log("⚖️ [변덕쟁이 심판] 모두 짝수! 배율 +0.5 보너스!");
            }
            else // 섞여있음
            {
                handMult -= 0.5f;
                if (handMult < 1.0f) handMult = 1.0f; // 배율이 1 이하로 내려가진 않게 방어
                currentHandName += " (홀짝 섞임...)";
                Debug.Log("⚖️ [변덕쟁이 심판] 홀짝이 섞임! 배율 -0.5 페널티!");
            }
        }

        // 4. 점수 합산 로직
        long sumOfDice = 0;
        foreach (var die in dice)
        {
            sumOfDice += die.totalScoreCalculated;
        }

        int scannerCount = activeItems.Count(i => i.itemName == "Skill Scanner");
        if (scannerCount > 0)
        {
            int specialCount = dice.Count(d => d.diceType != "Normal");
            if (specialCount > 0) sumOfDice += (specialCount * 30 * scannerCount);
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