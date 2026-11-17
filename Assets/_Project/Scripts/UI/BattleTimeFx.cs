using System;
using UnityEngine;

namespace Game.UI
{
    // 전투 시간 조절: 배속, 히트스톱(맞는 순간 아주 잠깐 멈춤), 처치 슬로모션, 일시정지.
    // Time.timeScale: 게임 시간의 흐름 배율 (1 = 보통, 0 = 멈춤, 2 = 두 배 빠르게). 연출·이동·대기(WaitForSeconds)가 모두 이 값을 따른다.
    // 우선순위: 일시정지(0) > 히트스톱(0) > 슬로모션(슬로 배율 × 배속) > 평소(배속).
    // 끝나는 시각은 Time.unscaledTime(배율과 무관한 실제 시간)으로 재서, 멈춘 동안에도 시간이 흘러 풀린다.
    // timeScale을 바꾸는 곳이 여럿이면 서로 덮어쓰므로, 배속·일시정지(BattlePauseView)까지 모두 여기서 관리한다.
    public class BattleTimeFx : MonoBehaviour
    {
        // 배속 설정을 기억하는 PlayerPrefs 키 (기기에 남는 개인 설정이라 세이브 파일이 아니라 PlayerPrefs에 둔다).
        private const string SpeedPrefsKey = "Battle.SpeedIndex";

        [Tooltip("슬로모션 때 시간 배율 (0.3 = 30% 속도). 배속이 켜져 있으면 배속을 곱한다")]
        [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.3f;

        [Tooltip("배속 버튼을 누를 때마다 차례로 바뀌는 배율 (첫 칸이 기본)")]
        [SerializeField] private float[] speedSteps = { 1f, 2f, 3f };

        private float hitStopEndTime;
        private float slowMotionEndTime;
        private int speedIndex;

        // 일시정지 중인지 (메뉴가 열려 있는 동안 true).
        public bool IsPaused { get; private set; }

        // 지금 배속 (1 = 보통).
        public float Speed => speedSteps.Length > 0 ? speedSteps[speedIndex] : 1f;

        // 배속이 바뀌었을 때 (버튼 글자 갱신용). Action<새 배속>
        public event Action<float> SpeedChanged;

        private void Awake()
        {
            // 지난 전투에서 고른 배속을 불러온다. Mathf.Clamp: 칸 수가 바뀌어도 범위 밖 번호가 되지 않게 0 ~ 마지막 칸으로 자른다.
            speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SpeedPrefsKey, 0), 0, Mathf.Max(0, speedSteps.Length - 1));
        }

        // 다음 배속으로 넘긴다 (1x → 2x → 3x → 1x …). %: 마지막 칸 다음은 처음으로 돌아온다.
        public void CycleSpeed()
        {
            if (speedSteps.Length == 0)
            {
                return;
            }

            speedIndex = (speedIndex + 1) % speedSteps.Length;
            PlayerPrefs.SetInt(SpeedPrefsKey, speedIndex);
            Apply();

            // ?.Invoke: 구독한 화면(배속 버튼)이 있을 때만 새 배속을 알린다.
            SpeedChanged?.Invoke(Speed);
        }

        // 일시정지를 켜고 끈다. 바로 timeScale에 반영해, 같은 프레임 안에서도 멈춘다.
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Apply();
        }

        // 맞는 순간 멈춘다. seconds는 1배속 기준 실제 시간이라 배속만큼 짧게 줄인다 (빠르게 볼 때 멈춤이 상대적으로 길어 보이지 않게).
        // 이미 더 길게 남아 있으면 그대로 둔다.
        public void HitStop(float seconds)
        {
            hitStopEndTime = Mathf.Max(hitStopEndTime, Time.unscaledTime + seconds / Speed);
        }

        // 처치 순간 느리게 흐른다. 히트스톱과 같은 이유로 배속만큼 짧게 줄인다.
        public void SlowMotion(float seconds)
        {
            slowMotionEndTime = Mathf.Max(slowMotionEndTime, Time.unscaledTime + seconds / Speed);
        }

        // Update는 timeScale이 0이어도 매 프레임 불린다 → 멈춘 동안에도 끝나는 시각을 확인할 수 있다.
        private void Update()
        {
            Apply();
        }

        // 지금 상태에 맞는 timeScale을 정한다 (우선순위는 클래스 위 설명).
        private void Apply()
        {
            if (IsPaused)
            {
                Time.timeScale = 0f;
                return;
            }

            float now = Time.unscaledTime;
            if (now < hitStopEndTime)
            {
                Time.timeScale = 0f;
            }
            else if (now < slowMotionEndTime)
            {
                Time.timeScale = slowMotionScale * Speed;
            }
            else
            {
                Time.timeScale = Speed;
            }
        }

        // 씬을 나가거나 꺼질 때 배속·멈춤이 로비까지 남지 않도록 1로 되돌린다.
        private void OnDisable()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
