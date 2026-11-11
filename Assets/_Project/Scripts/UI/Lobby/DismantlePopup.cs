using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 일괄 분해 창: 3·4·5성 중 분해할 등급을 고르면 그 등급의 무기가 (장착 중인 것만 빼고) 전부 분해된다.
    // 고를 때마다 "몇 개가 분해되고 재료를 몇 개 받는지"를 미리 보여주고, 분해 버튼을 눌러야 실제로 분해한다.
    public class DismantlePopup : MonoBehaviour
    {
        [Tooltip("3성 / 4성 / 5성 순서의 등급 선택 버튼")]
        [SerializeField] private List<Button> gradeButtons = new List<Button>();

        [Tooltip("등급 버튼마다 켜고 끄는 체크 표시 (버튼과 같은 순서)")]
        [SerializeField] private List<GameObject> gradeChecks = new List<GameObject>();

        [SerializeField] private Text previewText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Tooltip("고르지 않은 등급 버튼의 색 (고른 버튼은 그 등급 색)")]
        [SerializeField] private Color offColor = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // 버튼 순서와 같은 등급 목록
        private static readonly WeaponGrade[] Grades = { WeaponGrade.ThreeStar, WeaponGrade.FourStar, WeaponGrade.FiveStar };

        // 지금 고른 등급들. HashSet: 같은 값을 두 번 넣지 않는 모음이라 켜고 끄기(Add/Remove)에 알맞다.
        private readonly HashSet<WeaponGrade> selected = new HashSet<WeaponGrade>();

        // 분해가 끝나면 결과를 넘겨받을 함수 (InventoryView가 결과 문구를 띄운다)
        private Action<DismantleResult> onDismantled;

        // 버튼마다 자기 등급을 켜고 끄도록 연결한다.
        private void Awake()
        {
            for (int i = 0; i < gradeButtons.Count; i++)
            {
                // 반복 변수 i를 그대로 람다에 쓰면 모든 버튼이 마지막 값을 보므로, 반복마다 새 지역 변수에 담는다.
                WeaponGrade grade = Grades[i];
                gradeButtons[i].onClick.AddListener(() => ToggleGrade(grade));
            }

            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Close);
        }

        // 창을 연다. 처음에는 3성만 골라 둔다 (가장 흔하게 정리하는 등급).
        public void Open(Action<DismantleResult> onDone)
        {
            onDismantled = onDone;
            selected.Clear();
            selected.Add(WeaponGrade.ThreeStar);

            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void ToggleGrade(WeaponGrade grade)
        {
            // Remove는 지웠으면 true → 이미 골라져 있었으면 끄고, 아니면 켠다.
            if (!selected.Remove(grade))
            {
                selected.Add(grade);
            }

            Refresh();
        }

        // 등급 버튼 색·체크와 미리 보기 문구를 고른 등급에 맞게 다시 그린다.
        private void Refresh()
        {
            for (int i = 0; i < gradeButtons.Count; i++)
            {
                bool isOn = selected.Contains(Grades[i]);
                gradeButtons[i].image.color = isOn ? RarityColors.Get((int)Grades[i]) : offColor;
                gradeChecks[i].SetActive(isOn);
            }

            DismantleResult preview = PlayerProfile.PreviewDismantle(selected);
            string materialName = preview.Material != null ? preview.Material.DisplayName : "재료";
            previewText.text = preview.WeaponCount > 0
                ? $"분해 대상 <b>{preview.WeaponCount}개</b>  →  {materialName} <b>×{preview.MaterialAmount:N0}</b>"
                : "분해할 무기가 없습니다";

            // 분해할 무기가 없으면 분해 버튼을 누를 수 없게 한다.
            confirmButton.interactable = preview.WeaponCount > 0;
        }

        // 고른 등급을 실제로 분해하고, 결과를 넘긴 뒤 창을 닫는다.
        private void Confirm()
        {
            DismantleResult result = PlayerProfile.DismantleWeapons(selected);

            // ?.Invoke: 결과를 받을 함수가 없으면(null) 건너뛴다.
            onDismantled?.Invoke(result);
            Close();
        }
    }
}
