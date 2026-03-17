using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DiceEffectManager : MonoBehaviour
{
    public static void ApplyAllDiceEffects()
    {
        Debug.Log("⚡ 주사위 특수 스킬 계산기 작동 시작!");
        if (GameData.Instance == null || GameData.Instance.currentDice == null) return;
        List<DiceData> allDice = GameData.Instance.currentDice;

        // ★ 보스 기믹 판별 (조용한 그림자)
        bool isSilentShadow = GameData.Instance.isBossStage && GameData.Instance.currentBossName == "조용한 그림자 (Silent Shadow)";

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

            // 비주얼 연출 변수 초기화
            d.isBuffed = false;
            d.isNerfed = false;
            d.effectPopupText = "";
        }

        // =======================================================
        // ★ 보스 기믹 발동 시: 모든 특수 스킬 무효화 및 깡점수 적용 후 종료
        // =======================================================
        if (isSilentShadow)
        {
            Debug.Log("🌑 [조용한 그림자] 보스 기믹 발동! 모든 특수 주사위 봉인!");
            foreach (var d in allDice)
            {
                if (d.diceType != "Normal")
                {
                    SetNerf(d, "봉인됨!");
                }
                // ★ [버그 픽스] x10 제거: 원래 눈금(1~6) 그대로 적용
                d.totalScoreCalculated = d.value;
            }
            RefreshVisuals(allDice);
            return; // 여기서 계산 종료!
        }

        // =======================================================
        // 2. 복사 및 진화 (단일/자신 효과)
        // =======================================================
        foreach (var d in allDice)
        {
            var crossNeighbors = GetNeighbors(d, allDice, "cross");

            switch (d.diceType)
            {
                case "Ancient Dice":
                    if (d.roundsHeld >= 3)
                    {
                        // ★ [버그 픽스] 밸런스 조정 (50 -> 5)
                        d.bonusScore += 5;
                        SetBuff(d, "+5");
                    }
                    break;
                case "Chameleon Dice":
                    if (crossNeighbors.Count > 0)
                    {
                        int maxVal = crossNeighbors.Max(n => n.value);
                        d.finalScore = maxVal;
                        SetBuff(d, $"복사: {maxVal}");
                    }
                    break;
                case "Mirror Dice":
                    if (crossNeighbors.Count > 0)
                    {
                        var best = crossNeighbors.OrderByDescending(n => n.value).First();
                        d.bonusScore = best.bonusScore;
                        d.finalMult = best.finalMult;
                        SetBuff(d, "거울 복사!");
                    }
                    break;
            }
        }

        // =======================================================
        // 3. 범위 효과 (버프/너프) - 주변 주사위에게 영향을 줌
        // =======================================================
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
                    SetBuff(d, "광역 버프");
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
                        if (n.externalBuffMult > 1.0f) SetBuff(n, "반사 버프!");
                        else if (n.externalNerfMult < 1.0f) SetNerf(n, "반사 너프!");
                    }
                    break;

                case "Absorb Dice": // 주변 효과 흡수
                    foreach (var n in areaNeighbors)
                    {
                        if (n.externalBuffMult > 1.0f)
                        {
                            d.externalBuffMult *= n.externalBuffMult; // 내가 다 빨아먹음
                            n.externalBuffMult = 1.0f; // 뺏긴 애는 초기화
                            SetNerf(n, "흡수당함");
                        }
                    }
                    if (d.externalBuffMult > 1.0f) SetBuff(d, $"흡수: x{d.externalBuffMult:F1}");
                    break;

                case "Splash Dice": // 랜덤 효과 (룰렛)
                    foreach (var n in crossNeighbors)
                    {
                        float roulette = Random.Range(0.5f, 2.5f); // 0.5배 ~ 2.5배 랜덤
                        if (roulette >= 1.0f)
                        {
                            n.externalBuffMult *= roulette;
                            SetBuff(n, $"x{roulette:F1}");
                        }
                        else
                        {
                            n.externalNerfMult *= roulette;
                            SetNerf(n, $"x{roulette:F1}");
                        }
                    }
                    SetBuff(d, "스플래시!");
                    break;
            }
        }

        // =======================================================
        // 4. 최종 계산 및 특수 주사위 자기 효과
        // =======================================================
        foreach (var d in allDice)
        {
            float effectMod = (d.diceType == "Rubber Dice") ? 0.5f : 1.0f; // 고무 주사위는 효과 절반
            float finalExternalMult = d.externalBuffMult;

            // 강철 주사위(면역)가 아닐 때만 너프 적용
            if (!d.isImmuneToNerf)
            {
                finalExternalMult *= d.externalNerfMult;
            }
            else if (d.externalNerfMult < 1.0f) // 면역인데 너프를 받았다면 방어!
            {
                SetBuff(d, "디버프 면역!");
            }

            // 추가적인 자기 효과들
            if (d.diceType == "Time Dice")
            {
                float timeBonus = d.roundsHeld * 0.2f; // 오래 들고 있을수록 0.2배씩 증가
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
                    // ★ [버그 픽스] 밸런스 조정 (30 -> 3)
                    d.bonusScore += 3;
                    SetBuff(d, "+3");
                }
                else
                {
                    // ★ [버그 픽스] 밸런스 조정 (-10 -> -1)
                    d.bonusScore -= 1;
                    SetNerf(d, "-1");
                }
            }

            if (d.diceType == "Comeback Dice" && GameData.Instance.handsLeft == 1)
            {
                d.finalMult *= 2.0f; // 목숨 1개 남았을 때 2배 역전!
                SetBuff(d, "역전 x2.0");
            }

            // ★ [버그 픽스] 진짜 최종 점수 계산 (x10 제거하고 순수 눈금으로 계산)
            float resultMult = 1.0f + (finalExternalMult - 1.0f) * effectMod;

            // 공식: (눈금 + 보너스 점수) * 외부 배율 * 자체 배율
            float rawResult = (d.finalScore + d.bonusScore) * resultMult * d.finalMult;

            // 점수가 0 이하로 떨어지는 것 방지
            d.totalScoreCalculated = Mathf.Max(1, Mathf.RoundToInt(rawResult));
        }

        // 계산 끝! 팝업과 비주얼 갱신
        RefreshVisuals(allDice);
    }

    // =========================================================
    // ★ 헬퍼 함수들
    // =========================================================

    // 계산 완료 후 화면 업데이트
    private static void RefreshVisuals(List<DiceData> allDice)
    {
        if (GameManager.Instance != null && GameManager.Instance.diceSpawner != null)
        {
            foreach (var d in allDice)
            {
                GameManager.Instance.diceSpawner.UpdateDiceVisual(d.slotIndex, d);
            }
        }
    }

    // 버프/너프 연출 세팅 헬퍼
    private static void SetBuff(DiceData d, string text)
    {
        d.isBuffed = true;
        d.isNerfed = false;
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

    // 인접한 주사위 찾기
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

            if (type == "cross") // 상하좌우 (십자가형)
            {
                if ((oRow == cRow && Mathf.Abs(oCol - cCol) == 1) || (oCol == cCol && Mathf.Abs(oRow - cRow) == 1))
                    results.Add(other);
            }
            else if (type == "3x3") // 대각선 포함 주변 8칸
            {
                if (Mathf.Abs(oRow - cRow) <= 1 && Mathf.Abs(oCol - cCol) <= 1)
                    results.Add(other);
            }
        }
        return results;
    }
}