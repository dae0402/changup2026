using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    // ============================================
    // 인스펙터 설정
    // ============================================
    [Header("필수 연결")]
    public Transform boardParent;           // 슬롯들이 생성될 부모 패널
    public GameObject slotPrefab;           // 슬롯 프리팹
    public GameObject dicePrefab;           // 주사위 프리팹

    [Header("생성 개수")]
    public int rows = 3;                    // 행 (세로 줄 수)
    public int columns = 5;                 // 열 (가로 줄 수)

    [Header("리소스")]
    public Sprite[] diceSprites;

    // 내부 데이터
    private GameObject[] slots;
    private Dictionary<int, GameObject> activeDice = new Dictionary<int, GameObject>();

    void Start()
    {
        if (boardParent == null)
        {
            Debug.LogError("❌ Board Parent가 연결되지 않았습니다!");
            return;
        }
        CreateBoard();
    }

    void CreateBoard()
    {
        // 1. 기존 슬롯 청소
        foreach (Transform child in boardParent)
        {
            Destroy(child.gameObject);
        }

        int totalSlots = rows * columns;
        slots = new GameObject[totalSlots];

        // 2. Grid Layout Group 설정 (요청하신 값으로 자동 적용)
        GridLayoutGroup grid = boardParent.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = boardParent.gameObject.AddComponent<GridLayoutGroup>();

        // ★ [요청사항 적용] 셀 크기 100x100, 간격 X:15 Y:30 자동 설정
        grid.cellSize = new Vector2(100f, 100f);
        grid.spacing = new Vector2(15f, 30f);

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter; // 중앙 정렬

        // 3. 슬롯 생성
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject slot;
            if (slotPrefab != null)
            {
                slot = Instantiate(slotPrefab, boardParent);
            }
            else
            {
                slot = CreateDefaultSlot();
                slot.transform.SetParent(boardParent, false);
            }
            slot.name = $"Slot_{i}";
            slots[i] = slot;
        }

        Debug.Log("보드 생성 완료 (크기:100x100, 간격:15x30 적용됨)");
    }

    GameObject CreateDefaultSlot()
    {
        GameObject slot = new GameObject("Slot");
        Image img = slot.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // 어두운 배경

        Outline outline = slot.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.1f);
        outline.effectDistance = new Vector2(2, -2);

        return slot;
    }

    // ============================================
    // 주사위 생성
    // ============================================
    public void SpawnDice(int slotIndex, int value, bool isSpecial = false)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        if (activeDice.ContainsKey(slotIndex)) RemoveDice(slotIndex);

        GameObject dice;
        if (dicePrefab != null)
        {
            dice = Instantiate(dicePrefab, slots[slotIndex].transform);
        }
        else
        {
            dice = CreateDefaultDice(value);
            dice.transform.SetParent(slots[slotIndex].transform, false);
        }

        // 위치/크기 정중앙 고정
        dice.transform.localPosition = Vector3.zero;
        dice.transform.localScale = Vector3.one;

        // 배경 흰색 강제
        Image diceImg = dice.GetComponent<Image>();
        if (diceImg != null) diceImg.color = Color.white;

        SetupDiceVisual(dice, value);

        // 버튼 연결
        Button btn = dice.GetComponent<Button>();
        if (btn == null) btn = dice.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnDiceClicked(slotIndex));

        activeDice[slotIndex] = dice;
        UpdateDiceVisual(slotIndex, false);
    }

    GameObject CreateDefaultDice(int value)
    {
        GameObject dice = new GameObject($"Dice_{value}");
        Image bg = dice.AddComponent<Image>();
        bg.color = Color.white;

        RectTransform rect = dice.GetComponent<RectTransform>();
        // 슬롯(100)보다 약간 작게 (90)
        rect.sizeDelta = new Vector2(90f, 90f);
        rect.anchoredPosition = Vector2.zero;

        return dice;
    }

    void SetupDiceVisual(GameObject dice, int value)
    {
        if (diceSprites != null && diceSprites.Length >= 6)
        {
            Image img = dice.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = diceSprites[value - 1];
                img.color = Color.white;
            }
        }
        else
        {
            TextMeshProUGUI text = dice.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null)
            {
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(dice.transform, false);
                text = textObj.AddComponent<TextMeshProUGUI>();

                RectTransform textRect = text.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            text.text = value.ToString();
            text.fontSize = 60f; // 100크기에 맞춰 적절한 폰트 사이즈
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black; // 검은색 숫자
            text.fontStyle = FontStyles.Bold;
        }
    }

    public void UpdateDiceVisual(int slotIndex, bool isSelected)
    {
        if (!activeDice.ContainsKey(slotIndex)) return;
        GameObject dice = activeDice[slotIndex];

        Outline outline = dice.GetComponent<Outline>();
        if (outline == null) outline = dice.AddComponent<Outline>();

        if (isSelected)
        {
            outline.effectColor = Color.red;
            outline.effectDistance = new Vector2(4, -4);
            outline.enabled = true;
        }
        else
        {
            outline.enabled = false;
        }
    }

    public void RemoveDice(int slotIndex)
    {
        if (activeDice.ContainsKey(slotIndex))
        {
            if (activeDice[slotIndex] != null) Destroy(activeDice[slotIndex]);
            activeDice.Remove(slotIndex);
        }
    }

    public void ClearAllDice()
    {
        foreach (var dice in activeDice.Values)
        {
            if (dice != null) Destroy(dice);
        }
        activeDice.Clear();
    }

    void OnDiceClicked(int slotIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ToggleDiceSelection(slotIndex);
        }
    }
}