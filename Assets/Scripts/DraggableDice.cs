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

    // ★ 색상 정의 (UIManager와 동일하게)
    private Color buffColor = new Color(1f, 1f, 0f, 0.7f); // 노란색
    private Color nerfColor = new Color(1f, 0f, 0f, 0.7f); // 빨간색

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

    // ★ [수정됨] 드래그 중인 위치 아래의 슬롯을 찾아 범위 표시
    private void ShowBuffNerfRange(PointerEventData eventData)
    {
        ResetAllHighlights();

        DropSlot targetSlot = null;
        if (UIManager.Instance != null)
        {
            foreach (var slot in UIManager.Instance.allSlots)
            {
                // 마우스가 슬롯 위에 있는지 확인
                if (RectTransformUtility.RectangleContainsScreenPoint(
                    slot.GetComponent<RectTransform>(),
                    eventData.position,
                    parentCanvas.worldCamera))
                {
                    targetSlot = slot;
                    break;
                }
            }
        }

        if (targetSlot != null && diceData != null)
        {
            // ★ 주사위 타입별 범위 표시
            string typeName = diceData.diceType.Trim().ToLower();
            int targetIndex = targetSlot.slotIndex;

            // 버프 계열: 십자(+) 범위 노란색
            if (typeName == "buff dice" || typeName == "mirror dice" || typeName == "chameleon dice")
            {
                PaintNeighborsPreview(targetIndex, "cross", buffColor);
            }
            // 스프링 주사위: 십자(+) 노랑색 + 대각선 4칸 빨간색
            else if (typeName == "spring dice")
            {
                PaintNeighborsPreview(targetIndex, "cross", buffColor);
                PaintNeighborsPreview(targetIndex, "3x3_outer", nerfColor);
            }
            // 일반 주사위: 자기 자신만 하이라이트
            else
            {
                targetSlot.SetBuffHighlight(true, new Color(1f, 1f, 1f, 0.3f)); // 연한 흰색
            }
        }
    }

    // ★ 가로 5칸 그리드 기준으로 주변 인덱스를 찾아 색칠하는 함수
    void PaintNeighborsPreview(int centerIndex, string shape, Color color)
    {
        if (UIManager.Instance == null) return;

        int columns = 5; // 가로 5칸 기준
        int row = centerIndex / columns;
        int col = centerIndex % columns;

        foreach (var slot in UIManager.Instance.allSlots)
        {
            if (slot == null) continue;

            int slotIdx = slot.slotIndex;
            int r = slotIdx / columns;
            int c = slotIdx % columns;
            bool isTarget = false;

            // 자기 자신 슬롯은 색칠에서 제외
            if (slotIdx == centerIndex) continue;

            if (shape == "cross")
            {
                // 상하좌우 1칸 거리 체크
                if ((r == row && Mathf.Abs(c - col) == 1) || (c == col && Mathf.Abs(r - row) == 1))
                    isTarget = true;
            }
            else if (shape == "3x3")
            {
                // 주변 8칸 모두 체크
                if (Mathf.Abs(r - row) <= 1 && Mathf.Abs(c - col) <= 1)
                    isTarget = true;
            }
            else if (shape == "3x3_outer")
            {
                // 3x3 범위(주변 8칸) 중 상하좌우를 제외한 대각선 4칸만 체크
                bool in3x3 = Mathf.Abs(r - row) <= 1 && Mathf.Abs(c - col) <= 1;
                bool isCross = (r == row || c == col);
                if (in3x3 && !isCross) isTarget = true;
            }

            if (isTarget)
            {
                slot.SetBuffHighlight(true, color);
            }
        }
    }
}