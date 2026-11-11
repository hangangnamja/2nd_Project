using System;

namespace Game.Core
{
    // [SubclassSelector] 드롭다운에 보여줄 이름·분류·설명을 클래스에 붙인다.
    // 클래스 이름(DamageEffect 등) 대신 "공격/피해"처럼 한국어 이름으로 고르고, 마우스를 올리면 설명이 툴팁으로 보인다.
    // path의 "/"는 드롭다운 하위 메뉴 구분자다: "공격/피해" → 공격 메뉴 안의 "피해" 항목.
    // 붙이지 않은 클래스는 드롭다운 맨 아래 "기타"에 클래스 이름 그대로 나온다.
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SubclassMenuAttribute : Attribute
    {
        public string Path { get; }
        public string Description { get; }

        // order: 같은 분류 안에서 나열할 순서 (작을수록 위). 분류끼리의 순서는 각 분류에서 가장 작은 값으로 정해진다.
        public int Order { get; }

        public SubclassMenuAttribute(string path, string description, int order = 0)
        {
            Path = path;
            Description = description;
            Order = order;
        }

        // 버튼에 보여줄 짧은 이름: 경로의 마지막 부분 ("공격/피해" → "피해").
        public string DisplayName => Path.Substring(Path.LastIndexOf('/') + 1);
    }
}
