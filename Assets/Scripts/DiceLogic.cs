using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class DiceLogic : MonoBehaviour
{
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

        // 2. 특수 타일 효과
        ApplyGridBonus(dice);

        // 3. 유물 효과 적용 (신규 아이템 로직 포함)
        ApplyNewArtifacts(dice);

        // 4. 족보 판별
        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);

        // 5. 스트레이트 & 글리치 로직
        List<Item> activeItems = (GameData.Instance != null) ? GameData.Instance.GetAllActiveUpgrades() : new List<Item>();
        float handMult = handMultipliers[hand];
        bool isGlitch = (hand >= HandType.ThreeOfAKind);

        if (hand == HandType.Straight)
        {
            if (activeItems.Exists(i => i.itemName == "Glitch USB"))
            {
                isGlitch = true;
                Debug.Log("👾 [Glitch USB] 스트레이트 -> 글리치 발동!");
            }
            if (activeItems.Exists(i => i.itemName == "Order Emblem"))
            {
                handMult += 7.0f;
                Debug.Log($"🛡️ [Order Emblem] 스트레이트 배율 +7.0 추가! (총 x{handMult})");
            }
        }

        // 6. 최종 점수 계산
        long sumOfDice = 0;
        foreach (var die in dice)
        {
            float diceValue = Mathf.Max(0, die.value + die.bonusScore);
            float diceTotal = diceValue * die.finalMult;
            sumOfDice += (long)diceTotal;
        }

        // ★ [신규 아이템 2] Skill Scanner (특수 주사위 추가 점수)
        // Normal이 아닌 주사위 갯수만큼 점수 추가
        if (activeItems.Exists(i => i.itemName == "Skill Scanner"))
        {
            int specialDiceCount = dice.Count(d => d.diceType != "Normal");
            if (specialDiceCount > 0)
            {
                long bonus = specialDiceCount * 30; // 개당 30점
                sumOfDice += bonus;
                Debug.Log($"📡 [Skill Scanner] 특수 주사위 {specialDiceCount}개 감지 -> +{bonus}점");
            }
        }

        Debug.Log($"📊 [결과] 족보: {handNames[hand]} (x{handMult}) | 최종 주사위 점수 합: {sumOfDice}");

        double finalCalc = sumOfDice * handMult;
        int finalScore = (finalCalc > int.MaxValue) ? int.MaxValue : (int)finalCalc;

        return (hand, handNames[hand], finalScore, isGlitch);
    }

    private void ApplyNewArtifacts(List<DiceData> diceList)
    {
        if (GameData.Instance == null) return;
        List<Item> items = GameData.Instance.GetAllActiveUpgrades();

        int activatedArtifactCount = 0; // ★ 발동된 유물 횟수 카운트

        // 개별 아이템 적용 및 발동 여부 체크
        foreach (var item in items)
        {
            // ApplySingleArtifactEffect가 이제 bool(발동 성공 여부)을 반환합니다.
            bool triggered = ApplySingleArtifactEffect(item.itemName, diceList, items);
            if (triggered) activatedArtifactCount++;
        }

        // 일괄 적용 아이템들
        if (items.Exists(i => i.itemName == "Pandora's Box"))
        {
            float bonus = GameData.Instance.pandoraMultiplier - 1.0f;
            foreach (var d in diceList) d.finalMult += bonus;
            activatedArtifactCount++; // 판도라는 항상 발동
        }

        if (items.Exists(i => i.itemName == "Artifact Collector"))
        {
            float bonus = items.Count * 0.5f;
            foreach (var d in diceList) d.finalMult += bonus;
            activatedArtifactCount++; // 상시 발동
        }

        if (items.Exists(i => i.itemName == "Dice Collector"))
        {
            float bonus = diceList.Count * 0.5f;
            foreach (var d in diceList) d.finalMult += bonus;
            activatedArtifactCount++; // 상시 발동
        }

        // ★ [신규 아이템 1] Ancient Battery (유물 발동 시 추가 점수)
        if (items.Exists(i => i.itemName == "Ancient Battery"))
        {
            if (activatedArtifactCount > 0)
            {
                // 배터리 자기 자신을 뺀 횟수 (선택 사항, 여기선 포함함)
                float totalBonus = activatedArtifactCount * 50;
                foreach (var d in diceList) d.bonusScore += (int)(totalBonus / diceList.Count); // 점수를 주사위에 분배
                Debug.Log($"🔋 [Ancient Battery] 유물 {activatedArtifactCount}회 발동 -> 총 +{totalBonus}점 추가");
            }
        }
    }

    // ★ [중요] 반환 타입을 void -> bool로 변경 (발동했으면 true 리턴)
    private bool ApplySingleArtifactEffect(string itemName, List<DiceData> diceList, List<Item> inventory)
    {
        int sumOfValues = diceList.Sum(d => d.value);
        bool isTriggered = false;

        switch (itemName)
        {
            case "Mirror of Rank":
                var targetItem = inventory.Where(i => i.itemName != "Mirror of Rank").OrderByDescending(i => i.buyPrice).FirstOrDefault();
                if (targetItem != null)
                {
                    Debug.Log($"🪞 [Mirror of Rank] '{targetItem.itemName}' 효과 복사!");
                    // 복사된 효과가 발동했는지 체크
                    if (ApplySingleArtifactEffect(targetItem.itemName, diceList, inventory))
                    {
                        isTriggered = true;
                    }
                }
                break;

            case "Chaos Orb":
                string randomEffect = GameData.Instance.currentChaosEffectName;
                if (!string.IsNullOrEmpty(randomEffect))
                {
                    Debug.Log($"🌀 [Chaos Orb] 현재 효과 '{randomEffect}' 발동!");
                    if (ApplySingleArtifactEffect(randomEffect, diceList, inventory))
                    {
                        isTriggered = true;
                    }
                }
                break;

            case "Heavy Shackle":
                // 상시 적용이므로 true
                Debug.Log("🔗 [Heavy Shackle] 점수 2배");
                foreach (var d in diceList) d.finalMult += 1.0f;
                isTriggered = true;
                break;

            case "Underdog's Hope":
                if (sumOfValues <= 24)
                {
                    Debug.Log("🐶 [Underdog's Hope] 합 <= 24 달성! (배율 +2.0)");
                    foreach (var d in diceList) d.finalMult += 2.0f;
                    isTriggered = true; // ★ 조건 만족 시에만 true
                }
                break;

            case "Devil's Contract":
                Debug.Log("👿 [Devil's Contract] 점수 5배");
                foreach (var d in diceList) d.finalMult += 4.0f;
                isTriggered = true;
                break;

            case "Blackjack":
                if (sumOfValues == 21)
                {
                    Debug.Log("🃏 [Blackjack] 잭팟! 합 21! (배율 +19.0)");
                    foreach (var d in diceList) d.finalMult += 19.0f;
                    isTriggered = true; // ★ 조건 만족 시에만 true
                }
                break;

                // Glitch USB, Order Emblem 등은 AnalyzeDice에서 처리하므로 여기선 false
        }

        return isTriggered;
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
        if (paintActive) Debug.Log("🎨 [Magic Paint] 보너스 타일 적용 (+2점)");
    }

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