# 코드 구조 설계

> 게임 규칙은 `GameDesign.md`가 기준이고, 이 문서는 그 규칙을 **어떤 코드가 담당하는지**를 정한다.
> 핵심 원칙: 캐릭터는 코드가 아니라 **데이터(SO)로 조립**한다. 새 캐릭터를 만들 때 코드 추가 없이 Inspector에서 효과 블록을 골라 끼우는 것이 목표다.

---

## 1. 씬 구성

| 씬 | 역할 |
|---|---|
| Title | 세이브 불러오기, 공용 매니저 생성 (씬 전환 후에도 유지) |
| Lobby | 메인·전투원·인벤토리·편성·가챠·스테이지 선택. 화면마다 씬을 나누지 않고 Canvas 패널 전환으로 처리 |
| Battle | 전투 전용. Lobby에서 "스테이지 ID + 편성"을 넘겨받아 시작 |

- UI는 uGUI Canvas 사용 (1920×1080 기준, Scale With Screen Size)
- Lobby 씬: `LobbyCanvas` 아래 화면 패널(`MainScreen`, `StageSelectScreen`, `FormationScreen`, `GachaScreen`, …)을 `LobbyNavigator`가 하나만 켜서 전환. 새 화면은 패널로 추가하고 Navigator에 등록
- 편성 화면 `FormationView`는 두 경로로 열림: 메인 화면 편성 버튼(`OpenForManage`, 출격 없음) / 스테이지 선택의 전투 준비(`Open(stage)`). 팀 4개는 세이브의 `teams`·`currentTeam`, `PlayerProfile.GetParty/SetParty`는 지금 고른 팀을 다룸 (`SelectTeam`·`GetTeamName`·`SetTeamName`)
- 로비 → 전투: 스테이지 선택(`StageSelectView`) → 편성(`FormationView`) → `BattleSetup.Create`(편성 + 레벨 + 고른 스테이지)를 `BattleLaunch`(static)에 맡기고 Battle 씬을 불러옴 → `BattleManager`가 시작할 때 꺼내 씀. 맡긴 값이 없으면(씬 직접 실행) Inspector의 테스트 설정 사용
- 보유 캐릭터·편성은 세이브(`PlayerProfile`)에서 읽음. 로비에서 시작한 전투는 캐릭터별 보유 레벨로 싸우고, 이기면 `PlayerProfile.CompleteStage`가 보상 지급 + 클리어 기록을 한 번에 저장하고 받은 보상을 돌려줌 → `BattleManager.Rewards` → `BattleResultView`가 표시 (결과 색·파티 카드 `ResultPartyCard`·보상 칸 `ResultRewardSlot`, 칸은 씬의 꺼 둔 원본을 복사)
- 스테이지 선택 `StageSelectView`: 탭 스테이지 / 토벌 / 탐색. 탐색 장소를 고르면 상세 패널의 난이도 버튼으로 스테이지를 고름. 보상은 `StageRewardSlot`(씬의 꺼 둔 원본 칸을 복사·재사용)으로 아이콘 표시, 첫 클리어 보상은 클리어 기록에 따라 "첫 클리어"/"획득 완료". `GameDatabase.FindStage`는 탐색 스테이지까지 찾음
- 별 미션 (기획서 6장): `StarMission`(Data)이 조건(`StarMissionCondition`: 클리어 / 시간 안에 클리어 / 아군이 쓰러지지 않고 클리어)·판정(`IsAchieved(StarMissionResult)`)·문장(`Describe`)을 들고, 스테이지 에셋마다 목록으로 둔다
  - 전투: `BattleManager.Update`가 전투 시간(`BattleSeconds`, 배속을 곱한 시간이라 1배속 기준. 스킬 고르는 동안은 실제 시간, 일시정지는 0)을 재고, `BattleRecord.AlliesFallen`이 아군 처치 사건을 센다. `FinishBattle` → `JudgeStarMissions`가 이번 달성(`StarsAchieved`)·처음 달성(`StarsNewlyAchieved`)을 구하고 `CompleteStage(stage, 결과)`에 넘김
  - 저장: `PlayerProfile.CompleteStage`가 `RecordStars`로 처음 달성한 별만 `StageRecordSave.stars`(별 번호 목록)에 넣고 그 보상을 함께 지급. 조회는 `IsStarAchieved`·`GetStarCount`
  - 화면: `StageSelectView`(상세 패널 `detailMissions` 별·조건 줄, 보상 칸 ★n 꼬리표 = `StageRewardSlot.Bind(…, tagLabel, claimed)`, 노드 이름 아래 ★ 수), `BattleResultView.ShowStars`(별 한 줄 + NEW + 전투 시간)
- Build Settings: Title(0) → Lobby(1) → Battle(2)
- 시작 화면 `TitleView`(Title 씬): 화면 전체 투명 버튼을 누르면 `PlayerProfile.Load`(세이브 불러오기, 없으면 새 게임) → 검은 판으로 어두워진 뒤 Lobby 씬. 세이브 상태 글자는 `PlayerProfile.HasSaveFile`(파일만 확인, 불러오지 않음). 공용 매니저는 아직 없음 (세이브는 `PlayerProfile` static이 처음 쓰일 때 불러옴)
- **직접 실행 경로**: Battle 씬을 에디터에서 바로 실행해도 동작해야 한다
  - 공용 매니저가 없으면 자동 생성
  - 넘겨받은 편성이 없으면 테스트용 전투 설정(SO)으로 시작 → `BattleSetup_Test` (BattleManager에는 BattleSetup 하나만 연결)
- 웨이브 구성은 스테이지 에셋(`Data/Stages/`)의 Inspector에서 조절한다

### 웨이브 진행
- `StageRunner`(순수 C#)가 웨이브 순서만 판단하고, `BattleManager`가 적 교체를 실행, `BattleStageView`가 `WaveStarted` 사건으로 캡슐을 교체
- 전환 시점: 추가 공격(임시 턴)까지 모두 끝난 턴 사이. 무적이 아닌 적을 모두 잡았고 다음 웨이브가 있으면 전환, 마지막 웨이브면 승리
- 전환 시 `allUnits`(스케줄러·패시브 처리기·공용 도구가 같은 리스트를 참조)의 적만 교체하고, 아군 게이지를 10000으로 리셋. 버프·쿨타임·변신은 유지
- 같은 "전투 중 적 교체·추가" 처리를 보스 3 부하 소환에도 쓴다

---

## 2. 데이터와 값의 소유권

**SO = 원본 설정(읽기 전용)**, **런타임 객체 = 전투 중 바뀌는 값**. SO는 런타임에 절대 수정하지 않는다.

| 원본 (SO, 에셋) | 런타임 (전투 중 생성) |
|---|---|
| `CharacterData`: 등급, 직군, 일러스트(화면 공용, 비면 실루엣), 전투 3D 모델(`BattleModel`, 비면 캡슐), 기본 스탯, 치명, 속도, 1~3스, 패시브, 변신 스킬 세트 | `BattleUnit`: 현재 스탯·HP, 남은 거리, 남은 쿨타임, 상태효과 칸, 패시브 스택·사용 횟수 |
| `StatusEffectData`: 화상·독·버프의 공통 정의 (지속 턴, 중첩/갱신) + **효과 종류**(스탯 보정·지속피해·지속 회복·행동 불가·지연 버프·피해 기록·디버프 면역·피해 무효 중 복수 선택). Inspector는 고른 종류의 칸만 표시 (`StatusEffectDataEditor`) | `StatusInstance`: 실제로 걸린 효과 1개 (정의 참조, 부여자, 남은 턴, 시전 당시 공격력 스냅샷) |
| `StageData`: 적 레벨 + 웨이브 목록(웨이브마다 적 편성, 자리별 무적) + 보상(`clearRewards` 매번, `firstClearRewards` 첫 클리어만, 재료 + 개수) + 별 미션(`StarMission`: 조건·기준 시간·처음 달성 보상) + 전투 배경 프리팹(`battleBackground`, 비면 기본 평원). `StageCatalog`: 기본·토벌 목록 + 탐색 장소(`ExplorationSite`: 장소 이름·재화 표기·난이도별 스테이지) | `StageRunner`: 현재 웨이브, 웨이브 클리어 판정, 다음 웨이브 유무 |
| `BattleSetup`: 이번 전투의 아군 편성·레벨 + 스테이지 (지금은 테스트 에셋, 이후 로비가 만들어 넘김) | - |
| `EnemyCharacterData`: `CharacterData` + 행동 패턴 목록 (패턴이 필요한 적만) | `BattleManager.DecideEnemyAction`: 패턴을 순서대로 확인해 스킬·대상 결정 |
| `BattleRulesData`: 전투 공통 수치 (방어 상수 K 계산 파라미터, 레벨 성장 곡선, 피해 난수 켜기·범위 → `RollDamageVariance`를 `DamageCalculator`가 공격 피해에 곱함, 전투 공용 난수라 시드 고정 시 재현) | `BattleUnit` 기본 스탯 = 데이터 스탯(Lv1) × 레벨 배율(`StatScaling`), 전투 시작 때 고정. 아군 배율은 `GetAllyScaling(레벨, 진급)` (로비 → `BattleSetup`이 진급 횟수도 넘김, 모르면 그 레벨까지 진급 완료로 계산) |
| `GachaBannerData`: 배너 이름·강조 문구·종료 일시·픽업 S 1명 | `GachaView`: 보여주는 배너 순서 |
| `GachaRulesData`: 확률·천장·반천·A 확정·비용·디버그 무료 (배너 공통) | `GachaSave`(세이브): 천장·A 확정 카운터, 픽업 확정 여부 |
| `WeaponData`·`MaterialData`: 무기(등급 3~5성, 종류 칼·방패·마법지팡이·로자리오 = 장착 직군, 풀 육성 스탯)·재료(분류, 등급) 종류 | `WeaponSave`(개체 번호·레벨·진급·경험치·장착)·`MaterialSave`(개수) → `OwnedWeapon`·`OwnedMaterial`. 무기 스탯 = `WeaponStats.Calculate`(풀 육성 스탯 × `BattleRulesData.GetWeaponStatRatio`) |
| `InventoryRulesData`: 무기 칸 수, 재료 최대 개수, 분해 재료·등급별 보상 | - |
| `GrowthRulesData`: 레벨별 필요 경험치(StatTable "경험치" 시트), 진급 간격·횟수, 경험치 재료(재료 + 1개당 경험치), 진급 재료·단계별 개수(`GetPromotionCost`), 디버그 재료 무한(진급 재료 포함) | `OwnedCharacterSave.exp`·`promotion` → `OwnedCharacter` |
| `GameDatabase` (`Resources/`): ID → 캐릭터·스테이지·무기·재료 SO 찾기, 인벤토리 규칙, 새 게임 시작 캐릭터 | `SaveData` (JSON): ID와 바뀌는 값만 저장 |

### 가챠
- `GachaMachine`(순수 C#): 1회 판정만. 난수를 밖에서 받아 시드 고정 시 재현·시뮬레이션 가능. 카운터는 세이브의 `GachaSave`를 직접 갱신
- `GachaService`: 보석 확인 → 판정 → `PlayerProfile.CommitGachaPull`로 새 캐릭터 지급·보석 차감·저장
- `GachaPresenter`: 정해진 결과를 보여주기만 함 (물레 → 한 장씩 공개 → 결과 목록). 빛은 파티클·Bloom, 코드는 재생 순서와 색만
  - 물레: 3D 모델 `GachaWheel3D`(월드 공간, 카메라 앞 7 / 캔버스는 10이라 UI 배경보다 앞에 그려짐). 메시는 코드로 생성한 선반 가공 모양(`Art/Gacha/Wheel/Meshes`), 재질은 URP Lit 나무·황동·실
    - `GachaGearDial`(투명 UI 드래그 영역)이 바퀴(`WheelSpin`)만 자기 z축으로 돌린다. 등급 색은 줄 가운데 `ThreadGlow`, 바퀴 둘레 고리 빛 `GearGlow`, 물레를 비추는 점광원 `WheelLight`(돌릴수록 밝아짐)
    - 부품 깊이는 모두 캔버스(10)보다 앞이어야 보인다 (비스듬히 돌린 각도가 크면 뒤쪽 부품이 배경 뒤로 숨음)
  - 글로우(후처리)를 받으려고 연출은 Screen Space - Camera 캔버스(`GachaFxCanvas`)에서 재생하고, 그동안 Overlay인 `LobbyCanvas`를 끔
  - 파티클은 캔버스 픽셀 단위(Hierarchy 스케일)로 만들고 sorting order 5로 UI 위에 그림. 머티리얼은 Additive + HDR 밝기(×2.6) → Bloom이 번지게 함
- 임시 그림(물레 `Art/Gacha/Wheel/`, 칼·글로우·고리·그라데이션)은 `Art/Gacha/`
- 확률 공시 (기획서 7-5): `GachaRateCalculator`(순수 C#)가 `GachaRulesData`로 `GachaRateTable`(구간별 기본·최종 확률, 픽업 비율, 평균 횟수)을 계산
  - 최종 확률 = 카운터 상태(S 없이 뽑은 횟수 × A 이상 없이 뽑은 횟수)의 정상 분포를 반복 계산으로 구해 상태별 구간 확률과 곱해 더함. 구간 판정·상태 이동은 `GachaMachine.Pull`과 같은 규칙이라, 판정 규칙을 바꾸면 계산기도 함께 고친다
  - 화면: `GachaRatePopup`(GachaScreen 아래 `RateInfoPopup`, `GachaView.ShowRateInfo`가 지금 배너로 엶), 표 한 줄 `GachaRateRowView`(등급별 표·개별 목록 공용, 꺼 둔 원본 줄을 복사·재사용)

### 인벤토리 (기획서 2장)
- 무기 인벤과 재료 인벤은 세이브 목록부터 따로다: `SaveData.weapons`(개체 1개 = 1칸, `nextWeaponUid`로 개체 번호) / `SaveData.materials`(종류별 개수)
- `PlayerProfile.Inventory.cs`(partial): 무기 추가(`AddWeapon`), 재료 증감(`AddMaterial`), 목록 조회, 일괄 분해(`PreviewDismantle`·`DismantleWeapons`), 저장 후 `InventoryChanged` 알림
  - 자동 분해: `StoreWeaponOrDismantle`이 칸이 꽉 찼고 3성이면 칸에 넣지 않고 분해 보상만 준다. 가챠 10회는 `CommitGachaPull`이 무기를 한꺼번에 넣고 한 번 저장한 뒤, 무기마다 자동 분해 여부를 돌려준다(`GachaResult.IsDismantled`)
- 골드·보석은 재료 분류 `MaterialCategory.Currency`의 `MaterialData`(`Mat_Gold`·`Mat_Gem`, ID `gold`·`gems`)로 재료 탭에 보인다. 개수는 `MaterialSave`가 아니라 `SaveData.currency`에서 읽고(`GetMaterialCount`), `AddMaterial`로 더하면 재화 값에 더한다. 0개여도 목록에 넣고 정렬은 맨 앞. 흰 아이콘은 `MaterialData.IconColor`(아이콘 색)로 물들임 (골드 황금색, 보석 하늘색, 로비 우상단과 같은 색)
- 화면: `InventoryView`(탭·목록·정렬·상세), `InventorySlotView`(칸 하나, 칸 원본을 복사해 재사용), `DismantlePopup`(등급 선택 → 미리 보기 → 분해). 등급 색은 `RarityColors`(가챠와 같은 값)
- 에디터 메뉴 `InventoryDebugMenu`(Tools > Inventory): 테스트 아이템 지급, 무기 칸 채우기

### 전투원 화면 (기획서 2장)
- `OperatorView`: 켜질 때마다 `PlayerProfile.GetOwnedCharacters`로 오른쪽 목록(`OperatorListItem`, 원본 칸 복사)을 다시 만들고, 고른 캐릭터로 정보 패널·스킬 탭을 채운다
- 스탯은 `BattleRulesData.GetAllyScaling(레벨)`로 전투와 같은 배율을 곱해 보여준다 (공격력·방어력은 전투처럼 버림)
- 스킬 탭은 전투 상세 창과 같은 `SkillDescriptionBuilder`·`DetailEntryView`를 쓴다. 전투 밖이라 남은 쿨타임·스택은 -1로 넘겨 그 줄을 생략
- 육성 창 `GrowthPopup`: 고른 재료 개수만 들고, 미리 보기·실제 레벨업 모두 `GrowthRulesData.AddExp`(상한에서 멈추고 넘친 양은 버림) 하나로 계산
  - 키우는 대상은 `IGrowthTarget`(전략 패턴)으로 감춘다: `CharacterGrowthTarget`(캐릭터 규칙, 레벨 배율 스탯) / `WeaponGrowthTarget`(무기 규칙 `WeaponGrowthRules`, 무기 스탯). 창은 규칙·상태·스탯을 대상에게 묻기만 함
  - 재료 칸 수는 대상 규칙의 재료 수에 맞춰 켜고 가운데 정렬 (캐릭터 3칸, 무기 1칸)
- 무기 장착 `WeaponSelectPopup`: 낄 수 있는 무기(`PlayerProfile.GetEquippableWeapons`, 직군 = 무기 종류) 목록 + 장착·해제·강화. 저장은 `PlayerProfile.Weapon.cs`(`EquipWeapon`·`UnequipWeapon`·`LevelUpWeapon`·`PromoteWeapon`) → `InventoryChanged`·`OwnedCharactersChanged`
- 전투: 편성 화면이 캐릭터별 무기 스탯을 `BattleSetup`에 담아 넘기고, `BattleUnit`이 기본 스탯에 더한다
- 가챠 무기: `GachaMachine`이 캐릭터 구간 뒤 무기 구간(5성 1% → 4성 10% → 3성 나머지)으로 등급을 정하고 `GetWeaponsOfGrade`에서 고른다. 연출은 결과 등급 색만 보므로 4·5성 무기도 캐릭터와 같은 빛
  - 재료 칸 `GrowthMaterialSlot`의 누름·홀드 반복은 `HoldRepeatButton`(누른 채로 있으면 점점 빨리 반복)
  - 실행은 `PlayerProfile.LevelUp`·`Promote`(`PlayerProfile.Growth.cs`): 저장 후 `OwnedCharactersChanged` → 육성 창·전투원 화면이 다시 그려짐
  - 진급은 캐릭터·무기 공용 `TryPayPromotion`(진급 재료·골드 확인·소모, 재료 무한이면 재료만 소모 없음)을 거친다. 진급 화면 버튼은 `HasPromotionMaterials`·`HasGold`로 켜고 끈다
  - 골드 비용: `GrowthRulesData.GetLevelUpGoldCost`(넣은 경험치 × 경험치 1당 골드, 올림)·`GetPromotionGoldCost`(진급 단계별 목록). 레벨업은 `TryApplyExp`가 재료·골드를 모두 확인한 뒤 함께 소모. 골드는 "재료 무한"과 무관하게 항상 실제 보유량(`PlayerProfile.HasGold`)으로 판정
  - 육성 창은 확인 버튼 위 `goldCostText`(골드 아이콘 + 필요 / 보유)를 같은 계산으로 그리고, 모자라면 버튼을 끈다

### 업적 (기획서 12장)
- `AchievementData`(SO, `Data/Achievements/`): ID·분류·등급·제목·조건(`AchievementCondition`)·스테이지(첫 클리어용)·목표·보상. 설명은 `Describe()`가 조건·목표로 만든다
- `AchievementCatalog`(SO): 업적 전체 목록 = 화면 순서. `GameDatabase.AchievementCatalog`로 찾는다
- `PlayerProfile.Achievement.cs`(partial): 진행도(`GetAchievementProgress`), 달성·수령 판정, 보상 받기(`ClaimAchievement`·`ClaimAchievements`) → `AchievementsChanged`
  - 누적 기록은 `SaveData.counters`(기록 이름 = 조건 enum 이름). 첫 클리어·보유 수·최고 레벨은 세이브 상태로 계산. 저장하는 것은 받은 업적 ID(`claimedAchievements`)뿐
  - 기록을 더하는 곳: `CompleteStage`(스테이지 종류별 클리어, `GameDatabase.GetStageKind`), `CommitGachaPull`(모집 횟수·전투원·S 획득), `Promote`(진급), `LevelUpWeapon`(무기 강화), `RecordBattle`(전투 기록)
- 전투 기록: `BattleRecord`가 `BattleEvents`(처치·피해·스킬 사용)를 구독해 세고, `BattleManager.FinishBattle`이 로비에서 시작한 전투면 `PlayerProfile.RecordBattle`로 더한다
- 화면: `AchievementView`(개요 ↔ 목록, 뒤로 처리), `AchievementCategoryCard`(분류 카드), `AchievementRowView`(업적 줄, 원본 복사·재사용, 등급 색 `AchievementTierColors`), 메인 화면 버튼 알림 점 `AchievementBadge`. 그림은 `Art/UI/Achievement`(트로피·분류 심볼 5종, 흰 그림을 색으로 물들임)·`Art/UI/IconCheck`

### 세이브 (기획서 9장)
- `SaveData`: 파일에 그대로 저장되는 내용. 필드 이름이 곧 JSON 키라, 이름·구조를 바꾸면 `version`을 올리고 `SaveStorage.Upgrade`에 변환을 추가
- `SaveStorage`: 파일 읽기·쓰기만 (임시 파일에 쓴 뒤 원본과 교체, 깨진 파일 보관)
- `PlayerProfile` (static): 처음 쓰일 때 세이브를 불러오고(없으면 새 게임), 바꾸는 함수는 바꾼 직후 저장. 화면은 `SaveData`를 직접 만지지 않고 이 함수로 데이터 에셋을 받음
- 저장되는 SO(`CharacterData`, `StageData`)는 에셋 이름과 별도로 **ID** 칸을 가짐. 한 번 정하면 바꾸지 않는다
- 도메인 리로드를 꺼 두었으므로 static 값은 플레이 시작 때 직접 비운다 (`RuntimeInitializeOnLoadMethod`)
- 디버그 재화 키: `DebugCurrencyKey`(Core)가 플레이 시작 때 스스로 `DontDestroyOnLoad` 오브젝트를 만들고, F9(Input System `Keyboard.current`)를 누르면 `PlayerProfile.AddDebugCurrency(100000)` → 저장 + `InventoryChanged`(로비 재화 표시·모집 화면 보석이 바로 갱신)

### 캐릭터 조립 구조
```
CharacterData (SO 에셋 1개)
 ├─ 기본 정보, 스탯
 ├─ 1스 / 2스 / 3스  ─ 쿨타임, 시작 쿨타임, 대상 규칙, 효과 목록
 ├─ 패시브 목록       ─ 트리거 + 조건 + 효과 목록 + 제한(스택, 턴당·전투당 횟수)
 └─ (선택) 변신 스킬 세트
```
- 스킬은 별도 에셋이 아니라 `CharacterData` 안에 들어가는 데이터 묶음
- 여러 캐릭터가 공유하는 **상태효과만 별도 SO**로 분리 (같은 화상인지 판단, 수치 일괄 수정)

### 효과 블록 (전략 패턴)
- 효과 블록마다 "실행" 방식을 따로 구현하고, 스킬은 블록 종류를 몰라도 목록 순서대로 실행만 한다
- 여러 종류의 블록을 한 리스트에 담기 위해 `[SerializeReference]` 사용
  - Unity 기본 Inspector에는 블록 종류를 골라 추가하는 UI가 없으므로 **에디터 스크립트를 함께 작성**
- 드롭다운은 분류별 한국어 이름으로 보인다: 블록 클래스에 `[SubclassMenu("분류/이름", "설명", 순서)]`를 붙이고, 버튼에 마우스를 올리면 설명이 툴팁으로 나옴. 새 블록을 만들면 이 속성도 붙이고 아래 표에 추가
- 필드 이름도 한국어로 보인다: `[Label("이름", Min = 0f, Max = 1f)]` (`LabelDrawer`가 그림). Min·Max도 여기서 정한다 (Unity는 필드 하나에 드로어 하나만 써서 `[Range]`·`[Min]`과 함께 쓰면 한쪽이 무시됨). enum 항목은 `[InspectorName]`. 리스트 필드는 영어 이름 그대로
- 효과는 목록 순서대로 실행된다. 순서가 중요한 조합: 피해 → 격발(방금 붙은 화상도 터짐), 피해 → 버프 전부 소모(버프가 피해에 반영된 뒤 지움), 상대 턴 끌어오기 → 추가 공격 예약(끌어온 턴이 끝난 뒤 반격)

#### 스탯 보정 종류 (`StatType`, 상태효과·오라·경계의 스탯 보정에서 고름)
| Inspector 이름 | 코드 | 계산 | 비고 |
|---|---|---|---|
| 공격력 / 방어력 / 속도 / 최대 HP | `Attack` `Defense` `Speed` `MaxHp` | 기본 스탯: 원본 × (1 + % 합) + 고정 합 | 공격력·방어력 % 증가 버프끼리는 중첩 감소 |
| 치명 확률 / 치명 피해 | `CritRate` `CritDamage` | 비율 스탯: 원본 + % 합 (0.2 = +20%p) | 치명 확률 상한 100% |
| 주는 피해 증가 | `DamageBonus` | 비율 스탯 | 공격자 쪽 |
| 받는 피해 증가 | `DamageTakenIncrease` | 비율 스탯 | 대상 쪽, 지속피해에도 적용 |
| 피해 감소 | `DamageReduction` | 효과끼리 곱연산 1 - (1 - a)(1 - b)…, 최대 75% | 대상 쪽 |
| 지속피해 증가 | `DamageOverTimeBonus` | 비율 스탯, 지속피해가 발동할 때의 값 | 지속피해를 건 쪽 |

- 감소(디버프)는 % 칸에 음수로 넣는다 (-0.3 = 30% 감소)
- Inspector 이름은 `[InspectorName]`으로 붙인 것이라 에셋 저장 값(숫자 순서)과 무관하다. 새 스탯은 맨 뒤에 추가

#### 효과 블록 목록
| 분류 | 이름 (클래스) | 하는 일 | 사용 예 |
|---|---|---|---|
| 공격 | 피해 (`DamageEffect`) | 공격력 × 배율 피해. 확산은 메인 배율·양옆 배율을 따로 정함 | 대부분의 공격 스킬 |
| 공격 | 대상 최대 HP 비례 피해 (`MaxHpDamageEffect`) | 공격력 × 배율(일반 공격과 같음) + 대상 최대 HP × 비율 피해. 최대 HP 몫은 공격력·치명·방어력 무시, 대상 쪽 보정·보호막은 둘 다 적용 | 보스 2 디버프 없는 턴 공격 |
| 공격 | 버프 수 비례 피해 (`BuffScalingDamageEffect`) | 기본 배율 + 버프 1개당 배율 × 시전자 버프 개수 | 강탈 딜러 3스 |
| 공격 | 패시브 스택 비례 피해 (`PassiveStackDamageEffect`) | 기본 배율 + 스택 1개당 배율 × 시전자 패시브 스택, 피해 뒤 스택 전부 소모 | 니콜 2스 (이자 정산) |
| 공격 | 받은 피해 비례 반격 (`RecordedDamageCounterEffect`) | 기본 배율 + 피해 기록에 쌓인 받은 피해(최대 HP 대비) × 계수를 공격력 배율로 반격, 기록 삭제 | 하이퍼캐리 반격 태세 |
| 공격 | 격발 (`DetonateEffect`) | 대상의 지속피해를 즉시 1회씩 발동 (지속피해는 유지) | 격발 딜러 3스·패시브 |
| 회복·보호 | 회복 (`HealEffect`) | 기준(공격력·최대 HP·시전자 최대 HP·가한 피해·지난 자기 턴 이후 받은 피해) × 비율 회복. 시전자 자신·아군 전체 옵션. 가한 피해 = 이 행동에서 앞선 피해 블록들이 가한 피해 합계(`SkillContext.DamageDealt`, HP + 보호막 흡수) → 피흡, 피해 블록 뒤에 둠 | 아스트라 패시브, 변신 스킬 자가 회복, 강탈 딜러 3스 피흡 |
| 회복·보호 | 보호막 (`ShieldEffect`) | 기준 스탯 × 비율만큼 보호막. 이미 있으면 양을 더함 (`StatusApplier.GrantShield`) | 곰인형 버퍼 2스, 하이퍼캐리 불굴, 도발 탱커 2·3스 |
| 회복·보호 | 피격 무효 충전 (`HitBlockEffect`) | 시전자의 피격 무효를 횟수만큼 채움 (`BattleUnit.ChargeHitBlock`, 쌓이지 않음). 피해만 0, 효과는 들어감 | (지금 쓰는 캐릭터 없음) |
| 회복·보호 | 부활 (`ReviveEffect`) | 쓰러진 대상을 최대 HP × 비율로 부활 (`BattleUnit.Revive`). 살아 있는 대상은 건너뜀 | 부활 힐러 3스·패시브 |
| 상태효과 | 디버프 제거 (`RemoveDebuffsEffect`) | 대상 디버프를 전부 또는 가장 최근 1개 제거 | 부활 힐러 2스·3스 |
| 상태효과 | 상태효과 부여 (`ApplyStatusEffect`) | 상태효과를 개수만큼 부여. 아군 전체 옵션 | 화상·독·버프 부여 전반 |
| 상태효과 | 버프 강탈 (`StealBuffsEffect`) | 대상 버프 전부 즉시 제거, 턴 종료 단계에 시전자가 획득 | 강탈 딜러 2스·3스 (3스는 버프 전부 소모 뒤) |
| 상태효과 | 버프 전부 소모 (`ConsumeAllBuffsEffect`) | 시전자 버프 전부 삭제 | 강탈 딜러 3스, 축적 해방 |
| 턴·행동 | 행동 당기기 (`ActionAdvanceEffect`) | 행동 게이지를 비율만큼 당김. 시전자 자신 옵션 | 니콜 패시브(자신) |
| 턴·행동 | 추가 턴 (`GrantExtraTurnEffect`) | 이번 턴이 끝난 뒤 한 번 더 행동 (같은 턴 중복은 1회) | - |
| 턴·행동 | 추가 공격 예약 (`QueueFollowUpEffect`) | 블록 안 효과 목록을 시전자의 추가 공격(임시 턴)으로 예약. 전용 연출 프리팹을 넣을 수 있음 | 반격 태세의 반격, 패시브 추가 공격 |
| 턴·행동 | 상대 턴 끌어오기 (`ForceOpponentsAttackEffect`) | 상대 전원의 다음 정규 턴을 지금 턴 직후로 끌어옴 (AI가 스킬 선택, 단일·확산은 시전자를 노림) | 반격 태세 |
| 턴·행동 | 쿨타임 감소 (`ReduceCooldownEffect`) | 지정 스킬의 남은 쿨타임 감소. 대상 또는 시전자 자신 | 하이퍼캐리 2스, 아스트라 2스, 곰인형 패시브 |
| 턴·행동 | 다른 스킬 사용 (`UseSkillEffect`) | 시전자의 다른 스킬(1~3스)을 그 자리에서 함께 사용. 그 스킬의 대상 규칙·효과 그대로, 쿨타임은 안 걸림. 연동 안의 연동은 무시 | 니콜 3스 → 2스 |
| 변신·전장 | 변신 (`TransformEffect`) | 시전자 변신 (스킬 교체, 턴 제한·종결기는 변신 설정) | 하이퍼캐리 3스 |
| 변신·전장 | 아군 퇴장 (`ExitAlliesEffect`) | 시전자 외 아군 퇴장, 변신 해제·사망 때 복귀 | 하이퍼캐리 3스 |
| 변신·전장 | 경계: 고립 (`IsolationBoundaryEffect`) | 스탯 보정 + 행동 제어 면역 경계 (경계는 한 번에 하나) | 하이퍼캐리 3스 |
| 변신·전장 | 경계: 영역 (`DomainBoundaryEffect`) | 펼칠 때·주인 턴 종료마다 영역 공격(블록 안 효과 목록)을 적 전체에 사용, 정한 턴 동안. 영역 동안 1회 지정 칸이 다른 스킬로 바뀜 | 흉신 마라 3스 |
| 변신·전장 | 부하 소환 (`SummonEffect`) | 시전자 편에 부하를 소환 (`BattleServices.Summon` → `BattleManager.SummonUnit`). 시전자가 소환한 부하는 최대 수까지 | 군단 지휘관 2스 |
| 변신·전장 | 자폭 (사라짐) (`SelfDestructEffect`) | 시전자가 처치가 아닌 방식으로 사라짐 (`BattleUnit.Vanish`). 효과 목록 맨 끝에 둠 | 자폭병 2스 |
| 변신·전장 | 기간 오라 (`StartAuraEffect`) | 정해진 턴 동안 같은 편 전원 스탯 보정 오라, 주인 턴 스킵 옵션 | 아스트라 3스 공연 |

---

## 3. 전투 코드 구조

```
BattleManager            전투 흐름 상태 머신
  전투 시작 → 웨이브 시작 → 다음 행동자 결정 → 행동 선택 → 처리 → 종료 판정 → 반복
 ├─ TurnScheduler        남은 거리, 다음 행동자, 동시 도착 우선순위, 사이클 카운터
 ├─ ActionQueue          "다음에 무엇을 실행할지"만 관리 (순서 보장)
 ├─ 효과 처리 코드        "그 작업이 실제로 무엇을 바꾸는지" 담당 (효과 블록, DamageCalculator)
 ├─ BattleEvents         전투 중 일어난 일을 알림 (옵저버 패턴)
 ├─ 행동 결정자           같은 인터페이스: PlayerInput / RandomAI / BehaviorGraph(보스)
 └─ BattlePresenter      연출 재생, 재생이 끝나면 BattleManager에 완료를 알림

BattleServices            전투 공용 도구 묶음 (로그, BattleEvents, ActionQueue, 난수, BattleRulesData, 전투 유닛 전원,
                          대상 결정 함수 ResolveTargets = BattleManager의 규칙을 효과 블록이 빌려 씀)
                          효과 블록·패시브·스킬 실행에 하나로 전달. 난수 시드를 고정하면 전투를 그대로 재현

BattleUnit (순수 C#)      값과 규칙
UnitView (MonoBehaviour)  3D 모델, 애니메이션, HP바. 표시만 담당
```

### 장면 연출 (카메라·무대)
- 전투 배경: Battle 씬의 `BattleBackgroundView`가 `BattleStarted`(스테이지가 정해진 뒤)에 스테이지의 배경 프리팹(없으면 `defaultBackground` = `Prefabs/Backgrounds/BgPlains`)을 깔고, 프리팹 맨 위 `BattleBackground`의 하늘(Skybox 또는 단색)·선형 안개·단색 주변광·태양(방향광) 색·세기·방향을 적용, 기본 격자 바닥을 숨김
  - 배경 4종 `BgPlains`·`BgAltar`·`BgCave`·`BgWarfield` (기본 도형, 재질·노이즈 텍스처 `Art/Battle/Backgrounds`). 소품은 무대 가운데 (3, 4) 반지름 13과 카메라 뒤 통로(x ±9, z -18~0) 밖에만 두고, 콜라이더는 지움 (캡슐 누르기 판정을 막지 않게)
  - 영역 전개는 `DomainSceneryView`가 `BattleBackgroundView.SetSceneryVisible(false)`로 배경 지형을 숨김
  - 화면 색을 바꾸는 연출(`DomainSceneryView`·`BattleSpaceView`)은 원래 값을 씬이 켜질 때가 아니라 연출이 처음 시작될 때 기억함 (배경이 전투 시작 뒤에 적용되므로). 한 번도 시작하지 않았으면 되돌리지도 않음
- 전투 위치(로직)와 화면에 서는 자리(연출)를 나눈다. `BattleStageView.Stage(StageLayout, 행동 유닛)`가 장면마다 유닛을 다시 세우고 나머지는 숨김
  - `Overview`(아군 줄 + 멀리 적 줄: 시작·종료·적 차례), `ActorFocus`(행동 유닛만 원점 + 상대 줄 멀리), `TeamLine`(행동 유닛 편 전원이 카메라를 보고 한 줄)
  - 멀리 선 상대 줄은 `farLineArc`만큼 휜 호: 줄 끝일수록 앞(-z)으로 나오고(arc × t²) 각자 원점을 바라본다
  - `ActorFocus`에서는 상대 무리를 `StageFocusLine`이 오른쪽 앞(`focusLineCenter`)에 비스듬한 한 줄(`focusLineDirection`, 카메라 시선과 거의 수직)로 세우고 행동 유닛 쪽을 보게 한다. 간격은 `focusLineSpacing`과 `focusLineMaxWidth ÷ (인원 - 1)` 중 작은 값, 4명 이상이면 `focusLineShiftPerExtra`만큼 오른쪽으로 민다. 차례 구도(스킬 구도 1~3)는 행동 유닛 기준점(`Actor`)으로 바라볼 곳을 정한다
- `BattleCameraDirector`(카메라): `StateChanged`마다 장면을 고른다. 무대가 바뀌면 `SceneFade`(HUD 검은 판)로 덮은 사이에 다시 세우고 카메라를 바로 놓고, 같은 무대 안에서는 구도만 SmoothDamp로 이동
  - 구도 = `CameraShot`(기준점 + 오프셋, 바라볼 기준점 + 오프셋, 화각). 캡슐은 원점이 몸 가운데(y -1~1)
  - 차례 시작 때 자동으로 골라진 스킬은 구도를 바꾸지 않고, 플레이어가 다른 스킬을 누르면 그 스킬 대상 규칙에 맞는 장면으로 바뀜
- `BossHpBarView`: 상단 보스 HP바. `EnemyCharacterData.IsBoss`인 전장의 적을 찾아 이름·HP·보호막·버프 줄을 그리고(줄은 `PartyCardView.ShowRow` 공용), 누르면 `UnitDetailPanel`
- `BattleTargetPicker`: 캡슐을 누르면 뗄 때 대상 선택(`SelectTarget`, 같은 캡슐 위에서 뗐을 때만), `longPressSeconds` 이상 누르고 있으면 `UnitDetailPanel.Open` (적도 아군 카드와 같은 상세 창)
- `SkillNameBanner`: 적(기본은 보스만, `BattleFxView.enemySkillBannerBossOnly`)이 스킬로 행동하면 `BattleFxView.PlayAction`이 이름 띠를 띄우고 `LeadSeconds`만큼 기다린 뒤 공격 모션
- 아군·보스 캡슐의 머리 위 상태효과 줄(`UnitStatusBar`)은 끔 → 아군은 아래 카드, 보스는 상단 HP바에서만 보여준다
- 머리 위 이름·HP 글자(`UnitView.label`)는 쓰지 않음. 나머지 적은 상태효과 줄 아래(`hpBarOffsetY`)에 `UnitHpBar`(Quad 3장: 어두운 판·잔상·빨간 HP, 상태효과 블럭과 같은 단색 머티리얼 `UnitStatusBar.BlockMaterial`)를 붙여 화면 HP(`ShownHp`) 비율을 그림. 화면에서 쓰러지면 끔
- HP 막대는 세로 그라데이션 그림(`Art/UI/BarGradient`)을 Image 색으로 물들여 쓴다 (기본 UISprite는 늘리면 끝이 어둡게 번짐)
- `PartyBarView`·`PartyCardView`: 아래 아군 초상화 카드 (HP·스택·차례·대상 표시, 누르면 `SelectTarget`). 카드 아래 버프 줄·디버프 줄(줄마다 5칸 + "…")(목록은 `UnitView.BuildDisplayedStatuses`와 같음). 꾹 누르면(`LongPressed`, 손을 뗄 때의 클릭은 무시) `UnitDetailPanel`
- `UnitDetailPanel`: 탭(스탯 / 버프·디버프 / 스킬)마다 내용 묶음 하나를 켠다. 목록은 카드 원본(`DetailEntryView`)을 복사해 채우고 다시 열 때 재사용
  - 효과 카드 글은 `StatusDescriptionBuilder`, 스킬·패시브 카드 글은 `SkillDescriptionBuilder`
  - 효과 블록마다 `SkillEffect.Describe`(설정 수치로 만든 한 줄 설명)를 override한다. 설명이 없는 블록은 `[SubclassMenu]` 이름으로 대신함 (`DescribeOrName`). 새 블록을 만들면 Describe도 함께 작성
  - 숫자·스탯 이름·enum 한국어 이름(`[InspectorName]`) 표기는 `EffectText`(Data) 공용
- 스킬 버튼: `SkillButtonView`(원형 버튼 하나의 모양: 테두리·빛·이름·칸 표시·쿨타임 덮개)를 `BattleHud`가 상태만 넘겨 그리게 하고, 고른 스킬 이름판(`skillPlate`)도 `BattleHud`가 채운다. 원 그림은 `Art/UI/Battle`(CircleFill·CircleRing·CircleGlow)
- `TurnOrderView`: 칸마다 원형 초상화(`Disc` Mask + `IllustrationView`, 없으면 첫 글자)·테두리 색·이름·게이지. 지금 차례 칸은 `currentScale`배. 행동 순서 칸을 "유닛 + 몇 번째 등장"으로 기억해 순서가 바뀌면 새 자리로 미끄러뜨린다 (레이아웃 그룹은 끄고 자리를 직접 계산). 당기기 표시는 `BattleUnit.TotalActionAdvanced`(누적 당기기, 표시용)를 지난번과 비교
- 3D 모델 (캡슐 대신): `CharacterData.BattleModel` 프리팹(맨 위에 `UnitModel`)을 `UnitView.CreateModel`이 몸의 자식으로 만들고 캡슐 렌더러를 끈다. 이름표·상태효과 줄 높이·누르는 영역(캡슐 콜라이더)은 `UnitModel`의 값으로 맞춤 (큰 모델은 `BodyOffsetZ`만큼 뒤로 물려 세우고 이름표·콜라이더도 같이 옮김)
  - 모션은 캡슐과 같은 "기준 자리 + 어긋남"을 그대로 쓰고, 그때마다 애니메이션을 함께 재생: 돌진 → `PlayAttack`(나가는 시간 = `UnitModel.AttackImpactSeconds`, `UnitView.LungeOutSeconds`도 이 값이라 `BattleFxView`가 내리치는 순간에 타격 박자 재생), 시전 → `PlayCast`, 피격 → `PlayHit`, 처치 → `PlayDie`(굴러가며 날아가지 않고 조금만 밀림). 화면 생사(`ShownHp.IsDead`)로 쓰러진 자세·어두운 색, 부활하면 `PlayIdle`
  - 애니메이션은 Animator 상태 이름을 `CrossFadeInFixedTime`으로 바로 재생 (조건·트리거 없음). 색은 편 색 대신 원래 재질 색 × 배율(쓰러짐 어둡게, 번쩍임 밝게)을 MaterialPropertyBlock `_BaseColor`로
  - 바위 골렘(`Prefabs/Models/Model_RockGolem`): 외부 에셋(`Assets/3Dmodels/Mini Legion Rock Golem…`)의 `HP_Golem` 프리팹을 1.3배로 품고, 컨트롤러만 `Art/Character/Golem/GolemBattle`(Idle·Attack=Attack02·Hit=GetHit·Cast=Victory·Die, 원본 클립, 동작이 끝나면 Idle)로 바꿔 끼움. 에셋 재질은 내장 Standard 셰이더라 URP에서 분홍이어서 URP Lit로 바꿔 둠
  - 공격 타이밍: Attack02는 0.97초(원래 속도)에 주먹이 가장 앞으로 뻗음(손 위치를 시간별로 재서 확인) → 1.4배속이라 `AttackImpactSeconds` 0.7
  - 직접 만든 휴머노이드 근육 곡선 클립(`GolemIdle`·`GolemSmash`·`GolemHit`·`GolemCast`·`GolemDie`)은 남겨 두고 지금은 쓰지 않음
- 시전(버프·회복): 기본은 캡슐처럼 떠오르며 금빛 번쩍임 + `PlayCast`. `UnitModel.liftOnCast`를 끄면 떠오르지도 번쩍이지도 않고 "빛나는 부품"(`castGlowRenderers`)의 `_EmissionColor`만 MaterialPropertyBlock으로 0 → HDR 색 → 0 (재질에 Emission이 켜져 있어야 함). 이때 `UnitView.CastRiseSeconds`(버프 박자 시점)는 가장 밝아지는 시간
  - 마법 탑(`Prefabs/Models/Model_BuffTower`, 공세의 사도): 외부 에셋 `Assets/3Dmodels/Tower/Prefab/Tower Mage`를 1.15배로 품고, 보석(`Crystal`)만 발광을 켠 복사 재질 `Art/Character/Tower/TowerCrystal`로 바꿔 끼움. 애니메이터 없음
  - 나이트메어 드래곤(`Prefabs/Models/Model_NightmareDragon`, 갑각의 거체): 외부 에셋 `Assets/3Dmodels/FourEvilDragonsHP`의 `DarkBlue` 프리팹(Generic 리그)을 1.3배로 품고, 컨트롤러만 `Art/Character/Dragon/DragonBattle`(Idle·Attack=Basic Attack·Hit=getHit·Cast=defend·Die·Walk·Claw·Scream, 공격 상태는 끝나면 Idle)로 바꿔 끼움. 타격 시간은 원본 클립을 실제 Animator로 재생하며 입(`Jaw03`)·앞발(`R_Hand`) 위치로 잼. 스킬 동작 `Roar`(2스, 제자리)·`Sweep`(3스, 걸어가기)
- 몸 날리기 동작: `ModelAttackMotion.leap`(웅크림·날아가는 시간·높이·멈추는 거리·구르는 각도) → `UnitView.LeapAttackRoutine`(포물선 = 4 × 높이 × t(1 - t), 몸 옆축으로 회전). 타격 순간 = 웅크림 + 날아가는 시간. 날아가는 동안(`isLeaping`) 쓰러짐 표시를 미룸. `vanishOnImpact`면 닿는 순간 `UnitView.IsGone` + `SetActive(false)`, `BattleStageView.RefreshAll`은 `IsGone`인 유닛을 다시 켜지 않음
- 연타 동작: `ModelAttackMotion.hitInterval`(타 사이 시간) → `UnitView.LastHitInterval` → `BattleFxView.currentHitInterval`이 그 행동의 연출 다단 히트(스킬 "연출 타수") 간격을 동작의 타격 순간에 맞춤 (0이면 기본 `multiHitInterval`)
- 큰 모델 카메라 거리: `UnitModel.cameraDistanceScale`(기본 1) → `UnitView.CameraDistanceScale`. `BattleCameraDirector.GetShotPosition`이 상대 줄을 담는 구도(평소 시점·스킬 구도 1~3)일 때 화면에 보이는 상대 중 가장 큰 배율로 "바라볼 곳 + (자리 - 바라볼 곳) × 배율" (각도는 그대로, 거리만 늘림). 쓰러지면(`ShownHp.IsDead`) 1로 돌아와 SmoothDamp로 다가감. 나이트메어 드래곤 1.45, 오크 지휘관 1.2
  - 오크 지휘관(`Prefabs/Models/Model_OrcCommander`, 군단 지휘관): 외부 에셋 `Mini Legion Grunt…/Prefab/GruntHP`(휴머노이드)를 2배로 품고 `Art/Character/Orc/OrcBattle`(Attack=Attack02 주먹·도끼 2연타·Hit=GetHit·Cast=Attack01·Die·Walk). 스킬 동작 `Combo`(조금 걸어가기 + 0.58초 + 연타 간격 0.25초) + 1스 연출 타수 2. 시전은 떠오르지 않음(`liftOnCast` 끔)
  - 폭탄병(`Prefabs/Models/Model_Bomber`, 자폭병): 외부 에셋 `JKT_ART/mon_00/Mon_00`(Generic)을 2.2배로 품고 `Art/Character/Bomber/BomberBattle`(Attack=walk 1.6배속·Hit=damage·Walk·Explode=`Art/Character/Bomber/BomberExplode`, Idle로 돌아가지 않음). BomberExplode는 원본 attack01에서 폭발 메시(`bomb`)·연기(`smoke_fx01`)를 켜는 키만 뺀 복사본: 도화선 불꽃을 켜고 0.73초에 몸(`Object001`)을 끔 (폭발 이펙트는 직접 만들 예정). 재질은 MatCap(옛 LightMode Always라 URP에서 분홍) → URP Lit, 옛 파티클 셰이더 → URP Particles/Unlit(가산·반투명, 색 2배). 스킬 동작 `Explode`(몸 날리기 + 사라짐: 웅크림 0.3초 + 날아감 0.55초, 폭발 `FxBombBlast` 1.6배). 몸이 날아가는 도중 꺼지지 않게 BomberExplode에서 `Object001` 끄기 키도 뺐음
- 스킬마다 다른 모델 동작: `SkillData.ModelMotion`("모델 동작" 이름) → `UnitModel`의 스킬 동작 목록(`ModelAttackMotion`: 공격 상태·타격 시간·머무는 시간·걸어가기·걷는 상태·속도·최대 거리·멈추는 거리)에서 찾음. `BattleFxView`는 `UnitView.PlayAttackMotion(대상, 동작 이름)`이 돌려준 시간(걷는 시간 + 타격 시간)만큼 기다린 뒤 피해 박자를 재생. 이름이 없거나 목록에 없으면 기본 돌진
  - 스킬 동작의 `chargeGlow`·`impactEffectPrefab`: 공격 상태 시작부터 타격까지 `UnitModel.SetFlash`로 몸이 점점 밝아지며 맥동(진행도² × 빨라지는 sin), 타격 순간 `UnitModel.EffectAnchor`(가슴 뼈)에 이펙트를 만들고 빛을 0.4초에 걸쳐 뺌 (`UnitView.ChargeAndBurst`)
  - 축적의 수호자 축적 해방 = `Release`: 걸어가 직접 만든 `GolemRoar`(실제 Animator로 잰 승리 포효 자세: 팔꿈치 옆으로·주먹 머리 높이·가슴과 고개 젖힘, 팔·고개만 떨림 키가 점점 촘촘). 몸통을 젖히면 휴머노이드 몸 중심이 바뀌어 다리가 들리므로, 엉덩이 회전이 Idle과 같아지도록 RootQ를 앞뒤로 돌리고 RootT.y로 발을 땅에 맞춤 + 금빛 기 모으기 → 1.5초에 `FxMeteorExplosion`(3.5배) + 흔들림 1.0
  - 축적의 수호자 광역 강타 = `Slam`: 원본 `Walk`로 걸어가 직접 만든 `GolemSlam`(이 골렘의 Idle 0초 자세 기준 근육 곡선, 0.78초에 내려찍음). 근육 값은 감으로 넣지 않고, 한 자세짜리 클립을 실제 Animator로 재생(`Rebind`·`Play`·`Update`, 게임과 같은 경로)해 손 위치·몸 기울기를 재며 목표(치켜듦 = 머리 위 + 뒤로 젖힘, 내려찍음 = 몸 앞 낮은 곳 + 앞으로 24도 숙임)에 가장 가까운 값을 격자 탐색으로 찾음. `AnimationMode`·`HumanPoseHandler`로 잰 값은 실제 Animator와 척추·팔 앞뒤 방향이 반대로 나와서 쓰지 않음
  - 스킬 동작의 `impactShake`·`impactShakeSeconds`: `UnitView.LastImpactShake`로 넘겨 `BattleFxView`가 타격 순간에 `BattleCameraDirector.Shake`
  - 모델 칸에는 원본 모델이 아니라 `UnitModel`이 붙은 전투용 프리팹을 넣는다 (원본을 넣으면 캡슐로 남고, `CharacterData.OnValidate`가 경고)
- `Billboard`: 이름표·상태효과 줄이 카메라를 보고 거리에 맞춰 크기를 조절. `UnitView`의 조준 고리(`TargetRing`)는 카메라 쪽으로 당겨 그림
- 숨겨진 유닛의 이펙트는 `BattleFxView`가 건너뜀

### 연출 재생 (현재 임시 구현)
- 로직은 행동 하나(스킬 → 턴 종료 → 추가 공격)를 한 프레임에 끝까지 계산하고, 그동안 `BattleEvents`의 연출용 사건(`DamageTaken`, `Healed`, `BuffApplied`)을 알린다
  - 피해는 `StatusApplier.DealDamage`, 회복은 `HealEffect`·지속 회복, 버프는 `StatusApplier`의 부여 결과에서 한 곳씩만 알린다
- `BattleFxView`(`IBattlePresentation`)가 사건을 받은 순서대로 박자에 담아 간격을 두고 재생 (파티클 + 피해 숫자 `DamagePopup`)
  - 같은 종류이고 대상이 겹치지 않으면 한 박자(광역기 동시 타격), 아니면 다음 박자(타격 → 버프)
  - 지속피해는 예외: 이어진 지속피해끼리는 같은 유닛이어도 한 박자로 묶고, `PlayDotBurst`가 짧은 간격으로 숫자를 흩뿌린다. 색은 `DamageTakenInfo.DamageOverTimeSource`(상태효과)의 `DamagePopupColor`
  - 버프는 예외: 이어서 들어온 같은 유닛의 버프는 새 박자 대신 같은 글자에 이름을 줄바꿈으로 붙인다 (한 스킬의 여러 버프를 한 번에)
- `BattleManager`는 턴이 끝날 때마다 재생이 끝나기를 기다린 뒤 다음 턴·웨이브 전환으로 넘어간다
  - 행동 박자를 시작하기 전에 `BattleFxView.WaitForPreviousAction`이 앞 행동의 전용 연출(`isSkillFxRunning`)과 앞 행동 유닛의 모션(`UnitView.IsInMotion`, 꺼지면 풀림)이 끝날 때까지 기다린다(최대 15초) → 다음 행동의 피해가 앞 동작 도중에 끼어들지 않음. 지속피해만 든 박자(턴 종료 발동·격발)도 같은 대기를 거친다
  - 재생 코루틴(`PlayBeats`)은 마무리 대기(`endHold`) 중에 들어온 박자도 이어서 재생한 뒤 끝난다 (웨이브 시작 연출·끌어온 턴 직후처럼 재생이 끝나기 전에 새 행동이 들어와도 멈추지 않게). 전용 연출 알림은 박자마다 자기 대상 것만 꺼내고, 연출이 끝났는데 알림이 없는 대상은 남은 피해로 마무리한다
- 타격감 (기획서 3-11)
  - `BattleEvents.ActionStarted`(`SkillExecutor.ExecuteEffects`가 스킬·추가 공격 시작 때 알림, 3스 여부는 칸 번호)로 행동 박자를 따로 만들어 컷인(`SkillCutInView`) → 돌진(`UnitView.PlayLunge`) 또는 시전(`UnitView.PlayCast`)을 기다린 뒤 타격·버프 박자를 재생
    - 패시브 효과는 기본으로 알리지 않고, `PassiveRunner`가 따로 보이는 행동(턴 종료 단계 효과, `IsStandaloneAction`: 공격 안 한 턴 종료·자가 부활·등장·처치·자기 턴 시작)만 `announcePassive`로 알린다
    - 스킬 전용 연출: `SkillData.PresentationPrefab`에 `SkillFx`(전략 패턴, 예: `MeteorShowerFx`)가 있으면 `ActionStartInfo.Skill`로 찾아 돌진 대신 재생. 연출이 대상에 닿을 때 부르는 콜백 `onImpact(대상, 비율)`로 `BattleFxView`가 그 대상 피해의 그 비율만큼만 보여준다(`impactQueue`·`shownRatio`, `PlayHitsOnImpact` → `PlayPortion`). 비율 합이 1이 되는 마지막 타격에 남은 피해·처치·보호막 깨짐(연출 타수가 2 이상이면 남은 피해를 다시 나눔). 연출이 끝나면 남은 대상은 비율 1로 마무리. 예: 잔챙이 메테오가 대상마다 합쳐 30%, 거대 메테오가 나머지. 메테오·폭발 모양은 프리팹 파티클(`Prefabs/Fx/FxMeteor*`), 코드는 위치·순서·시간·카메라 컷만
    - 종결기처럼 추가 공격으로 나가는 스킬도 `FollowUpAttack.Skill`에 스킬 데이터를 담아 넘기므로 같은 방식으로 전용 연출을 쓴다 (`StatusApplier.QueueFinisher`). 하이퍼캐리 종결기 = `BoundaryCollapseFx`(영역 벽 구체·큐브 파편 파티클 `Prefabs/Fx/FxBoundaryCollapse`, 그림·재질 `Art/Battle/Domain`)
    - 격발 딜러 2스 = `WebBurstFx`(`Prefabs/Fx/FxWebBurst`), 3스 = `BindingBurstFx`(`Prefabs/Fx/FxBindingBurst`, 실 새장 + 번개는 `GlowLines.SetZigzag`). 선 모양은 프리팹 안 꺼 둔 견본 `LineRenderer`(재질 `Art/Battle/Web/FxLine`, 색·굵기)를 `GlowLines`가 복제해 쓰고, 코드는 점 위치·밝기(MaterialPropertyBlock)·투명도만 바꾼다. 불꽃·폭발은 `FxWebSpark`·`FxBindBurst`(타격·메테오 폭발 파티클을 보라·분홍으로 바꾼 것)
    - 하이퍼캐리 2스 = `GoldenSlashFx`(`Prefabs/Fx/FxGoldenSlash`, 굵기 곡선을 준 견본 선 + 겹마다 색 그라데이션으로 초승달 검기, 캐리를 중심으로 커지는 호, 어깨 시점 컷). 연출이 `CasterView.PlayLunge`로 직접 돌진시킨다
    - 패시브 추가 공격의 전용 연출: `PassiveData`의 "전용 연출" 칸 → `FollowUpAttack(owner, passive, …)`가 `PresentationPrefab`으로 넘김. 격발 딜러 패시브 추가 공격 = `ThreadBindFx`(`Prefabs/Fx/FxThreadBind`, 베지어 곡선으로 쏜 실 → 대상 둘레 나선 → 반지름을 줄여 조이기)
    - 추가 공격에도 전용 연출: `QueueFollowUpEffect`의 "전용 연출" 칸 → `FollowUpAttack.PresentationPrefab` → `ActionStartInfo.PresentationPrefab`(따로 준 프리팹, 없으면 스킬 데이터의 프리팹). `BattleFxView.FindSkillFx`는 이 값만 본다. 반격 태세의 반격 = `GroundSplitFx`(`Prefabs/Fx/FxGroundSplit`, 위 시점 컷 + 바닥에 눕힌 견본 선 + 균열 칸마다 솟는 바위 조각·튀는 파편(견본 큐브, `Art/Battle/Web/SplitRock` 재질) + 메테오 폭발 파티클)
    - 강탈 딜러 2스 = `StealSlashFx`(`Prefabs/Fx/FxStealSlash`, 대시·X자 검흔 + 대상 → 딜러로 길을 찾아 날아가는 기운 파티클: `Emit`으로 내보내고 `GetParticles`/`SetParticles`로 매 프레임 방향을 틈), 3스 = `MoonStrikeFx`(`Prefabs/Fx/FxMoonStrike`, 밤·안개(`RenderSettings.ambientProbe`·안개 색·안개 시작/끝 거리·배경색·방향광을 섞었다가 되돌림) + 안개 뭉치 `FxFogPuff`(알파 블렌딩 재질)로 사라짐 → 적 시점 컷 + 하늘의 달 판(안개를 받지 않는 `Sprites/Default` 재질, `Art/Battle/Moon`) → 내리꽂히다 위 시점 컷 → 폭발 `FxMoonImpact` + 바닥에 눕힌 견본 선으로 충격파 고리·균열)
    - 하이퍼캐리 변신 1스 = `SpaceRendFx`(`Prefabs/Fx/FxSpaceRend`, 카메라 평면 위 2점 참격선(견본 선의 색 그라데이션·굵기 곡선) → 참격마다 `onImpact` 일부 비율, 마무리 참격이 비율 1. 공간 조각 `FxRiftShards` = 영역 붕괴의 검은 큐브 재질 파편)
    - 하이퍼캐리 변신 2스(반격 태세) = `WingGuardFx`(`Prefabs/Fx/FxWingGuard`, 시전자 화면 오브젝트 아래의 `TransformAppearance.SetWrap(0~1)`로 날개를 접었다 펼침: 부모 기준 세로축으로 크게 돌리고(90도 근처는 날개 판이 옆에서 보여 얇아지므로 150도) 뿌리를 앞으로 옮겨 몸 앞에서 엇갈리게. 감싼 동안 날개 앞에 태양 문장 판 `Art/Battle/Sun/GuardSigil`(가산 발광)을 돌리며 맥동)
    - 독 서브딜러 3스 = `PoisonFlaskFx`(`Prefabs/Fx/FxPoisonFlask`, 기본 도형으로 만든 플라스크 견본(유리 투명 재질·발광 독·해골 라벨 `Art/Battle/Poison`) → 포물선 투척 → `FxFlaskShatter` → 연기 `FxPoisonSmoke`가 퍼지는 반지름이 적에게 닿는 순간 피해)
    - 도발 탱커 2스 = `GuardSlamFx`(`Prefabs/Fx/FxGuardEmblemSlam`, 방패 문양 판(가산 발광 재질 `Art/Battle/Guard/GuardEmblem`)을 MaterialPropertyBlock으로 밝기 조절, 충격파 `FxGuardSlam`·흡수 빛 `FxGuardAbsorb`, 탱커가 바라보는 방향(`CasterView.transform.forward`) 정면에서 탱커를 보는 연출 컷)
    - 도발 탱커 3스 = `FortressGuardFx`(`Prefabs/Fx/FxFortress`, 기본 도형 성벽 견본: 원점 = 아군 줄 가운데 바닥, +z = 카메라 쪽. `Risers` 아래 조각을 +z가 큰 것부터 솟게 함, 성문 빛 `GateGlow`, 깃발 `Art/Battle/Guard/Banner`(알파 컷), 돌먼지 `FxStoneDust`, 탱커 앞에 드는 원형 마법진 방패 `Art/Battle/Guard/MagicShield`(판의 앞 방향 축으로 계속 돌림), 연출 컷 카메라)
    - 곰인형 버퍼 2스 = `TeddyHugFx`(`Prefabs/Fx/FxTeddyHug`, 기본 도형으로 만든 곰인형 견본(`Art/Battle/Teddy`), 어깨 축 `ArmPivotL/R`을 돌려 팔을 벌리고 감쌈, 하트 `FxTeddyHeart`·반짝임 `FxTeddyPop`)
    - 곰인형 버퍼 3스 = `TeddyCheerFx`(`Prefabs/Fx/FxTeddyCheer`, 포옹 곰인형 견본에 폼폼(작은 공 뭉치)을 단 견본을 여러 마리 복제해 아군 줄 앞에 세우고, 박자 위치로 뛰기·흔들기·팔 들기를 계산)
      - 아군 대상 연출은 `SkillFx.AllyTargets`(BattleFxView가 같은 편 대상의 화면 위치를 넣어 줌)를 쓴다. 상대 타격이 없으므로 연출이 끝날 때까지 기다린 뒤 버프 박자를 재생
      - 타격 뒤에도 이어지는 연출(걸어 돌아오기 등)이 있어서, `BattleFxView.PlayBeats`는 전용 연출이 끝날 때까지(최대 15초) 기다린 뒤 재생을 마친다 → 그 전에 다음 턴의 무대 재배치로 캡슐이 순간 이동하지 않게
      - 캡슐을 연출이 직접 움직일 때는 `UnitView.SetScriptedPose`(월드 위치·방향 → 기준 자리 대비 어긋남으로 저장)와 `ClearScriptedPose`(기준 자리로 복귀, 연출이 사라지면 `OnDestroy`에서도 부름)
    - 흉신 마라 2스 = `InvertCleaveFx` 월드 참격선 모드(`useWorldSlashes`): 반전 동안 견본 선(`FxDomainSlash`와 같은 하얀 선, 점 12개)을 대상 몸에 한꺼번에 그어 두고, 반전이 끝나면 선마다 번쩍 → 혈흔·`onImpact(1/선 수)`. 체크를 끄면 아래 화면 베기 방식. `InvertCleaveFx`(`Prefabs/Fx/FxInvertCleave`): 옆 시점 컷 + 돌진·손대기(`SetScriptedPose`) → 런타임에 만든 Screen Space - Overlay 캔버스(정렬 1000)의 화면 전체 Image에 `ScreenInvert` 셰이더(블렌드 `OneMinusDstColor OneMinusSrcColor` = Lerp(화면, 1 - 화면, Image 밝기))로 반전 + 런타임 전역 `Volume`(우선순위 100, `ColorAdjustments` 채도 -100, weight = 반전 세기)으로 흑백 → 반전 중 같은 캔버스의 반전 판 뒤에 붙인 얇은 UI Image(대상 몸통을 지나는 긴 월드 직선을 `WorldToScreenPoint`로 옮김)로 화면을 가르는 선(가운데 각도 ± 폭 절반의 부채꼴)을 한꺼번에 그어 두고 → 반전이 끝나는 순간 반전·선을 끄고 `WaitForEndOfFrame` 뒤 `ScreenCapture.CaptureScreenshotAsTexture`로 화면을 찍어(리니어 색 공간에서 허옇게 보이지 않게 sRGB 그림으로 옮기고 알파를 1로 채움), 화면 사각형을 선마다 반평면으로 자른 볼록 다각형 조각(`ScreenSlicePiece`: MaskableGraphic, 꼭짓점 = 화면 픽셀, UV = 픽셀 ÷ 화면 크기. 코드로 만든 Graphic은 CanvasRenderer가 자동으로 안 붙어 같이 붙여야 그려짐)으로 검은 틈 판 위에 그린다. 조각마다 "선의 어느 쪽인지(2D 외적 부호) × 선 방향 × 거리"를 더해 어긋나게 하고, 베인 자리에 하얀 선 → 조각이 흐려지는 동안 실제 화면에서 혈흔 `FxBloodSpray`(알파 블렌딩 재질 `Art/Battle/Blood/Blood`, 늘어진 핏방울 + 피 안개, 프리팹의 +z로 뿜음)를 선에 수직(`Cross(카메라 쪽, 선 방향)`)으로 뿜고 `onImpact(1/선 수)`(숫자만 `hitInterval`씩 엇갈림), 마지막 1. `SkillFx.HidesDefaultHitEffect`를 켜 두어 `BattleFxView`가 이 대상들의 피해에 공통 타격 이펙트(`hitFx`·`criticalHitFx`)를 띄우지 않는다 (숫자·맞는 모션은 그대로)
    - 흉신 마라 3스 = `DomainDeployFx`(`Prefabs/Fx/FxDomainDeploy`, 정면 컷 + 떠오르기, 배경 `DomainScenery.Reveal`을 재생), 영역 공격 = `DomainSlashFx`(`Prefabs/Fx/FxDomainSlash`, `SkillFx.CutsToOpponents`가 켜져 있어 `BattleFxView`가 대상 위치를 읽기 전에 `BattleCameraDirector.CutToOpponentsOf`로 어깨 뒤·적 줄 장면에 컷(진행 중인 페이드 전환은 멈춤)하고 `OpponentRevealSeconds` 기다린 뒤, 견본 선으로 대상마다 하얀 참격 n번, 참격마다 `onImpact(1/n)`, 혈흔 `FxBloodSpray`)
      - 영역 배경: `DomainBoundaryEffect`의 "영역 배경" 칸(`FxShrineScenery`, `DomainScenery`) → `DomainBoundary.SceneryPrefab`. Battle 씬의 `DomainSceneryView`가 `StateChanged`마다 영역을 펼친 유닛(`ActiveBoundary is DomainBoundary`)을 찾아 배경을 만들고(`Place`), 영역이 끝나면 `Sink`. 펼치는 연출이 `Reveal`을 부르지 않으면 몇 초 뒤 `RevealInstant`
      - `DomainScenery`: 사찰·하늘 판은 `DomainSceneryView.LateUpdate`가 주인 화면 위치·방향·카메라 위치를 넘기는 `Follow`로 주인 등 뒤 수평 거리에 세워 주인 쪽을 마주 보게(주인이 안 보이는 장면에서는 마지막으로 본 주인 자리 기준). 카메라가 주인 등 뒤에 있으면 사찰·하늘을 "카메라 등 뒤 거리 + `cameraClearance`(7)"까지 밀어 큰 보스의 카메라 거리 배율에도 카메라가 사찰 안에 들어가지 않음, 물바닥(반투명 판)은 펼친 자리 중심 고정. 반사는 몸체 복사본의 부모 세로 크기를 -솟은 정도로 둬 바닥 아래로 자람. 분위기 세기 `Blend`(하늘 투명도)를 `DomainSceneryView`가 읽어 안개·카메라 배경·주변광을 섞고 원래 바닥(`BattleStage/Floor`)을 숨김
      - 영역 공격 연출 프리팹은 `FollowUpAttack`(턴 종료)·`ExecuteEffects`(펼칠 때)의 `presentationPrefab`으로 넘김
      - 영역 공격·해제 연출은 `SkillFx.CasterCasts = false`라 시전자가 떠오르지 않음
      - 업화 = `HellfireBowFx`(`Prefabs/Fx/FxHellfireBow`, 영역 블록의 바뀌는 스킬 칸 "전용 연출"): 떠오르기 컷(`SetScriptedPose`) → 겹마다 `GlowLines` 하나인 불꽃 줄(점 24개, Perlin 잡음으로 일렁임) + 불씨 `ParticleSystem.Emit` → 내려다보는 컷 → 시위·화살대(굵기 일정한 견본 선) + 화살 머리 `FxMeteor` → 폭발 `FxMeteorExplosion` + 적 전원 `onImpact(1)`
      - 영역 해제 순서: 남은 턴이 0이 되는 턴의 영역 공격에 `FollowUpAttack.Completed`(추가 공격이 끝나면 `BattleManager`가 부름, 건너뛴 경우도)를 달아, 그 공격이 끝난 뒤에야 `ShouldEnd`가 참 → `CheckEnd`. 끝날 때 `DomainBoundary.OnEnd`가 해제 연출(`DomainCloseFx`, `Prefabs/Fx/FxDomainClose`)을 `ActionStarted` 사건으로 알려, 연출 재생 순서상 마지막 공격의 피해 다음에 나온다
      - `DomainSceneryView`는 해제 연출이 있으면 끝난 영역의 배경을 지우지 않고 남겨 두고(`closingScenery`, 안전장치로 20초 뒤 가라앉힘), `DomainCloseFx`가 `DomainScenery.Dissolve`(사찰 조각을 반투명 재질 `ShrineFade`로 바꿔 끼우고 MaterialPropertyBlock 투명도 + 물·하늘·분위기 세기를 함께 줄임)로 지운다. 주인이 쓰러져 끝나면 화면에서도 쓰러질 때(`ShownHp.IsDead`, 쓰러뜨린 공격 연출이 끝난 뒤)까지 기다렸다가 `Sink` (`SinkAfterOwnerFalls`)
    - 하이퍼캐리 변신기 = `TransformCocoonFx`(`Prefabs/Fx/FxTransformCocoon`, 그림·재질 `Art/Battle/Sun`). 상대 타격이 없는 연출은 `BattleFxView`가 끝날 때까지 기다린 뒤 다음 박자로
    - 변신 모습(헤일로·날개): `TransformationData.AppearancePrefab`을 `UnitView.ApplyAppearance`가 변신 중(`IsTransformed`)에 몸의 자식으로 붙인다. 움직임은 `TransformAppearance`
      - 로직은 변신을 즉시 바꾸므로, 모습을 바꾸는 연출(`SkillFx.ChangesAppearance`: 변신기·종결기)이면 `BattleFxView`가 행동 시작 사건 때 `FreezeAppearance`로 지금 모습을 고정하고, 연출이 바뀌는 순간 `RevealAppearance`(→ `ReleaseAppearance`)로 실제 상태를 반영한다. 모습을 바꾸는 연출이 끝나거나 못 보여주면 그때 풀림 (다른 연출은 풀지 않음: 마지막 엑스트라 턴의 스킬 연출이 끝날 때 종결기가 걸어 둔 고정을 먼저 풀어 날개가 일찍 사라지던 문제 방지)
    - 연출 컷: `BattleCameraDirector.BeginCinematic`(정한 자리로 컷, 지정한 점을 계속 바라봄) / `MoveCinematic`(컷 중 자리·화각 이동, 기 모을 때 다가가기) / `EndCinematic`(원래 구도로 컷). 연출이 중간에 사라지면 `OnDestroy`에서 되돌림
    - 행동 유닛이 숨겨져 있으면 `BattleCameraDirector.ShowActionBy`가 그 유닛이 보이는 장면으로 페이드 전환한 뒤 모션을 재생한다
  - 캡슐 모션은 `UnitView`가 "기준 자리(무대가 `SetAnchor`로 정함) + 어긋남"으로 그린다 → 모션 중에 무대가 다시 세워져도 튀지 않음. 피격 번쩍임·밀림(`PlayHitReaction`), 처치 날아감(`PlayKnockdown`)
  - 타격 박자마다 가장 센 타격 하나로 화면 흔들림(`BattleCameraDirector.Shake`, 지난 흔들림을 빼고 이동한 뒤 다시 더함)과 히트스톱·슬로모션(`BattleTimeFx`, `Time.timeScale`을 실제 시간으로 잼)
  - 자동 전투: `BattleManager.IsAutoBattle`(`SetAutoBattle`)이 켜져 있으면 `WaitForPlayerAction`이 `AllyAutoPilot.Decide`(순수 C#, 스킬 칸·대상만 정함)의 결과를 고른 상태로 보여 준 뒤 `ConfirmAction`으로 확정한다 (플레이어 입력과 같은 경로). 버튼은 `BattleAutoButton`, 켜짐 여부는 PlayerPrefs `Battle.Auto`
  - 배속: `BattleTimeFx`가 배속(`speedSteps`, 기본 1·2·3)을 들고 평소 timeScale = 배속, 슬로모션 = 슬로 배율 × 배속, 히트스톱·슬로 시간은 ÷ 배속. 버튼은 `BattleSpeedButton`(`CycleSpeed`, `SpeedChanged`로 글자 갱신), 고른 단계는 PlayerPrefs `Battle.SpeedIndex`
  - 일시정지(`BattlePauseView`)도 `BattleTimeFx.SetPaused`로 같은 timeScale을 쓴다 (timeScale을 바꾸는 곳을 한 곳으로 모아 서로 덮어쓰지 않게, 정지가 가장 우선). 실제 시간으로 움직이는 연출(컷인·행동 순서 바·피해 합계)은 정지 중에도 마저 움직임
  - 처치 판정은 `DamageTakenInfo.IsLethal`(이번 피해로 HP가 0이 됨). 피해 합계는 `DamageTotalView`, 재생 한 번(= 한 턴)마다 0부터
  - 보호막 깨짐: 피해 전 보호막 > 0이고 후 = 0이면 `FxShieldBreak` 프리팹 + "보호막 파괴"
  - HP바 흔들림: `BattleFxView`가 `HpBarFeedback.RequestShake`(static 알림)를 보내고, 그 유닛을 그리는 `PartyCardView`·`BossHpBarView`가 `UiShake`로 흔듦 (레이아웃 그룹 안에서도 지난 흔들림을 빼고 더하는 방식)
  - 연출 다단 히트: `SkillData.PresentationHits`가 2 이상이면 `PlayMultiHit`이 타격 박자를 나눠 재생 (몫은 정수 나눗셈, 나머지는 마지막 타에. 화면 HP는 누적 비율로 줄임)
- 화면 HP는 연출에 맞춘다: `ShownHp`(static)가 연출 중에 피해·회복을 받은 유닛의 "사건 전" HP·보호막을 붙잡고(`Hold`), `BattleFxView`가 박자를 재생할 때 그 사건 직후 값으로 바꾸고(`Set`), 재생이 끝나면 놓는다(`ReleaseAll`)
  - HP바·숫자를 그리는 곳(`PartyCardView`, `BossHpBarView`, `UnitView` 머리 위 글자)은 `ShownHp.Hp/Shield`를 읽고 `ShownHp.Changed`에 다시 그린다
  - 피해 전후 값은 `DamageTakenInfo.HpBefore/HpAfter/ShieldBefore/ShieldAfter`, 회복 전 값은 "지금 HP - 회복량"
  - 상세 창(`UnitDetailPanel`)은 실제 값을 쓴다
  - 쓰러짐 표시도 화면 기준: `ShownHp.IsDead`(로직에서 쓰러졌고 연출이 그 유닛을 붙잡고 있지 않음)·`ShownHp.IsOnField`. 캡슐 회색, 카드 흐림, 보스 HP바 숨김, 상태효과 아이콘 지움, 무대 줄 세우기가 이 값을 써서 쓰러뜨린 행동의 연출이 끝난 뒤에 바뀐다. `BattleFxView`가 다음 행동 박자(추가 공격 등)를 시작하기 전에 `ShownHp.ReleaseFallen`(화면 HP가 0까지 내려간 쓰러진 유닛만 놓음), 재생이 끝나면 `ReleaseAll`

### 역할 경계
- **ActionQueue는 순서만** 관리한다. 피해 계산이나 상태효과 적용은 큐 밖의 효과 처리 코드가 맡아, 큐가 거대한 매니저가 되지 않게 한다
- **로직이 결과를 모두 정하고, 연출은 결과를 재생만** 한다. 피해량·사망 여부를 애니메이션 쪽에서 결정하지 않는다
- 로직이 화면에 의존하지 않으므로 캡슐 → 3D 모델 교체 시 로직은 수정하지 않고, 피해 공식 등은 Unity 없이 테스트할 수 있다

### 공격 한 번의 처리 순서
1. 스킬의 효과 블록을 순서대로 실행 (피해, 상태효과 부여, 강탈 등)
   - HP가 0이 되어도 이 스킬의 효과는 끝까지 적용
   - **즉시 개입형 반응**은 이 단계 안에서 처리: 피해가 들어가는 순간 결과를 바꾸는 것 (하이퍼캐리 버티기, 변신 강제 해제, 공격 판정에 묻는 화상)
2. 공격 종료 → 사망 처리 (HP 0인 유닛 제거)
3. 처리 중 발생한 이벤트로 **후속 행동형 반응**을 큐에 등록 (추가 공격, 추가 턴, 처치 반응, 턴 종료 단계 효과)
4. 정규 턴 종료: 지속피해 → 상태효과 감소 → 쿨타임 감소 → 게이지 리셋 → 미뤄둔 당기기 → **턴 종료 단계 효과** (강탈 버프 획득, 추격)
5. 큐에 쌓인 추가 공격을 순서대로 **임시 턴**으로 실행: 공격 → 사망 처리 → 턴 종료 단계 효과 (정규 턴 종료 처리 없음, 턴당 횟수 초기화 없음)
6. 추가 턴이 예약됐으면 같은 유닛이 다시 행동, 아니면 승패 판정 → 다음 행동자 결정

### 패시브와 큐의 규칙
- 패시브는 이벤트를 받아도 **직접 피해를 실행하지 않고 큐에 등록**한다 (즉시 개입형 제외)
- 후속 행동에는 **발생 원인**(누가, 어떤 행동으로 일으켰는지)을 담아, "자기 추가 공격으로 처치하면 재발동 안 함" 같은 규칙을 판단한다
- 반응형 패시브는 반드시 스택·횟수 제한을 가져 연쇄가 끝나도록 한다
  - 횟수 기준은 패시브마다 고른다: 매 턴 초기화(AnyTurn) / 자기 턴 시작 시만 초기화(OwnTurn). 추가 공격(임시 턴)은 초기화하지 않는다
- 상태효과의 추가·제거(만료, 소모, 보호막 깨짐, 밀려남)와 피해 적용(보호막 흡수)은 모두 `StatusApplier`를 거친다
  - "버프 획득"·"버프 지워짐" 사건과 지연 버프 변환 예약이 한 곳에서 빠짐없이 나가도록 하기 위함
- 같은 턴에 같은 유닛의 추가 턴이 여러 번 예약되면 큐에 등록할 때 1회로 합친다 (현재 기획상 겹치는 캐릭터는 없지만 안전장치로 유지)
- 추가 공격(임시 턴)은 패시브 전용이 아니다: "누가, 효과 목록, 대상 규칙, 노리는 대상"으로 큐에 들어간다
  - 패시브 추가 공격, 반격 태세의 반격, 변신 종결기가 모두 같은 큐를 먼저 들어온 순서대로 쓴다 ("반격 예약 → (턴 제한이면) 종결기 예약")
- 끌어온 턴 (반격 태세): `ForceOpponentsAttackEffect`가 `ActionQueue.EnqueuePulledTurn`으로 예약하고, `BattleManager.RunPulledTurns`가 행동자의 정규 턴 종료 직후 진행
  - 끌어온 턴은 상대의 실제 정규 턴: 스케줄러 시간을 흘리지 않고 바로 행동(`StartTurnOf` → AI → `EndTurn` → 그 턴의 추가 공격)
  - 끌어오기 전에 예약된 추가 공격(반격·종결기)은 `TakeAllFollowUps`로 맡아 두었다가 끌어온 턴이 모두 끝나면 `RestoreFollowUps`로 되돌림 → 상대 행동 → 반격 → 종결기 순서
- HP 돌파 반응(돌파 딜러)은 `StatusApplier.DealDamage`의 `HpLowered` 사건으로 "기준선 위 → 아래"를 판단하고, 타수를 `HitCounter`(유닛·패시브마다 하나)에 쌓는다
  - 쌓일 때 예약된 추가 공격이 없으면 1개만 예약하고, 이미 있거나 실행 중이면 그 추가 공격의 타수만 늘어난다
  - `BattleManager.ExecuteMultiHitFollowUp`이 타수를 한 타씩 꺼내 적 전체에 효과를 반복하고, 적(무적 제외)이 모두 HP 0이면 남은 타수를 버린다. 칠 적이 없으면 타수를 남겨 다음 발동에 더한다
- 오라 (기획서 3-7): 버프 칸 밖의 보정이라 `BattleUnit.GetAuraModifiers`가 스탯 계산 때 같은 편을 돌며 모은다
  - 패시브 오라(`PassiveTrigger.Aura`)와 기간 오라(`BattleUnit.ActiveAura`, `TimedAura`) 두 가지. 주인이 쓰러지면 꺼지고, **퇴장해도 유지**(`IsDead`만 확인)
  - 기간 오라는 `StartAuraEffect`(아스트라 공연)가 만들고, 주인의 정규 턴 끝(`BattleManager.EndTurn`)마다 남은 턴이 준다. 만든 턴은 세지 않음
  - `SkipsOwnerTurns`면 그동안 주인의 턴이 강제로 스킵된다(`BattleUnit.IsPerforming`, 기절과 같은 경로로 턴 종료 처리만)
  - 전장 이펙트: `AuraFieldView`가 매 프레임 카메라 앞(수평 거리 `followDistance`)으로 옮겨 카메라와 같은 방향을 보게 한다 (이펙트는 그 자식). 오라 데이터가 프리팹(`FieldEffect`)을 들고, Battle 씬의 `AuraFieldView`가 `StateChanged`마다 켜진 오라의 이펙트를 만들고 꺼진 오라의 이펙트를 지운다. 띠 흐름은 `FieldFxScroller`(텍스처 이동·서서히 나타남), 빛은 가산 HDR 재질 + Bloom
- "받은 피해 회복" 패시브(`OnOwnTurnStartAfterDamage`, 흉신 마라 역류 재생): `BattleUnit.TakeDamage`가 깎인 HP를 `DamageTakenSinceOwnTurn`에 더하고, `StartTurnOf`가 `HandleOwnTurnStart`(0보다 크면 발동, 회복 기준 "지난 자기 턴 이후 받은 피해") 뒤에 0으로 되돌린다. 부활하면 비움
- "공격하지 않은 턴" 패시브(`OnTurnEndWithoutAttack`, 아스트라): `PassiveRunner`가 자기 스킬 공격 행동을 턴마다 기록하고, `EndTurn`이 지속피해·사망 확정 다음에 `HandleOwnTurnEnd`를 부른다
- "상태효과 걸린 적 피격" 패시브(`OnAllyHitDebuffedEnemy`, 니콜 이자): `HandleAttackHit`에서 같은 편 전장 유닛의 패시브를 확인해, 맞은 적에게 `requiredStatus`가 있으면 발동. 발동 때 `stacksGainedPerTrigger`만큼 스택을 얻고(쓰는 스택 0), 2스의 스택 비례 피해 블록이 소모
- 보호막 (기획서 3-7): 유닛당 하나. `StatusApplier.GrantShield`가 보호막이 없으면 새 버프로 걸고, 있으면 그 하나에 양을 더하고 남은 턴을 긴 쪽으로 맞춘다(갱신이라 `BuffGained` 없음)
  - 양을 보탠 유닛은 `StatusInstance.ShieldContributors`에 남고, "버프 지워짐" 패시브는 `IsGivenBy`로 보탠 유닛 모두를 부여자로 본다
  - 보호막을 강탈해 받을 때(`Receive`)도 기존 보호막에 합친다
- 디버프 면역: 상태효과 종류 `DebuffImmunity`. `BattleUnit.IsDebuffImmune`이면 `StatusApplier.Apply`가 새 디버프를 막는다 (빈 결과)
- 시선집중 (도발 탱커): 패시브 발동 조건 `TauntWhileShielded`(상시). `BattleUnit.IsTaunting` = 전장에 있음 + 그 패시브 + 보호막 있음 (매번 계산, 버프 칸 아님)
  - `BattleManager.PickSingleOpponent`: 단일·확산의 (메인) 대상은 시선집중 유닛 → 고른 대상 → 무작위 순. 추가 공격도 같은 `ResolveTargets`를 거친다
  - 아이콘은 패시브의 표시 효과(`TauntDisplay`)를 `UnitView.BuildDisplayedStatuses`가 맨 앞에 넣는다
- 치명적인 피해(남은 HP 이상)는 `StatusApplier.DealDamage`에서 HP에 넣기 직전에 처리한다
  - 변신 중이면 강제 해제(HP 고정 + 종결기 예약), 아니면 `FatalDamageIncoming` 사건 → 버티기 패시브가 막으면 HP 1

### 전장 상태 (퇴장·변신)
- `IsDead`(쓰러짐)와 `IsOnField`(살아 있고 퇴장하지 않음)를 나눈다
  - 행동 순서·게이지 전진·대상 선택·반응 패시브·오라는 `IsOnField`, 승패 판정은 `IsDead`
- 변신은 `BattleUnit.Transformation`(런타임 상태)이 들고, 스킬·쿨타임은 항상 `BattleUnit.GetSkill` 등 유닛을 거쳐 읽어 변신 스킬로 자동 교체된다
- 하이퍼캐리 3스는 효과 블록 3개로 나뉜다: 변신(`TransformEffect`, 스킬 교체) → 아군 퇴장(`ExitAlliesEffect`) → 경계: 고립(`IsolationBoundaryEffect`, 스탯·면역)
  - 아군 퇴장: 퇴장시킨 유닛 목록을 시전자(`BattleUnit.ExitAllies`)가 들고, 변신 해제·시전자 사망 때 `ReturnExitedAllies`로 복귀
- **경계** (기획서 3-12)
  - `BoundaryEffect`(경계 블록 공통): 경계를 만들어 `BattleServices.Boundaries`(`BoundaryField`)에 펼친다. 공통 규칙은 "한 번에 하나"뿐
    - 아군: `BattleUnit.CanUseSkill`이 같은 편에 경계가 있으면 경계 스킬(`SkillData.DeploysBoundary`)을 막는다
    - 그래도 새 경계가 들어오면 `BoundaryField.Deploy`가 기존 경계를 끝내고 교체한다
  - 경계마다 블록 하위 클래스 + 런타임 `Boundary` 하위 클래스: 스탯 보정(`StatModifiers`), 행동 제어 면역, 표시용 효과(`DisplayStatus`), 끝날 조건(`ShouldEnd`)
    - 펼치면 주인의 `BattleUnit.ActiveBoundary`가 되어 스탯 계산(상태효과 칸 밖 보정)·면역(`IsCrowdControlImmune`)·머리 위 아이콘(`UnitView`)에 반영된다. 버프 칸이 아니라 강탈·소모·만료 대상이 아니다
  - `BattleManager`가 변신 해제·사망 확정 직후 `Boundaries.CheckEnd()`를 불러 끝날 조건을 확인한다
  - 주인 턴 종료: `EndTurn`이 `Boundaries.HandleTurnEnd(actor)` → `Boundary.OnOwnerTurnEnd`(영역: 적 전체 추가 공격 예약 + 남은 턴 감소) 뒤 끝날 조건 확인. 남은 턴은 `Boundary.RemainingTurns`로 아이콘 숫자에 표시
  - 스킬 교체: `Boundary.GetSkillOverride(slot)`이 있으면 `BattleUnit.GetSkill`이 변신 스킬보다 먼저 그 스킬을 돌려주고 쿨타임은 0. `SkillExecutor.ExecuteSkill`은 바꾼 스킬을 쓰면 쿨타임 대신 `OnSkillOverrideUsed`로 알려 1회용을 되돌린다 (원래 칸의 쿨타임은 건드리지 않음)
- 중첩 방식은 버프·디버프 공통으로 데이터를 따른다 (`StatusContainer.IsRefreshOnly`). 아군 버프 에셋은 모두 '갱신', 보스 1 공세만 '중첩'(걸 때마다 새 칸)
- 스택 효과 (기획서 3-7, 버프·디버프 공통, 지금 쓰는 효과 없음): 상태효과 중첩 방식 `StackCount`면 `StatusContainer.Add`가 기존 칸의 `StatusInstance.AddStack`(스택 +1·최대 스택·남은 턴 갱신·스냅샷은 높은 쪽)만 부른다
  - 지속피해는 `DamageCalculator.CalculateDamageOverTime`에 스택 수를 넘겨 1스택 피해 × 스택. 아이콘 숫자는 `StatusInstance.IconText`("×스택")
  - 스탯 보정은 `BattleUnit.GetStatusModifiers`가 `StatModifier.Scaled(스택)`로 한 줄로 넘긴다 (공·방 % 중첩 감소에서도 버프 1개)
  - 강탈로 받을 때(`StatusContainer.Receive`) 같은 스택 효과가 있으면 `MergeStacks`로 스택을 합친다 (새 버프가 아님). 없으면 1칸 그대로 새 버프
- 적 패턴 조건 `SelfDebuffCountBelow`(자신의 디버프 N개 미만, 스택 효과는 1칸 = 1개): 보스 2의 디버프 없는 턴 공격
- 적 패턴 조건 `SelfBuffStacksAtLeast`(자신의 버프 스택 합 N 이상, 스택이 없는 버프는 1칸 = 1, `StatusContainer.CountBuffStacks`): 보스 1 공세 4개 광역·10개 축적 해방
- 피격 무효 (지금 쓰는 보스 없음): `BattleUnit.HitBlockCharges`(버프 칸 밖의 횟수)를 `StatusApplier.DealDamage`가 무적 확인 다음에 1회 쓰고 피해를 0으로 바꾼다 (보호막·피해 기록·HP 돌파 사건 없음, 연출은 `DamageTakenInfo.BlockedLabel` "무효")
  - 피해 함수만 막으므로 같은 스킬의 다른 효과 블록은 그대로 실행된다. 충전은 패시브 발동 조건 `OnOwnTurnStart`(자기 턴 시작 시, `BattleManager.BeginTurn` → `PassiveRunner.HandleOwnTurnStart`)
  - 아이콘은 충전 블록의 표시 효과를 `UnitView.BuildDisplayedStatuses`가 남은 횟수와 함께 맨 앞에 넣는다
- 무적(`IsInvulnerable`)은 스테이지 설정으로 정해진다: 피해 0, 승리 판정에서 제외 (무적이 아닌 적 전멸 = 승리)
  - 피해를 막는 판정은 `IsDamageImmune` = 스테이지 무적 또는 "피해 무효" 버프(힐러 3스 무적). 버프 무적은 승리 판정과 무관
- 소환 (보스 3): `BattleManager.SummonUnit`이 부하를 만들어 편 목록(소환자 왼쪽·오른쪽 번갈아)과 `allUnits`에 넣고 `UnitsSpawned`로 화면(`BattleStageView`)에 알린다
  - 부하는 `BattleUnit.Summoner`를 가져 `IsSummon` → 승리 조건(`StageRunner.IsWaveCleared`)에서 빠지고, 소환자가 쓰러지면 `ConfirmDeaths`에서 함께 `Vanish`
  - `Vanish`는 처치 사건 없이 사라지는 처리 (자폭·함께 퇴장). 자기 턴 중에 사라진 유닛은 턴 종료의 지속피해·남은 턴 처리를 건너뛴다
  - 패시브 발동 조건 `WhileSummonsAlive`(상시): 소환한 부하가 있는 동안 "오라 스탯 보정" 칸을 자신에게만 적용 (`GetNonStatusModifiers`)
- 자기 보정 상시 패시브는 `BattleUnit.GetSelfPassiveScale`(0 = 꺼짐, 1 = 그대로, 2 이상 = 그 배수) 하나로 판정해, 스탯 계산(`GetNonStatusModifiers`)과 아이콘이 같은 값을 쓴다
  - `WhileOwnStatusCount`(상태효과 수 조건): 자신의 버프 또는 디버프 칸 수가 기준 이상/미만이면 보정 1배. 보스 2: 디버프 2개 미만이면 속도 증가
  - `ScalesWithOwnStatusCount`(상태효과 수 비례): 칸 수(최대 개수까지)만큼 `StatModifier.Scaled`. 보스 1: 버프 1개당 피해 증가·피해 감소 10%. 아이콘의 숫자 = 지금 개수
  - 칸 수로 세므로 스택 효과는 1개. 판정·배수 계산은 `PassiveData.GetStatusCountScale`, 상시형 목록은 `PassiveData.IsAlwaysOnTrigger`(에디터·설명 글 공용)
  - 패시브 발동 조건 `OnEnter`(등장 시): 전투 시작(전원)·웨이브 시작(새 적) 때 `PassiveRunner.HandleUnitsEntered`가 실행 → 시작 부하 소환. 화면 캡슐을 만들기 전에 실행해 부하가 처음부터 자리에 선다
  - 적 패턴 조건 `OwnTurnAtLeast`(자기 턴 N번째 이상, `BattleUnit.OwnTurnsTaken`)·`SummonsBelow`(소환한 부하 N마리 미만)
  - 카운트다운 아이콘: `EnemyCharacterData`의 표시 효과·행동 턴으로 `UnitView`가 남은 턴(행동 턴 - 지난 자기 턴)을 그린다
- 상시 패시브 아이콘: `BattleUnit.GetActiveConditionalPassives`(시선집중·부하가 있는 동안·상태효과 수 조건/비례)의 표시 효과(`PassiveData.DisplayStatus`)를 `UnitView`가 맨 앞에 넣는다
- 부활 (기획서 3-9): `BattleUnit.Revive`가 `IsDead`를 풀고 HP·상태효과(비움)·남은 거리(10000)를 정한다. 쿨타임·스택은 유지
  - 자가 부활은 패시브 발동 조건 `OnOwnDeath`: `ConfirmDeaths`의 처치 사건 → `PassiveRunner`가 처치자 반응 다음에 쓰러진 본인의 패시브를 실행
  - 쓰러진 아군을 고르는 대상 규칙 `AllySingleOrFallen`: `IsValidTarget`이 퇴장하지 않은 아군이면 쓰러졌어도 허용
  - `BattleStageView`의 TeamLine은 쓰러진 아군의 자리를 비워 두고(`fallenSlots`), 고를 수 있을 때만 그 자리에 흐리게 보여 클릭할 수 있게 한다. 하단 카드도 같은 판정
- 기절(행동 불가 상태효과)이면 행동 선택·스킬을 건너뛰고 턴 종료 처리만 한다

---

## 4. 폴더 구조

```
Assets/_Project/
  Scripts/
    Core/        공용 (세이브, 씬 이동, 매니저, SubclassSelector 속성)
    Data/        SO·데이터 정의 (CharacterData, StatusEffectData, StatModifier, ...)
      Skills/    스킬·패시브 정의 (SkillData, SkillTargetType, PassiveData)
    Battle/      전투 흐름 (BattleManager, BattleUnit, TurnScheduler, DamageCalculator,
                 BattleEvents, ActionQueue, ActionSource)
      Skills/    스킬 실행 (SkillExecutor, SkillContext)
        Effects/ 효과 블록 (SkillEffect, DamageEffect, HealEffect, ApplyStatusEffect, ...)
      Passives/  패시브 실행 (PassiveRunner)
      Status/    상태효과 (StatusInstance, StatusContainer, StatusTurnProcessor)
    Outgame/     가챠(GachaMachine = 확률 판정, GachaService = 뽑기·저장), 육성, 편성
    UI/          화면 (UnitView, BattleStageView, BattleHud, ...)
    Editor/      에디터 스크립트 (효과 블록 드롭다운 등)
  Data/          SO 에셋 (Characters/ = 플레이어블, Enemies/ = 적·보스, StatusEffects/, Stages/, Gacha/ = 가챠 배너)
  Resources/     코드가 이름으로 불러오는 에셋 (GameDatabase)
  Prefabs/  Scenes/  Art/ (Art/UI = 로비 UI 그림: 배경·패널·아이콘)
```
- `_Project`로 감싸 에셋 스토어 에셋과 섞이지 않게 한다
- **정의(Data)와 실행(Battle)을 나눈다**: Inspector에서 조립하는 데이터는 `Data/`, 그 데이터를 전투에서 실행하는 코드는 `Battle/`
- 폴더를 옮겨도 **네임스페이스는 바꾸지 않는다**. 효과 블록은 `[SerializeReference]`로 "네임스페이스 + 클래스 이름"이 에셋에 저장되므로, 바꾸면 조립해둔 효과가 사라진다

---

## 5. 구현 순서 (첫 목표)

1. 두 유닛 평타 → 피해 → 사망 → 전투 종료
2. 화상 하나 + 반응형 패시브 하나를 붙여 책임 분리와 처리 순서 검증
3. 행동 순서(속도), 쿨타임, 1~3스
4. 효과 블록 확장 → 핵심 캐릭터 3명 조립

- 위 4단계는 모두 완료 (2026-11-10 기준, 아래 6장 참고)

---

## 6. 구현 현황 (2026-11-18 기준)

### 데이터·저장
- SO 데이터: 캐릭터(등급·직군·스탯·1~3스·패시브·변신), 적(행동 패턴), 상태효과 33종(출혈·영역 표시 추가), 스테이지 11개(기본 7 + 보스 3 + 테스트 `Stage_BuffEnemy`), 전투 규칙, 가챠 규칙·배너, 무기·재료(골드·보석 포함)
- 효과 블록 27종 (목록은 2장 "효과 블록 목록" 표)
- Inspector 도구: 효과 블록 드롭다운(`SubclassSelector`), 상태효과 종류별 칸 표시
- 세이브: JSON 저장·불러오기, 임시 파일 교체, 깨진 파일 보관, `version` 필드(현재 7: 별 미션 기록), 새 게임 시작 캐릭터, 에디터 메뉴(세이브 삭제·폴더 열기, 테스트 아이템, 전부 Lv1로)
- 성장 규칙: 캐릭터·무기 경험치 곡선(Lv1→60 = 900,000), 진급 5회, 경험치 재료 3종·무기 강화석, 레벨업·진급 골드 비용 (`GrowthRulesData`)
- 재화: 뽑기는 보석을 씀(디버그 무료 뽑기 꺼짐), 골드·경험치·진급 재료는 육성에 씀(디버그 재료 무한 꺼짐), 디버그 키 F9 = 보석·골드 +100,000

### 전투 코어
- 행동 순서(남은 거리·속도·동시 도착 우선순위·사이클 카운터), 행동 당기기와 미뤄둔 당기기
- 스킬·쿨타임(시작 쿨타임), 피해 공식(방어 상수 K, 치명, 피해 감소 곱연산·상한), 레벨 성장 배율(아군·적 교차 곡선)
- 상태효과: 버프·디버프 각 10칸, 밀어내기, 중첩/갱신, 공·방 % 버프 중첩 감소, 지연 버프, 보호막, 지속피해(화상·독, 스냅샷), 지속 회복, 기절, 무적
- 큐 구조: 추가 공격(임시 턴), 추가 턴, 턴 종료 단계 효과, 반응형 패시브 횟수 제한(AnyTurn/OwnTurn)
- 전장 상태: 퇴장·변신·엑스트라 턴·종결기·강제 해제·행동 제어 면역
- 웨이브 진행(`StageRunner`), 승패 판정, 결과 화면(파티·획득 보상, 로비 복귀), 일시정지(계속하기·메인 화면으로), 자동 전투·배속(1·2·3x)
- 피해 난수(0.9~1.1, 전투 규칙에서 켜고 끔)
- 레벨·진급 배율(진급 시점에 점프), 장착 무기 스탯 반영
- 적 AI: 패턴 목록(조건 → 스킬·대상), 일반 적 랜덤 공격

### 캐릭터 (11 / 12명)
- S: 격발 딜러(DotMainAttack), 강탈 딜러(BuffStealAttack), 하이퍼캐리(HyperCarry), 곰인형 버퍼(BearBuffer), 공연 오라 버퍼(Astra), 도발 탱커(TauntTank), 부활 힐러(ReviveHealer), 흉신 마라(Mara)
- A: 독 서브딜러(DotSubAttack), 안젤리카(Ally_Support, 치명 버퍼 → 회복 직군으로 테스트 중), 돌파 반응 광역 딜러(GeniusAoE)
- 수치는 전부 테스트 값

### 스테이지·적
- 기본 스테이지 1-1 ~ 1-7: 1장 적 6종(`Data/Enemies/Ch1_*`: 오크 졸병·주술 탑·치유 탑·폭탄병·바위 골렘·오크 대장)으로 스테이지마다 기믹 하나씩, 이름·설명 포함 (모델 `Model_OrcGrunt`(지휘관 컨트롤러 + `OrcGruntBattle` 덮어쓰기: 공격 = Attack01)·`Model_GolemSmall`·`Model_HealTower`)
- 탐색 던전(재화 던전): 장소 4곳(골드·무기 강화석·경험치·진급 재료) × 난이도 3단계(Lv20·40·60), 이길 때마다 보상
- 스테이지 보상: 매번 받는 보상 + 첫 클리어 보상(지금 비어 있음) + 별 미션 보상(★1 클리어 20 / ★2 2분 이내 30 / ★3 아군 생존 50 보석), 상세 패널에 별 조건·보상 아이콘, 결과 화면에 별 결과
- 보스 1 축적의 수호자(지금은 `Stage_BuffEnemy` 2웨이브, `Stage_Boss1`은 테스트용): 무적 공세의 사도 2 + 보스, 공세 중첩 버프(4개 이상 광역, 10개 축적 해방 = 버프 해제 + 공격력 100% + 최대 HP 90%)
- 보스 2 갑각의 거체(`Stage_Boss2`, 보스전 2번째 칸, 2웨이브): HP 2배, 디버프가 없는 턴에 아군 전체 최대 HP 40% 피해
- 보스 3 군단 지휘관(`Stage_Boss3`, 보스전 3번째 칸, 2웨이브): 등장 때 부하 2마리, 부하(자폭병) 소환·최대 4, 부하가 있는 동안 피해 감소 50%, 부하는 2번째 자기 턴에 자폭(광역 + 기절)

### 화면
- 전투 HUD: 보스 HP바(상단), 웨이브·사이클, 원형 스킬 버튼(`SkillButtonView`)·쿨타임 덮개·스킬 이름판, 대상 선택, 행동 순서 목록(원형 초상화·지금 차례 확대·움직이는 순서), 상태효과 아이콘·툴팁 (전투 로그는 화면에서 숨김, 콘솔에만)
- 전투 임시 연출: 타격·치명·지속피해·회복·버프 이펙트 + 피해 숫자, 행동 단위로 순서대로 재생 (3장 "연출 재생")
  - 타격감: 돌진·시전 모션(추가 공격·패시브 행동 포함), 피격 번쩍임·밀림, 히트스톱·슬로모션, 화면 흔들림, 3스 컷인, 피해 합계, HP를 타격 순간에 맞춰 줄임, 보호막 깨짐, HP바 흔들림, 연출 다단 히트
  - 스킬 전용 연출(`SkillFx`) + 연출 컷 카메라: 선브링어 변신 3스 메테오, 하이퍼캐리 변신기·종결기, 격발 딜러 2스 거미줄·3스 실 새장, 하이퍼캐리 2스 황금 검기·반격 땅 가르기, 강탈 딜러 2스 강탈·3스 달빛 일격, 독 서브딜러 3스 독 플라스크, 곰인형 버퍼 2스 곰인형 포옹·3스 응원 춤, 도발 탱커 2스 방패 문양·3스 마법진 방패·성벽, 하이퍼캐리 변신 1스 공간 가르기·반격 태세 날개 감싸기, 흉신 마라 2스 반전 참격(하얀 참격선)·영역 공격 하얀 참격·업화 불의 활·3스 영역 전개(사찰·물바닥 배경)·영역 공격 하얀 참격 (임시)
- 상세 창(카드 꾹 누르기): 스탯 / 버프·디버프 / 스킬 탭
- 시작 화면(임시): 터치하면 세이브를 불러와 로비로, 이어하기/새 게임 표시
- 로비: 메인 화면(출격·모집·인벤토리·전투원 연결), 스테이지 선택(지도·보스 탭·상세 패널), 편성(4칸·보유 목록·팀 4개·팀 이름·저장, 메인 화면에서 열면 편성 관리만)
- 전투원: 보유 목록, 상세(스탯·무기 +n)/스킬 탭, 육성 창(레벨업·일괄 추가·진급), 무기 칸 → 장착 창(직군 제한·장착·해제·강화)
- 업적: 개요(달성 수·트로피·분류 카드·모두 수령) → 분류 목록(제목·자동 설명·진행 막대·보상·수령), 업적 56개, 메인 화면 알림 점
- 인벤토리: 무기·재료 탭, 정렬, 상세 패널(무기는 레벨 반영 스탯), 일괄 분해(등급 선택), 무기 칸 200·자동 분해
- 무기: 칼·방패·마법지팡이·로자리오 × 3·4·5성 = 12종
- 모집: 픽업 배너 8개, 1·10회 뽑기(천장·반천·A 확정, 4성 무기 10%·5성 무기 1%), 확정까지 남은 횟수, 물레 → 공개 → 결과 목록 연출(파티클 + Bloom), 확률 공시 창(등급별·개별 기본·최종 확률)

---

## 7. 남은 구현

> 기획서 1장 개발 순서 기준. 분류 안에서 위에 있을수록 먼저 할 것. (2026-11-18 정리)

### 기획 결정 (구현 전에 먼저 정할 것)
- 핵심재미·반복구조 확정 (기획서 0장). 후보: "큰 데미지를 뽑아내는 쾌감" (기획서 10장)
- 부활 힐러 역할 재검토: 적이 대부분 광역이라 단일 회복의 쓸모가 적음 (빼기 / 다른 컨셉으로 교체 검토 중)
- 강탈 딜러 범용 성능: 적 버프가 없으면 약함 (범용 서열 하이퍼캐리 > 지속피해 > 강탈). 바닥 올리기 후보: 뺏을 버프가 없으면 대신 1턴 버프 획득
- A 회복 힐러 안젤리카(Ally_Support): 스킬 구성 미정, 부활 힐러 결정과 함께 정리

### 전투
- 스킬 전용 연출 남은 것: 독 서브딜러 2스, 아스트라 2스·3스(공연 시작), 부활 힐러 2스·3스·자가 부활, 돌파 딜러 2스·3스·돌파 추가 공격, 니콜 2스·3스 (구현된 목록은 6장)
- 자동 전투 규칙 고도화 (지금은 높은 칸 우선의 임시 규칙, 배속·자동 버튼은 구현)
- 무기 반영 후 밸런스: 전투에 무기 스탯이 들어가 아군이 강해짐 → 적 데이터 에셋을 StatTable 적 스탯(3성 무기 0.8 기준)에 맞춰 다시 조정
- 타격감 보강 남은 후보: 치명·3스 카메라 줌 펀치, 스쿼시&스트레치, 방향 있는 히트 스파크, 돌진 잔상, 처치 폭발 (다단 히트·보호막 깨짐·HP바 흔들림은 구현, 화면 가장자리 번쩍임은 넣어 봤다가 뺌)
- 효과음 (타격·치명·처치·돌진·시전·컷인, 임시 소리 파일 필요)
- 전투 UI 정식 배치: 현재 행동 캐릭터 패널, 아군 스킬 이름 알림 (보스 스킬 이름 띠·보스 HP바·아군 카드·상세 창·행동 순서 초상화·원형 스킬 버튼은 구현)
- 방어력 관통 (기획서 10장 미정)

### 보스·스테이지
- 기본 스테이지 3분 목표 밸런스 (1장 적 구성은 초안으로 들어감)
- 보스 1 스테이지 정리: 지금은 `Stage_BuffEnemy` 2웨이브에 있고 `Stage_Boss1`은 테스트용
- 기록 후보 (핵심재미가 "큰 데미지"로 정해지면): 최고 한 방·한 턴 피해 합계, 토벌 최단 사이클 → 결과 화면 "신기록", 업적 연결
- 보스 AI를 Unity Behavior 패키지로 교체 (지금은 패턴 목록)

### 육성·재화
- 진급 재료·골드 수량 확정 (개수 임시, 골드 사용처는 레벨업·진급으로 구현)
- 중복 캐릭터 처리 (돌파 / 재화 변환, 미정)
- 무기 종류별 스탯 차이 여부 (지금은 등급만으로 차등, 미정)
- 스태미나·입장 제한 여부 (미정)

### 아웃게임 기타
- 시작(타이틀) 화면: 임시 화면 구현 (터치 → 세이브 불러오기 → 로비). 게임 제목·정식 그림은 미정
- 메인 화면: 플레이어 레벨·이름을 세이브와 연결 (지금은 고정 글자, 재화는 `CurrencyBarView`로 연결됨), 나머지 버튼(설정·공지·우편·출석·친구 등)은 틀만
- 심사자용 디버그 모드: 재화 무한, 모든 스테이지 개방
- 숨은 업적 추가 (사용자가 하나씩 정함), 업적 제목·목표 수치 확정

### 아트 (에셋 수급 필요)
- 캐릭터 3D 모델 + 애니메이션(대기·공격·피격·사망): 캡슐 교체. `CharacterData`의 전투 3D 모델 칸에 `UnitModel` 프리팹을 넣으면 됨 (지금은 축적의 수호자 = 바위 골렘, 공세의 사도 = 마법 탑, 갑각의 거체 = 나이트메어 드래곤)
- 메테오 바위·폭발 등 스킬 연출 에셋 (지금은 코드로 만든 임시 메시·파티클, URP·카툰풍 에셋 권장)
- 캐릭터 일러스트 (없는 캐릭터는 직군 색 실루엣)

### 문서
- 기획서 0장 핵심재미·반복구조, 8장 무료 재화 분배표 재작성 (보석: 첫 클리어 2,200 + 업적, 목표 9,000)
