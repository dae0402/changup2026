using UnityEngine;
using UnityEngine.EventSystems;

public class ItemHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Item targetItem; // 이 카드의 아이템 정보

    // 마우스가 들어왔을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ShopManager.Instance != null && targetItem != null)
        {
            // 상점 매니저에게 툴팁을 띄우라고 신호 보냄
            ShopManager.Instance.ShowTooltip(targetItem, transform.position);
        }
    }

    // 마우스가 나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (ShopManager.Instance != null)
        {
            // 상점 매니저에게 툴팁 끄라고 신호 보냄
            ShopManager.Instance.HideTooltip();
        }
    }
}