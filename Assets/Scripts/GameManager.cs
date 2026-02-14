using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("필수 연결")]
    public DiceLogic diceLogic;
    public DiceSpawner diceSpawner;
    public GameOverDirector gameOverDirector;

    [Header("설정")]
    public int totalSlots = 15;
    public int dicePerRoll = 5;

    // ★ 글리치용 변수 복구
    private int storedScore = 0;
    private int comboCount = 0;

    void Awake() { if (Instance == null) Instance = this; }

    void Start()
    {
        if (diceLogic == null) diceLogic = GetComponent<DiceLogic>();
        if (diceSpawner == null) diceSpawner = GetComponent<DiceSpawner>();
        if (gameOverDirector == null) gameOverDirector = GetComponent<GameOverDirector>();
    }

    public void StartNewGame()
    {
        GameData.Instance.ResetGame();
        ResetGlitchState();
        diceSpawner.ClearAllDice();
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.ShowGameScreen();
    }

    public void RollDice()
    {
        if (GameData.Instance.handsLeft <= 0 || GameData.Instance.isRolling) return;
        ResetGlitchState(); // 굴리기 전 초기화
        StartCoroutine(RollDiceCoroutine());
    }

    private void ResetGlitchState()
    {
        storedScore = 0;
        comboCount = 0;
    }

    private IEnumerator RollDiceCoroutine()
    {
        // ... (타임 캡슐 로직 동일) ...
        bool hasTimeCapsule = GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Time Capsule");
        int baseRerolls = 3;
        if (hasTimeCapsule) GameData.Instance.rerollsLeft += baseRerolls;
        else GameData.Instance.rerollsLeft = baseRerolls;

        GameData.Instance.StartNewTurn();
        if (hasTimeCapsule) GameData.Instance.rerollsLeft -= baseRerolls;
        if (!hasTimeCapsule) GameData.Instance.rerollsLeft = 3;

        GameData.Instance.handsLeft--;
        GameData.Instance.isRolling = true;
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.UpdateButtons();

        // 주사위 생성
        yield return StartCoroutine(ProcessSpawnAndRoll(true));

        // ★ [핵심 복구] 패 분석 후 글리치 여부 확인
        DiceLogic.HandResult result = EvaluateCurrentHand();

        if (result.isGlitch)
        {
            Debug.Log("👾 글리치 감지! 루프 시작!");
            yield return StartCoroutine(GlitchRoutine()); // 글리치면 반복 루틴 진입
        }
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    // ★ [복구됨] 글리치 루틴
    private IEnumerator GlitchRoutine()
    {
        bool keepGlitching = true;
        while (keepGlitching)
        {
            yield return new WaitForSeconds(0.8f); // 연출 대기 시간

            // 점수 누적 로직 (콤보)
            storedScore += GameData.Instance.currentHandScore;
            comboCount++;

            Debug.Log($"👾 글리치 반복 {comboCount}회차 | 누적 점수: {storedScore}");

            // 주사위 다시 생성 (글리치 효과)
            yield return StartCoroutine(ProcessSpawnAndRoll(false));

            // 다시 분석해서 또 글리치인지 확인
            DiceLogic.HandResult result = EvaluateCurrentHand();

            // 글리치가 끊기면 종료
            if (!result.isGlitch) keepGlitching = false;
        }

        GameData.Instance.isRolling = false;
        UIManager.Instance.UpdateButtons();
    }

    private IEnumerator ProcessSpawnAndRoll(bool isInitial)
    {
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();

        // ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼ 테스트 모드 시작 ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
        Debug.LogWarning("⚡테스트 모드 발동: 버프/너프 주사위만 강제로 생성합니다!⚡");

        List<int> slots = GetRandomSlots(dicePerRoll);

        // 강제로 2개의 특수 주사위만 생성합니다.
        if (slots.Count >= 1)
        {
            // 1번 타자: Buff Dice (주변을 노랗게 만듦)
            GameData.Instance.currentDice.Add(new DiceData(slots[0], 6, "Buff Dice"));
        }
        if (slots.Count >= 2)
        {
            // 2번 타자: Spring Dice (안쪽은 노랑, 바깥쪽은 빨갛게 만듦 - 너프 확인용)
            GameData.Instance.currentDice.Add(new DiceData(slots[1], 1, "Spring Dice"));
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲ 테스트 모드 끝 ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        /* 원래 코드는 주석 처리됨
        List<int> slots = GetRandomSlots(dicePerRoll);
        foreach (int slot in slots)
        {
            int val = GetRandomDiceValue();
            GameData.Instance.currentDice.Add(new DiceData(slot, val));
        }
        */

        // 매직 다이스 체크 (테스트 중엔 잠시 꺼둬도 됨)
        /*
        if (isInitial && GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Magic Dice"))
        {
            if (GameData.Instance.currentDice.Count > 0) GameData.Instance.currentDice[0].value = 6;
        }
        */

        // 실제 화면에 생성
        foreach (var dice in GameData.Instance.currentDice)
        {
            diceSpawner.SpawnDice(dice.slotIndex, dice.value, isInitial);
        }

        yield return new WaitForSeconds(0.3f);

        // ★ 생성 직후에도 효과 계산 및 UI 갱신 (중요!)
        DiceEffectManager.ApplyAllDiceEffects();
        UIManager.Instance.UpdateAllUI();
    }

    // ★ [수정됨] 결과를 반환하도록 변경 (void -> HandResult)
    private DiceLogic.HandResult EvaluateCurrentHand()
    {
        if (GameData.Instance.currentDice.Count == 0) return new DiceLogic.HandResult();

        DiceEffectManager.ApplyAllDiceEffects();
        DiceLogic.HandResult result = DiceLogic.AnalyzeDice(GameData.Instance.currentDice);

        GameData.Instance.feverMultiplier = result.multiplier;

        // 글리치 콤보가 있으면 배율 뻥튀기
        float glitchMult = (comboCount > 0) ? Mathf.Pow(1.5f, comboCount) : 1.0f;
        int calculatedScore = (int)(result.totalScore * result.multiplier * glitchMult);

        GameData.Instance.currentHandScore = calculatedScore;
        GameData.Instance.totalScore = storedScore + calculatedScore;
        GameData.Instance.canSubmit = true;

        string displayName = result.handName + (comboCount > 0 ? $" (x{glitchMult:F1} Glitch!)" : "");
        UIManager.Instance.DisplayHandResult(displayName, result.multiplier);
        UIManager.Instance.UpdateAllUI();

        return result; // ★ 결과 반환
    }

    // 리롤 등 나머지 코드는 기존과 동일... (생략 시 에러 날 수 있으니 Reroll 등은 이전 코드 유지)
    public void RerollSelectedDice()
    {
        if (GameData.Instance.rerollsLeft <= 0) return;
        List<DiceData> selected = GameData.Instance.currentDice.Where(d => d.isSelected).ToList();
        if (selected.Count == 0) return;
        StartCoroutine(RerollCoroutine(selected));
    }

    private IEnumerator RerollCoroutine(List<DiceData> toReroll)
    {
        // (기존 코드와 동일하게 복사)
        bool isFreeReroll = false;
        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Lucky Coin") && Random.value < 0.1f) isFreeReroll = true;
        if (!isFreeReroll) GameData.Instance.rerollsLeft--;

        GameData.Instance.isRolling = true;
        UIManager.Instance.UpdateButtons();
        List<int> occupied = GameData.Instance.currentDice.Where(d => !d.isSelected).Select(d => d.slotIndex).ToList();

        foreach (var d in toReroll)
        {
            diceSpawner.RemoveDice(d.slotIndex);
            GameData.Instance.currentDice.Remove(d);
            int newSlot = GetAvailableSlot(occupied);
            occupied.Add(newSlot);
            int newVal = GetRandomDiceValue();
            GameData.Instance.currentDice.Add(new DiceData(newSlot, newVal));
            diceSpawner.SpawnDice(newSlot, newVal, false);
            yield return new WaitForSeconds(0.1f);
        }

        DiceLogic.HandResult result = EvaluateCurrentHand();
        if (result.isGlitch) yield return StartCoroutine(GlitchRoutine()); // 리롤 후에도 글리치 체크
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    public void ToggleDiceSelection(int slotIndex)
    {
        var d = GameData.Instance.currentDice.Find(x => x.slotIndex == slotIndex);
        if (d != null)
        {
            d.isSelected = !d.isSelected;
            diceSpawner.UpdateDiceVisual(slotIndex, d.isSelected);
        }
    }

    public void SubmitHand()
    {
        if (!GameData.Instance.canSubmit) return;
        EvaluateCurrentHand();
        int finalScore = GameData.Instance.totalScore; // totalScore 사용 (글리치 누적 포함)

        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Payback"))
        {
            int bonus = GameData.Instance.rerollsLeft * 2;
            if (bonus > 0) GameData.Instance.AddChips(bonus);
        }

        GameData.Instance.AddScore(finalScore);
        int chipsEarned = Mathf.Max(1, finalScore / 100);
        GameData.Instance.AddChips(chipsEarned);

        GameData.Instance.canSubmit = false;
        GameData.Instance.currentHandScore = 0;
        GameData.Instance.totalScore = 0;
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();
        ResetGlitchState();
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.UpdateButtons();
        UIManager.Instance.ShowScorePopup(finalScore);
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (GameData.Instance.handsLeft <= 0)
        {
            if (gameOverDirector != null) gameOverDirector.TriggerGameOver();
        }
    }
    private List<int> GetRandomSlots(int count) => Enumerable.Range(0, totalSlots).OrderBy(x => Random.value).Take(count).ToList();
    private int GetRandomDiceValue() => GameData.Instance.availableDiceValues[Random.Range(0, GameData.Instance.availableDiceValues.Count)];
    private int GetAvailableSlot(List<int> used)
    {
        var avail = Enumerable.Range(0, totalSlots).Except(used).ToList();
        return avail.Count > 0 ? avail[Random.Range(0, avail.Count)] : 0;
    }
}