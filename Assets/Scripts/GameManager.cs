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

        // 굴리기 로직 실행
        yield return StartCoroutine(ProcessSpawnAndRoll(true));

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

    private IEnumerator GlitchRoutine()
    {
        bool keepGlitching = true;
        while (keepGlitching)
        {
            yield return new WaitForSeconds(0.8f);
            storedScore += GameData.Instance.currentHandScore;
            comboCount++;

            yield return StartCoroutine(ProcessSpawnAndRoll(false));

            DiceLogic.HandResult result = EvaluateCurrentHand();
            if (!result.isGlitch) keepGlitching = false;
        }

        GameData.Instance.isRolling = false;
        UIManager.Instance.UpdateButtons();
    }

    private IEnumerator ProcessSpawnAndRoll(bool isInitial)
    {
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();

        // ★ 첫 굴리기일 때만 내 덱(주머니)을 흔들어서 섞어줍니다.
        if (isInitial)
        {
            GameData.Instance.ShuffleDeck(dicePerRoll);
        }

        List<int> slots = GetRandomSlots(dicePerRoll);
        foreach (int slot in slots)
        {
            int val = GetRandomDiceValue();

            // ★ 중요: 무조건 Normal이 아니라, 주머니에서 하나씩 꺼냅니다!
            string drawnDiceType = GameData.Instance.DrawDiceFromDeck(dicePerRoll);

            GameData.Instance.currentDice.Add(new DiceData(slot, val, drawnDiceType));
        }

        // 매직 다이스 보정
        if (isInitial && GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Magic Dice"))
        {
            if (GameData.Instance.currentDice.Count > 0)
                GameData.Instance.currentDice[0].value = 6;
        }

        // 실제 생성
        foreach (var dice in GameData.Instance.currentDice)
        {
            diceSpawner.SpawnDice(dice.slotIndex, dice.value, isInitial);
        }

        yield return new WaitForSeconds(0.3f);

        DiceEffectManager.ApplyAllDiceEffects();
        UIManager.Instance.UpdateAllUI();
    }

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

        foreach (var d in toReroll)
        {
            diceSpawner.RemoveDice(d.slotIndex);
            GameData.Instance.currentDice.Remove(d);
        }

        List<int> occupiedSlots = GameData.Instance.currentDice.Select(d => d.slotIndex).ToList();

        foreach (var oldDice in toReroll)
        {
            int newSlot = GetAvailableSlot(occupiedSlots);
            occupiedSlots.Add(newSlot);

            int newVal = GetRandomDiceValue();

            // ★ 리롤을 할 때도 버려진 자리만큼 다시 주머니에서 꺼냅니다!
            string newType = GameData.Instance.DrawDiceFromDeck(dicePerRoll);

            DiceData newDiceData = new DiceData(newSlot, newVal, newType);
            GameData.Instance.currentDice.Add(newDiceData);

            diceSpawner.SpawnDice(newSlot, newVal, false);

            yield return new WaitForSeconds(0.1f);
        }

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
            // 선택 효과도 색상 로직에 맞춰 갱신
            diceSpawner.UpdateDiceVisual(slotIndex, d);
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

    private int GetAvailableSlot(List<int> used)
    {
        var avail = Enumerable.Range(0, totalSlots).Except(used).ToList();
        return avail.Count > 0 ? avail[Random.Range(0, avail.Count)] : 0;
    }

    // 이펙트 재적용 후 비주얼 갱신용 도우미 함수 (DiceEffectManager에서 호출됨)
    public void RefreshAllDiceVisuals()
    {
        foreach (var diceData in GameData.Instance.currentDice)
        {
            diceSpawner.UpdateDiceVisual(diceData.slotIndex, diceData);
        }
    }
}