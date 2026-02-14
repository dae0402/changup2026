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
    public Transform boardParent;           // 슬롯들이 생성될 부모 패널 (Grid가 붙을 곳)
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
            Debug.LogError("❌ Board Parent가 연결되지 않았습니다! 인스펙터를 확인하세요.");
            return;
        }
        CreateBoard();
    }

    public void CreateBoard()
    {
        // 1. 기존 슬롯 청소
        foreach (Transform child in boardParent)
        {
            Destroy(child.gameObject);
        }

        // 2. UIManager 리스트 초기화 (색칠 대상 명단 비우기)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.allSlots.Clear();
        }

        int totalSlots = rows * columns;
        slots = new GameObject[totalSlots];

        // 3. Grid Layout Group 자동 설정
        GridLayoutGroup grid = boardParent.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = boardParent.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = new Vector2(100f, 100f);
        grid.spacing = new Vector2(15f, 30f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        // 4. 슬롯 생성 및 번호 부여
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, boardParent);
            newSlot.name = $"Slot_{i}";
            slots[i] = newSlot;

            DropSlot slotScript = newSlot.GetComponent<DropSlot>();
            if (slotScript != null)
            {
                slotScript.slotIndex = i;
                // ★ UIManager 명단에 등록해야 색깔이 칠해집니다.
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.allSlots.Add(slotScript);
                }
            }
        }
        Debug.Log($"✅ 보드 생성 완료! (슬롯 {totalSlots}개)");
    }

    // ============================================
    // 주사위 생성 및 슬롯 연결
    // ============================================
    public void SpawnDice(int slotIndex, int value, bool isSpecial = false)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

        // 이미 해당 위치에 주사위가 있다면 안전하게 제거
        if (activeDice.ContainsKey(slotIndex)) RemoveDice(slotIndex);

        GameObject dice;
        // 주사위 인스턴스 생성 (복제본 생성)
        if (dicePrefab != null)
        {
            dice = Instantiate(dicePrefab, slots[slotIndex].transform);
        }
        else
        {
            dice = CreateDefaultDice(value);
            dice.transform.SetParent(slots[slotIndex].transform, false);
        }

        // ★ [핵심 수정] 슬롯 스크립트에게 생성된 주사위 오브젝트를 알려줌
        // 이렇게 해야 나중에 DropSlot.ClearSlot()에서 '원본'이 아닌 '복제본'을 파괴합니다.
        DropSlot currentSlotScript = slots[slotIndex].GetComponent<DropSlot>();
        if (currentSlotScript != null)
        {
            currentSlotScript.diceObject = dice;
        }

        // 위치 및 크기 정렬
        dice.transform.localPosition = Vector3.zero;
        dice.transform.localScale = Vector3.one;

        SetupDiceVisual(dice, value);

        // 클릭 이벤트 연결
        Button btn = dice.GetComponent<Button>();
        if (btn == null) btn = dice.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnDiceClicked(slotIndex));

        activeDice[slotIndex] = dice;
        UpdateDiceVisual(slotIndex, false);
    }

    public void RemoveDice(int slotIndex)
    {
        if (activeDice.ContainsKey(slotIndex))
        {
            // 1. 실제 주사위 오브젝트 파괴
            if (activeDice[slotIndex] != null)
            {
                Destroy(activeDice[slotIndex]);
            }
            activeDice.Remove(slotIndex);

            // 2. 슬롯의 참조 변수도 비워줌
            if (slots[slotIndex] != null)
            {
                DropSlot ds = slots[slotIndex].GetComponent<DropSlot>();
                if (ds != null) ds.diceObject = null;
            }
        }
    }

    public void ClearAllDice()
    {
        // 딕셔너리를 돌면서 모든 주사위 제거
        List<int> keys = new List<int>(activeDice.Keys);
        foreach (int key in keys)
        {
            RemoveDice(key);
        }
        activeDice.Clear();
    }

    void SetupDiceVisual(GameObject dice, int value)
    {
        // 주사위 배경색 흰색 강제
        Image bgImg = dice.GetComponent<Image>();
        if (bgImg != null) bgImg.color = Color.white;

        if (diceSprites != null && diceSprites.Length >= 6)
        {
            if (bgImg != null) bgImg.sprite = diceSprites[value - 1];
        }
        else
        {
            TextMeshProUGUI text = dice.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = value.ToString();
        }
    }

    public void UpdateDiceVisual(int slotIndex, bool isSelected)
    {
        if (!activeDice.ContainsKey(slotIndex)) return;
        GameObject dice = activeDice[slotIndex];

        Outline outline = dice.GetComponent<Outline>();
        if (outline == null) outline = dice.AddComponent<Outline>();

        outline.enabled = isSelected;
        if (isSelected)
        {
            outline.effectColor = Color.red;
            outline.effectDistance = new Vector2(4, -4);
        }
    }

    void OnDiceClicked(int slotIndex)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ToggleDiceSelection(slotIndex);
    }

    GameObject CreateDefaultDice(int value)
    {
        GameObject dice = new GameObject($"Dice_{value}");
        dice.AddComponent<Image>();
        RectTransform rect = dice.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 90f);
        return dice;
    }
}