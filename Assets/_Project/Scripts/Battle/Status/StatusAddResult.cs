namespace Game.Battle.Status
{
    // 상태효과를 칸에 넣은 결과.
    //  - Instance: 새로 들어간 효과, 또는 갱신된 기존 효과
    //  - IsNew: 새로 칸을 차지했으면 true, 같은 효과가 갱신만 됐으면 false (새 버프 획득 판정에 사용)
    //  - PushedOut: 칸이 가득 차 밀려난 효과 (없으면 null)
    public readonly struct StatusAddResult
    {
        public StatusInstance Instance { get; }
        public bool IsNew { get; }
        public StatusInstance PushedOut { get; }

        public StatusAddResult(StatusInstance instance, bool isNew, StatusInstance pushedOut)
        {
            Instance = instance;
            IsNew = isNew;
            PushedOut = pushedOut;
        }
    }
}
