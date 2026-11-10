using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // 에디터 상단 메뉴 Tools > Save: 테스트할 때 세이브 파일을 지우거나 저장 폴더를 연다.
    public static class SaveMenu
    {
        // 세이브를 지운다 → 다음 플레이는 새 게임(GameDatabase의 시작 캐릭터)으로 시작한다.
        [MenuItem("Tools/Save/세이브 삭제 (새 게임)")]
        private static void DeleteSave()
        {
            SaveStorage.Delete();
            Debug.Log($"[Save] 세이브를 삭제했습니다: {SaveStorage.FilePath}");
        }

        // 세이브 파일이 있는 폴더를 탐색기로 연다 (JSON 내용 확인용).
        [MenuItem("Tools/Save/세이브 폴더 열기")]
        private static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(SaveStorage.FilePath);
        }
    }
}
