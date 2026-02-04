using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class DiceLogic : MonoBehaviour
{
    // ... (상단 변수 및 Enum 정의는 기존과 동일) ...
    private const int GRID_WIDTH = 5;

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
        { HandType.HighCard, "노 페어" }, { HandType.OnePair, "원 페어" }, { HandType.TwoPair, "투 페어" },
        { HandType.ThreeOfAKind, "쓰리 카드" }, { HandType.Straight, "스트레이트" },
        { HandType.FullHouse, "풀 하우스" }, { HandType.FourOfAKind, "포 카드" },
        { HandType.FiveOfAKind, "파이브 카드" }
    };

    public (HandType type, string name, int currentScore, bool isGlitch) AnalyzeDice(List<DiceData> dice)
    {
        if (dice == null || dice.Count == 0) return (HandType.HighCard, "없음", 0, false);

        Debug.Log("🎲 [계산 시작] 주사위 분석 중...");

        // 1. 초기화
        foreach (var die in dice)
        {
            die.finalScore = die.value;
            die.bonusScore = 0;
            die.finalMult = 1.0f;
        }

        // 2. 특수 타일 효과 (로그 추가)
        ApplyGridBonus(dice);

        // 3. 유물 효과 적용 (로그 추가)
        ApplyNewArtifacts(dice);

        // 4. 족보 판별
        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);

        // 5. 글리치 판정
        bool hasGlitchUSB = GameData.Instance != null && GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Glitch USB");
        if (hasGlitchUSB && hand == HandType.Straight) Debug.Log("👾 [Glitch USB] 스트레이트가 글리치로 판정됨!");

        bool isGlitch = (hand >= HandType.ThreeOfAKind) || (hasGlitchUSB && hand == HandType.Straight);

        // 6. 최종 점수 계산
        long sumOfDice = 0;
        foreach (var die in dice)
        {
            float diceValue = Mathf.Max(0, die.value + die.bonusScore);
            float diceTotal = diceValue * die.finalMult;
            sumOfDice += (long)diceTotal;
        }

        float handMult = handMultipliers[hand];

        // 최종 로그 출력
        Debug.Log($"📊 [결과] 족보: {handNames[hand]} (x{handMult}) | 최종 주사위 점수 합: {sumOfDice}");

        double finalCalc = sumOfDice * handMult;
        int finalScore = (finalCalc > int.MaxValue) ? int.MaxValue : (int)finalCalc;

        return (hand, handNames[hand], finalScore, isGlitch);
    }

    private void ApplyNewArtifacts(List<DiceData> diceList)
    {
        if (GameData.Instance == null) return;
        List<Item> items = GameData.Instance.GetAllActiveUpgrades();

        Debug.Log($"🎒 [유물 적용] 보유 아이템 수: {items.Count}개");

        foreach (var item in items)
        {
            ApplySingleArtifactEffect(item.itemName, diceList, items);
        }
    }

    private void ApplySingleArtifactEffect(string itemName, List<DiceData> diceList, List<Item> inventory)
    {
        int sumOfValues = diceList.Sum(d => d.value);

        switch (itemName)
        {
            case "Mirror of Rank":
                var targetItem = inventory
                    .Where(i => i.itemName != "Mirror of Rank")
                    .OrderByDescending(i => i.buyPrice)
                    .FirstOrDefault();

                if (targetItem != null)
                {
                    Debug.Log($"🪞 [Mirror of Rank] '{targetItem.itemName}' 효과를 복사합니다!");
                    ApplySingleArtifactEffect(targetItem.itemName, diceList, inventory);
                }
                else
                {
                    Debug.Log("🪞 [Mirror of Rank] 복사할 다른 아이템이 없습니다.");
                }
                break;

            case "Chaos Orb":
                string randomEffect = GameData.Instance.currentChaosEffectName;
                if (!string.IsNullOrEmpty(randomEffect))
                {
                    Debug.Log($"🌀 [Chaos Orb] 현재 효과 '{randomEffect}' 발동!");
                    ApplySingleArtifactEffect(randomEffect, diceList, inventory);
                }
                break;

            case "Heavy Shackle":
                Debug.Log("🔗 [Heavy Shackle] 점수 2배 적용 (배율 +1.0)");
                foreach (var d in diceList) d.finalMult += 1.0f;
                break;

            case "Underdog's Hope":
                if (sumOfValues <= 24)
                {
                    Debug.Log($"🐶 [Underdog's Hope] 합 {sumOfValues} (<=24) 달성! 점수 3배 적용 (배율 +2.0)");
                    foreach (var d in diceList) d.finalMult += 2.0f;
                }
                break;

            case "Devil's Contract":
                Debug.Log("👿 [Devil's Contract] 점수 5배 적용 (배율 +4.0)");
                foreach (var d in diceList) d.finalMult += 4.0f;
                break;

            case "Blackjack":
                if (sumOfValues == 21)
                {
                    Debug.Log("🃏 [Blackjack] 잭팟! 합 21 달성! 점수 20배 적용 (배율 +19.0)");
                    foreach (var d in diceList) d.finalMult += 19.0f;
                }
                else
                {
                    // (너무 자주 뜨면 시끄러우니 주석 처리 가능)
                    // Debug.Log($"🃏 [Blackjack] 합 {sumOfValues} (조건 불만족)");
                }
                break;
        }
    }

    private void ApplyGridBonus(List<DiceData> dice)
    {
        if (GameData.Instance == null) return;

        bool paintActive = false;
        for (int i = 0; i < dice.Count; i++)
        {
            if (GameData.Instance.bonusTileIndices.Contains(dice[i].slotIndex))
            {
                dice[i].bonusScore += 2;
                paintActive = true;
            }
        }
        if (paintActive) Debug.Log("🎨 [Magic Paint] 보너스 타일 위 주사위에 +2점 적용됨");
    }

    // ... (GameManager 호환용 더미 함수 및 헬퍼 함수들은 기존과 동일하게 유지) ...
    public float CheckPositionBonus(List<DiceData> dice) { return 1.0f; }
    public float CheckSpecialDiceBonus(List<DiceData> dice) { return 1.0f; }
    public int CalculateFinalGlitchScore(int storedScore, int currentHandScore, int comboCount) => Mathf.RoundToInt((storedScore + currentHandScore) * Mathf.Pow(2, comboCount));
    private Vector2Int GetPos(int idx) => new Vector2Int(idx % GRID_WIDTH, idx / GRID_WIDTH);
    private Dictionary<int, int> CountDiceValues(List<DiceData> dice)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (var d in dice) { if (counts.ContainsKey(d.value)) counts[d.value]++; else counts[d.value] = 1; }
        return counts;
    }
    private HandType DetermineHandType(List<DiceData> dice, Dictionary<int, int> counts)
    {
        if (counts.ContainsValue(5)) return HandType.FiveOfAKind;
        if (counts.ContainsValue(4)) return HandType.FourOfAKind;
        if (counts.ContainsValue(3) && counts.ContainsValue(2)) return HandType.FullHouse;
        if (IsStraight(dice)) return HandType.Straight;
        if (counts.ContainsValue(3)) return HandType.ThreeOfAKind;
        if (counts.Values.Count(c => c == 2) >= 2) return HandType.TwoPair;
        if (counts.Values.Count(c => c == 2) == 1) return HandType.OnePair;
        return HandType.HighCard;
    }
    private bool IsStraight(List<DiceData> dice)
    {
        if (dice.Count < 5) return false;
        var values = dice.Select(d => d.value).Distinct().OrderBy(v => v).ToList();
        if (values.Count < 5) return false;
        return values.SequenceEqual(new List<int> { 1, 2, 3, 4, 5 }) || values.SequenceEqual(new List<int> { 2, 3, 4, 5, 6 });
    }
    public void OnRoundEnd() { }
}