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

    [Header("효과 색상 설정")]
    public Color buffColor = Color.yellow;
    public Color nerfColor = Color.red;
    public Color selectionColor = Color.cyan;

    [Header("효과 텍스트 설정")]
    public Vector2 effectTextOffset = new Vector2(0f, 60f);

    [Header("주사위 타입별 배경 색상")]
    public Color normalBgColor = Color.white;
    public Color buffSourceBgColor = new Color(0.7f, 0.9f, 1f);
    public Color nerfSourceBgColor = new Color(0.9f, 0.7f, 1f);

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

    public void SpawnDice(int slotIndex, int value, bool isSpecial = false)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

        Transform slotTransform = slots[slotIndex].transform;

        // ★ 유령 오브젝트 방지: 삭제할 때 확실히 부모 관계를 끊어버립니다.
        if (slotTransform.childCount > 0)
        {
            for (int i = slotTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = slotTransform.GetChild(i);
                if (child != null)
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }
        }

        GameObject dice = null;
        if (dicePrefab != null)
            dice = Instantiate(dicePrefab, slotTransform);
        else
            dice = CreateDefaultDice(value, slotTransform);

        DropSlot currentSlot = slots[slotIndex].GetComponent<DropSlot>();
        if (currentSlot != null) currentSlot.diceObject = dice;

        dice.transform.localPosition = Vector3.zero;
        dice.transform.localScale = Vector3.one;
        dice.transform.localRotation = Quaternion.identity;

        CanvasGroup cg = dice.GetComponent<CanvasGroup>();
        if (cg == null) cg = dice.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;

        SetupDiceVisual(dice, value);

        DraggableDice draggable = dice.GetComponent<DraggableDice>();
        if (draggable == null) draggable = dice.AddComponent<DraggableDice>();

        DiceData newData = new DiceData(slotIndex, value);
        draggable.Initialize(newData);

        Button btn = dice.GetComponent<Button>();
        if (btn == null) btn = dice.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnDiceClicked(slotIndex));

        UpdateDiceVisual(dice, newData);
    }

    public void RemoveDice(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slots.Length)
        {
            Transform slotTransform = slots[slotIndex].transform;

            // ★ 유령 오브젝트 방지
            for (int i = slotTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = slotTransform.GetChild(i);
                if (child != null)
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }

            DropSlot ds = slots[slotIndex].GetComponent<DropSlot>();
            if (ds != null) ds.diceObject = null;
        }
    }

    public void ClearAllDice()
    {
        if (slots == null) return;
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

    public void UpdateDiceVisual(GameObject dice, DiceData data)
    {
        // ★ 최후의 안전장치: dice가 유령 오브젝트인지 한 번 더 확인합니다.
        if (dice == null || data == null) return;

        Outline outline = dice.GetComponent<Outline>();
        if (outline == null) outline = dice.AddComponent<Outline>();

        outline.effectDistance = new Vector2(4, -4);

        if (data.isSelected)
        {
            outline.enabled = true;
            outline.effectColor = selectionColor;
        }
        else if (data.isBuffed || data.effectState == DiceEffectState.Buff)
        {
            outline.enabled = true;
            outline.effectColor = buffColor;
        }
        else if (data.isNerfed || data.effectState == DiceEffectState.Nerf)
        {
            outline.enabled = true;
            outline.effectColor = nerfColor;
        }
        else
        {
            outline.enabled = false;
        }

        Transform popupTransform = dice.transform.Find("EffectPopupText");
        TextMeshProUGUI popupText = null;

        bool showPopup = (data.isBuffed || data.isNerfed) && !string.IsNullOrEmpty(data.effectPopupText);

        if (showPopup)
        {
            if (popupTransform == null)
            {
                GameObject newPopupObj = new GameObject("EffectPopupText");
                popupTransform = newPopupObj.transform;
                popupTransform.SetParent(dice.transform, false);

                popupText = newPopupObj.AddComponent<TextMeshProUGUI>();

                popupText.alignment = TextAlignmentOptions.Center;
                popupText.fontSize = 30f;
                popupText.fontStyle = FontStyles.Bold;

                RectTransform rt = popupText.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = effectTextOffset;
                rt.sizeDelta = new Vector2(100f, 40f);
            }
            else
            {
                popupText = popupTransform.GetComponent<TextMeshProUGUI>();
            }

            // 팝업 트랜스폼이 유효한지 확인 후 적용
            if (popupTransform != null && popupText != null)
            {
                popupText.text = data.effectPopupText;
                popupText.color = data.isBuffed ? buffColor : nerfColor;
                popupTransform.gameObject.SetActive(true);
            }
        }
        else
        {
            if (popupTransform != null)
            {
                popupTransform.gameObject.SetActive(false);
            }
        }

        Image backgroundImage = dice.GetComponent<Image>();
        if (backgroundImage != null)
        {
            if (data.diceType == "Buff Dice" || data.diceType == "Time Dice" || data.diceType == "Ancient Dice" || data.diceType == "Comeback Dice" || data.diceType == "Spring Dice" || data.diceType == "Splash Dice")
            {
                backgroundImage.color = buffSourceBgColor;
            }
            else if (data.diceType == "Absorb Dice" || data.diceType == "Ice Dice")
            {
                backgroundImage.color = nerfSourceBgColor;
            }
            else
            {
                backgroundImage.color = normalBgColor;
            }
        }
    }

    public void UpdateDiceVisual(int slotIndex, DiceData data)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

        DropSlot ds = slots[slotIndex].GetComponent<DropSlot>();
        if (ds != null && ds.diceObject != null)
        {
            UpdateDiceVisual(ds.diceObject, data);
        }
        else
        {
            Transform slotTransform = slots[slotIndex].transform;
            if (slotTransform.childCount > 0)
            {
                Transform child = slotTransform.GetChild(slotTransform.childCount - 1);

                // ★ 문제 해결의 핵심! child가 유령 상태가 아닌지(null이 아닌지) 검사합니다.
                if (child != null)
                {
                    UpdateDiceVisual(child.gameObject, data);
                }
            }
        }
    }

    public void UpdateDiceVisual(int slotIndex, bool isSelected)
    {
        if (GameData.Instance == null) return;
        var data = GameData.Instance.currentDice.Find(d => d.slotIndex == slotIndex);
        if (data != null)
        {
            UpdateDiceVisual(slotIndex, data);
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