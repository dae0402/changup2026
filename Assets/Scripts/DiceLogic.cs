using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 주사위 족보 분석 및 특수 주사위 + 유물 효과 처리
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
        { HandType.ThreeOfAKind, "쓰리 카드 (GLITCH!)" }, { HandType.Straight, "스트레이트" },
        { HandType.FullHouse, "풀 하우스 (GLITCH!)" }, { HandType.FourOfAKind, "포 카드 (GLITCH!)" },
        { HandType.FiveOfAKind, "파이브 카드 (GLITCH!)" }
    };

    // ============================================
    // ★ 메인 분석 함수
    // ============================================
    public (HandType type, string name, int currentScore, bool isGlitch) AnalyzeDice(List<DiceData> dice)
    {
        if (dice == null || dice.Count == 0) return (HandType.HighCard, "없음", 0, false);

        // ★ [신규] 1. 쌍둥이의 축복 (첫 롤 강제 조작)
        // 라운드 첫 리롤이고 아이템이 있다면 주사위 값을 강제로 투 페어로 바꿈
        if (GameData.Instance != null && GameData.Instance.isFirstRoll)
        {
            ApplyTwinBlessing(dice);
            GameData.Instance.isFirstRoll = false; // 한 번 적용 후 해제
        }

        // 1. 초기화
        foreach (var die in dice)
        {
            die.finalScore = die.value;
            die.bonusScore = 0;
            die.finalMult = 1.0f;
        }

        // 2. 특수 주사위 효과
        ApplySpecialDiceEffects(dice);

        // 3. ★ 유물 효과 적용 (영혼 수집가, 무거운 손 등)
        ApplyGlobalUpgrades(dice);

        // 4. 족보 판별
        Dictionary<int, int> counts = CountDiceValues(dice);
        HandType hand = DetermineHandType(dice, counts);

        // ★ [신규] 2. 데자뷰 (연속 족보 체크)
        CheckDejaVuEffect(hand);

        // 5. 점수 계산
        float totalDiceScore = 0;
        foreach (var die in dice)
        {
            float score = (die.value + die.bonusScore) * die.finalMult;
            if (score < 0) score = 0;
            totalDiceScore += score;
        }

        float handMult = handMultipliers[hand];
        float globalMult = 1.0f;
        if (GameData.Instance != null) globalMult = GameData.Instance.feverMultiplier;

        int finalScore = Mathf.RoundToInt(totalDiceScore * handMult * globalMult);

        bool isGlitch = (hand == HandType.ThreeOfAKind || hand == HandType.FullHouse ||
                         hand == HandType.FourOfAKind || hand == HandType.FiveOfAKind);

        return (hand, handNames[hand], finalScore, isGlitch);
    }

    // ============================================
    // ★ 유물 효과 적용
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
                    int sum = 0;
                    foreach (var d in diceList) sum += d.value;
                    if (sum == 21)
                    {
                        foreach (var d in diceList) d.finalMult *= 7.0f;
                        Debug.Log($"♠️ [Blackjack] 21 달성! 점수 7배!");
                    }
                    break;

                case "Devil Dice":
                    foreach (var d in diceList) d.finalMult *= 5.0f;
                    break;

                case "Vampire":
                    int lostHands = 3 - GameData.Instance.handsLeft;
                    if (lostHands > 0)
                    {
                        foreach (var d in diceList) d.bonusScore += lostHands;
                    }
                    break;

                case "Sniper Scope":
                    Dictionary<int, int> counts = CountDiceValues(diceList);
                    HandType hand = DetermineHandType(diceList, counts);
                    if (hand == HandType.OnePair)
                    {
                        foreach (var d in diceList) d.finalMult *= 2.0f;
                    }
                    break;

                case "Heavy Weight":
                    foreach (var d in diceList)
                    {
                        if (d.value >= 4) d.bonusScore += 3;
                    }
                    break;

                case "Odd Eye":
                    bool isAllOdd = true;
                    foreach (var d in diceList) if (d.value % 2 == 0) { isAllOdd = false; break; }
                    if (isAllOdd && diceList.Count > 0)
                    {
                        foreach (var d in diceList) d.finalMult *= 3.0f;
                    }
                    break;

                case "Golden Scale":
                    if (GameData.Instance.chips > 0)
                    {
                        int stacks = GameData.Instance.chips / 10;
                        float scaleBonus = stacks * 0.1f;
                        if (scaleBonus > 0)
                        {
                            foreach (var d in diceList) d.finalMult *= (1.0f + scaleBonus);
                        }
                    }
                    break;

                case "Artisan Whetstone":
                    foreach (var d in diceList)
                    {
                        if (d.diceType == "Normal") d.finalMult *= 2.0f;
                    }
                    break;

                // ▼ [신규] 영혼 수집가 (삭제된 유물 수만큼 배율 중첩)
                case "Soul Collector":
                    if (GameData.Instance.soulCollectorStack > 0)
                    {
                        float bonus = Mathf.Pow(2, GameData.Instance.soulCollectorStack);
                        foreach (var d in diceList) d.finalMult *= bonus;
                        Debug.Log($"👻 [Soul Collector] 영혼 {GameData.Instance.soulCollectorStack}개 -> 배율 x{bonus}");
                    }
                    break;

                // ▼ [신규] 무거운 손 (최종 점수 2배)
                case "Heavy Hand":
                    foreach (var d in diceList) d.finalMult *= 2.0f;
                    Debug.Log($"🦾 [Heavy Hand] 점수 2배 적용 (리롤 비용 증가)");
                    break;
            }
        }
    }

    // ============================================
    // ★ 신규 유물 특수 로직 (조작 및 상태 체크)
    // ============================================

    // 1. 쌍둥이의 축복: 강제로 투 페어 만들기
    private void ApplyTwinBlessing(List<DiceData> dice)
    {
        if (GameData.Instance == null) return;

        bool hasItem = false;
        foreach (var item in GameData.Instance.GetAllActiveUpgrades())
            if (item.itemName == "Twin's Blessing") { hasItem = true; break; }

        if (hasItem && dice.Count >= 5)
        {
            // 강제로 값을 변경 (예: 2, 2, 4, 4, 6)
            dice[0].value = 2;
            dice[1].value = 2;
            dice[2].value = 4;
            dice[3].value = 4;
            // 나머지는 랜덤성 유지를 위해 놔두거나 확정값 부여 가능 (여기선 5번째만 6으로)
            if (dice.Count > 4) dice[4].value = 6;

            Debug.Log("👯 [Twin's Blessing] 첫 리롤! 투 페어로 값이 조작되었습니다.");
        }
    }

    // 2. 데자뷰: 연속 족보 체크
    private void CheckDejaVuEffect(HandType currentHand)
    {
        if (GameData.Instance == null) return;

        bool hasItem = false;
        foreach (var item in GameData.Instance.GetAllActiveUpgrades())
            if (item.itemName == "Deja Vu") { hasItem = true; break; }

        if (hasItem)
        {
            // 노 페어(HighCard)는 보통 제외하지만, 기획에 따라 포함 가능 (여기선 포함)
            if (currentHand == GameData.Instance.lastHandType)
            {
                GameData.Instance.handStreak++;
                if (GameData.Instance.handStreak >= 1) // 2회 연속(0->1)이면 발동
                {
                    GameData.Instance.rerollsLeft++;
                    Debug.Log($"✨ [Deja Vu] {currentHand} 연속 등장! 리롤 횟수 +1 회복!");
                    // 연속 보상을 한 번만 줄지, 계속 줄지에 따라 초기화 여부 결정
                    GameData.Instance.handStreak = 0;
                }
            }
            else
            {
                GameData.Instance.handStreak = 0;
            }

            // 현재 족보를 마지막 족보로 저장
            GameData.Instance.lastHandType = currentHand;
        }
    }

    // ============================================
    // ★ 라운드 종료 시 효과 처리 (이자, 도박, 환급 등)
    // ============================================
    public void OnRoundEnd()
    {
        if (GameData.Instance == null) return;

        // 1. 블랙 카드 이자
        if (GameData.Instance.hasCreditCard && GameData.Instance.chips < 0)
        {
            int debt = Mathf.Abs(GameData.Instance.chips);
            int interest = Mathf.Clamp(debt / 10 + 1, 1, 4);
            GameData.Instance.chips -= interest;
            Debug.Log($"💳 [Black Card] 빚 이자 -{interest}C");
        }

        // 2. 유물 효과 처리
        foreach (var item in GameData.Instance.GetAllActiveUpgrades())
        {
            switch (item.itemName)
            {
                // 황금 돼지 저금통
                case "Golden Piggy Bank":
                    if (GameData.Instance.chips > 0)
                    {
                        int pigInterest = Mathf.Min(GameData.Instance.chips / 10, 10);
                        if (pigInterest > 0)
                        {
                            GameData.Instance.AddChips(pigInterest);
                            Debug.Log($"🐷 [Piggy Bank] 이자 +{pigInterest}C");
                        }
                    }
                    break;

                // 운명의 주사위 (도박)
                case "Fate Die":
                    int roll = Random.Range(1, 7);
                    int profit = 0;
                    if (roll == 1) profit = -2;
                    else if (roll == 2) profit = -1;
                    else if (roll == 3) profit = 0;
                    else if (roll == 4) profit = 1;
                    else if (roll == 5) profit = 2;
                    else if (roll == 6) profit = 3;

                    if (profit != 0)
                    {
                        GameData.Instance.AddChips(profit);
                        Debug.Log($"🎲 [Fate Die] 결과 {roll} -> {profit}C");
                    }
                    break;

                // 친환경 재활용통
                case "Eco Bin":
                    int refund = GameData.Instance.rerollsLeft;
                    if (refund > 0)
                    {
                        GameData.Instance.AddChips(refund);
                        Debug.Log($"♻️ [Eco Bin] 리롤 환급 +{refund}C");
                    }
                    break;
            }
        }
    }

    // ============================================
    // 기타 함수들 (기존 유지)
    // ============================================
    private void ApplySpecialDiceEffects(List<DiceData> diceList)
    {
        foreach (var sourceDie in diceList)
        {
            Vector2Int pos = GetPos(sourceDie.slotIndex);
            switch (sourceDie.diceType)
            {
                case "TimeAttack":
                    int lifeBonus = (GameData.Instance != null) ? (3 - GameData.Instance.handsLeft) * 2 : 0;
                    ApplyBuffToRange(diceList, pos, "Cross", lifeBonus, 1.0f);
                    break;
                case "Laser":
                    int count = CountDiceInRowCol(diceList, pos);
                    sourceDie.bonusScore += count * 3;
                    break;
                case "Offer":
                    sourceDie.finalMult = 0f;
                    ApplyBuffToRange(diceList, pos, "3x3", 0, 3.0f);
                    break;
                case "Ice": ApplyIceEffect(diceList, pos); break;
                case "Glass":
                    if (sourceDie.value <= 2) sourceDie.finalMult = 0f;
                    else sourceDie.finalMult *= 2.0f;
                    break;
            }
        }
        foreach (var sourceDie in diceList)
        {
            if (sourceDie.diceType == "Rubber") ApplyRubberEffect(diceList, GetPos(sourceDie.slotIndex));
        }
    }

    private Vector2Int GetPos(int index) => new Vector2Int(index % GRID_WIDTH, index / GRID_WIDTH);

    private void ApplyBuffToRange(List<DiceData> allDice, Vector2Int center, string shape, int scoreAdd, float multMult)
    {
        foreach (var target in allDice)
        {
            Vector2Int tPos = GetPos(target.slotIndex);
            if (tPos == center) continue;
            int dx = Mathf.Abs(tPos.x - center.x);
            int dy = Mathf.Abs(tPos.y - center.y);
            bool isInRange = (shape == "3x3") ? (dx <= 1 && dy <= 1) : ((dx == 1 && dy == 0) || (dx == 0 && dy == 1));

            if (isInRange) { target.bonusScore += scoreAdd; target.finalMult *= multMult; }
        }
    }

    private void ApplyIceEffect(List<DiceData> allDice, Vector2Int center)
    {
        foreach (var target in allDice)
        {
            Vector2Int tPos = GetPos(target.slotIndex);
            if (tPos == center) continue;
            int dx = Mathf.Abs(tPos.x - center.x);
            int dy = Mathf.Abs(tPos.y - center.y);
            if (dx > 1 || dy > 1) continue;
            if (dx + dy == 1) target.bonusScore += 5; // 십자가
            else if (dx == 1 && dy == 1) target.bonusScore -= 4; // 대각선
        }
    }

    private void ApplyRubberEffect(List<DiceData> allDice, Vector2Int center)
    {
        foreach (var target in allDice)
        {
            Vector2Int tPos = GetPos(target.slotIndex);
            if (tPos == center) continue;
            if (Mathf.Abs(tPos.x - center.x) <= 1 && Mathf.Abs(tPos.y - center.y) <= 1)
            {
                target.bonusScore /= 2;
                if (target.finalMult > 1.0f) target.finalMult = 1.0f + (target.finalMult - 1.0f) * 0.5f;
                else if (target.finalMult < 1.0f) target.finalMult = 1.0f - (1.0f - target.finalMult) * 0.5f;
            }
        }
    }

    private int CountDiceInRowCol(List<DiceData> allDice, Vector2Int center)
    {
        int count = 0;
        foreach (var d in allDice)
        {
            Vector2Int p = GetPos(d.slotIndex);
            if (p == center) continue;
            if (p.x == center.x || p.y == center.y) count++;
        }
        return count;
    }

    public int CalculateFinalGlitchScore(int storedScore, int currentHandScore, int comboCount)
    {
        return Mathf.RoundToInt((storedScore + currentHandScore) * Mathf.Pow(2, comboCount));
    }

    public float CheckPositionBonus(List<DiceData> dice)
    {
        if (dice.Count < 3) return 1f;
        HashSet<int> rows = new HashSet<int>();
        foreach (var die in dice) rows.Add(die.slotIndex / 5);
        return (rows.Count == 1) ? 1.5f : 1f;
    }

    public float CheckSpecialDiceBonus(List<DiceData> dice) => 1.0f;

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