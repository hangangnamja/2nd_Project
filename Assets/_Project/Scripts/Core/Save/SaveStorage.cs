using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    // 세이브 파일 읽기·쓰기만 담당한다 (무엇을 언제 저장할지는 PlayerProfile이 정한다).
    // 위치: Application.persistentDataPath = 기기마다 정해진 앱 전용 저장 폴더 (Windows는 AppData\LocalLow\회사명\제품명).
    public static class SaveStorage
    {
        private const string FileName = "save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private static string TempPath => FilePath + ".tmp";
        private static string BrokenPath => FilePath + ".broken";

        // 세이브 파일을 읽어 data에 담는다. 파일이 없거나 읽을 수 없으면 false → 호출한 쪽이 새 게임으로 시작한다.
        // out: 읽은 결과를 반환값(성공 여부)과 별도로 data 변수에 담아 돌려준다.
        public static bool TryLoad(out SaveData data)
        {
            data = null;
            if (!File.Exists(FilePath))
            {
                return false;
            }

            try
            {
                // DeserializeObject<SaveData>: JSON 글자를 읽어 같은 이름의 필드에 값을 채운 SaveData를 만든다.
                // JSON에 없는 필드는 SaveData에 적어 둔 초기값이 그대로 남는다 (예전 세이브에 새 칸이 없어도 안전).
                data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 세이브를 읽지 못했습니다: {e.Message}");
            }

            // 깨진 파일은 다음 저장 때 덮어써 사라지지 않도록 따로 복사해 둔다 (원인 확인·복구용).
            if (data == null)
            {
                File.Copy(FilePath, BrokenPath, overwrite: true);
                Debug.LogWarning($"[Save] 깨진 세이브를 {BrokenPath}에 보관하고 새 게임으로 시작합니다.");
                return false;
            }

            Upgrade(data);
            return true;
        }

        // 세이브를 JSON으로 저장한다.
        // 임시 파일에 다 쓴 뒤 원본과 바꿔치기한다: 쓰는 도중 앱이 꺼져도 원본은 이전 내용 그대로 남는다 (기획서 9장 파일 깨짐 방지).
        public static void Save(SaveData data)
        {
            // Formatting.Indented: 줄바꿈·들여쓰기를 넣어 사람이 파일을 열어 확인하기 쉽게 한다.
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(TempPath, json);

            if (File.Exists(FilePath))
            {
                // File.Replace: 임시 파일을 원본 자리로 옮기고 원본은 지운다 (세 번째 인자 null = 백업 파일 안 만듦).
                File.Replace(TempPath, FilePath, null);
            }
            else
            {
                File.Move(TempPath, FilePath);
            }
        }

        // 세이브 파일을 지운다 (테스트용: 다음 실행은 새 게임).
        public static void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        // 옛 버전 세이브를 현재 구조로 바꾼다. 지금은 버전 1뿐이라 바꿀 내용이 없다.
        // 구조를 바꾸면 버전마다 단계를 추가한다. 예: if (data.version < 2) { 버전 2에서 바뀐 칸 채우기 }
        private static void Upgrade(SaveData data)
        {
            if (data.version < SaveData.CurrentVersion)
            {
                data.version = SaveData.CurrentVersion;
            }
        }
    }
}
