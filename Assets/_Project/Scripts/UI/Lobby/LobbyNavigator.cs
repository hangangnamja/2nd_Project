using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 로비 화면 전환: 메인 화면 ↔ 스테이지 선택 ↔ 편성 등 Canvas 안의 화면(패널)을 하나만 켜고 나머지는 끈다 (Architecture.md 1장).
    // 화면 모양은 씬에서 만들고, 이 컴포넌트는 "어느 버튼이 어느 화면을 여는지"만 연결한다.
    public class LobbyNavigator : MonoBehaviour
    {
        [Tooltip("처음에 보여줄 메인 화면")]
        [SerializeField] private GameObject mainScreen;

        [SerializeField] private StageSelectView stageSelect;
        [SerializeField] private GameObject formationScreen;
        [SerializeField] private GameObject gachaScreen;
        [SerializeField] private GameObject inventoryScreen;
        [SerializeField] private GameObject operatorScreen;
        [SerializeField] private GameObject achievementScreen;

        [Header("버튼")]
        [Tooltip("메인 화면의 출격 버튼")]
        [SerializeField] private Button stageButton;

        [Tooltip("메인 화면의 모집 버튼")]
        [SerializeField] private Button gachaButton;

        [Tooltip("메인 화면의 인벤토리(DEPOT) 버튼")]
        [SerializeField] private Button inventoryButton;

        [Tooltip("메인 화면의 전투원(OPERATOR) 버튼")]
        [SerializeField] private Button operatorButton;

        [Tooltip("메인 화면의 업적(MISSION) 버튼")]
        [SerializeField] private Button achievementButton;

        [Tooltip("메인 화면의 편성(SQUAD) 버튼 → 출격 없이 팀 편성만")]
        [SerializeField] private Button formationButton;

        [Tooltip("메인 화면으로 돌아가는 버튼들 (뒤로, 홈)")]
        [SerializeField] private List<Button> homeButtons = new List<Button>();

        // 화면 목록. 전환할 때 이 중 하나만 켠다. 새 화면이 생기면 여기에 추가한다.
        private readonly List<GameObject> screens = new List<GameObject>();

        // 버튼마다 누르면 열 화면을 등록하고, 메인 화면에서 시작한다.
        private void Awake()
        {
            screens.Add(mainScreen);
            screens.Add(stageSelect.gameObject);
            screens.Add(formationScreen);
            screens.Add(gachaScreen);
            screens.Add(inventoryScreen);
            screens.Add(operatorScreen);
            screens.Add(achievementScreen);

            // 람다식 () => ...: 버튼을 누르면 실행할 동작. 메인 → 출격은 스테이지 탭부터 새로 보여준다.
            stageButton.onClick.AddListener(() =>
            {
                ShowStageSelect();
                stageSelect.ResetView();
            });

            gachaButton.onClick.AddListener(() => Show(gachaScreen));
            inventoryButton.onClick.AddListener(() => Show(inventoryScreen));
            operatorButton.onClick.AddListener(() => Show(operatorScreen));
            achievementButton.onClick.AddListener(() => Show(achievementScreen));

            // 편성 화면은 열 때 모드(편성 관리)를 정해야 하므로 FormationView를 거쳐 연다.
            formationButton.onClick.AddListener(() => formationScreen.GetComponent<FormationView>().OpenForManage());

            foreach (Button button in homeButtons)
            {
                button.onClick.AddListener(() => Show(mainScreen));
            }

            Show(mainScreen);
        }

        // 메인 화면으로 돌아간다 (편성 관리에서 뒤로).
        public void ShowMain()
        {
            Show(mainScreen);
        }

        // 스테이지 선택 화면을 연다. 편성 화면에서 뒤로 갈 때는 고르던 탭·스테이지를 그대로 둔다.
        public void ShowStageSelect()
        {
            Show(stageSelect.gameObject);
        }

        // target 화면만 켜고 나머지 화면은 끈다.
        public void Show(GameObject target)
        {
            foreach (GameObject screen in screens)
            {
                screen.SetActive(screen == target);
            }
        }
    }
}
