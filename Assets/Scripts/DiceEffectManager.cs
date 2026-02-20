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

        // 1. 초기화 (모든 주사위의 상태를 기본값으로 되돌림)
        foreach (var d in allDice)
        {
            d.finalScore = d.value;
            d.finalMult = 1.0f;
            d.bonusScore = 0;
            d.externalBuffMult = 1.0f;
            d.externalNerfMult = 1.0f;
            d.isImmuneToNerf = (d.diceType == "Steel Dice");

            d.effectState = DiceEffectState.None;

            // ★ [추가] 비주얼 연출 변수 초기화
            d.isBuffed = false;
            d.isNerfed = false;
            d.effectPopupText = "";
        }

        // 2. 복사 및 진화 (단일/자신 효과)
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");

            switch (d.diceType)
            {
                case "Ancient Dice":
                    if (d.roundsHeld >= 3)
                    {
                        d.bonusScore += 10;
                        SetBuff(d, "+10");
                    }
                    break;
                case "Chameleon Dice":
                    if (crossNeighbors.Count > 0)
                    {
                        int maxVal = crossNeighbors.Max(n => n.value);
                        d.finalScore = maxVal;
                        SetBuff(d, $"Copy {maxVal}");
                    }
                    break;
                case "Mirror Dice":
                    if (crossNeighbors.Count > 0)
                    {
                        var best = crossNeighbors.OrderByDescending(n => n.value).First();
                        d.bonusScore = best.bonusScore;
                        d.finalMult = best.finalMult;
                        SetBuff(d, "Mirror");
                    }
                    break;
            }
        }

        // 3. 범위 효과 (버프/너프) - 주변 주사위에게 영향을 줌
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");
            var areaNeighbors = GetNeighbors(d, allDice, "3x3");

            switch (d.diceType)
            {
                case "Buff Dice": // 십자 방향 버프
                    foreach (var n in crossNeighbors)
                    {
                        n.externalBuffMult *= 3.0f;
                        SetBuff(n, "x3");
                    }
                    break;

                case "Spring Dice": // 십자는 버프, 대각선은 너프
                    foreach (var n in areaNeighbors)
                    {
                        if (crossNeighbors.Contains(n))
                        {
                            n.externalBuffMult *= 2.0f;
                            SetBuff(n, "x2");
                        }
                        else
                        {
                            n.externalNerfMult *= 0.5f;
                            SetNerf(n, "x0.5");
                        }
                    }
                    break;

                case "Reflect Dice": // 십자 방향 효과 반전
                    foreach (var n in crossNeighbors)
                    {
                        float temp = n.externalBuffMult;
                        n.externalBuffMult = n.externalNerfMult;
                        n.externalNerfMult = temp;

                        // 반전 효과 표시
                        if (n.externalBuffMult > 1.0f) SetBuff(n, "Reflect Buff");
                        else if (n.externalNerfMult < 1.0f) SetNerf(n, "Reflect Nerf");
                    }
                    break;

                case "Absorb Dice": // 주변 효과 흡수
                    foreach (var n in areaNeighbors)
                    {
                        if (n.externalBuffMult > 1.0f)
                        {
                            d.externalBuffMult *= n.externalBuffMult; // 내가 흡수
                            n.externalBuffMult = 1.0f; // 뺏김
                            SetNerf(n, "Absorbed"); // 뺏긴 애는 너프 연출
                        }
                    }
                    if (d.externalBuffMult > 1.0f) SetBuff(d, "Absorb!");
                    break;

                case "Splash Dice": // 랜덤 효과
                    foreach (var n in crossNeighbors)
                    {
                        float roulette = Random.Range(0.5f, 2.0f);
                        if (roulette >= 1.0f)
                        {
                            n.externalBuffMult *= roulette;
                            SetBuff(n, $"x{roulette:F1}"); // 소수점 첫째자리까지 표시
                        }
                        else
                        {
                            n.externalNerfMult *= roulette;
                            SetNerf(n, $"x{roulette:F1}");
                        }
                    }
                    break;
            }
        }

        // 4. 최종 계산 및 특수 주사위 자기 효과
        foreach (var d in allDice)
        {
            float effectMod = (d.diceType == "Rubber Dice") ? 0.5f : 1.0f;
            float finalExternalMult = d.externalBuffMult;

            // 면역이 아닐 때만 너프 적용
            if (!d.isImmuneToNerf)
            {
                finalExternalMult *= d.externalNerfMult;
            }
            else if (d.isNerfed) // 면역인데 너프를 받았다면 무효화
            {
                d.isNerfed = false;
                d.effectState = DiceEffectState.None;
                d.effectPopupText = "Immune"; // 면역 텍스트 표시
            }

            // 추가적인 자기 효과들
            if (d.diceType == "Time Dice")
            {
                float timeBonus = d.roundsHeld * 0.1f;
                if (timeBonus > 0)
                {
                    d.finalMult += timeBonus;
                    SetBuff(d, $"+{timeBonus * 100}%");
                }
            }

            if (d.diceType == "Ice Dice")
            {
                if (d.slotIndex % 2 == 0)
                {
                    d.bonusScore += 5;
                    SetBuff(d, "+5");
                }
                else
                {
                    d.bonusScore -= 4;
                    SetNerf(d, "-4");
                }
            }

            if (d.diceType == "Comeback Dice" && GameData.Instance.handsLeft == 1)
            {
                d.finalMult *= 1.5f;
                SetBuff(d, "x1.5");
            }

            // 진짜 최종 점수 계산
            float resultMult = 1.0f + (finalExternalMult - 1.0f) * effectMod;
            float rawResult = (d.finalScore + d.bonusScore) * resultMult * d.finalMult;
            d.totalScoreCalculated = Mathf.RoundToInt(rawResult);
        }

        // ★ 계산이 끝난 후 비주얼 즉시 업데이트 
        if (GameManager.Instance != null && GameManager.Instance.diceSpawner != null)
        {
            foreach (var d in allDice)
            {
                GameManager.Instance.diceSpawner.UpdateDiceVisual(d.slotIndex, d);
            }
        }
    }

    // =========================================================
    // ★ [추가된 헬퍼 함수] 상태 변경을 간편하게 하기 위한 함수들
    // =========================================================
    private static void SetBuff(DiceData d, string text)
    {
        d.isBuffed = true;
        d.isNerfed = false; // 버프가 들어오면 너프 연출 덮어쓰기 (기획에 따라 다를 수 있음)
        d.effectState = DiceEffectState.Buff;
        d.effectPopupText = text;
    }

    private static void SetNerf(DiceData d, string text)
    {
        d.isNerfed = true;
        d.isBuffed = false;
        d.effectState = DiceEffectState.Nerf;
        d.effectPopupText = text;
    }

    // =========================================================

    private static List<DiceData> GetNeighbors(DiceData center, List<DiceData> all, string type)
    {
        int columns = 5;
        int cRow = center.slotIndex / columns;
        int cCol = center.slotIndex % columns;

        List<DiceData> results = new List<DiceData>();

        foreach (var other in all)
        {
            if (other == center) continue;

            int oRow = other.slotIndex / columns;
            int oCol = other.slotIndex % columns;

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