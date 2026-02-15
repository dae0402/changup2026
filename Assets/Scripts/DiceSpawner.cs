using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    [Header("필수 연결")]
    public Transform boardParent;
    public GameObject slotPrefab;
    public GameObject dicePrefab;

    [Header("생성 설정")]
    public int rows = 3;
    public int columns = 5;

    [Header("그리드 레이아웃")]
    public Vector2 cellSize = new Vector2(100f, 100f);
    public Vector2 spacing = new Vector2(15f, 30f);
    public int paddingLeft = 0;
    public int paddingTop = 0;

    [Header("리소스")]
    public Sprite[] diceSprites;

    private GameObject[] slots;

    void Start()
    {
        if (boardParent != null) CreateBoard();
    }

    public void CreateBoard()
    {
        foreach (Transform child in boardParent) Destroy(child.gameObject);
        if (UIManager.Instance != null) UIManager.Instance.allSlots.Clear();

        int totalSlots = rows * columns;
        slots = new GameObject[totalSlots];

        GridLayoutGroup grid = boardParent.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = boardParent.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.padding = new RectOffset(paddingLeft, 0, paddingTop, 0);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, boardParent);
            newSlot.name = $"Slot_{i}";
            slots[i] = newSlot;

            DropSlot slotScript = newSlot.GetComponent<DropSlot>();
            if (slotScript != null)
            {
                slotScript.slotIndex = i;
                if (UIManager.Instance != null) UIManager.Instance.allSlots.Add(slotScript);
            }
        }
    }

    // ★ 요청하신 코드: 기존 것 파괴 후 새로 생성
    public void SpawnDice(int slotIndex, int value, bool isSpecial = false)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

        Transform slotTransform = slots[slotIndex].transform;

        // 1. 기존 주사위가 있으면 무조건 파괴 (재활용 X -> 사라짐 연출 가능)
        if (slotTransform.childCount > 0)
        {
            foreach (Transform child in slotTransform)
            {
                Destroy(child.gameObject);
            }
        }

        // 2. 항상 새로운 주사위 생성
        GameObject dice = null;
        if (dicePrefab != null)
        {
            dice = Instantiate(dicePrefab, slotTransform);
        }
        else
        {
            dice = CreateDefaultDice(value, slotTransform);
        }

        // 3. DropSlot 스크립트 연결
        DropSlot currentSlot = slots[slotIndex].GetComponent<DropSlot>();
        if (currentSlot != null) currentSlot.diceObject = dice;

        // 4. 위치 및 크기 강제 초기화
        dice.transform.localPosition = Vector3.zero;
        dice.transform.localScale = Vector3.one;
        dice.transform.localRotation = Quaternion.identity;

        // 5. CanvasGroup 자동 추가 (드래그 에러 방지)
        CanvasGroup cg = dice.GetComponent<CanvasGroup>();
        if (cg == null) cg = dice.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;

        // 6. 시각 효과(이미지/숫자) 설정
        SetupDiceVisual(dice, value);

        // 7. 데이터 설정
        DraggableDice draggable = dice.GetComponent<DraggableDice>();
        if (draggable == null) draggable = dice.AddComponent<DraggableDice>();
        draggable.Initialize(new DiceData(slotIndex, value));

        // 8. 클릭 이벤트 연결
        Button btn = dice.GetComponent<Button>();
        if (btn == null) btn = dice.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnDiceClicked(slotIndex));

        // 9. 선택 효과 끄기
        UpdateDiceVisual(dice, false);
    }

    // 특정 슬롯 비우기 (리롤 전 이동 위해 필요)
    public void RemoveDice(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slots.Length)
        {
            Transform slotTransform = slots[slotIndex].transform;
            foreach (Transform child in slotTransform) Destroy(child.gameObject);

            DropSlot ds = slots[slotIndex].GetComponent<DropSlot>();
            if (ds != null) ds.diceObject = null;
        }
    }

    public void ClearAllDice()
    {
        for (int i = 0; i < slots.Length; i++) RemoveDice(i);
    }

    void SetupDiceVisual(GameObject dice, int value)
    {
        Image bgImg = dice.GetComponent<Image>();
        if (bgImg != null) bgImg.color = Color.white;

        if (diceSprites != null && value > 0 && value <= diceSprites.Length)
        {
            if (bgImg != null) bgImg.sprite = diceSprites[value - 1];
        }
        else
        {
            TextMeshProUGUI text = dice.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null)
            {
                GameObject textObj = new GameObject("ValueText");
                textObj.transform.SetParent(dice.transform, false);
                text = textObj.AddComponent<TextMeshProUGUI>();
                text.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                text.GetComponent<RectTransform>().anchorMax = Vector2.one;
                text.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            }
            text.text = value.ToString();
            text.color = Color.black;
            text.fontSize = 50f;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
        }
    }

    public void UpdateDiceVisual(GameObject dice, bool isSelected)
    {
        if (dice == null) return;
        Outline outline = dice.GetComponent<Outline>();
        if (outline == null) outline = dice.AddComponent<Outline>();

        outline.enabled = isSelected;
        if (isSelected)
        {
            outline.effectColor = Color.red;
            outline.effectDistance = new Vector2(4, -4);
        }
    }

    public void UpdateDiceVisual(int slotIndex, bool isSelected)
    {
        if (slotIndex >= 0 && slotIndex < slots.Length)
        {
            Transform slotTransform = slots[slotIndex].transform;
            if (slotTransform.childCount > 0)
                UpdateDiceVisual(slotTransform.GetChild(0).gameObject, isSelected);
        }
    }

    void OnDiceClicked(int slotIndex)
    {
        if (GameManager.Instance != null) GameManager.Instance.ToggleDiceSelection(slotIndex);
    }

    GameObject CreateDefaultDice(int value, Transform parent)
    {
        GameObject dice = new GameObject($"Dice_{value}");
        dice.transform.SetParent(parent, false);
        dice.AddComponent<Image>();
        return dice;
    }
}