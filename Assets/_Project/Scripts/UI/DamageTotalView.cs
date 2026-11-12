using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 화면 오른쪽 위: 이번 턴(행동 + 지속피해 + 추가 공격)에 들어간 피해의 합계.
    // 맞을 때마다 숫자가 목표값까지 빠르게 올라가며(카운트업) 튀어서, 연쇄가 이어질수록 숫자가 커지는 게 보인다.
    // 값은 BattleFxView가 타격 박자를 재생할 때마다 넘겨 준다 (표시만 담당).
    public class DamageTotalView : MonoBehaviour
    {
        [Tooltip("통째로 켜고 끌 묶음 (CanvasGroup으로 서서히 사라짐)")]
        [SerializeField] private CanvasGroup group;

        [SerializeField] private Text titleText;
        [SerializeField] private Text amountText;

        [Tooltip("아군이 준 피해일 때 숫자 색")]
        [SerializeField] private Color allyDealtColor = new Color(1f, 0.85f, 0.35f);

        [Tooltip("적이 준 피해(아군이 받은 피해)일 때 숫자 색")]
        [SerializeField] private Color enemyDealtColor = new Color(1f, 0.45f, 0.4f);

        [Tooltip("숫자가 목표값을 따라가는 빠르기 (클수록 빨리 도착)")]
        [SerializeField, Min(0.1f)] private float countSpeed = 12f;

        [Tooltip("마지막 피해 뒤 사라지기 시작할 때까지(초)")]
        [SerializeField, Min(0f)] private float holdSeconds = 1.6f;

        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;

        private int total;
        private float shownAmount;
        private float punch;
        private float sinceLastHit;

        private void Awake()
        {
            group.alpha = 0f;
        }

        // 새 턴의 연출이 시작될 때: 합계를 0으로 되돌린다 (보이던 숫자는 그대로 사라지게 둔다).
        public void ResetTotal()
        {
            total = 0;
        }

        // 피해 하나를 더한다. alliesDealt: 아군이 준 피해인지(적이 맞음) → 숫자 색. isBig: 치명·처치처럼 크게 튈지.
        public void Add(int amount, bool alliesDealt, bool isBig)
        {
            if (amount <= 0)
            {
                return;
            }

            // 새 턴의 첫 피해면 표시 숫자도 0부터 다시 센다.
            if (total == 0)
            {
                shownAmount = 0f;
            }

            total += amount;
            amountText.color = alliesDealt ? allyDealtColor : enemyDealtColor;
            punch = isBig ? 0.35f : 0.18f;
            sinceLastHit = 0f;
            group.alpha = 1f;
        }

        // 매 프레임: 숫자를 목표값 쪽으로 올리고, 튄 크기를 되돌리고, 오래 피해가 없으면 서서히 사라진다.
        // unscaledDeltaTime: 히트스톱·슬로모션과 상관없이 실제 시간으로 움직인다 (멈춘 순간에도 숫자는 올라감).
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 1 - Exp(-속도 × 시간): 프레임 속도와 상관없이 일정한 비율로 목표에 다가가는 계수.
            shownAmount = Mathf.Lerp(shownAmount, total, 1f - Mathf.Exp(-countSpeed * dt));
            if (total - shownAmount < 1f)
            {
                shownAmount = total;
            }

            // "N0": 천 단위 쉼표 (123456 → 123,456). Mathf.RoundToInt: 가장 가까운 정수.
            amountText.text = Mathf.RoundToInt(shownAmount).ToString("N0");

            // 튄 크기는 매 프레임 줄어들어 1로 돌아온다.
            punch = Mathf.MoveTowards(punch, 0f, dt * 1.5f);
            amountText.transform.localScale = Vector3.one * (1f + punch);

            sinceLastHit += dt;
            if (sinceLastHit > holdSeconds && group.alpha > 0f)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt / fadeSeconds);
            }
        }
    }
}
