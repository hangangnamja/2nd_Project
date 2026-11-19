using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    // 디버그 키: 플레이 중 어느 씬에서든 F9를 누르면 보석·골드를 100,000씩 받는다 (무한 재화 대신 쓰는 테스트·심사용).
    // 씬에 따로 놓지 않아도 되도록, 게임이 시작될 때 스스로 오브젝트를 하나 만들어 씬이 바뀌어도 남겨 둔다.
    public class DebugCurrencyKey : MonoBehaviour
    {
        private const int Amount = 100000;

        // RuntimeInitializeOnLoadMethod(AfterSceneLoad): 플레이 시작 후 첫 씬을 불러온 다음 Unity가 자동으로 부른다.
        // DontDestroyOnLoad: 씬을 바꿔도 이 오브젝트를 지우지 않는다 → 로비·전투 어디서든 키가 먹는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var keyObject = new GameObject(nameof(DebugCurrencyKey));
            keyObject.AddComponent<DebugCurrencyKey>();
            DontDestroyOnLoad(keyObject);
        }

        // Keyboard.current: 연결된 키보드 (없으면 null → ?.로 건너뜀).
        // wasPressedThisFrame: 이번 프레임에 막 눌렸을 때만 true → 누르고 있어도 한 번만 지급된다.
        private void Update()
        {
            if (Keyboard.current?.f9Key.wasPressedThisFrame == true)
            {
                PlayerProfile.AddDebugCurrency(Amount);
                Debug.Log($"[Debug] F9: 보석·골드 +{Amount:N0}");
            }
        }
    }
}
