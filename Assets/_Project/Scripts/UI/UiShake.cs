using UnityEngine;

namespace Game.UI
{
    // UI 요소 하나를 잠깐 흔든다 (크게 맞은 아군 카드·보스 HP바).
    // 레이아웃 그룹 안의 칸도 흔들 수 있게, 매 프레임 "지난번에 더한 흔들림"을 빼서 원래 자리를 구한 뒤 새 흔들림을 더한다.
    // 실제 시간(unscaledDeltaTime)으로 움직여 히트스톱·슬로모션 중에도 흔들린다.
    public class UiShake : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 applied;
        private float strength;
        private float duration;
        private float remaining;

        // 이 요소를 strength 픽셀만큼 seconds 동안 흔든다. 이미 흔들리는 중이면 더 센 쪽으로 다시 시작한다.
        // 없으면 붙여서 쓴다: GetComponent ?? AddComponent 대신 Unity 오브젝트는 == null로 확인한다.
        public static void Shake(RectTransform target, float strength, float seconds)
        {
            if (target == null)
            {
                return;
            }

            UiShake shake = target.GetComponent<UiShake>();
            if (shake == null)
            {
                shake = target.gameObject.AddComponent<UiShake>();
            }

            shake.Begin(strength, seconds);
        }

        private void Begin(float newStrength, float seconds)
        {
            rect = (RectTransform)transform;

            // 남은 흔들림보다 센 것만 덮어쓴다 (광역기로 여러 번 맞아도 가장 센 것 하나).
            float current = duration > 0f ? strength * (remaining / duration) : 0f;
            if (newStrength < current)
            {
                return;
            }

            strength = newStrength;
            duration = Mathf.Max(0.01f, seconds);
            remaining = duration;
        }

        // LateUpdate: 레이아웃이 자리를 정한 뒤 흔들림을 더한다.
        private void LateUpdate()
        {
            if (rect == null)
            {
                return;
            }

            // 지난 프레임에 더한 흔들림을 빼서 원래 자리로 돌린다.
            rect.anchoredPosition -= applied;
            applied = Vector2.zero;

            if (remaining <= 0f)
            {
                return;
            }

            remaining -= Time.unscaledDeltaTime;

            // 남은 비율만큼 약해지는 무작위 흔들림. Random.insideUnitCircle: 반지름 1 원 안의 무작위 점.
            float ratio = Mathf.Clamp01(remaining / duration);
            applied = Random.insideUnitCircle * (strength * ratio);
            rect.anchoredPosition += applied;
        }

        private void OnDisable()
        {
            if (rect != null)
            {
                rect.anchoredPosition -= applied;
            }

            applied = Vector2.zero;
            remaining = 0f;
        }
    }
}
