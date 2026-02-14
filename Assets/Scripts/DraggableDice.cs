using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class DraggableDice : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private DiceData diceData;
    public Image diceImage;
    public TextMeshProUGUI valueText;
    public TextMeshProUGUI typeText;

    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Canvas parentCanvas;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void Initialize(DiceData data)
    {
        diceData = data;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (diceData == null) return;

        // ★ [수정됨] 기본 눈금 대신, 계산된 최종 점수를 표시
        // 아직 계산 전(0점)이라면 기본 눈금 표시
        int displayScore = (diceData.totalScoreCalculated > 0) ? diceData.totalScoreCalculated : diceData.value;

        if (valueText != null)
        {
            valueText.text = displayScore.ToString();

            // 점수 변화에 따른 색상 변경 (시각적 피드백)
            if (displayScore > diceData.value) valueText.color = Color.green;      // 버프됨
            else if (displayScore < diceData.value) valueText.color = Color.red;   // 너프됨
            else valueText.color = Color.white;                                    // 기본
        }

        if (typeText != null) typeText.text = diceData.diceType;

        string iconName = GetIconNameByType(diceData.diceType);
        Sprite icon = Resources.Load<Sprite>($"DiceIcons/{iconName}");
        if (icon != null && diceImage != null)
        {
            diceImage.sprite = icon;
            diceImage.color = Color.white;
        }
    }

    string GetIconNameByType(string type)
    {
        // 파일 이름 규칙에 맞춰 수정하세요
        switch (type)
        {
            case "Fire Dice": return "FireIcon";
            case "Ice Dice": return "IceIcon";
            default: return "NormalIcon";
        }
    }

    public DiceData GetDiceData() { return diceData; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        transform.SetParent(parentCanvas.transform, true); // 캔버스 최상위로 이동
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false; // 드래그 중에는 클릭 통과

        // 드래그 시작 시 원래 있던 슬롯의 하이라이트 끄기
        DropSlot currentSlot = originalParent.GetComponent<DropSlot>();
        if (currentSlot != null) currentSlot.ResetHighlight();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;

        // ★ 드래그 중 범위 표시 (DropSlot과 연동)
        ShowBuffNerfRange(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(originalParent, true);
        rectTransform.anchoredPosition = Vector2.zero;
        canvasGroup.blocksRaycasts = true;

        ResetAllHighlights();
    }

    // 모든 슬롯 하이라이트 끄기
    private void ResetAllHighlights()
    {
        if (UIManager.Instance == null) return;
        foreach (var slot in UIManager.Instance.allSlots)
        {
            slot.ResetHighlight();
        }
    }

    // 드래그 중인 위치 아래의 슬롯을 찾아 범위 표시
    private void ShowBuffNerfRange(PointerEventData eventData)
    {
        ResetAllHighlights();

        DropSlot targetSlot = null;
        if (UIManager.Instance != null)
        {
            foreach (var slot in UIManager.Instance.allSlots)
            {
                // 마우스가 슬롯 위에 있는지 확인
                if (RectTransformUtility.RectangleContainsScreenPoint(slot.GetComponent<RectTransform>(), eventData.position, parentCanvas.worldCamera))
                {
                    targetSlot = slot;
                    break;
                }
            }
        }

        if (targetSlot != null && diceData != null)
        {
            // 여기에 주사위별 범위 로직 적용 (버프: 노랑 / 너프: 빨강)
            // (간단 예시: 자기 자신만 버프)
            targetSlot.SetBuffHighlight();

            // 만약 십자 범위 등을 원하시면 아까 드린 긴 코드를 적용하면 됩니다.
        }
    }
}