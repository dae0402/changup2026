using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
        UpdateBossUI(); // 보스 UI 갱신
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

        DiceLogic.HandResult result = EvaluateCurrentHandPreview();

        if (result.isGlitch)
        {
            StartCoroutine(PlayJuiceText($"글리치 발동!\n<size=70%>[{result.handName}]</size>", new Color(0.2f, 1f, 0.5f), 110f, 1.5f));
            yield return new WaitForSeconds(1.2f);

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
            storedScore = GameData.Instance.currentHandScore;
            comboCount++;
            yield return StartCoroutine(ProcessSpawnAndRoll(false));

            DiceLogic.HandResult result = EvaluateCurrentHandPreview();

            if (result.isGlitch)
            {
                StartCoroutine(PlayJuiceText($"연쇄 글리치!\n<size=70%>[{result.handName}]</size>", new Color(0.2f, 1f, 0.5f), 110f, 1.2f));
            }
            else
            {
                keepGlitching = false;
            }
        }

        GameData.Instance.isRolling = false;
        UIManager.Instance.UpdateButtons();
    }

    private IEnumerator ProcessSpawnAndRoll(bool isInitial)
    {
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();

        if (isInitial) GameData.Instance.ShuffleDeck(dicePerRoll);

        List<int> slots = GetRandomSlots(dicePerRoll);
        foreach (int slot in slots)
        {
            int val = GetRandomDiceValue();
            string drawnDiceType = GameData.Instance.DrawDiceFromDeck(dicePerRoll);
            GameData.Instance.currentDice.Add(new DiceData(slot, val, drawnDiceType));
        }

        if (isInitial && GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Magic Dice"))
        {
            if (GameData.Instance.currentDice.Count > 0)
                GameData.Instance.currentDice[0].value = 6;
        }

        foreach (var dice in GameData.Instance.currentDice)
        {
            diceSpawner.SpawnDice(dice.slotIndex, dice.value, isInitial);
        }

        yield return new WaitForSeconds(0.3f);

        DiceEffectManager.ApplyAllDiceEffects();
        UIManager.Instance.UpdateAllUI();
    }

    public void ToggleDiceSelection(int slotIndex)
    {
        var d = GameData.Instance.currentDice.Find(x => x.slotIndex == slotIndex);
        if (d != null)
        {
            d.isSelected = !d.isSelected;
            diceSpawner.UpdateDiceVisual(slotIndex, d);
        }
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
            string newType = GameData.Instance.DrawDiceFromDeck(dicePerRoll);

            DiceData newDiceData = new DiceData(newSlot, newVal, newType);
            GameData.Instance.currentDice.Add(newDiceData);
            diceSpawner.SpawnDice(newSlot, newVal, false);

            yield return new WaitForSeconds(0.1f);
        }

        DiceLogic.HandResult result = EvaluateCurrentHandPreview();

        if (result.isGlitch)
        {
            storedScore = GameData.Instance.currentHandScore;

            StartCoroutine(PlayJuiceText($"글리치 발동!\n<size=70%>[{result.handName}]</size>", new Color(0.2f, 1f, 0.5f), 110f, 1.5f));
            yield return new WaitForSeconds(1.2f);

            yield return StartCoroutine(GlitchRoutine());
        }
        else
        {
            GameData.Instance.isRolling = false;
            UIManager.Instance.UpdateButtons();
        }
    }

    private DiceLogic.HandResult EvaluateCurrentHandPreview()
    {
        if (GameData.Instance.currentDice.Count == 0) return new DiceLogic.HandResult();

        DiceEffectManager.ApplyAllDiceEffects();
        DiceLogic.HandResult result = DiceLogic.AnalyzeDice(GameData.Instance.currentDice);

        GameData.Instance.feverMultiplier = result.multiplier;
        float glitchMult = (comboCount > 0) ? Mathf.Pow(1.5f, comboCount) : 1.0f;
        int calculatedScore = (int)(result.totalScore * result.multiplier * glitchMult);

        GameData.Instance.currentHandScore = storedScore + calculatedScore;
        GameData.Instance.canSubmit = true;

        string displayName = result.handName + (comboCount > 0 ? $" (x{glitchMult:F1} Glitch!)" : "");
        UIManager.Instance.DisplayHandResult(displayName, result.multiplier);
        UIManager.Instance.UpdateAllUI();

        return result;
    }

    // =====================================================================
    // ★ 4단계 도파민 연출 시퀀스!
    // =====================================================================
    public void SubmitHand()
    {
        if (!GameData.Instance.canSubmit) return;
        StartCoroutine(SubmitSequenceCoroutine());
    }

    private IEnumerator SubmitSequenceCoroutine()
    {
        GameData.Instance.canSubmit = false;
        GameData.Instance.isRolling = true;
        UIManager.Instance.UpdateButtons();

        DiceLogic.HandResult result = DiceLogic.AnalyzeDice(GameData.Instance.currentDice);
        float glitchMult = (comboCount > 0) ? Mathf.Pow(1.5f, comboCount) : 1.0f;
        float finalMult = result.multiplier * glitchMult;

        int rollingBaseScore = 0;
        GameData.Instance.currentHandScore = storedScore;
        UIManager.Instance.UpdateAllUI();

        // 1. 주사위 개별 합산 연출
        foreach (var die in GameData.Instance.currentDice)
        {
            if (GameData.Instance.isBossStage && GameData.Instance.currentBossName == "공허의 눈 (The Void)" && die.finalScore == 1) continue;

            diceSpawner.HighlightDice(die.slotIndex, true);
            rollingBaseScore += die.totalScoreCalculated;
            GameData.Instance.currentHandScore = storedScore + rollingBaseScore;
            UIManager.Instance.UpdateAllUI();

            yield return new WaitForSeconds(0.2f);
            diceSpawner.HighlightDice(die.slotIndex, false);
        }

        // 연출 후 점수 동기화
        rollingBaseScore = result.totalScore;
        GameData.Instance.currentHandScore = storedScore + rollingBaseScore;
        UIManager.Instance.UpdateAllUI();

        // 2. 거대한 족보 이름 팝업 연출
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(PlayJuiceText(result.handName, new Color(1f, 0.8f, 0f), 130f, 1.5f));

        // 3. 유물/아이템 순차적 발동 연출
        List<Item> activeUpgrades = GameData.Instance.GetAllActiveUpgrades();
        if (activeUpgrades.Count > 0)
        {
            yield return new WaitForSeconds(0.2f);
            long currentSum = GameData.Instance.currentDice.Sum(d => d.finalScore);

            foreach (var item in activeUpgrades)
            {
                if (GameData.Instance.isBossStage && GameData.Instance.currentBossName == "조용한 그림자 (Silent Shadow)" && item.type == ItemType.Dice) continue;

                bool shouldShowPopup = false;

                if (item.itemName == "Devil's Contract" || item.itemName == "Golden Scale" ||
                    item.itemName == "Mirror of Rank" || item.itemName == "Heavy Shackle" ||
                    item.itemName == "Artifact Collector" || item.itemName == "Dice Collector" ||
                    item.itemName == "Ancient Battery" || item.itemName == "Skill Scanner")
                {
                    shouldShowPopup = true;
                }
                else if (item.itemName == "Order Emblem" && result.handName.Contains("스트레이트")) shouldShowPopup = true;
                else if (item.itemName == "Glitch USB" && result.handName.Contains("스트레이트")) shouldShowPopup = true;
                else if (item.itemName == "Underdog's Hope" && currentSum <= 24) shouldShowPopup = true;
                else if (item.itemName == "Blackjack" && currentSum == 21) shouldShowPopup = true;

                if (shouldShowPopup)
                {
                    yield return StartCoroutine(PlayJuiceText($"[{item.itemName}]\n발동!", new Color(0.7f, 0.4f, 1f), 70f, 0.8f));
                    yield return new WaitForSeconds(0.15f);
                }
            }
        }

        // 4. 최종 배율 곱하기 & 카메라 타격감
        int finalHandScore = (int)(rollingBaseScore * finalMult);
        int totalFinalScore = storedScore + finalHandScore;

        GameData.Instance.currentHandScore = totalFinalScore;
        UIManager.Instance.UpdateAllUI();

        StartCoroutine(ShakeScreen(0.3f, 0.5f));
        yield return StartCoroutine(PlayJuiceText($"X {finalMult:F1}", Color.red, 160f, 1.2f));

        // 5. 최종 점수 및 동전 날아가기 (지갑 입금 연출)
        yield return StartCoroutine(PlayJuiceText($"+ {totalFinalScore} 점!", Color.green, 90f, 1.0f));

        int coinsToSpawn = Mathf.Clamp(totalFinalScore / 50, 3, 15);
        yield return StartCoroutine(PlayFlyingCoins(coinsToSpawn));

        // 6. 데이터 입금 및 후처리
        GameData.Instance.AddScore(totalFinalScore);
        GameData.Instance.AddMoney(totalFinalScore);

        if (GameData.Instance.GetAllActiveUpgrades().Exists(x => x.itemName == "Payback"))
        {
            int bonus = GameData.Instance.rerollsLeft * 2;
            if (bonus > 0) GameData.Instance.AddChips(bonus);
        }

        int chipsEarned = Mathf.Max(1, totalFinalScore / 100);
        GameData.Instance.AddChips(chipsEarned);

        GameData.Instance.currentHandScore = 0;
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();
        ResetGlitchState();

        GameData.Instance.isRolling = false;
        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.UpdateButtons();
        UIManager.Instance.ShowScorePopup(totalFinalScore);

        CheckWinCondition();
    }

    private TMP_FontAsset GetKoreanFont()
    {
        foreach (var t in FindObjectsOfType<TextMeshProUGUI>(true))
        {
            if (t.font != null && t.font.name != "LiberationSans SDF" && t.gameObject.name != "JuiceText")
            {
                if (t.text.Any(c => c >= 0xAC00 && c <= 0xD7A3))
                {
                    return t.font;
                }
            }
        }

        foreach (var t in FindObjectsOfType<TextMeshProUGUI>(true))
        {
            if (t.font != null && t.font.name != "LiberationSans SDF" && t.gameObject.name != "JuiceText" && t.gameObject.name != "CoinJuice" && t.gameObject.name != "BossStatusUI")
            {
                return t.font;
            }
        }
        return null;
    }

    private void UpdateBossUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existingUI = canvas.transform.Find("BossStatusUI");

        if (GameData.Instance.isBossStage)
        {
            GameObject bossUI;
            TextMeshProUGUI tmp;

            if (existingUI == null)
            {
                bossUI = new GameObject("BossStatusUI");
                bossUI.transform.SetParent(canvas.transform, false);
                tmp = bossUI.AddComponent<TextMeshProUGUI>();

                TMP_FontAsset kFont = GetKoreanFont();
                if (kFont != null) tmp.font = kFont;

                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;

                Outline outline = bossUI.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2, -2);

                RectTransform rt = bossUI.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -60f);
                rt.sizeDelta = new Vector2(800f, 150f);
            }
            else
            {
                bossUI = existingUI.gameObject;
                tmp = bossUI.GetComponent<TextMeshProUGUI>();
            }

            tmp.text = $"<color=#FF3333>⚠️ BOSS STAGE ⚠️</color>\n<size=80%>{GameData.Instance.currentBossName}</size>\n<color=#FFD700><size=60%>{GameData.Instance.currentBossDesc}</size></color>";
            tmp.fontSize = 50f;
            bossUI.SetActive(true);
        }
        else
        {
            if (existingUI != null) existingUI.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlayJuiceText(string text, Color color, float size, float duration)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) yield break;

        GameObject textObj = new GameObject("JuiceText");
        textObj.transform.SetParent(canvas.transform, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();

        TMP_FontAsset kFont = GetKoreanFont();
        if (kFont != null) tmp.font = kFont;

        tmp.text = text; tmp.color = color; tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4, -4);

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scale = 1f;
            if (t < 0.15f) scale = Mathf.Lerp(0f, 1.3f, t / 0.15f);
            else if (t < 0.3f) scale = Mathf.Lerp(1.3f, 1f, (t - 0.15f) / 0.15f);

            if (t > 0.7f)
            {
                float alpha = Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                tmp.color = new Color(color.r, color.g, color.b, alpha);
                outline.effectColor = new Color(0, 0, 0, alpha);
            }

            rt.localScale = new Vector3(scale, scale, 1f);
            rt.anchoredPosition = new Vector2(0, Mathf.Lerp(0, 150f, t));

            yield return null;
        }
        Destroy(textObj);
    }

    private IEnumerator PlayFlyingCoins(int count)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) yield break;

        TMP_FontAsset kFont = GetKoreanFont();

        for (int i = 0; i < count; i++)
        {
            GameObject coinObj = new GameObject("CoinJuice");
            coinObj.transform.SetParent(canvas.transform, false);
            TextMeshProUGUI tmp = coinObj.AddComponent<TextMeshProUGUI>();

            if (kFont != null) tmp.font = kFont;

            tmp.text = "C";
            tmp.color = new Color(1f, 0.84f, 0f);
            tmp.fontSize = 80f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            Outline outline = coinObj.AddComponent<Outline>();
            outline.effectColor = Color.black; outline.effectDistance = new Vector2(2, -2);

            RectTransform rt = coinObj.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;

            Vector2 targetPos = new Vector2(400f, 300f);

            StartCoroutine(MoveCoin(rt, Vector2.zero, targetPos, 0.6f));
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator MoveCoin(RectTransform rt, Vector2 start, Vector2 end, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float yOffset = Mathf.Sin(t * Mathf.PI) * 150f;
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.Lerp(start, end, t) + new Vector2(0, yOffset);
                rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.5f, t);
            }
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private IEnumerator ShakeScreen(float duration, float magnitude)
    {
        if (Camera.main == null) yield break;
        Vector3 originalPos = Camera.main.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            Camera.main.transform.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Camera.main.transform.localPosition = originalPos;
    }

    private void CheckWinCondition()
    {
        if (GameData.Instance.totalScore >= GameData.Instance.debt)
        {
            Debug.Log("🎉 목표 점수 달성! 스테이지 클리어!");

            if (GameData.Instance.isBossStage && GameData.Instance.currentBossName == "소매치기 고블린 (Pocket Thief)")
            {
                GameData.Instance.AddChips(8);
            }

            GameData.Instance.currentStage++;

            if (GameData.Instance.currentStage % 3 == 0)
            {
                GameData.Instance.isBossStage = true;
                GameData.Instance.debt += 100; // [테스트용] 보스 목표점수 100점 오름

                int randomBoss = Random.Range(0, 3);

                if (randomBoss == 0)
                {
                    GameData.Instance.currentBossName = "변덕쟁이 심판 (The Fickle)";
                    GameData.Instance.currentBossDesc = "모두 홀수/짝수면 배율 +0.5, 섞여있으면 -0.5";
                }
                else if (randomBoss == 1)
                {
                    GameData.Instance.currentBossName = "조용한 그림자 (Silent Shadow)";
                    GameData.Instance.currentBossDesc = "특수 주사위의 광역 이펙트(버프/너프)가 작동하지 않음";
                }
                else if (randomBoss == 2)
                {
                    GameData.Instance.currentBossName = "소매치기 고블린 (Pocket Thief)";
                    GameData.Instance.currentBossDesc = "시작 시 3칩을 훔쳐가고 클리어 시 8칩으로 돌려줌";

                    int stolen = Mathf.Min(GameData.Instance.chips, 3);
                    if (stolen > 0) GameData.Instance.chips -= stolen;
                }
            }
            else
            {
                GameData.Instance.isBossStage = false;
                GameData.Instance.currentBossName = "";
                GameData.Instance.currentBossDesc = "";
                GameData.Instance.debt += 100; // [테스트용] 일반 목표점수 100점 오름
            }

            GameData.Instance.totalScore = 0;
            GameData.Instance.handsLeft = GameData.Instance.maxHands;
            GameData.Instance.rerollsLeft = GameData.Instance.baseRerolls;

            if (UIManager.Instance != null)
            {
                if (GameData.Instance.isBossStage)
                    UIManager.Instance.ShowMessage("WARNING: BOSS STAGE!");
                else
                    UIManager.Instance.ShowMessage("STAGE CLEAR!");

                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.ResetShop();
                }

                // ★ [버그 픽스] 상점 띄우기 전에 보스 UI 강제 숨김 처리
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    Transform bossUI = canvas.transform.Find("BossStatusUI");
                    if (bossUI != null) bossUI.gameObject.SetActive(false);
                }

                UIManager.Instance.ShowShopScreen();
            }
        }
        else if (GameData.Instance.handsLeft <= 0)
        {
            Debug.Log("💀 목숨 소진... 게임 오버");
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

    public void RefreshAllDiceVisuals()
    {
        foreach (var diceData in GameData.Instance.currentDice)
        {
            diceSpawner.UpdateDiceVisual(diceData.slotIndex, diceData);
        }
    }

    public void StartNextRound()
    {
        diceSpawner.ClearAllDice();
        GameData.Instance.currentDice.Clear();
        ResetGlitchState();

        GameData.Instance.isRolling = false;
        GameData.Instance.canSubmit = false;

        UIManager.Instance.UpdateAllUI();
        UIManager.Instance.ShowGameScreen();
        UpdateBossUI(); // 보스 UI 갱신
    }
}