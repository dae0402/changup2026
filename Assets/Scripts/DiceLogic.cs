using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 주사위 족보 분석 및 점수 계산
/// 공식: [(주사위1 * 배율) + ... + (주사위5 * 배율)] * 족보배율
/// </summary>
public class DiceLogic : MonoBehaviour
{
    private const int GRID_WIDTH = 5;
    private const int GRID_HEIGHT = 3;

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

    // ============================================
    // ★ 메인 분석 함수
    // ============================================
    public (HandType type, string name, int currentScore, bool isGlitch) AnalyzeDice(List<DiceData> dice)
    {
        if (dice == null || dice.Count == 0) return (HandType.HighCard, "없음", 0, false);

        // 1. 쌍둥이의 축복
        if (GameData.Instance != null && GameData.Instance.isFirstRoll)
        {
            ApplyTwinBlessing(dice);
            GameData.Instance.isFirstRoll = false;
        }

        // 2. 초기화 (배율은 1.0 = 100% 부터 시작)
        foreach (var die in dice)
        {
            die.finalScore = die.value;
            die.bonusScore = 0;
            die.finalMult = 1.0f;
        }

        // 3. 특수 주사위 효과 (배율 합연산)
        ApplySpecialDiceEffects(dice);

        // 4. 유물 효과 (배율 합연산)
        ApplyGlobalUpgrades(dice);

        // 5. 족보 판별
        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);

        // 6. 데자뷰 체크
        CheckDejaVuEffect(hand);

        // ==========================================================
        // ★ [핵심] 요청하신 계산식 적용
        // 식: [(주사위1 * 배율) + (주사위2 * 배율) + ...] * 족보배율
        // ==========================================================

        long sumOfDice = 0;

        foreach (var die in dice)
        {
            // (1) 주사위 값 (기본값 + 보너스값)
            float diceValue = Mathf.Max(0, die.value + die.bonusScore);

            // (2) 개별 배율 적용
            float diceTotal = diceValue * die.finalMult;

            // (3) 총합에 더하기
            sumOfDice += (long)diceTotal;
        }

        // (4) 족보 배율 적용
        float handMult = handMultipliers[hand];

        // (5) 글로벌 배율(피버 등) 적용
        float globalMult = (GameData.Instance != null) ? GameData.Instance.feverMultiplier : 1.0f;

        // 최종 계산
        double finalCalc = sumOfDice * handMult * globalMult;

        // 오버플로우 방지 (21억 초과 시 최대값 고정)
        int finalScore = (finalCalc > int.MaxValue) ? int.MaxValue : (int)finalCalc;
        bool isGlitch = (hand >= HandType.ThreeOfAKind);

        return (hand, handNames[hand], finalScore, isGlitch);
    }

    // ============================================
    // ★ 유물 효과 (점수 폭발 방지를 위해 += 사용)
    // ============================================
    private void ApplyGlobalUpgrades(List<DiceData> diceList)
    {
        if (GameData.Instance == null) return;
        List<Item> activeItems = GameData.Instance.GetAllActiveUpgrades();

        foreach (var item in activeItems)
        {
            switch (item.itemName)
            {
                case "Fire Aura":
                    foreach (var d in diceList) d.bonusScore += 1;
                    break;

                case "Blackjack":
                    // x7배 -> +600% (+6.0)
                    if (diceList.Sum(d => d.value) == 21) foreach (var d in diceList) d.finalMult += 6.0f;
                    break;

                case "Devil Dice":
                    // x5배 -> +400% (+4.0)
                    foreach (var d in diceList) d.finalMult += 4.0f;
                    break;

                case "Heavy Hand":
                    // x2배 -> +100% (+1.0)
                    foreach (var d in diceList) d.finalMult += 1.0f;
                    break;

                case "Sniper Scope":
                    Dictionary<int, int> counts = CountDiceValues(diceList);
                    if (DetermineHandType(diceList, counts) == HandType.OnePair) foreach (var d in diceList) d.finalMult += 1.0f;
                    break;

                case "Heavy Weight":
                    foreach (var d in diceList) if (d.value >= 4) d.bonusScore += 3;
                    break;

                case "Odd Eye":
                    // x3배 -> +200% (+2.0)
                    if (diceList.All(d => d.value % 2 != 0) && diceList.Count > 0) foreach (var d in diceList) d.finalMult += 2.0f;
                    break;

                case "Golden Scale":
                    if (GameData.Instance.chips > 0)
                    {
                        float bonus = (GameData.Instance.chips / 10) * 0.2f;
                        foreach (var d in diceList) d.finalMult += bonus;
                    }
                    break;

                case "Artisan Whetstone":
                    foreach (var d in diceList) if (d.diceType == "Normal") d.finalMult += 1.0f;
                    break;

                case "Soul Collector":
                    if (GameData.Instance.soulCollectorStack > 0)
                    {
                        float bonus = GameData.Instance.soulCollectorStack * 0.5f;
                        foreach (var d in diceList) d.finalMult += bonus;
                    }
                    break;
            }
        }
    }

    // ============================================
    // ★ 특수 주사위 효과 (인플레이션 방지 적용)
    // ============================================
    private void ApplySpecialDiceEffects(List<DiceData> dice)
    {
        // [Phase 1] 값 변경
        foreach (var d in dice)
        {
            Vector2Int pos = GetPos(d.slotIndex);
            if (d.diceType == "Chameleon Dice")
            {
                int maxVal = 0;
                List<Vector2Int> offsets = new List<Vector2Int> { new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
                foreach (var off in offsets)
                {
                    DiceData t = GetDieAt(dice, pos + off);
                    if (t != null && t.value > maxVal) maxVal = t.value;
                }
                if (maxVal > 0) d.value = maxVal;
            }
            else if (d.diceType == "Ancient Dice")
            {
                if (d.roundsHeld >= 5)
                {
                    d.value = 6;
                    d.finalMult += 4.0f; // 진화 시 +400%
                }
            }
        }

        // [Phase 2] 버프/디버프
        foreach (var d in dice)
        {
            Vector2Int pos = GetPos(d.slotIndex);
            int lostLife = (GameData.Instance != null) ? Mathf.Max(0, 3 - GameData.Instance.handsLeft) : 0;

            switch (d.diceType)
            {
                case "Time Dice":
                    d.finalMult += (d.roundsHeld * 0.5f);
                    break;
                case "Ice Dice":
                    ApplyToOffsets(dice, pos, new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) }, target => target.bonusScore += 5);
                    ApplyToOffsets(dice, pos, new[] { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) }, target => target.bonusScore -= 4);
                    break;
                case "TimeAttack(R)":
                    ApplyCross(dice, pos, target => target.bonusScore += (lostLife * 2));
                    break;
                case "TimeAttack(S)":
                    d.finalMult += (lostLife * 1.5f);
                    break;
                case "Buff Dice":
                    ApplyCross(dice, pos, target => { if (target.bonusScore > 0) target.bonusScore *= 3; });
                    break;
                case "Spring Dice":
                    ApplyGlobal(dice, pos, (target, isInside) => {
                        if (isInside) target.finalMult += 1.0f;
                        else target.finalMult -= 0.5f;
                    });
                    break;
                case "Laser Dice":
                    int count = dice.Count(t => { Vector2Int p = GetPos(t.slotIndex); return (p != pos) && (p.x == pos.x || p.y == pos.y); });
                    d.bonusScore += count * 3;
                    break;
                case "Offer Dice":
                    d.finalMult = 0f;
                    Apply3x3(dice, pos, target => target.finalMult += 2.0f);
                    break;
                case "Glass Dice":
                    if (d.value <= 2) d.finalMult = 0f;
                    else d.finalMult += 1.0f;
                    break;
                case "Stone Dice":
                    ApplyCross(dice, pos, target => { target.finalMult = 0f; target.bonusScore = 0; });
                    break;
            }
        }

        // [Phase 3] 변조 및 특수 상호작용
        foreach (var d in dice)
        {
            Vector2Int pos = GetPos(d.slotIndex);
            if (d.diceType == "Mirror Dice")
            {
                float maxMult = 1.0f;
                ApplyCross(dice, pos, target => { if (target.finalMult > maxMult) maxMult = target.finalMult; });
                d.finalMult = maxMult;
            }
            else if (d.diceType == "Reflection Dice")
            {
                ApplyToOffsets(dice, pos, new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0) }, target => {
                    target.bonusScore *= -1;
                    float currentBonus = target.finalMult - 1.0f;
                    target.finalMult = 1.0f + (currentBonus * -1.0f);
                });
            }
            else if (d.diceType == "Absorb Dice")
            {
                Apply3x3(dice, pos, target => {
                    if (target.bonusScore > 0) { d.bonusScore += target.bonusScore; target.bonusScore = 0; }
                });
            }
            else if (d.diceType == "Rubber Dice")
            {
                Apply3x3(dice, pos, target => {
                    target.bonusScore /= 2;
                    target.finalMult = 1.0f + (target.finalMult - 1.0f) * 0.5f;
                });
            }
        }

        // [Phase 4] 강철 주사위 (디버프 제거)
        foreach (var d in dice)
        {
            if (d.diceType == "Steel Dice")
            {
                if (d.bonusScore < 0) d.bonusScore = 0;
                if (d.finalMult < 1.0f) d.finalMult = 1.0f;
            }
        }
    }

    // ============================================
    // GameManager 호환용 (오류 방지)
    // ============================================
    public float CheckPositionBonus(List<DiceData> dice) { return 1.0f; }
    public float CheckSpecialDiceBonus(List<DiceData> dice) { return 1.0f; }
    public int CalculateFinalGlitchScore(int storedScore, int currentHandScore, int comboCount)
    {
        return Mathf.RoundToInt((storedScore + currentHandScore) * Mathf.Pow(2, comboCount));
    }

    // ============================================
    // 헬퍼 함수
    // ============================================
    private Vector2Int GetPos(int idx) => new Vector2Int(idx % GRID_WIDTH, idx / GRID_WIDTH);
    private DiceData GetDieAt(List<DiceData> list, Vector2Int p) => list.FirstOrDefault(d => GetPos(d.slotIndex) == p);

    private void ApplyCross(List<DiceData> list, Vector2Int center, System.Action<DiceData> action)
    {
        ApplyToOffsets(list, center, new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) }, action);
    }

    private void Apply3x3(List<DiceData> list, Vector2Int center, System.Action<DiceData> action)
    {
        foreach (var t in list)
        {
            Vector2Int p = GetPos(t.slotIndex);
            if (p == center) continue;
            if (Mathf.Abs(p.x - center.x) <= 1 && Mathf.Abs(p.y - center.y) <= 1) action(t);
        }
    }

    private void ApplyGlobal(List<DiceData> list, Vector2Int center, System.Action<DiceData, bool> action)
    {
        foreach (var t in list)
        {
            Vector2Int p = GetPos(t.slotIndex);
            if (p == center) continue;
            bool isInside = (Mathf.Abs(p.x - center.x) <= 1 && Mathf.Abs(p.y - center.y) <= 1);
            action(t, isInside);
        }
    }

    private void ApplyToOffsets(List<DiceData> list, Vector2Int center, Vector2Int[] offsets, System.Action<DiceData> action)
    {
        foreach (var off in offsets)
        {
            DiceData t = GetDieAt(list, center + off);
            if (t != null) action(t);
        }
    }

    // ============================================
    // 라운드 종료 및 기타
    // ============================================
    public void OnRoundEnd()
    {
        if (GameData.Instance == null) return;
        if (GameData.Instance.hasCreditCard && GameData.Instance.chips < 0)
        {
            int interest = Mathf.Clamp(Mathf.Abs(GameData.Instance.chips) / 10 + 1, 1, 4);
            GameData.Instance.chips -= interest;
        }
        if (GameData.Instance.isStageRewardBlocked) return;

        foreach (var item in GameData.Instance.GetAllActiveUpgrades())
        {
            switch (item.itemName)
            {
                case "Golden Piggy Bank":
                    if (GameData.Instance.chips > 0) GameData.Instance.AddChips(Mathf.Min(GameData.Instance.chips / 10, 10));
                    break;
                case "Fate Die":
                    int r = Random.Range(1, 101);
                    int profit = (r < 30) ? -10 : (r < 60) ? 5 : (r < 90) ? 15 : 30;
                    GameData.Instance.AddChips(profit);
                    break;
                case "Eco Bin":
                    bool hasTime = GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Time Capsule");
                    if (!hasTime && GameData.Instance.rerollsLeft > 0) GameData.Instance.AddChips(GameData.Instance.rerollsLeft);
                    break;
                case "Payback":
                    bool hasTime2 = GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Time Capsule");
                    if (!hasTime2 && GameData.Instance.rerollsLeft > 0) GameData.Instance.AddChips(GameData.Instance.rerollsLeft * 2);
                    break;
            }
        }
    }

    private void ApplyTwinBlessing(List<DiceData> dice)
    {
        bool hasItem = GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Twin's Blessing");
        if (hasItem && dice.Count >= 5 && !dice.Any(d => d.isSelected))
        {
            dice[0].value = 2; dice[1].value = 2; dice[2].value = 4; dice[3].value = 4; dice[4].value = 6;
        }
    }

    private void CheckDejaVuEffect(HandType currentHand)
    {
        if (!GameData.Instance.GetAllActiveUpgrades().Exists(i => i.itemName == "Deja Vu")) return;

        if (currentHand != HandType.HighCard && currentHand == GameData.Instance.lastHandType)
        {
            GameData.Instance.handStreak++;
            if (GameData.Instance.handStreak >= 1)
            {
                if (GameData.Instance.rerollsLeft < GameData.MAX_REROLLS) GameData.Instance.rerollsLeft++;
                GameData.Instance.handStreak = 0;
            }
        }
        else GameData.Instance.handStreak = 0;
        GameData.Instance.lastHandType = currentHand;
    }

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
}