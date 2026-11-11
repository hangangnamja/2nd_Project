using UnityEngine;

namespace Game.Core
{
    // Inspector에 필드 이름(영어 변수명) 대신 보여줄 한국어 이름을 붙인다. 설명은 [Tooltip]을 함께 쓰면 마우스를 올렸을 때 나온다.
    // 숫자 칸은 Min·Max도 여기서 정한다: Unity는 필드 하나에 PropertyDrawer를 하나만 쓰므로 [Range]·[Min]과 함께 쓰면 한쪽이 무시되기 때문.
    //  - Min만: 그 값보다 작게 입력하면 Min으로 맞춤   - Min·Max 둘 다: 슬라이더
    // 실제 그리기는 Editor 폴더의 LabelDrawer가 담당한다. 리스트 필드에 붙이면 원소마다 적용되므로 리스트에는 쓰지 않는다.
    public class LabelAttribute : PropertyAttribute
    {
        public string Text { get; }

        // 속성의 이름 붙은 인자(Min = 0f)로 정한다. 기본값 무한대 = 제한 없음.
        public float Min { get; set; } = float.NegativeInfinity;
        public float Max { get; set; } = float.PositiveInfinity;

        public LabelAttribute(string text)
        {
            Text = text;
        }

        public bool HasMin => !float.IsNegativeInfinity(Min);
        public bool HasRange => HasMin && !float.IsPositiveInfinity(Max);
    }
}
