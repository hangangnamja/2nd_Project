using UnityEngine;

namespace Game.UI
{
    // 타격감용 시간 조절: 히트스톱(맞는 순간 아주 잠깐 멈춤)과 처치 슬로모션.
    // Time.timeScale: 게임 시간의 흐름 배율 (1 = 보통, 0 = 멈춤, 0.3 = 느리게). 연출·이동·대기(WaitForSeconds)가 모두 이 값을 따른다.
    // 여러 요청이 겹치면 더 강한 쪽(멈춤 > 슬로) 하나만 적용하고, 끝나면 1로 되돌린다.
    // 끝나는 시각은 Time.unscaledTime(배율과 무관한 실제 시간)으로 재서, 멈춘 동안에도 시간이 흘러 풀린다.
    // 일시정지(BattlePauseView)도 여기서 함께 관리한다: timeScale을 바꾸는 곳이 둘이면 서로 덮어쓰므로 한곳에 모았다.
    // 일시정지가 가장 우선이라, 정지 중에 히트스톱이 끝나도 1로 되돌리지 않는다.
    public class BattleTimeFx : MonoBehaviour
    {
        [Tooltip("슬로모션 때 시간 배율 (0.3 = 30% 속도)")]
        [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.3f;

        private float hitStopEndTime;
        private float slowMotionEndTime;

        // 이 컴포넌트가 timeScale을 바꿔 둔 상태인지. 아무 효과도 없을 때 다른 기능의 timeScale을 덮어쓰지 않으려고 기억한다.
        private bool isControlling;

        // 일시정지 중인지 (메뉴가 열려 있는 동안 true).
        public bool IsPaused { get; private set; }

        // 일시정지를 켜고 끈다. 바로 timeScale에 반영해, 같은 프레임 안에서도 멈춘다.
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Update();
        }

        // 맞는 순간 seconds(실제 시간) 동안 멈춘다. 이미 더 길게 남아 있으면 그대로 둔다.
        public void HitStop(float seconds)
        {
            hitStopEndTime = Mathf.Max(hitStopEndTime, Time.unscaledTime + seconds);
        }

        // seconds(실제 시간) 동안 느리게 흐른다 (처치 순간).
        public void SlowMotion(float seconds)
        {
            slowMotionEndTime = Mathf.Max(slowMotionEndTime, Time.unscaledTime + seconds);
        }

        // Update는 timeScale이 0이어도 매 프레임 불린다 → 멈춘 동안에도 끝나는 시각을 확인할 수 있다.
        private void Update()
        {
            if (IsPaused)
            {
                Time.timeScale = 0f;
                isControlling = true;
                return;
            }

            float now = Time.unscaledTime;
            bool isStopped = now < hitStopEndTime;
            bool isSlow = now < slowMotionEndTime;

            if (isStopped || isSlow)
            {
                Time.timeScale = isStopped ? 0f : slowMotionScale;
                isControlling = true;
            }
            else if (isControlling)
            {
                Time.timeScale = 1f;
                isControlling = false;
            }
        }

        // 씬을 나가거나 꺼질 때 멈춘 채로 남지 않도록 되돌린다.
        private void OnDisable()
        {
            IsPaused = false;
            if (isControlling)
            {
                Time.timeScale = 1f;
                isControlling = false;
            }
        }
    }
}
