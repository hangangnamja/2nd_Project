using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 적(보스)이 스킬을 쓸 때 화면 위쪽에 "누가 · 어떤 스킬"을 잠깐 띄우는 이름 띠.
    // BattleFxView가 적의 스킬 행동이 시작될 때 Show를 부르고, LeadSeconds만큼 기다린 뒤 공격 모션을 재생한다
    // → 이름을 먼저 읽고 공격을 보게 한다. 띠는 공격이 진행되는 동안에도 조금 더 남았다가 사라진다.
    // 실제 시간(unscaledDeltaTime)으로 움직여 히트스톱·슬로모션의 영향을 받지 않는다.
    public class SkillNameBanner : MonoBehaviour
    {
        [Tooltip("통째로 투명도를 조절할 묶음")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("살짝 커지며 나타날 띠")]
        [SerializeField] private RectTransform band;

        [SerializeField] private Text casterNameText;
        [SerializeField] private Text skillNameText;

        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.12f;

        [Tooltip("나타난 뒤 공격 모션이 시작되기까지 기다리는 시간")]
        [SerializeField, Min(0f)] private float leadHoldSeconds = 0.35f;

        [Tooltip("공격 모션이 시작된 뒤에도 띠가 남아 있는 시간")]
        [SerializeField, Min(0f)] private float tailHoldSeconds = 0.6f;

        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.2f;

        private Coroutine playing;

        // 띠가 나타나 공격 모션이 시작될 때까지의 시간 (BattleFxView가 이만큼 기다린다).
        public float LeadSeconds => fadeInSeconds + leadHoldSeconds;

        private void Awake()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        // 이름 띠를 띄운다. 이미 떠 있으면(연달아 쓰는 스킬) 처음부터 다시 재생한다.
        public void Show(string casterName, string skillName)
        {
            casterNameText.text = casterName;
            skillNameText.text = skillName;

            if (playing != null)
            {
                StopCoroutine(playing);
            }

            playing = StartCoroutine(PlayRoutine());
        }

        // 나타남(투명 → 불투명, 1.15배 → 1배) → 머묾 → 사라짐.
        private IEnumerator PlayRoutine()
        {
            yield return Animate(fadeInSeconds, t =>
            {
                group.alpha = t;
                band.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, 1f - (1f - t) * (1f - t));
            });

            yield return new WaitForSecondsRealtime(leadHoldSeconds + tailHoldSeconds);
            yield return Animate(fadeOutSeconds, t => group.alpha = 1f - t);

            playing = null;
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다. Mathf.Clamp01은 진행도를 0~1로 자른다.
        private static IEnumerator Animate(float seconds, System.Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            apply(1f);
        }
    }
}
