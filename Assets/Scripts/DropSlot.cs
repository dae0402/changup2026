using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DropSlot : MonoBehaviour, IDropHandler
{
    [Header("슬롯 정보")]
    public int slotIndex = -1;
    public GameObject diceObject;

    [Header("하이라이트 효과")]
    public Image highlightImage;

    // 색상 설정 (0.7f 알파값)
    private Color buffColor = new Color(1f, 1f, 0f, 0.7f);
    private Color nerfColor = new Color(1f, 0f, 0f, 0.7f);
    private Color clearColor = new Color(0f, 0f, 0f, 0f);

    void Awake()
    {
        // 하이라이트 이미지 찾기 또는 생성
        if (highlightImage == null)
        {
            Transform child = transform.Find("Highlight");
            if (child != null) highlightImage = child.GetComponent<Image>();
            else CreateHighlightImage();
        }

        // 마우스 클릭 가로채기 방지
        if (highlightImage != null) highlightImage.raycastTarget = false;

        ResetHighlight();
    }

    void CreateHighlightImage()
    {
        GameObject highlightObj = new GameObject("Highlight");
        highlightObj.transform.SetParent(transform, false);

        highlightImage = highlightObj.AddComponent<Image>();
        highlightImage.color = clearColor;
        highlightImage.raycastTarget = false; // ★ 필수: 주사위 클릭 방해 금지

        RectTransform rect = highlightImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // ★ [수정] SetAsFirstSibling() 제거! 
        // 배경 이미지보다 위에 그려지도록 그냥 둡니다. (주사위는 나중에 생성되니 자연스럽게 제일 위로 감)
    }

    // --- 색상 변경 ---
    public void SetSlotColor(Color color)
    {
        if (highlightImage != null)
        {
            highlightImage.color = color;
            highlightImage.enabled = true;
        }
    }

    public void SetBuffHighlight() => SetSlotColor(buffColor);
    public void SetNerfHighlight() => SetSlotColor(nerfColor);
    public void ResetHighlight() => SetSlotColor(clearColor);

    // DraggableDice 호환용
    public void SetBuffHighlight(bool isVisible, Color color)
    {
        if (isVisible) SetSlotColor(color);
        else ResetHighlight();
    }

    // --- 주사위 관리 ---
    public void SetDice(DiceData data)
    {
        if (diceObject != null)
        {
            var draggable = diceObject.GetComponent<DraggableDice>();
            if (draggable != null) draggable.Initialize(data);
        }
    }

    public void ClearSlot()
    {
        if (diceObject != null && diceObject.scene.name != null)
        {
            Destroy(diceObject);
            diceObject = null;
        }
        ResetHighlight();
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            DraggableDice draggable = droppedObject.GetComponent<DraggableDice>();
            if (draggable != null)
            {
                GameData.Instance.MoveDice(draggable.GetDiceData(), slotIndex);
                DiceEffectManager.ApplyAllDiceEffects();
                UIManager.Instance.UpdateAllUI();
            }
        }
    }
}