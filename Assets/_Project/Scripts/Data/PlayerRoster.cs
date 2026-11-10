using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 플레이어가 가진 캐릭터 목록과 기본 편성 (임시).
    // 세이브 시스템(기획서 9장: 보유 캐릭터 ID·레벨)이 생기면 이 에셋 대신 세이브에서 읽는다.
    // 그때까지는 편성 화면이 이 목록을 "보유 캐릭터"로 보여준다.
    [CreateAssetMenu(fileName = "PlayerRoster", menuName = "Game/Player Roster (Temp)")]
    public class PlayerRoster : ScriptableObject
    {
        [Tooltip("보유 캐릭터 (편성 화면 목록에 이 순서로 나온다)")]
        [SerializeField] private List<CharacterData> ownedCharacters = new List<CharacterData>();

        [Tooltip("보유 캐릭터 전원의 레벨 (성장 시스템 전까지 임시)")]
        [SerializeField, Range(1, 60)] private int level = 60;

        [Tooltip("편성 화면을 처음 열 때의 편성 (최대 4명)")]
        [SerializeField] private List<CharacterData> defaultParty = new List<CharacterData>();

        public IReadOnlyList<CharacterData> OwnedCharacters => ownedCharacters;
        public int Level => level;
        public IReadOnlyList<CharacterData> DefaultParty => defaultParty;
    }
}
