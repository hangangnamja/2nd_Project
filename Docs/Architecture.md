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
- 로비 → 전투: 스테이지 선택(`StageSelectView`) → 편성(`FormationView`) → `BattleSetup.Create`(편성 + 레벨 + 고른 스테이지)를 `BattleLaunch`(static)에 맡기고 Battle 씬을 불러옴 → `BattleManager`가 시작할 때 꺼내 씀. 맡긴 값이 없으면(씬 직접 실행) Inspector의 테스트 설정 사용
- 보유 캐릭터·편성은 세이브(`PlayerProfile`)에서 읽음. 로비에서 시작한 전투는 캐릭터별 보유 레벨로 싸우고, 이기면 스테이지 클리어를 저장
- Build Settings: Lobby(0) → Battle(1)
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
| `CharacterData`: 등급, 직군, 일러스트(화면 공용, 비면 실루엣), 기본 스탯, 치명, 속도, 1~3스, 패시브, 변신 스킬 세트 | `BattleUnit`: 현재 스탯·HP, 남은 거리, 남은 쿨타임, 상태효과 칸, 패시브 스택·사용 횟수 |
| `StatusEffectData`: 화상·독·버프의 공통 정의 (지속 턴, 중첩/갱신) + **효과 종류**(스탯 보정·지속피해·지속 회복·행동 불가·지연 버프·피해 기록·디버프 면역·피해 무효 중 복수 선택). Inspector는 고른 종류의 칸만 표시 (`StatusEffectDataEditor`) | `StatusInstance`: 실제로 걸린 효과 1개 (정의 참조, 부여자, 남은 턴, 시전 당시 공격력 스냅샷) |
| `StageData`: 적 레벨 + 웨이브 목록(웨이브마다 적 편성, 자리별 무적). 보상은 이후 | `StageRunner`: 현재 웨이브, 웨이브 클리어 판정, 다음 웨이브 유무 |
| `BattleSetup`: 이번 전투의 아군 편성·레벨 + 스테이지 (지금은 테스트 에셋, 이후 로비가 만들어 넘김) | - |
| `EnemyCharacterData`: `CharacterData` + 행동 패턴 목록 (패턴이 필요한 적만) | `BattleManager.DecideEnemyAction`: 패턴을 순서대로 확인해 스킬·대상 결정 |
| `BattleRulesData`: 전투 공통 수치 (방어 상수 K 계산 파라미터, 레벨 성장 곡선) | `BattleUnit` 기본 스탯 = 데이터 스탯(Lv1) × 레벨 배율(`StatScaling`), 전투 시작 때 고정 |
| `GachaBannerData`: 배너 이름·강조 문구·종료 일시·픽업 S 1명 | `GachaView`: 보여주는 배너 순서 |
| `GachaRulesData`: 확률·천장·반천·A 확정·비용·디버그 무료 (배너 공통) | `GachaSave`(세이브): 천장·A 확정 카운터, 픽업 확정 여부 |
| `WeaponData`·`MaterialData`: 무기(등급 3~5성, 기초 스탯)·재료(분류, 등급) 종류 | `WeaponSave`(개체 번호·레벨·장착)·`MaterialSave`(개수) → `OwnedWeapon`·`OwnedMaterial` |
| `InventoryRulesData`: 무기 칸 수, 재료 최대 개수, 분해 재료·등급별 보상 | - |
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

### 인벤토리 (기획서 2장)
- 무기 인벤과 재료 인벤은 세이브 목록부터 따로다: `SaveData.weapons`(개체 1개 = 1칸, `nextWeaponUid`로 개체 번호) / `SaveData.materials`(종류별 개수)
- `PlayerProfile.Inventory.cs`(partial): 무기 추가(`AddWeapon`), 재료 증감(`AddMaterial`), 목록 조회, 일괄 분해(`PreviewDismantle`·`DismantleWeapons`), 저장 후 `InventoryChanged` 알림
  - 자동 분해: `StoreWeaponOrDismantle`이 칸이 꽉 찼고 3성이면 칸에 넣지 않고 분해 보상만 준다. 가챠 10회는 `CommitGachaPull`이 무기를 한꺼번에 넣고 한 번 저장한 뒤, 무기마다 자동 분해 여부를 돌려준다(`GachaResult.IsDismantled`)
- 화면: `InventoryView`(탭·목록·정렬·상세), `InventorySlotView`(칸 하나, 칸 원본을 복사해 재사용), `DismantlePopup`(등급 선택 → 미리 보기 → 분해). 등급 색은 `RarityColors`(가챠와 같은 값)
- 에디터 메뉴 `InventoryDebugMenu`(Tools > Inventory): 테스트 아이템 지급, 무기 칸 채우기

### 세이브 (기획서 9장)
- `SaveData`: 파일에 그대로 저장되는 내용. 필드 이름이 곧 JSON 키라, 이름·구조를 바꾸면 `version`을 올리고 `SaveStorage.Upgrade`에 변환을 추가
- `SaveStorage`: 파일 읽기·쓰기만 (임시 파일에 쓴 뒤 원본과 교체, 깨진 파일 보관)
- `PlayerProfile` (static): 처음 쓰일 때 세이브를 불러오고(없으면 새 게임), 바꾸는 함수는 바꾼 직후 저장. 화면은 `SaveData`를 직접 만지지 않고 이 함수로 데이터 에셋을 받음
- 저장되는 SO(`CharacterData`, `StageData`)는 에셋 이름과 별도로 **ID** 칸을 가짐. 한 번 정하면 바꾸지 않는다
- 도메인 리로드를 꺼 두었으므로 static 값은 플레이 시작 때 직접 비운다 (`RuntimeInitializeOnLoadMethod`)

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
- 효과는 목록 순서대로 실행된다. 순서가 중요한 조합: 피해 → 격발(방금 붙은 화상도 터짐), 피해 → 버프 전부 소모(버프가 피해에 반영된 뒤 지움), 상대 강제 공격 → 추가 공격 예약(상대 공격이 끝난 뒤 반격)

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
| 공격 | 대상 최대 HP 비례 피해 (`MaxHpDamageEffect`) | 대상 최대 HP × 비율 피해. 공격력·치명·방어력 무시, 대상 쪽 보정·보호막은 적용 | 보스 2 디버프 없는 턴 공격 |
| 공격 | 버프 수 비례 피해 (`BuffScalingDamageEffect`) | 기본 배율 + 버프 1개당 배율 × 시전자 버프 개수 | 강탈 딜러 3스 |
| 공격 | 패시브 스택 비례 피해 (`PassiveStackDamageEffect`) | 기본 배율 + 스택 1개당 배율 × 시전자 패시브 스택, 피해 뒤 스택 전부 소모 | 니콜 2스 (이자 정산) |
| 공격 | 받은 피해 비례 반격 (`RecordedDamageCounterEffect`) | 피해 기록에 쌓인 받은 피해(최대 HP 대비) × 계수를 공격력 배율로 반격, 기록 삭제 | 하이퍼캐리 반격 태세 |
| 공격 | 격발 (`DetonateEffect`) | 대상의 지속피해를 즉시 1회씩 발동 (지속피해는 유지) | 격발 딜러 3스·패시브 |
| 회복·보호 | 회복 (`HealEffect`) | 기준 스탯(공격력·최대 HP·시전자 최대 HP) × 비율 회복. 시전자 자신·아군 전체 옵션 | 아스트라 패시브, 변신 스킬 자가 회복 |
| 회복·보호 | 보호막 (`ShieldEffect`) | 기준 스탯 × 비율만큼 보호막. 이미 있으면 양을 더함 (`StatusApplier.GrantShield`) | 곰인형 버퍼 2스, 하이퍼캐리 불굴, 도발 탱커 2·3스 |
| 회복·보호 | 피격 무효 충전 (`HitBlockEffect`) | 시전자의 피격 무효를 횟수만큼 채움 (`BattleUnit.ChargeHitBlock`, 쌓이지 않음). 피해만 0, 효과는 들어감 | (지금 쓰는 캐릭터 없음) |
| 회복·보호 | 부활 (`ReviveEffect`) | 쓰러진 대상을 최대 HP × 비율로 부활 (`BattleUnit.Revive`). 살아 있는 대상은 건너뜀 | 부활 힐러 3스·패시브 |
| 상태효과 | 디버프 제거 (`RemoveDebuffsEffect`) | 대상 디버프를 전부 또는 가장 최근 1개 제거 | 부활 힐러 2스·3스 |
| 상태효과 | 상태효과 부여 (`ApplyStatusEffect`) | 상태효과를 개수만큼 부여. 아군 전체 옵션 | 화상·독·버프 부여 전반 |
| 상태효과 | 버프 강탈 (`StealBuffsEffect`) | 대상 버프 전부 즉시 제거, 턴 종료 단계에 시전자가 획득 | 강탈 딜러 2스·3스 (3스는 버프 전부 소모 뒤) |
| 상태효과 | 버프 전부 소모 (`ConsumeAllBuffsEffect`) | 시전자 버프 전부 삭제 | 강탈 딜러 3스, 축적 해방 |
| 턴·행동 | 행동 당기기 (`ActionAdvanceEffect`) | 행동 게이지를 비율만큼 당김. 시전자 자신 옵션 | 니콜 패시브(자신) |
| 턴·행동 | 추가 턴 (`GrantExtraTurnEffect`) | 이번 턴이 끝난 뒤 한 번 더 행동 (같은 턴 중복은 1회) | - |
| 턴·행동 | 추가 공격 예약 (`QueueFollowUpEffect`) | 블록 안 효과 목록을 시전자의 추가 공격(임시 턴)으로 예약 | 반격 태세의 반격, 패시브 추가 공격 |
| 턴·행동 | 상대 강제 공격 (`ForceOpponentsAttackEffect`) | 상대 전원이 시전자를 1스로 1회씩 추가 공격 | 반격 태세 |
| 턴·행동 | 쿨타임 감소 (`ReduceCooldownEffect`) | 지정 스킬의 남은 쿨타임 감소. 대상 또는 시전자 자신 | 하이퍼캐리 2스, 아스트라 2스, 곰인형 패시브 |
| 턴·행동 | 다른 스킬 사용 (`UseSkillEffect`) | 시전자의 다른 스킬(1~3스)을 그 자리에서 함께 사용. 그 스킬의 대상 규칙·효과 그대로, 쿨타임은 안 걸림. 연동 안의 연동은 무시 | 니콜 3스 → 2스 |
| 변신·전장 | 변신 (`TransformEffect`) | 시전자 변신 (스킬 교체, 턴 제한·종결기는 변신 설정) | 하이퍼캐리 3스 |
| 변신·전장 | 아군 퇴장 (`ExitAlliesEffect`) | 시전자 외 아군 퇴장, 변신 해제·사망 때 복귀 | 하이퍼캐리 3스 |
| 변신·전장 | 경계: 고립 (`IsolationBoundaryEffect`) | 스탯 보정 + 행동 제어 면역 경계 (경계는 한 번에 하나) | 하이퍼캐리 3스 |
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
- 전투 위치(로직)와 화면에 서는 자리(연출)를 나눈다. `BattleStageView.Stage(StageLayout, 행동 유닛)`가 장면마다 유닛을 다시 세우고 나머지는 숨김
  - `Overview`(아군 줄 + 멀리 적 줄: 시작·종료·적 차례), `ActorFocus`(행동 유닛만 원점 + 상대 줄 멀리), `TeamLine`(행동 유닛 편 전원이 카메라를 보고 한 줄)
  - 멀리 선 상대 줄은 `farLineArc`만큼 휜 호: 줄 끝일수록 앞(-z)으로 나오고(arc × t²) 각자 원점을 바라본다
  - `ActorFocus`에서는 상대 무리를 `StageFocusLine`이 오른쪽 앞(`focusLineCenter`)에 비스듬한 한 줄(`focusLineDirection`, 카메라 시선과 거의 수직)로 세우고 행동 유닛 쪽을 보게 한다. 간격은 `focusLineSpacing`과 `focusLineMaxWidth ÷ (인원 - 1)` 중 작은 값, 4명 이상이면 `focusLineShiftPerExtra`만큼 오른쪽으로 민다. 차례 구도(스킬 구도 1~3)는 행동 유닛 기준점(`Actor`)으로 바라볼 곳을 정한다
- `BattleCameraDirector`(카메라): `StateChanged`마다 장면을 고른다. 무대가 바뀌면 `SceneFade`(HUD 검은 판)로 덮은 사이에 다시 세우고 카메라를 바로 놓고, 같은 무대 안에서는 구도만 SmoothDamp로 이동
  - 구도 = `CameraShot`(기준점 + 오프셋, 바라볼 기준점 + 오프셋, 화각). 캡슐은 원점이 몸 가운데(y -1~1)
  - 차례 시작 때 자동으로 골라진 스킬은 구도를 바꾸지 않고, 플레이어가 다른 스킬을 누르면 그 스킬 대상 규칙에 맞는 장면으로 바뀜
- `BossHpBarView`: 상단 보스 HP바. `EnemyCharacterData.IsBoss`인 전장의 적을 찾아 이름·HP·보호막·버프 줄을 그리고(줄은 `PartyCardView.ShowRow` 공용), 누르면 `UnitDetailPanel`
- 아군·보스 캡슐의 머리 위 상태효과 줄(`UnitStatusBar`)은 끔 → 아군은 아래 카드, 보스는 상단 HP바에서만 보여준다
- HP 막대는 세로 그라데이션 그림(`Art/UI/BarGradient`)을 Image 색으로 물들여 쓴다 (기본 UISprite는 늘리면 끝이 어둡게 번짐)
- `PartyBarView`·`PartyCardView`: 아래 아군 초상화 카드 (HP·스택·차례·대상 표시, 누르면 `SelectTarget`). 카드 아래 버프 줄·디버프 줄(줄마다 5칸 + "…")(목록은 `UnitView.BuildDisplayedStatuses`와 같음). 꾹 누르면(`LongPressed`, 손을 뗄 때의 클릭은 무시) `UnitDetailPanel`(스탯 + 효과 설명 `StatusDescriptionBuilder`)
- `TurnOrderView`: 행동 순서 칸을 "유닛 + 몇 번째 등장"으로 기억해 순서가 바뀌면 새 자리로 미끄러뜨린다 (레이아웃 그룹은 끄고 자리를 직접 계산). 당기기 표시는 `BattleUnit.TotalActionAdvanced`(누적 당기기, 표시용)를 지난번과 비교
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
- 타격감 (기획서 3-11)
  - `BattleEvents.ActionStarted`(`SkillExecutor.ExecuteEffects`가 스킬·추가 공격 시작 때 알림, 3스 여부는 칸 번호)로 행동 박자를 따로 만들어 컷인(`SkillCutInView`) → 돌진(`UnitView.PlayLunge`)을 끝까지 기다린 뒤 타격 박자를 재생
  - 캡슐 모션은 `UnitView`가 "기준 자리(무대가 `SetAnchor`로 정함) + 어긋남"으로 그린다 → 모션 중에 무대가 다시 세워져도 튀지 않음. 피격 번쩍임·밀림(`PlayHitReaction`), 처치 날아감(`PlayKnockdown`)
  - 타격 박자마다 가장 센 타격 하나로 화면 흔들림(`BattleCameraDirector.Shake`, 지난 흔들림을 빼고 이동한 뒤 다시 더함)과 히트스톱·슬로모션(`BattleTimeFx`, `Time.timeScale`을 실제 시간으로 잼)
  - 처치 판정은 `DamageTakenInfo.IsLethal`(이번 피해로 HP가 0이 됨). 피해 합계는 `DamageTotalView`, 재생 한 번(= 한 턴)마다 0부터
- 화면 HP는 연출에 맞춘다: `ShownHp`(static)가 연출 중에 피해·회복을 받은 유닛의 "사건 전" HP·보호막을 붙잡고(`Hold`), `BattleFxView`가 박자를 재생할 때 그 사건 직후 값으로 바꾸고(`Set`), 재생이 끝나면 놓는다(`ReleaseAll`)
  - HP바·숫자를 그리는 곳(`PartyCardView`, `BossHpBarView`, `UnitView` 머리 위 글자)은 `ShownHp.Hp/Shield`를 읽고 `ShownHp.Changed`에 다시 그린다
  - 피해 전후 값은 `DamageTakenInfo.HpBefore/HpAfter/ShieldBefore/ShieldAfter`, 회복 전 값은 "지금 HP - 회복량"
  - 상세 창(`UnitDetailPanel`)과 쓰러짐 표시(카드 흐림)는 실제 값을 쓴다

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
  - 패시브 추가 공격, 반격 태세의 적 강제 공격·반격, 변신 종결기가 모두 같은 큐를 먼저 들어온 순서대로 쓴다
  - 그래서 "적 공격 예약 → 반격 예약 → (턴 제한이면) 종결기 예약" 순서가 코드 분기 없이 지켜진다
- HP 돌파 반응(돌파 딜러)은 `StatusApplier.DealDamage`의 `HpLowered` 사건으로 "기준선 위 → 아래"를 판단하고, 타수를 `HitCounter`(유닛·패시브마다 하나)에 쌓는다
  - 쌓일 때 예약된 추가 공격이 없으면 1개만 예약하고, 이미 있거나 실행 중이면 그 추가 공격의 타수만 늘어난다
  - `BattleManager.ExecuteMultiHitFollowUp`이 타수를 한 타씩 꺼내 적 전체에 효과를 반복하고, 적(무적 제외)이 모두 HP 0이면 남은 타수를 버린다. 칠 적이 없으면 타수를 남겨 다음 발동에 더한다
- 오라 (기획서 3-7): 버프 칸 밖의 보정이라 `BattleUnit.GetAuraModifiers`가 스탯 계산 때 같은 편을 돌며 모은다
  - 패시브 오라(`PassiveTrigger.Aura`)와 기간 오라(`BattleUnit.ActiveAura`, `TimedAura`) 두 가지. 주인이 쓰러지면 꺼지고, **퇴장해도 유지**(`IsDead`만 확인)
  - 기간 오라는 `StartAuraEffect`(아스트라 공연)가 만들고, 주인의 정규 턴 끝(`BattleManager.EndTurn`)마다 남은 턴이 준다. 만든 턴은 세지 않음
  - `SkipsOwnerTurns`면 그동안 주인의 턴이 강제로 스킵된다(`BattleUnit.IsPerforming`, 기절과 같은 경로로 턴 종료 처리만)
  - 전장 이펙트: `AuraFieldView`가 매 프레임 카메라 앞(수평 거리 `followDistance`)으로 옮겨 카메라와 같은 방향을 보게 한다 (이펙트는 그 자식). 오라 데이터가 프리팹(`FieldEffect`)을 들고, Battle 씬의 `AuraFieldView`가 `StateChanged`마다 켜진 오라의 이펙트를 만들고 꺼진 오라의 이펙트를 지운다. 띠 흐름은 `FieldFxScroller`(텍스처 이동·서서히 나타남), 빛은 가산 HDR 재질 + Bloom
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
- 스택 효과 (기획서 3-7, 지금 쓰는 효과 없음): 상태효과 중첩 방식 `StackCount`면 `StatusContainer.Add`가 기존 칸의 `StatusInstance.AddStack`(스택 +1·최대 스택·남은 턴 갱신·스냅샷은 높은 쪽)만 부른다
  - 지속피해는 `DamageCalculator.CalculateDamageOverTime`에 스택 수를 넘겨 1스택 피해 × 스택. 아이콘 숫자는 `StatusInstance.IconText`("×스택")
- 적 패턴 조건 `SelfDebuffCountBelow`(자신의 디버프 N개 미만, 스택 효과는 1칸 = 1개): 보스 2의 디버프 없는 턴 공격
- 피격 무효 (지금 쓰는 보스 없음): `BattleUnit.HitBlockCharges`(버프 칸 밖의 횟수)를 `StatusApplier.DealDamage`가 무적 확인 다음에 1회 쓰고 피해를 0으로 바꾼다 (보호막·피해 기록·HP 돌파 사건 없음, 연출은 `DamageTakenInfo.BlockedLabel` "무효")
  - 피해 함수만 막으므로 같은 스킬의 다른 효과 블록은 그대로 실행된다. 충전은 패시브 발동 조건 `OnOwnTurnStart`(자기 턴 시작 시, `BattleManager.BeginTurn` → `PassiveRunner.HandleOwnTurnStart`)
  - 아이콘은 충전 블록의 표시 효과를 `UnitView.BuildDisplayedStatuses`가 남은 횟수와 함께 맨 앞에 넣는다
- 무적(`IsInvulnerable`)은 스테이지 설정으로 정해진다: 피해 0, 승리 판정에서 제외 (무적이 아닌 적 전멸 = 승리)
  - 피해를 막는 판정은 `IsDamageImmune` = 스테이지 무적 또는 "피해 무효" 버프(힐러 3스 무적). 버프 무적은 승리 판정과 무관
- 소환 (보스 3): `BattleManager.SummonUnit`이 부하를 만들어 편 목록(소환자 왼쪽·오른쪽 번갈아)과 `allUnits`에 넣고 `UnitsSpawned`로 화면(`BattleStageView`)에 알린다
  - 부하는 `BattleUnit.Summoner`를 가져 `IsSummon` → 승리 조건(`StageRunner.IsWaveCleared`)에서 빠지고, 소환자가 쓰러지면 `ConfirmDeaths`에서 함께 `Vanish`
  - `Vanish`는 처치 사건 없이 사라지는 처리 (자폭·함께 퇴장). 자기 턴 중에 사라진 유닛은 턴 종료의 지속피해·남은 턴 처리를 건너뛴다
  - 패시브 발동 조건 `WhileSummonsAlive`(상시): 소환한 부하가 있는 동안 "오라 스탯 보정" 칸을 자신에게만 적용 (`GetNonStatusModifiers`)
  - 패시브 발동 조건 `OnEnter`(등장 시): 전투 시작(전원)·웨이브 시작(새 적) 때 `PassiveRunner.HandleUnitsEntered`가 실행 → 시작 부하 소환. 화면 캡슐을 만들기 전에 실행해 부하가 처음부터 자리에 선다
  - 적 패턴 조건 `OwnTurnAtLeast`(자기 턴 N번째 이상, `BattleUnit.OwnTurnsTaken`)·`SummonsBelow`(소환한 부하 N마리 미만)
  - 카운트다운 아이콘: `EnemyCharacterData`의 표시 효과·행동 턴으로 `UnitView`가 남은 턴(행동 턴 - 지난 자기 턴)을 그린다
- 상시 패시브 아이콘: `BattleUnit.GetActiveConditionalPassives`(시선집중·부하가 있는 동안)의 표시 효과(`PassiveData.DisplayStatus`)를 `UnitView`가 맨 앞에 넣는다
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

## 6. 구현 현황 (2026-11-10 기준)

### 데이터·저장
- SO 데이터: 캐릭터(등급·직군·스탯·1~3스·패시브·변신), 적(행동 패턴), 상태효과 17종, 스테이지 9개, 전투 규칙, 가챠 규칙·배너
- 효과 블록 18종: 피해, 버프 수 비례 피해, 상태효과 부여, 격발, 강탈, 버프 전부 소모, 회복(시전자 최대 HP 기준·아군 전원 옵션), 보호막, 행동 당기기, 추가 턴, 추가 공격 예약, 쿨타임 감소, 변신, 아군 퇴장, 적 강제 공격(반격 태세), 받은 피해 비례 반격, 경계(고립), 기간 오라(공연)
- Inspector 도구: 효과 블록 드롭다운(`SubclassSelector`), 상태효과 종류별 칸 표시
- 세이브: JSON 저장·불러오기, 임시 파일 교체, 깨진 파일 보관, `version` 필드, 새 게임 시작 캐릭터, 에디터 메뉴(세이브 삭제·폴더 열기)

### 전투 코어
- 행동 순서(남은 거리·속도·동시 도착 우선순위·사이클 카운터), 행동 당기기와 미뤄둔 당기기
- 스킬·쿨타임(시작 쿨타임), 피해 공식(방어 상수 K, 치명, 피해 감소 곱연산·상한), 레벨 성장 배율(아군·적 교차 곡선)
- 상태효과: 버프·디버프 각 10칸, 밀어내기, 중첩/갱신, 공·방 % 버프 중첩 감소, 지연 버프, 보호막, 지속피해(화상·독, 스냅샷), 지속 회복, 기절, 무적
- 큐 구조: 추가 공격(임시 턴), 추가 턴, 턴 종료 단계 효과, 반응형 패시브 횟수 제한(AnyTurn/OwnTurn)
- 전장 상태: 퇴장·변신·엑스트라 턴·종결기·강제 해제·행동 제어 면역
- 웨이브 진행(`StageRunner`), 승패 판정, 결과 화면(로비 복귀)
- 적 AI: 패턴 목록(조건 → 스킬·대상), 일반 적 랜덤 공격

### 캐릭터 (10 / 11명)
- S: 격발 딜러(DotMainAttack), 강탈 딜러(BuffStealAttack), 하이퍼캐리(HyperCarry), 곰인형 버퍼(BearBuffer), 공연 오라 버퍼(Astra), 도발 탱커(TauntTank), 부활 힐러(ReviveHealer)
- A: 독 서브딜러(DotSubAttack), 안젤리카(Ally_Support, 치명 버퍼 → 회복 직군으로 테스트 중), 돌파 반응 광역 딜러(GeniusAoE)
- 수치는 전부 테스트 값

### 스테이지·적
- 기본 스테이지 1-1 ~ 1-7: 테스트 적(잡몹·정예·보스)으로 2웨이브 구성
- 보스 1 축적의 수호자(지금은 `Stage_BuffEnemy` 2웨이브, `Stage_Boss1`은 테스트용): 무적 버프몹 2 + 보스, 축적 해방(광역 + 기절 + 버프 소모)
- 보스 2 갑각의 거체(`Stage_Boss2`, 보스전 2번째 칸, 2웨이브): HP 2배, 디버프가 없는 턴에 아군 전체 최대 HP 40% 피해
- 보스 3 군단 지휘관(`Stage_Boss3`, 보스전 3번째 칸, 2웨이브): 등장 때 부하 2마리, 부하(자폭병) 소환·최대 4, 부하가 있는 동안 피해 감소 50%, 부하는 2번째 자기 턴에 자폭(광역 + 기절)

### 화면
- 전투 HUD: 보스 HP바(상단), 웨이브·사이클, 스킬 버튼·쿨타임, 대상 선택, 행동 순서 목록(임시 세로 목록), 상태효과 아이콘·툴팁 (전투 로그는 화면에서 숨김, 콘솔에만)
- 전투 임시 연출: 타격·치명·지속피해·회복·버프 이펙트 + 피해 숫자, 행동 단위로 순서대로 재생 (3장 "연출 재생")
- 로비: 메인 화면(출격·모집·인벤토리 연결), 스테이지 선택(지도·보스 탭·상세 패널), 편성(4칸·보유 목록·저장)
- 인벤토리: 무기·재료 탭, 정렬, 상세 패널, 일괄 분해(등급 선택), 무기 칸 200·자동 분해, 세이브 버전 2
- 모집: 픽업 배너 7개, 1·10회 뽑기(천장·반천·A 확정), 확정까지 남은 횟수, 기어 → 공개 → 결과 목록 연출(파티클 + Bloom)

---

## 7. 남은 구현

> 기획서 1장 개발 순서 기준. 위에 있을수록 먼저 할 것.

### 전투 (핵심 재미 검증에 필요)
- A 회복 힐러: 안젤리카(Ally_Support)를 회복 직군으로 바꿔 테스트 중, 스킬 구성 미정
- 자동 전투, 배속, 일시정지 (기획서 3-9·3-11)
- 전투 UI 정식 배치: 현재 행동 캐릭터 패널, 행동 순서 바(초상화), 스킬 이름 알림 (보스 HP바는 구현)
- 연출 정리: HP 표시를 이펙트 재생에 맞춰 줄이기, 공격 모션(대기·공격·피격·사망) 자리
- 방어력 관통 (기획서 10장 미정)

### 보스·스테이지
- 보스 AI를 Unity Behavior 패키지로 교체 (지금은 패턴 목록)
- 기본 스테이지 정식 적 데이터(스테이지 보스 포함)와 수치, 3분 목표 밸런스
- 별 미션 3개(사이클 제한 등)와 기록 저장

### 육성·인벤토리
- 전투원 화면: 보유 캐릭터 목록, 스킬·스탯 확인
- 캐릭터 레벨업·진급 (경험치 곡선·진급 재료 미정) → 저장 후 `PlayerProfile.OwnedCharactersChanged` 알림
- 무기: 레벨·진급, 장착, 전투 스탯 반영 (개체 보관·분해는 인벤토리로 구현됨)

### 가챠·재화
- 확률 공시 화면 (버튼만 있음)
- 보석 획득처(첫 클리어·별 미션·업적)와 디버그 무료 뽑기 끄기
- 중복 캐릭터 처리 (돌파 / 재화 변환, 미정)
- 성장 재화(골드·경험치·진급·무기 재료) 획득과 사용

### 아웃게임 기타
- 시작(타이틀) 화면: 세이브 불러오기, 공용 매니저 생성
- 메인 화면 나머지 버튼(설정·공지·우편·출석·친구 등)은 틀만 있음
- 심사자용 디버그 모드: 재화 무한, 모든 스테이지 개방
- 업적 (후순위)
- 캐릭터 모델·일러스트 교체 (지금은 캡슐·직군 색 박스)

### 문서
- 기획서 0장 핵심재미·반복구조, 8장 무료 재화 분배표 재작성
