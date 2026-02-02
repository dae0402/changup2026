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
    public GameOverDirector gameOverDirector; // 엔딩 연출용

    [Header("설정")]
    public int totalSlots = 15;
    public int dicePerRoll = 5;

    private int storedScore = 0;
    private int comboCount = 0;

    void Awake() { if (Instance == null) Instance = this; }

    void Start()
    {
        if (diceLogic == null) diceLogic = GetComponent<DiceLogic>();
        if (diceSpawner == null) diceSpawner = GetComponent<DiceSpawner>();
        if (gameOverDirector == null) gameOverDirector = GetComponent<GameOverDirector>();
    }

    // 새 게임
    public void StartNewGame()
    {
        GameData.Instance.ResetGame();
        ResetGlitchState();
        diceSpawner.ClearAllDice();
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.ShowGameScreen();
    }

    // 굴리기 버튼 (라운드 시작)
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

    // 메인 루틴 (턴 진행)
    private IEnumerator RollDiceCoroutine()
    {
        // ★ [수정됨] 타임 캡슐 체크 (GetAllActiveUpgrades 사용)
        bool hasTimeCapsule = GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Time Capsule");
        int baseRerolls = 3;

        // 타임 캡슐이 있으면 '초기화(3)'가 아니라 '기존 + 3'으로 누적됨
        if (hasTimeCapsule)
        {
            GameData.Instance.rerollsLeft += baseRerolls;
            Debug.Log("💊 타임 캡슐 발동! 리롤 횟수 이월됨.");
        }
        else
        {
            GameData.Instance.rerollsLeft = baseRerolls; // 없으면 3으로 초기화
        }

        GameData.Instance.StartNewTurn();

        // StartNewTurn에서 rerollsLeft를 초기화해버릴 수 있으므로 값 보정
        if (hasTimeCapsule) GameData.Instance.rerollsLeft -= baseRerolls;
        if (!hasTimeCapsule) GameData.Instance.rerollsLeft = 3;

        GameData.Instance.handsLeft--;
        GameData.Instance.isRolling = true;
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.UpdateButtons();

        // 주사위 생성
        yield return StartCoroutine(ProcessSpawnAndRoll(true));

        // 패 분석
        bool isGlitch = EvaluateCurrentHand();

        // 글리치면 자동 반복
        if (isGlitch) yield return StartCoroutine(GlitchRoutine());
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    // 글리치 루틴 (자동 반복)
    private IEnumerator GlitchRoutine()
    {
        bool keepGlitching = true;
        while (keepGlitching)
        {
            yield return new WaitForSeconds(0.5f);

            storedScore = GameData.Instance.totalScore;
            comboCount++;

            yield return StartCoroutine(ProcessSpawnAndRoll(false));

            bool isGlitch = EvaluateCurrentHand();
            if (!isGlitch) keepGlitching = false;
        }
        GameData.Instance.isRolling = false;
        UIManager.Instance.UpdateButtons();
    }

    // 생성 로직
    private IEnumerator ProcessSpawnAndRoll(bool isInitial)
    {
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();
        List<int> slots = GetRandomSlots(dicePerRoll);

        // 주사위 데이터 생성
        foreach (int slot in slots)
        {
            int val = GetRandomDiceValue();
            GameData.Instance.currentDice.Add(new DiceData(slot, val));
        }

        // ★ [수정됨] 매직 다이스 체크 (GetAllActiveUpgrades 사용)
        if (isInitial && GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Magic Dice"))
        {
            if (GameData.Instance.currentDice.Count > 0)
            {
                GameData.Instance.currentDice[0].value = 6;
                Debug.Log("🪄 매직 다이스 발동! 첫 주사위 6 고정");
            }
        }

        // 실제 화면에 생성
        foreach (var dice in GameData.Instance.currentDice)
        {
            diceSpawner.SpawnDice(dice.slotIndex, dice.value, isInitial);
        }

        yield return new WaitForSeconds(0.3f);
    }

    // 리롤 (Cheat) 로직
    public void RerollSelectedDice()
    {
        if (GameData.Instance.rerollsLeft <= 0) return;
        List<DiceData> selected = GameData.Instance.currentDice.Where(d => d.isSelected).ToList();
        if (selected.Count == 0) return;

        StartCoroutine(RerollCoroutine(selected));
    }

    private IEnumerator RerollCoroutine(List<DiceData> toReroll)
    {
        // ★ [수정됨] 럭키 코인 체크 (GetAllActiveUpgrades 사용)
        bool isFreeReroll = false;
        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Lucky Coin"))
        {
            if (Random.value < 0.1f) // 10%
            {
                isFreeReroll = true;
                Debug.Log("🍀 럭키 코인 발동! 공짜 리롤!");
            }
        }

        if (!isFreeReroll)
        {
            GameData.Instance.rerollsLeft--;
        }

        GameData.Instance.isRolling = true;
        UIManager.Instance.UpdateButtons();

        // 현재 자리 차지 중인 슬롯 파악
        List<int> occupied = GameData.Instance.currentDice
            .Where(d => !d.isSelected).Select(d => d.slotIndex).ToList();

        foreach (var d in toReroll)
        {
            diceSpawner.RemoveDice(d.slotIndex);
            GameData.Instance.currentDice.Remove(d);

            int newSlot = GetAvailableSlot(occupied);
            occupied.Add(newSlot); // 자리 차지함 표시
            int newVal = GetRandomDiceValue();

            GameData.Instance.currentDice.Add(new DiceData(newSlot, newVal));
            diceSpawner.SpawnDice(newSlot, newVal, false);
            yield return new WaitForSeconds(0.1f);
        }

        if (EvaluateCurrentHand()) yield return StartCoroutine(GlitchRoutine());
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    // 선택 토글
    public void ToggleDiceSelection(int slotIndex)
    {
        if (GameData.Instance.isRolling) return;
        var d = GameData.Instance.currentDice.Find(x => x.slotIndex == slotIndex);
        if (d != null)
        {
            d.isSelected = !d.isSelected;
            diceSpawner.UpdateDiceVisual(slotIndex, d.isSelected); // 빨간 테두리만 갱신
        }
    }

    // 점수 분석
    private bool EvaluateCurrentHand()
    {
        if (GameData.Instance.currentDice.Count == 0) return false;

        var result = diceLogic.AnalyzeDice(GameData.Instance.currentDice);
        float extra = diceLogic.CheckPositionBonus(GameData.Instance.currentDice) * diceLogic.CheckSpecialDiceBonus(GameData.Instance.currentDice) * GameData.Instance.feverMultiplier;

        int curScore = Mathf.RoundToInt(result.currentScore * extra);
        GameData.Instance.currentHandScore = curScore;
        GameData.Instance.totalScore = diceLogic.CalculateFinalGlitchScore(storedScore, curScore, comboCount);
        GameData.Instance.canSubmit = true;

        string name = result.name + (comboCount > 0 ? $" (x{Mathf.Pow(2, comboCount)})" : "");
        UIManager.Instance.DisplayHandResult(name, 0);
        UIManager.Instance.UpdateGamePanel();

        return result.isGlitch;
    }

    // ============================================
    // 점수 제출 (턴 종료)
    // ============================================
    public void SubmitHand()
    {
        // 1. 방어 코드: 이미 제출했으면 실행 안 함
        if (!GameData.Instance.canSubmit) return;

        // 2. 현재 점수 임시 저장
        int finalScore = GameData.Instance.totalScore;

        // ★ [수정됨] 페이백 체크 (GetAllActiveUpgrades 사용)
        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Payback"))
        {
            int bonus = GameData.Instance.rerollsLeft * 2;
            if (bonus > 0)
            {
                GameData.Instance.AddChips(bonus);
                Debug.Log($"💰 페이백 발동! +{bonus}칩");
            }
        }

        // 3. 돈 더하기
        GameData.Instance.AddMoney(finalScore);

        // 4. 제출 즉시 잠금 및 점수 초기화
        GameData.Instance.canSubmit = false;
        GameData.Instance.totalScore = 0;
        GameData.Instance.currentHandScore = 0;

        // 5. 보드 정리
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();
        ResetGlitchState();

        // 6. UI 갱신
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.UpdateButtons();
        UIManager.Instance.ShowScorePopup(finalScore);

        // 7. 승패 체크
        CheckWinCondition();
    }

    // 승리 및 게임오버 체크 로직
    private void CheckWinCondition()
    {
        // 1. 목표 금액 달성 (승리)
        if (GameData.Instance.wallet >= GameData.Instance.debt)
        {
            Debug.Log("🎉 승리! 빚을 모두 갚았습니다!");
        }
        // 2. 횟수 모두 소진 (패배)
        else if (GameData.Instance.handsLeft <= 0)
        {
            Debug.Log("💀 게임 오버... 엔딩 연출 시작");

            if (gameOverDirector != null)
            {
                gameOverDirector.TriggerGameOver();
            }
            else
            {
                Debug.LogWarning("GameOverDirector가 연결되지 않았습니다!");
            }
        }
    }

    // 유틸리티
    private List<int> GetRandomSlots(int count)
    {
        return Enumerable.Range(0, totalSlots).OrderBy(x => Random.value).Take(count).ToList();
    }

    private int GetRandomDiceValue()
    {
        var list = GameData.Instance.availableDiceValues;
        return list[Random.Range(0, list.Count)];
    }

    private int GetAvailableSlot(List<int> used)
    {
        var avail = Enumerable.Range(0, totalSlots).Except(used).ToList();
        return avail.Count > 0 ? avail[Random.Range(0, avail.Count)] : 0;
    }
}