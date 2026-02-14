using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DropSlot : MonoBehaviour, IDropHandler
{
    [Header("슬롯 정보")]
    public int slotIndex;          // 코드가 부여하는 슬롯의 고유 번호
    public Image highlightImage;   // 색칠할 자식 오브젝트 (Highlight)

    [Header("현재 상태")]
    public GameObject diceObject;  // 이 슬롯 위에 생성된 실제 주사위 오브젝트
    private DiceData currentDice;  // 이 슬롯에 할당된 데이터

    // ★ 색상 설정: 0.7f로 투명도를 조절하여 검은 배경에서도 선명하게 보이게 함
    private Color buffColor = new Color(1f, 1f, 0f, 0.7f); // 선명한 노랑
    private Color nerfColor = new Color(1f, 0f, 0f, 0.7f); // 선명한 빨강
    private Color clearColor = new Color(0f, 0f, 0f, 0f);  // 완전 투명

    void Awake()
    {
        // 1. 인스펙터에서 연결 안했을 경우를 대비해 자동으로 'Highlight' 자식을 찾음
        if (highlightImage == null)
        {
            Transform child = transform.Find("Highlight");
            if (child != null) highlightImage = child.GetComponent<Image>();
        }

        // 2. 초기 상태는 항상 투명하게 설정
        ResetHighlight();
    }

    // --- 색상 관리 함수 ---

    public void SetSlotColor(Color color)
    {
        if (highlightImage != null) highlightImage.color = color;
    }

    public void SetBuffHighlight() => SetSlotColor(buffColor);
    public void SetNerfHighlight() => SetSlotColor(nerfColor);
    public void ResetHighlight() => SetSlotColor(clearColor);
    public void ResetColor() => ResetHighlight();

    // --- 주사위 상호작용 함수 ---

    public void OnDrop(PointerEventData eventData)
    {
        // 드래그 중인 오브젝트를 가져옴
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            DraggableDice draggable = droppedObject.GetComponent<DraggableDice>();
            if (draggable != null)
            {
                // 데이터 업데이트 및 위치 이동
                DiceData dice = draggable.GetDiceData();
                GameData.Instance.MoveDice(dice, slotIndex);

                // 효과 재계산 및 UI 전체 새로고침 (이때 색깔도 다시 칠해짐)
                DiceEffectManager.ApplyAllDiceEffects();
                UIManager.Instance.UpdateAllUI();
            }
        }
    }

    public void SetDice(DiceData dice)
    {
        currentDice = dice;
        // 주사위 시각화 로직 (필요시 추가)
        if (diceObject != null)
        {
            DraggableDice draggable = diceObject.GetComponent<DraggableDice>();
            if (draggable != null) draggable.Initialize(dice);
        }
    }

    public void ClearSlot()
    {
        currentDice = null;

        // ★ [중요] Destroy 에러 방지용 안전장치
        // 프로젝트 창의 원본(Asset)이 아니라 씬에 있는 복제본(Instance)일 때만 파괴
        if (diceObject != null)
        {
            if (diceObject.scene.name != null) // 씬에 존재하는 오브젝트인지 확인
            {
                Destroy(diceObject);
            }
            diceObject = null;
        }

        ResetHighlight();
    }

    public bool IsEmpty() => currentDice == null;
}