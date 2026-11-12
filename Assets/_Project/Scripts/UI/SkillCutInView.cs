using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 3스(필살기) 컷인: 화면을 가로지르는 띠 위로 캐릭터 일러스트와 스킬 이름이 미끄러져 들어왔다가 나간다.
    // BattleFxView가 3스 행동이 시작될 때 Play를 부르고, 컷인 길이(Duration)만큼 다음 연출을 기다린다.
    // 실제 시간(unscaledDeltaTime)으로 움직여 히트스톱·슬로모션의 영향을 받지 않는다.
    public class SkillCutInView : MonoBehaviour
    {
        [Tooltip("통째로 켜고 끌 묶음")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("가로로 미끄러지는 띠 (일러스트·이름의 부모)")]
        [SerializeField] private RectTransform band;

        [SerializeField] private Image illustration;
        [SerializeField] private Text skillNameText;
        [SerializeField] private Text casterNameText;

        [Tooltip("띠가 화면 밖에서 들어오는 거리 (픽셀)")]
        [SerializeField] private float slideDistance = 1400f;

        [SerializeField, Min(0.01f)] private float inSeconds = 0.18f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float outSeconds = 0.15f;

        private Coroutine playing;

        // 컷인 전체 길이 (BattleFxView가 이만큼 기다린 뒤 돌진·타격을 재생한다).
        public float Duration => inSeconds + holdSeconds + outSeconds;

        private void Awake()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        // 컷인을 재생한다. art가 없으면(일러스트 없는 캐릭터) 이름만 보여준다.
        public void Play(Sprite art, string casterName, string skillName)
        {
            illustration.sprite = art;
            illustration.enabled = art != null;
            casterNameText.text = casterName;
            skillNameText.text = skillName;

            if (playing != null)
            {
                StopCoroutine(playing);
            }

            playing = StartCoroutine(PlayRoutine());
        }

        // 오른쪽 밖 → 가운데(감속) → 잠시 머묾(살짝 계속 흐름) → 왼쪽 밖(가속).
        private IEnumerator PlayRoutine()
        {
            group.alpha = 1f;

            yield return Slide(slideDistance, 0f, inSeconds, t => 1f - (1f - t) * (1f - t));
            yield return Slide(0f, -60f, holdSeconds, t => t);
            yield return Slide(-60f, -slideDistance, outSeconds, t => t * t);

            group.alpha = 0f;
            playing = null;
        }

        // 띠의 가로 위치를 from → to로 옮긴다. ease: 0~1 진행도에 가속·감속을 입히는 함수(람다).
        private IEnumerator Slide(float from, float to, float seconds, System.Func<float, float> ease)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float x = Mathf.Lerp(from, to, ease(Mathf.Clamp01(elapsed / seconds)));
                band.anchoredPosition = new Vector2(x, band.anchoredPosition.y);
                yield return null;
            }
        }
    }
}
