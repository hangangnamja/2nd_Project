using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 육성 창의 경험치 재료 칸 하나: 아이콘(누르면 +1, 홀드하면 연속 추가) + "고른 개수 / 보유" + 빼기 버튼.
    // GrowthPopup이 칸마다 재료와 개수를 채우고, 누름 알림(Added·Removed)을 받는다.
    public class GrowthMaterialSlot : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Text countText;
        [SerializeField] private Text expText;
        [SerializeField] private HoldRepeatButton addButton;
        [SerializeField] private HoldRepeatButton removeButton;

        // 칸을 누를 때(+1)·빼기를 누를 때(-1) 알린다.
        public event Action Added;
        public event Action Removed;

        private void Awake()
        {
            // 람다 () => Added?.Invoke(): 버튼 알림을 받아 이 칸의 알림으로 그대로 넘긴다.
            addButton.Pressed += () => Added?.Invoke();
            removeButton.Pressed += () => Removed?.Invoke();
        }

        // 재료 아이콘·등급 색·1개당 경험치를 채운다.
        public void SetMaterial(Sprite sprite, Color gradeColor, int experience)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            frame.color = gradeColor;
            expText.text = $"EXP {experience:N0}";
        }

        // 고른 개수 / 보유 개수. 보유가 무한(디버그)이면 ∞로 보여준다. 고른 게 없으면 빼기 버튼을 숨긴다.
        public void SetCount(int selected, int usable)
        {
            string usableText = usable == int.MaxValue ? "∞" : usable.ToString("N0");
            countText.text = $"{selected:N0}/{usableText}";
            removeButton.gameObject.SetActive(selected > 0);
        }
    }
}
