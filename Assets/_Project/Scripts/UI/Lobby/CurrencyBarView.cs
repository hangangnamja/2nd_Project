using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 로비 우상단 재화 표시 (보석·골드): 세이브의 실제 보유량을 보여준다 (인벤토리 재료 탭의 골드·보석과 같은 값).
    // 화면이 켜질 때마다 다시 읽고, 켜져 있는 동안 인벤토리(재화 포함)가 바뀌면 바로 갱신한다.
    public class CurrencyBarView : MonoBehaviour
    {
        [SerializeField] private Text gemText;
        [SerializeField] private Text goldText;

        // 이벤트 구독·해제: 켜져 있는 동안만 재화 변경 알림(InventoryChanged)을 받는다 (꺼진 화면을 고치지 않게).
        private void OnEnable()
        {
            PlayerProfile.InventoryChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerProfile.InventoryChanged -= Refresh;
        }

        // {N0}: 천 단위 쉼표를 넣은 정수 표기 (예: 40,000).
        private void Refresh()
        {
            gemText.text = PlayerProfile.GetGems().ToString("N0");
            goldText.text = PlayerProfile.GetGold().ToString("N0");
        }
    }
}
