using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DropSlot : MonoBehaviour, IDropHandler
{
    public int slotIndex;
    private DiceData currentDice;
    public GameObject diceObject;
    private Image backgroundImage; // 슬롯의 배경 이미지
    private Color originalColor;   // 원래 색상 저장

    // ★ 버프/너프 색상 정의
    private Color buffColor = new Color(1f, 1f, 0f, 0.5f); // 노란색 (반투명)
    private Color nerfColor = new Color(1f, 0f, 0f, 0.5f); // 빨간색 (반투명)

    void Awake()
    {
        backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            originalColor = backgroundImage.color;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            DraggableDice draggable = droppedObject.GetComponent<DraggableDice>();
            if (draggable != null)
            {
                DiceData dice = draggable.GetDiceData();
                GameData.Instance.MoveDice(dice, slotIndex); // GameData에 MoveDice가 필요함
                UIManager.Instance.UpdateAllUI();
            }
        }
    }

    public void SetDice(DiceData dice)
    {
        currentDice = dice;
        if (diceObject == null)
        {
            // DiceManager가 없다면 Instantiate 부분을 확인해야 함 (보통 프리팹을 UIManager나 다른 곳에서 가져옴)
            // 여기서는 UIManager나 ShopManager의 프리팹을 참조하거나, DiceManager가 있다고 가정
            if (UIManager.Instance != null && UIManager.Instance.slotPrefab != null)
            {
                // 주의: 여기서는 주사위 알맹이 프리팹이 필요합니다. 
                // 만약 DiceManager가 없다면 UIManager에 주사위 프리팹을 추가해서 연결해야 합니다.
                // 일단 기존 코드를 유지합니다.
                // diceObject = Instantiate(DiceManager.Instance.dicePrefab, transform); 
            }
        }

        // (참고) 주사위 생성 로직은 기존 프로젝트 상황에 맞춰야 합니다.
        // DraggableDice 컴포넌트 설정
        if (diceObject != null)
        {
            DraggableDice draggable = diceObject.GetComponent<DraggableDice>();
            if (draggable != null) draggable.Initialize(dice);
        }
    }

    public void ClearSlot()
    {
        currentDice = null;
        if (diceObject != null)
        {
            Destroy(diceObject);
            diceObject = null;
        }
        ResetHighlight();
    }

    public bool IsEmpty() => currentDice == null;

    // ★ 버프 하이라이트 (노란색)
    public void SetBuffHighlight()
    {
        if (backgroundImage != null) backgroundImage.color = buffColor;
    }

    // ★ 너프 하이라이트 (빨간색)
    public void SetNerfHighlight()
    {
        if (backgroundImage != null) backgroundImage.color = nerfColor;
    }

    // ★ 하이라이트 초기화 (원래 색으로)
    public void ResetHighlight()
    {
        if (backgroundImage != null) backgroundImage.color = originalColor;
    }
}