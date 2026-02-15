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

    // 글리치용 변수
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
        ResetGlitchState();
        StartCoroutine(RollDiceCoroutine());
    }

    private void ResetGlitchState()
    {
        storedScore = 0;
        comboCount = 0;
    }

    private IEnumerator RollDiceCoroutine()
    {
        // 1. 코스트 처리
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

        // 2. 주사위 생성 (전체 새로고침)
        yield return StartCoroutine(ProcessSpawnAndRoll(true));

        // 3. 결과 확인
        DiceLogic.HandResult result = EvaluateCurrentHand();

        if (result.isGlitch)
        {
            Debug.Log("👾 글리치 감지! 루프 시작!");
            yield return StartCoroutine(GlitchRoutine());
        }
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    // ★ [핵심] 글리치 효과: 주사위가 싹 사라졌다가 다시 나옵니다.
    private IEnumerator GlitchRoutine()
    {
        bool keepGlitching = true;
        while (keepGlitching)
        {
            // 1. 대기 (연출)
            yield return new WaitForSeconds(0.8f);

            // 2. 점수 누적
            storedScore += GameData.Instance.currentHandScore;
            comboCount++;
            Debug.Log($"👾 글리치 반복 {comboCount}회차 | 누적 점수: {storedScore}");

            // 3. 주사위 싹 지우고 다시 랜덤 생성 (ProcessSpawnAndRoll이 ClearAllDice 포함)
            yield return StartCoroutine(ProcessSpawnAndRoll(false));

            // 4. 다시 검사
            DiceLogic.HandResult result = EvaluateCurrentHand();
            if (!result.isGlitch) keepGlitching = false;
        }

        GameData.Instance.isRolling = false;
        UIManager.Instance.UpdateButtons();
    }

    private IEnumerator ProcessSpawnAndRoll(bool isInitial)
    {
        // ★ 기존 주사위 싹 지우기 (사라지는 연출)
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();

        // ★ 랜덤 슬롯 뽑기
        List<int> slots = GetRandomSlots(dicePerRoll);
        foreach (int slot in slots)
        {
            int val = GetRandomDiceValue();
            GameData.Instance.currentDice.Add(new DiceData(slot, val));
        }

        // 매직 다이스 보정
        if (isInitial && GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Magic Dice"))
        {
            if (GameData.Instance.currentDice.Count > 0)
                GameData.Instance.currentDice[0].value = 6;
        }

        // ★ 실제 생성 (SpawnDice가 항상 새로 만듦)
        foreach (var dice in GameData.Instance.currentDice)
        {
            diceSpawner.SpawnDice(dice.slotIndex, dice.value, isInitial);
        }

        yield return new WaitForSeconds(0.3f); // 생성 후 잠깐 대기

        DiceEffectManager.ApplyAllDiceEffects();
        UIManager.Instance.UpdateAllUI();
    }

    // ============================================
    // ★ [핵심 수정] 리롤 시 랜덤 이동 로직
    // ============================================
    public void RerollSelectedDice()
    {
        if (GameData.Instance.rerollsLeft <= 0) return;
        List<DiceData> selected = GameData.Instance.currentDice.Where(d => d.isSelected).ToList();
        if (selected.Count == 0) return;

        StartCoroutine(RerollCoroutine(selected));
    }

    private IEnumerator RerollCoroutine(List<DiceData> toReroll)
    {
        bool isFreeReroll = false;
        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Lucky Coin") && Random.value < 0.1f) isFreeReroll = true;
        if (!isFreeReroll) GameData.Instance.rerollsLeft--;

        GameData.Instance.isRolling = true;
        UIManager.Instance.UpdateButtons();

        // 1. 선택된 주사위들을 일단 화면과 데이터에서 제거
        foreach (var d in toReroll)
        {
            diceSpawner.RemoveDice(d.slotIndex); // 화면에서 지움
            GameData.Instance.currentDice.Remove(d); // 데이터에서 뺌
        }

        // 2. 남은 주사위들이 차지한 자리 파악
        List<int> occupiedSlots = GameData.Instance.currentDice.Select(d => d.slotIndex).ToList();

        // 3. 지운 개수만큼 새로운 랜덤 자리에 생성
        foreach (var oldDice in toReroll)
        {
            int newSlot = GetAvailableSlot(occupiedSlots); // 빈자리 찾기
            occupiedSlots.Add(newSlot); // 예약

            int newVal = GetRandomDiceValue();

            // 데이터 추가
            DiceData newDiceData = new DiceData(newSlot, newVal);
            GameData.Instance.currentDice.Add(newDiceData);

            // ★ 새로운 자리에 생성!
            diceSpawner.SpawnDice(newSlot, newVal, false);

            yield return new WaitForSeconds(0.1f); // 하나씩 나오는 연출
        }

        // 4. 결과 계산
        DiceLogic.HandResult result = EvaluateCurrentHand();
        if (result.isGlitch) yield return StartCoroutine(GlitchRoutine());
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    private DiceLogic.HandResult EvaluateCurrentHand()
    {
        if (GameData.Instance.currentDice.Count == 0) return new DiceLogic.HandResult();

        DiceEffectManager.ApplyAllDiceEffects();
        DiceLogic.HandResult result = DiceLogic.AnalyzeDice(GameData.Instance.currentDice);

        GameData.Instance.feverMultiplier = result.multiplier;
        float glitchMult = (comboCount > 0) ? Mathf.Pow(1.5f, comboCount) : 1.0f;
        int calculatedScore = (int)(result.totalScore * result.multiplier * glitchMult);

        GameData.Instance.currentHandScore = calculatedScore;
        GameData.Instance.canSubmit = true;

        string displayName = result.handName + (comboCount > 0 ? $" (x{glitchMult:F1} Glitch!)" : "");
        UIManager.Instance.DisplayHandResult(displayName, result.multiplier);
        UIManager.Instance.UpdateAllUI();

        return result;
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

        int handScore = GameData.Instance.currentHandScore;
        GameData.Instance.AddScore(handScore);

        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Payback"))
        {
            int bonus = GameData.Instance.rerollsLeft * 2;
            if (bonus > 0) GameData.Instance.AddChips(bonus);
        }

        int chipsEarned = Mathf.Max(1, handScore / 100);
        GameData.Instance.AddChips(chipsEarned);

        GameData.Instance.canSubmit = false;
        GameData.Instance.currentHandScore = 0;

        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();
        ResetGlitchState();

        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.UpdateButtons();
        UIManager.Instance.ShowScorePopup(handScore);
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

    // ★ 빈 슬롯 찾기 함수
    private int GetAvailableSlot(List<int> used)
    {
        var avail = Enumerable.Range(0, totalSlots).Except(used).ToList();
        return avail.Count > 0 ? avail[Random.Range(0, avail.Count)] : 0;
    }
}