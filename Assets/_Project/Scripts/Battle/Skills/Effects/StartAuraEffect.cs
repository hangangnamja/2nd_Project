using System;
using Game.Core;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 기간 오라 생성 (아스트라 3스 "공연"): 시전자가 정해진 턴 동안 같은 편 전원에게 스탯 보정을 주는 오라를 만든다.
    // 오라는 버프 칸이 아니라 시전자에게 붙어 있어, 강탈·소모되지 않고 시전자가 퇴장해도 유지된다 (기획서 3-7 오라).
    // 이미 오라를 생성하고 있으면 새 오라로 바꾼다 (한 유닛이 동시에 여러 기간 오라를 만들지 않음).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/기간 오라", "정해진 턴 동안 같은 편 전원에게 스탯 보정을 주는 오라를 만든다. 버프 칸 밖이라 강탈·소모되지 않음 (아스트라 공연)", 530)]
    [Serializable]
    public class StartAuraEffect : SkillEffect
    {
        [Label("오라 이름")]
        [SerializeField] private string auraName = "공연";

        [Tooltip("같은 편 전원에게 주는 스탯 보정 (예: 치명확률 +20%, 피해 증가 +30%)")]
        [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();

        [Tooltip("오라를 유지하는 시전자의 턴 수 (생성한 턴은 세지 않음)")]
        [Label("지속 턴", Min = 1f)]
        [SerializeField] private int durationTurns = 3;

        [Tooltip("체크하면 오라가 켜져 있는 동안 시전자의 턴이 강제로 스킵된다 (공연)")]
        [Label("주인 턴 스킵")]
        [SerializeField] private bool skipOwnerTurns = true;

        [Tooltip("시전자 머리 위에 보여줄 표시용 효과 (아이콘·툴팁)")]
        [Label("표시용 효과")]
        [SerializeField] private StatusEffectData displayStatus;

        [Tooltip("오라가 켜져 있는 동안 전장에 띄울 이펙트 프리팹 (예: 흐르는 악보·음표)")]
        [Label("전장 이펙트")]
        [SerializeField] private GameObject fieldEffectPrefab;

        public override void Apply(SkillContext context)
        {
            context.Caster.StartAura(new TimedAura(auraName, statModifiers, durationTurns, skipOwnerTurns, displayStatus, fieldEffectPrefab));
            string skipText = skipOwnerTurns ? ", 그동안 행동 스킵" : string.Empty;
            context.Log($"  {context.Caster.Name}: {auraName} 시작 ({durationTurns}턴{skipText})");
        }
    }
}
