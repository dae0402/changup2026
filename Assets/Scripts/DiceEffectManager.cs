using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DiceEffectManager : MonoBehaviour
{
    public static void ApplyAllDiceEffects()
    {
        List<DiceData> allDice = GameData.Instance.currentDice;

        // 1. 초기화 (매 계산마다 리셋)
        foreach (var d in allDice)
        {
            d.finalScore = d.value;
            d.finalMult = 1.0f;
            d.bonusScore = 0;
            d.externalBuffMult = 1.0f;
            d.externalNerfMult = 1.0f;
            d.isImmuneToNerf = (d.diceType == "Steel Dice"); // 강철 주사위 면역
        }

        // 2. 기초 능력치 결정 (진화, 복사 등)
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");

            switch (d.diceType)
            {
                case "Ancient Dice": // 일정 라운드 후 진화
                    if (d.roundsHeld >= 3) d.bonusScore += 10;
                    break;

                case "Chameleon Dice": // 범위 내 가장 큰 눈금 복사
                    if (crossNeighbors.Count > 0) d.finalScore = crossNeighbors.Max(n => n.value);
                    break;

                case "Mirror Dice": // 범위 내 가장 높은 등급의 효과 복사
                    if (crossNeighbors.Count > 0)
                    {
                        var best = crossNeighbors.OrderByDescending(n => n.value).First();
                        d.bonusScore = best.bonusScore;
                        d.finalMult = best.finalMult;
                    }
                    break;
            }
        }

        // 3. 범위 상호작용 (버프, 너프 누적)
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");
            var areaNeighbors = GetNeighbors(d, allDice, "3x3");

            switch (d.diceType)
            {
                case "Buff Dice": // 십자 범위 내 버프 3배
                    foreach (var n in crossNeighbors) n.externalBuffMult *= 3.0f;
                    break;

                case "Spring Dice": // 안쪽 버프 2배, 바깥쪽 너프 2배
                    foreach (var n in areaNeighbors)
                    {
                        if (crossNeighbors.Contains(n)) n.externalBuffMult *= 2.0f;
                        else n.externalNerfMult *= 0.5f;
                    }
                    break;

                case "Reflect Dice": // 십자 범위 내 버프/너프 반전
                    foreach (var n in crossNeighbors)
                    {
                        float temp = n.externalBuffMult;
                        n.externalBuffMult = n.externalNerfMult;
                        n.externalNerfMult = temp;
                    }
                    break;

                case "Absorb Dice": // 3x3 범위 내 모든 버프를 자신에게 흡수
                    foreach (var n in areaNeighbors)
                    {
                        d.externalBuffMult *= n.externalBuffMult;
                        n.externalBuffMult = 1.0f;
                    }
                    break;

                case "Splash Dice": // 범위 내 주사위에게 룰렛 효과
                    foreach (var n in crossNeighbors)
                    {
                        // ★ 오타 수정: routte -> roulette
                        float roulette = Random.Range(0.5f, 2.0f);
                        if (roulette >= 1.0f) n.externalBuffMult *= roulette;
                        else n.externalNerfMult *= roulette;
                    }
                    break;
            }
        }

        // 4. 최종 점수 확정
        foreach (var d in allDice)
        {
            float effectMod = (d.diceType == "Rubber Dice") ? 0.5f : 1.0f;

            float finalExternalMult = d.externalBuffMult;
            if (!d.isImmuneToNerf) finalExternalMult *= d.externalNerfMult;

            if (d.diceType == "Time Dice") d.finalMult += (d.roundsHeld * 0.1f);
            if (d.diceType == "Ice Dice") d.bonusScore += (d.slotIndex % 2 == 0) ? 5 : -4;
            if (d.diceType == "Comeback Dice")
            {
                if (GameData.Instance.handsLeft == 1) d.finalMult *= 1.5f;
            }

            float resultMult = 1.0f + (finalExternalMult - 1.0f) * effectMod;
            float rawResult = (d.finalScore + d.bonusScore) * resultMult * d.finalMult;

            d.totalScoreCalculated = Mathf.RoundToInt(rawResult);
        }
    }

    private static List<DiceData> GetNeighbors(DiceData center, List<DiceData> all, string type)
    {
        int cRow = center.slotIndex / 8;
        int cCol = center.slotIndex % 8;
        List<DiceData> results = new List<DiceData>();

        foreach (var other in all)
        {
            if (other == center) continue;
            int oRow = other.slotIndex / 8;
            int oCol = other.slotIndex % 8;

            // ★ 오타 수정: Math.Abs -> Mathf.Abs
            if (type == "cross")
            {
                if ((oRow == cRow && Mathf.Abs(oCol - cCol) == 1) || (oCol == cCol && Mathf.Abs(oRow - cRow) == 1))
                    results.Add(other);
            }
            else if (type == "3x3")
            {
                if (Mathf.Abs(oRow - cRow) <= 1 && Mathf.Abs(oCol - cCol) <= 1)
                    results.Add(other);
            }
        }
        return results;
    }
}