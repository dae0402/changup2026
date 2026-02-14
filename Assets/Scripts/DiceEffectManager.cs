using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DiceEffectManager : MonoBehaviour
{
    public static void ApplyAllDiceEffects()
    {
        Debug.Log("⚡ 계산기 작동 시작!");
        if (GameData.Instance == null) return;
        List<DiceData> allDice = GameData.Instance.currentDice;

        // 1. 초기화
        foreach (var d in allDice)
        {
            d.finalScore = d.value;
            d.finalMult = 1.0f;
            d.bonusScore = 0;
            d.externalBuffMult = 1.0f;
            d.externalNerfMult = 1.0f;
            d.isImmuneToNerf = (d.diceType == "Steel Dice");
        }

        // 2. 복사 및 진화
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");

            switch (d.diceType)
            {
                case "Ancient Dice": if (d.roundsHeld >= 3) d.bonusScore += 10; break;
                case "Chameleon Dice": if (crossNeighbors.Count > 0) d.finalScore = crossNeighbors.Max(n => n.value); break;
                case "Mirror Dice":
                    if (crossNeighbors.Count > 0)
                    {
                        var best = crossNeighbors.OrderByDescending(n => n.value).First();
                        d.bonusScore = best.bonusScore; d.finalMult = best.finalMult;
                    }
                    break;
            }
        }

        // 3. 범위 효과 (버프/너프)
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");
            var areaNeighbors = GetNeighbors(d, allDice, "3x3");

            switch (d.diceType)
            {
                case "Buff Dice": foreach (var n in crossNeighbors) n.externalBuffMult *= 3.0f; break;
                case "Spring Dice":
                    foreach (var n in areaNeighbors)
                    {
                        if (crossNeighbors.Contains(n)) n.externalBuffMult *= 2.0f;
                        else n.externalNerfMult *= 0.5f;
                    }
                    break;
                case "Reflect Dice":
                    foreach (var n in crossNeighbors)
                    {
                        float temp = n.externalBuffMult; n.externalBuffMult = n.externalNerfMult; n.externalNerfMult = temp;
                    }
                    break;
                case "Absorb Dice":
                    foreach (var n in areaNeighbors)
                    {
                        d.externalBuffMult *= n.externalBuffMult; n.externalBuffMult = 1.0f;
                    }
                    break;
                case "Splash Dice":
                    foreach (var n in crossNeighbors)
                    {
                        float roulette = Random.Range(0.5f, 2.0f);
                        if (roulette >= 1.0f) n.externalBuffMult *= roulette; else n.externalNerfMult *= roulette;
                    }
                    break;
            }
        }

        // 4. 최종 계산
        foreach (var d in allDice)
        {
            float effectMod = (d.diceType == "Rubber Dice") ? 0.5f : 1.0f;
            float finalExternalMult = d.externalBuffMult;
            if (!d.isImmuneToNerf) finalExternalMult *= d.externalNerfMult;

            if (d.diceType == "Time Dice") d.finalMult += (d.roundsHeld * 0.1f);
            if (d.diceType == "Ice Dice") d.bonusScore += (d.slotIndex % 2 == 0) ? 5 : -4;
            if (d.diceType == "Comeback Dice" && GameData.Instance.handsLeft == 1) d.finalMult *= 1.5f;

            float resultMult = 1.0f + (finalExternalMult - 1.0f) * effectMod;
            float rawResult = (d.finalScore + d.bonusScore) * resultMult * d.finalMult;
            d.totalScoreCalculated = Mathf.RoundToInt(rawResult);
        }
    }

    private static List<DiceData> GetNeighbors(DiceData center, List<DiceData> all, string type)
    {
        int cRow = center.slotIndex / 8; int cCol = center.slotIndex % 8;
        List<DiceData> results = new List<DiceData>();
        foreach (var other in all)
        {
            if (other == center) continue;
            int oRow = other.slotIndex / 8; int oCol = other.slotIndex % 8;
            if (type == "cross")
            {
                if ((oRow == cRow && Mathf.Abs(oCol - cCol) == 1) || (oCol == cCol && Mathf.Abs(oRow - cRow) == 1)) results.Add(other);
            }
            else if (type == "3x3")
            {
                if (Mathf.Abs(oRow - cRow) <= 1 && Mathf.Abs(oCol - cCol) <= 1) results.Add(other);
            }
        }
        return results;
    }
}