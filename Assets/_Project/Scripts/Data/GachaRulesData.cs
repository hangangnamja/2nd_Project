using UnityEngine;

namespace Game.Data
{
    // 가챠 공통 규칙 수치 (기획서 7-1). 배너마다 다르지 않으므로 에셋 하나를 모든 배너가 함께 쓴다.
    // 임시값(소프트 천장 증가폭 등)을 테스트하며 바꿀 수 있도록 코드가 아니라 에셋으로 뺐다.
    [CreateAssetMenu(fileName = "GachaRules", menuName = "Game/Gacha Rules")]
    public class GachaRulesData : ScriptableObject
    {
        [Header("기본 확률 (값은 비율: 0.006 = 0.6%)")]
        [SerializeField, Range(0f, 1f)] private float sRate = 0.006f;
        [SerializeField, Range(0f, 1f)] private float aRate = 0.09f;

        // 상위 무기는 캐릭터 구간(S·A) 밖의 "무기" 구간에서만 나온다 → 캐릭터 확률·천장은 그대로이고 무기에는 천장이 없다.
        [Tooltip("5성 무기 확률 (0.01 = 1%). 천장 없음")]
        [SerializeField, Range(0f, 1f)] private float fiveStarWeaponRate = 0.01f;

        [Tooltip("4성 무기 확률 (0.1 = 10%). 천장 없음")]
        [SerializeField, Range(0f, 1f)] private float fourStarWeaponRate = 0.1f;

        [Header("S 천장")]
        [Tooltip("S 없이 이 횟수째 뽑기는 S 확정")]
        [SerializeField, Min(1)] private int hardPity = 90;

        [Tooltip("S 없이 이 횟수째부터 S 확률이 오르기 시작")]
        [SerializeField, Min(1)] private int softPityStart = 71;

        [Tooltip("소프트 천장 구간에서 1회마다 S 확률에 더하는 값 (0.04 = +4%p)")]
        [SerializeField, Range(0f, 1f)] private float softPityStep = 0.04f;

        [Header("A 확정")]
        [Tooltip("A 이상 없이 이 횟수째 뽑기는 A 확정")]
        [SerializeField, Min(1)] private int aGuaranteeInterval = 10;

        [Header("픽업")]
        [Tooltip("픽업 확정이 아닐 때 S가 픽업일 확률 (반천)")]
        [SerializeField, Range(0f, 1f)] private float pickupChance = 0.5f;

        [Header("비용·지급 (임시)")]
        [Tooltip("1회 뽑기 보석 (기획서 8장: 1뽑 = 보석 100개)")]
        [SerializeField, Min(0)] private int gemsPerPull = 100;

        [Tooltip("새로 얻은 캐릭터의 레벨 (기본 Lv1, 이후 전투원 화면에서 육성)")]
        [SerializeField, Range(1, 60)] private int obtainedLevel = 1;

        [Tooltip("체크하면 보석 없이 뽑을 수 있다 (기획서 2장: 심사자용 디버그 모드 재화 무한)")]
        [SerializeField] private bool freePulls = true;

        public int HardPity => hardPity;
        public float ARate => aRate;
        public float FiveStarWeaponRate => fiveStarWeaponRate;
        public float FourStarWeaponRate => fourStarWeaponRate;
        public int AGuaranteeInterval => aGuaranteeInterval;
        public float PickupChance => pickupChance;
        public int GemsPerPull => gemsPerPull;
        public int ObtainedLevel => obtainedLevel;
        public bool FreePulls => freePulls;

        // S 없이 pullNumber번째(1부터) 뽑기의 S 확률.
        // 예 (기본값): 1~70번째 0.6% → 71번째 0.6% + 4%p = 4.6% → 72번째 8.6% … → 90번째 100%
        public float GetSRate(int pullNumber)
        {
            if (pullNumber >= hardPity)
            {
                return 1f;
            }

            if (pullNumber >= softPityStart)
            {
                // 소프트 천장 구간에 들어온 지 몇 번째인지(71번째 = 1)만큼 증가폭을 더한다. Mathf.Min으로 100%를 넘지 않게 자른다.
                int stepsIntoSoftPity = pullNumber - softPityStart + 1;
                return Mathf.Min(1f, sRate + softPityStep * stepsIntoSoftPity);
            }

            return sRate;
        }
    }
}
