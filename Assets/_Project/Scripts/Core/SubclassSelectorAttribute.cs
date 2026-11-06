using System;
using UnityEngine;

namespace Game.Core
{
    // [SerializeReference] 필드에 붙이면 Inspector에 "어떤 하위 클래스를 넣을지" 고르는 드롭다운을 보여준다.
    // 실제 드롭다운 그리기는 Editor 폴더의 SubclassSelectorDrawer가 담당한다.
    // PropertyAttribute: Inspector가 이 필드를 그릴 때 연결된 PropertyDrawer를 사용하도록 표시하는 기반 클래스.
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
