namespace Game.Data
{
    // 캐릭터 등급 (기획서 4-1: S 6명, A 4명). 가챠에서 어느 확률 묶음에 들어갈지 정한다.
    // 가챠 화면에서는 S = 5성(황금빛), A = 4성(보라)으로 표시한다. 3성은 무기라 캐릭터 등급이 아니다.
    public enum CharacterGrade
    {
        S = 0,
        A = 1,
    }
}
