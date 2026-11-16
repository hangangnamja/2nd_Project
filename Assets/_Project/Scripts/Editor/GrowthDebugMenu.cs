using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // 육성 확인용 에디터 메뉴 (Tools > Growth). 캐릭터가 Lv60으로 지급되는 동안 레벨업·진급을 시험하려고 둔다.
    // 세이브 파일에 바로 저장되므로, 플레이 중에 누르면 열린 전투원 화면에도 바로 반영된다 (OwnedCharactersChanged).
    public static class GrowthDebugMenu
    {
        [MenuItem("Tools/Growth/보유 캐릭터 전부 Lv1로")]
        private static void ResetAllToLevelOne()
        {
            PlayerProfile.ResetAllGrowthForTest();
            Debug.Log("[Growth] 보유 캐릭터를 전부 Lv1 · 진급 0회로 되돌렸습니다.");
        }
    }
}
