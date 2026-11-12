using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using Game.Battle.Status;
using UnityEngine;

namespace Game.UI
{
    // 전투 임시 연출: 맞은 유닛에 타격 이펙트 + 피해 숫자, 회복받은 유닛에 회복 이펙트 + 회복량, 버프받은 유닛에 버프 이펙트 + 버프 이름.
    //
    // 실행 흐름
    //  1) 전투 로직은 행동 하나(스킬 → 턴 종료 → 추가 공격)를 한 번에 끝까지 계산하면서 연출용 사건을 알린다
    //  2) 이 컴포넌트는 사건을 받은 순서대로 "박자(beat)"에 담아 두고, 박자마다 간격을 두고 재생한다
    //     → 같은 프레임에 계산된 일이라도 "타격 → 지속피해 → 추가 공격 → 버프" 같은 발동 순서가 눈에 보인다
    //  3) BattleManager는 IsPlaying이 false가 될 때까지 다음 턴을 기다린다
    // 이펙트 모양은 파티클 프리팹(머티리얼 HDR 색 + Bloom)에 있고, 이 코드는 언제·어디서 재생할지만 정한다.
    public class BattleFxView : MonoBehaviour, IBattlePresentation
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private BattleStageView stageView;

        [Header("이펙트 (파티클 프리팹, 재생이 끝나면 스스로 지워지게 설정)")]
        [SerializeField] private ParticleSystem hitFx;
        [SerializeField] private ParticleSystem criticalHitFx;
        [SerializeField] private ParticleSystem damageOverTimeFx;
        [SerializeField] private ParticleSystem healFx;
        [SerializeField] private ParticleSystem buffFx;

        [Header("숫자")]
        [SerializeField] private DamagePopup popupPrefab;
        [SerializeField] private Color damageColor = Color.white;
        [SerializeField] private Color criticalColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color damageOverTimeColor = new Color(1f, 0.55f, 0.3f);
        [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private Color buffColor = new Color(0.5f, 0.85f, 1f);
        [SerializeField] private Color blockedColor = new Color(0.7f, 0.7f, 0.7f);

        [Header("위치 (캡슐 중심 기준)")]
        [SerializeField] private float effectHeight;
        [SerializeField] private float popupHeight = 0.8f;

        [Tooltip("이펙트·숫자를 카메라 쪽으로 당기는 거리. 캡슐 몸에 가려지지 않게 한다")]
        [SerializeField] private float towardCamera = 1.2f;

        [Header("타이밍")]
        [Tooltip("박자 하나를 재생한 뒤 다음 박자까지의 간격(초)")]
        [SerializeField, Min(0f)] private float beatInterval = 0.3f;

        [Tooltip("마지막 박자 뒤에 숫자를 읽을 시간(초)")]
        [SerializeField, Min(0f)] private float endHold = 0.3f;

        private enum FxKind
        {
            Hit,
            Heal,
            Buff,
        }

        // 재생할 연출 하나 (어떤 종류를, 누구에게, 무슨 값으로)
        private readonly struct FxEntry
        {
            public readonly FxKind Kind;
            public readonly BattleUnit Unit;
            public readonly DamageTakenInfo Damage;
            public readonly int HealAmount;
            public readonly string BuffName;

            // 기본값이 있는 매개변수: 종류마다 필요한 값만 넘기고 나머지는 비워 둔다 (예: 회복이면 healAmount만).
            public FxEntry(FxKind kind, BattleUnit unit, DamageTakenInfo damage = default, int healAmount = 0, string buffName = null)
            {
                Kind = kind;
                Unit = unit;
                Damage = damage;
                HealAmount = healAmount;
                BuffName = buffName;
            }
        }

        // 박자 목록. 박자 하나 = 동시에 재생할 연출 묶음 (예: 광역기에 맞은 적 전원).
        private readonly List<List<FxEntry>> beats = new List<List<FxEntry>>();
        private BattleEvents events;
        private bool isRunning;

        // 재생 중이거나 재생할 박자가 남아 있으면 true → BattleManager가 다음 턴을 미룬다.
        public bool IsPlaying => isRunning || beats.Count > 0;

        // BattleStarted는 BattleManager.Start에서 알린다. OnEnable은 씬의 모든 Start보다 먼저 불리므로 놓치지 않는다.
        private void OnEnable()
        {
            battleManager.BattleStarted += HandleBattleStarted;
        }

        private void OnDisable()
        {
            battleManager.BattleStarted -= HandleBattleStarted;
            Unsubscribe();
        }

        // 전투가 시작되면 사건 통로가 생기므로, 그때 연출 사건을 구독하고 자신을 연출 재생기로 등록한다.
        private void HandleBattleStarted()
        {
            events = battleManager.Events;
            events.DamageTaken += HandleDamageTaken;
            events.Healed += HandleHealed;
            events.BuffApplied += HandleBuffApplied;
            battleManager.SetPresentation(this);
        }

        private void Unsubscribe()
        {
            if (events == null)
            {
                return;
            }

            events.DamageTaken -= HandleDamageTaken;
            events.Healed -= HandleHealed;
            events.BuffApplied -= HandleBuffApplied;
            events = null;
        }

        private void HandleDamageTaken(DamageTakenInfo info)
        {
            Enqueue(new FxEntry(FxKind.Hit, info.Target, damage: info));
        }

        private void HandleHealed(BattleUnit target, int amount)
        {
            Enqueue(new FxEntry(FxKind.Heal, target, healAmount: amount));
        }

        private void HandleBuffApplied(BattleUnit target, StatusInstance buff)
        {
            Enqueue(new FxEntry(FxKind.Buff, target, buffName: buff.Data.DisplayName));
        }

        // 연출을 박자에 담는다. 마지막 박자와 종류가 같고 그 박자에 같은 유닛이 없으면 같은 박자에 넣는다.
        //  - 광역기로 적 3명이 맞음 → 한 박자에 동시 재생
        //  - 같은 적이 화상 3개로 3번 맞음 → 박자 3개로 하나씩 재생 (발동 횟수가 보이도록)
        //  - 타격 뒤 버프 → 종류가 달라 다음 박자 (발동 순서가 보이도록)
        //  - 한 스킬이 같은 유닛에게 버프 여러 개 → 한 박자, 버프 이름을 한 글자 묶음으로 합침 (니콜 3스 치명 확률·가속, 수나 3스 4개)
        private void Enqueue(FxEntry entry)
        {
            List<FxEntry> last = beats.Count > 0 ? beats[beats.Count - 1] : null;

            // 버프는 효과마다 하나씩 따로 보여주면 같은 스킬의 버프가 박자를 나눠 따로따로 떠서 느리다 → 같은 유닛의 버프 글자에 이어 붙인다.
            // FindIndex(조건): 조건이 맞는 첫 원소의 위치, 없으면 -1.
            if (last != null && last[0].Kind == FxKind.Buff && entry.Kind == FxKind.Buff)
            {
                int sameUnit = last.FindIndex(other => other.Unit == entry.Unit);
                if (sameUnit >= 0)
                {
                    FxEntry merged = last[sameUnit];
                    last[sameUnit] = new FxEntry(FxKind.Buff, merged.Unit, buffName: $"{merged.BuffName}\n{entry.BuffName}");
                    return;
                }
            }

            // Exists(조건): 조건(람다식)이 true인 원소가 하나라도 있으면 true → 이미 그 유닛의 연출이 있는 박자인지 확인
            bool canJoin = last != null
                && last[0].Kind == entry.Kind
                && !last.Exists(other => other.Unit == entry.Unit);

            if (canJoin)
            {
                last.Add(entry);
            }
            else
            {
                beats.Add(new List<FxEntry> { entry });
            }

            if (!isRunning)
            {
                StartCoroutine(PlayBeats());
            }
        }

        // 쌓인 박자를 앞에서부터 하나씩 재생한다. 재생 중에 새 박자가 들어와도 이어서 재생한다.
        private IEnumerator PlayBeats()
        {
            isRunning = true;

            // 사건은 로직 계산 도중에 들어오므로, 한 프레임 기다려 이번 행동의 사건이 모두 모인 뒤 시작한다.
            // yield return null: 다음 프레임까지 멈춘다.
            yield return null;

            while (beats.Count > 0)
            {
                List<FxEntry> beat = beats[0];
                beats.RemoveAt(0);

                foreach (FxEntry entry in beat)
                {
                    Play(entry);
                }

                yield return new WaitForSeconds(beatInterval);
            }

            yield return new WaitForSeconds(endHold);
            isRunning = false;
        }

        // 연출 하나를 재생한다: 유닛 위치에 파티클 + 머리 위에 숫자·글자.
        // 퇴장해서 숨겨진 유닛(하이퍼캐리 변신 중 아군)은 건너뛴다.
        private void Play(FxEntry entry)
        {
            UnitView view = stageView.FindView(entry.Unit);
            if (view == null || !view.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector3 basePosition = view.transform.position;
            switch (entry.Kind)
            {
                case FxKind.Hit:
                    PlayHit(entry.Damage, basePosition);
                    break;
                case FxKind.Heal:
                    SpawnEffect(healFx, basePosition);
                    SpawnPopup(basePosition, $"+{entry.HealAmount}", healColor, 1f);
                    break;
                case FxKind.Buff:
                    SpawnEffect(buffFx, basePosition);
                    SpawnPopup(basePosition, entry.BuffName, buffColor, 0.7f);
                    break;
            }
        }

        // 피해 종류에 맞는 이펙트와 숫자를 고른다.
        //  - 무적: 회색 "무적" / 지속피해: 주황 작은 숫자 / 치명: 노란 큰 숫자 + "!" + 큰 이펙트 / 그 외: 흰 숫자
        //  - 보호막이 흡수한 양이 있으면 숫자 아래 줄에 함께 적는다
        private void PlayHit(DamageTakenInfo damage, Vector3 basePosition)
        {
            if (damage.IsBlocked)
            {
                SpawnEffect(hitFx, basePosition);
                SpawnPopup(basePosition, damage.BlockedLabel, blockedColor, 0.8f);
                return;
            }

            string shieldText = damage.Absorbed > 0 ? $"\n(보호막 {damage.Absorbed})" : string.Empty;

            if (damage.IsDamageOverTime)
            {
                SpawnEffect(damageOverTimeFx, basePosition);
                SpawnPopup(basePosition, $"{damage.Amount}{shieldText}", damageOverTimeColor, 0.8f);
            }
            else if (damage.IsCritical)
            {
                SpawnEffect(criticalHitFx, basePosition);
                SpawnPopup(basePosition, $"{damage.Amount}!{shieldText}", criticalColor, 1.4f);
            }
            else
            {
                SpawnEffect(hitFx, basePosition);
                SpawnPopup(basePosition, $"{damage.Amount}{shieldText}", damageColor, 1f);
            }
        }

        // 파티클 프리팹을 유닛 몸 높이에 하나 만든다. 재생이 끝나면 프리팹 설정(Stop Action = Destroy)으로 스스로 지워진다.
        private void SpawnEffect(ParticleSystem prefab, Vector3 basePosition)
        {
            if (prefab == null)
            {
                return;
            }

            Instantiate(prefab, basePosition + Vector3.up * effectHeight + GetTowardCamera(), Quaternion.identity, transform);
        }

        // 머리 위에 숫자·글자를 띄운다.
        private void SpawnPopup(Vector3 basePosition, string message, Color color, float scale)
        {
            if (popupPrefab == null)
            {
                return;
            }

            // 연달아 뜨는 숫자가 정확히 겹치지 않도록 좌우로 조금 흩뜨린다. Random.Range(a, b): a~b 사이 무작위 실수.
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            Vector3 position = basePosition + Vector3.up * popupHeight + jitter + GetTowardCamera();
            DamagePopup popup = Instantiate(popupPrefab, position, Quaternion.identity, transform);
            popup.Show(message, color, scale);
        }

        // 카메라 쪽으로 towardCamera만큼 당기는 이동량. -forward: 카메라가 바라보는 방향의 반대 = 카메라 쪽.
        private Vector3 GetTowardCamera()
        {
            Camera mainCamera = Camera.main;
            return mainCamera != null ? -mainCamera.transform.forward * towardCamera : Vector3.zero;
        }
    }
}
