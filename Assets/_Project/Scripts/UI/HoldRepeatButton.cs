using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    // 누르면 한 번, 계속 누르고 있으면 반복해서 Pressed를 알리는 버튼 (육성 창 재료: "홀드하면 연속으로 추가").
    // IPointerDownHandler 등: EventSystem이 이 오브젝트(또는 Graphic)를 누르고 뗄 때 해당 함수를 자동으로 부른다.
    // 반복 간격은 실제 시간(unscaledDeltaTime)으로 잰다.
    public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("누른 뒤 반복이 시작되기까지 기다리는 시간 (초)")]
        [SerializeField, Min(0f)] private float repeatDelay = 0.35f;

        [Tooltip("반복 간격 (초). 오래 누를수록 이 값까지 점점 빨라진다")]
        [SerializeField, Min(0.01f)] private float fastestInterval = 0.03f;

        [SerializeField, Min(0.01f)] private float startInterval = 0.12f;

        // 눌렸을 때·반복될 때마다 알린다. event Action: 구독한 함수를 인자 없이 부른다.
        public event Action Pressed;

        private bool isHeld;
        private float timer;
        private float interval;

        public void OnPointerDown(PointerEventData eventData)
        {
            isHeld = true;
            timer = repeatDelay;
            interval = startInterval;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isHeld = false;
        }

        // 누른 채로 버튼 밖으로 나가면 반복을 멈춘다.
        public void OnPointerExit(PointerEventData eventData)
        {
            isHeld = false;
        }

        private void OnDisable()
        {
            isHeld = false;
        }

        // 누르고 있는 동안 timer가 0이 될 때마다 한 번 알리고, 간격을 조금씩 줄인다 (Mathf.Max: 최소 간격 아래로는 안 줄임).
        private void Update()
        {
            if (!isHeld)
            {
                return;
            }

            timer -= Time.unscaledDeltaTime;
            if (timer > 0f)
            {
                return;
            }

            Pressed?.Invoke();
            interval = Mathf.Max(fastestInterval, interval * 0.85f);
            timer = interval;
        }
    }
}
