using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Crux.Unit;
using Crux.Core;

namespace Crux.Cinematic
{
    /// <summary>
    /// FireActionScene Canvas 내 반격 무기 선택 패널.
    /// 피격 직후 자동 출현 → 3s 카운트다운 → 무기 선택 또는 취소.
    /// OnGUI 금지 — uGUI Text 전용.
    /// </summary>
    public class CounterFireUIPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text[] weaponSlots;   // 4개: 0=취소, 1=주포, 2=동축MG, 3=거치MG
        [SerializeField] private Text timerText;

        private System.Action<WeaponType> onConfirm;
        private System.Action onCancel;
        private bool active;

        private void Awake()
        {
            // SerializeField 배열 미연결 시 자식 이름으로 자동 탐색
            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                string[] slotNames = { "Slot0", "Slot1", "Slot2", "Slot3" };
                var found = new System.Collections.Generic.List<Text>();
                foreach (var n in slotNames)
                {
                    var t = transform.Find(n);
                    if (t != null) found.Add(t.GetComponent<Text>());
                }
                weaponSlots = found.ToArray();
            }
            if (titleText == null)
            {
                var t = transform.Find("TitleText");
                if (t != null) titleText = t.GetComponent<Text>();
            }
            if (timerText == null)
            {
                var t = transform.Find("TimerText");
                if (t != null) timerText = t.GetComponent<Text>();
            }
            if (panelRoot == null) panelRoot = gameObject;
            // 초기 비활성
            panelRoot.SetActive(false);
        }

        // ===== 공개 API =====

        public void Show(GridTankUnit playerUnit, string attackerName,
                         System.Action<WeaponType> confirmCallback,
                         System.Action cancelCallback)
        {
            onConfirm = confirmCallback;
            onCancel  = cancelCallback;
            active    = true;
            if (titleText != null) titleText.text = $"반격 — {attackerName}을(를) 조준 중";
            SetupSlots(playerUnit);
            if (panelRoot != null) panelRoot.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(CountdownRoutine());
        }

        public void Hide()
        {
            active = false;
            StopAllCoroutines();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ===== 내부 =====

        private static readonly (string label, Color col)[] SlotDefaults =
        {
            ("[0] 반격 취소",   new Color(0.9f, 0.3f, 0.3f)),
            ("[1] 주포",        Color.white),
            ("[2] 동축 MG",     new Color(0.8f, 0.8f, 0.8f)),
            ("[3] 거치 MG",     new Color(0.8f, 0.8f, 0.8f)),
        };

        private void SetupSlots(GridTankUnit unit)
        {
            if (weaponSlots == null) return;
            for (int i = 0; i < weaponSlots.Length && i < SlotDefaults.Length; i++)
            {
                if (weaponSlots[i] == null) continue;
                string label = SlotDefaults[i].label;
                if (i == 1 && unit != null) label += $"  ({unit.MainGunAmmoCount}발)";
                weaponSlots[i].text  = label;
                weaponSlots[i].color = SlotDefaults[i].col;
            }
        }

        private IEnumerator CountdownRoutine()
        {
            for (int s = 3; s > 0 && active; s--)
            {
                if (timerText != null) timerText.text = $"{s}s";
                yield return new WaitForSeconds(1f);
            }
            if (!active) yield break;
            if (timerText != null) timerText.text = "0s";
            Confirm(WeaponType.MainGun); // 타임아웃 — 주포 자동
        }

        private void Update()
        {
            if (!active) return;
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.N)
                || Input.GetMouseButtonDown(1)) { Cancel(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)) { Confirm(WeaponType.MainGun); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { Confirm(WeaponType.CoaxialMG); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { Confirm(WeaponType.MountedMG); return; }
        }

        private void Confirm(WeaponType weapon)
        {
            if (!active) return;
            Hide();
            onConfirm?.Invoke(weapon);
        }

        private void Cancel()
        {
            if (!active) return;
            Hide();
            onCancel?.Invoke();
        }
    }
}
