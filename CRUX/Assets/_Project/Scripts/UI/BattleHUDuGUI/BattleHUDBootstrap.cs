using UnityEngine;
using Crux.Core;

namespace Crux.UI
{
    /// <summary>
    /// BattleHUD uGUI 초기화 부트스트랩 — 씬 시작 시 Canvas와 자식 패널 바인더 장착.
    /// 기존 OnGUI BattleHUD와 병행 운영.
    /// </summary>
    public class BattleHUDBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject contextMenuPrefab;
        [SerializeField] private GameObject weaponSelectPanelPrefab;
        [SerializeField] private GameObject ammoSelectPanelPrefab;

        private void Awake()
        {
            // BattleController 찾기
            var controller = FindFirstObjectByType<BattleController>();
            if (controller == null)
            {
                Debug.LogError("[CRUX] BattleHUDBootstrap: BattleController를 찾을 수 없습니다.");
                return;
            }

            // Canvas 찾기 (이 컴포넌트를 Canvas 또는 그 자식에 붙일 수 있음)
            Transform canvasTransform = null;
            if (gameObject.name == "BattleHUDCanvas")
                canvasTransform = transform;
            else
                canvasTransform = FindCanvasTransform();

            if (canvasTransform == null)
            {
                Debug.LogError("[CRUX] BattleHUDBootstrap: BattleHUDCanvas를 찾을 수 없습니다.");
                return;
            }

            // 5개 자식 패널 찾기
            Transform turnCounter = canvasTransform.Find("TurnCounterPanel");
            Transform banner = canvasTransform.Find("BannerPanel");
            Transform ammo = canvasTransform.Find("AmmoCounterPanel");
            Transform unitCard = canvasTransform.Find("UnitInfoCard");
            Transform actionStack = canvasTransform.Find("ActionStack");

            if (turnCounter == null || banner == null || ammo == null || unitCard == null || actionStack == null)
            {
                Debug.LogError("[CRUX] BattleHUDBootstrap: 자식 패널을 찾을 수 없습니다. 예상 이름: TurnCounterPanel, BannerPanel, AmmoCounterPanel, UnitInfoCard, ActionStack");
                return;
            }

            // 초기 숨김 — 메시지/모드 트리거 전까지 보이면 안 되는 오버레이
            banner.gameObject.SetActive(false);

            // InputModePanel 선택적 찾기
            Transform inputModePanel = canvasTransform.Find("InputModePanel");

            // Binder 추가 및 초기화
            var binder = canvasTransform.gameObject.AddComponent<BattleHUDBinder>();
            binder.Initialize(controller, turnCounter, banner, ammo, unitCard);

            // ActionStackController 추가 및 초기화
            var actionController = canvasTransform.gameObject.AddComponent<ActionStackController>();
            actionController.Initialize(controller, actionStack);

            // FirePreviewCard 선택적 초기화 (있을 때만)
            var firePreviewCard = canvasTransform.Find("FirePreviewCard");
            if (firePreviewCard != null)
            {
                firePreviewCard.gameObject.SetActive(false);
                var fpBinder = canvasTransform.gameObject.AddComponent<FirePreviewCardBinder>();
                fpBinder.Initialize(controller, firePreviewCard);
            }

            // FacingWheel 선택적 초기화 (있을 때만)
            var facingWheel = canvasTransform.Find("FacingWheel");
            if (facingWheel != null)
            {
                facingWheel.gameObject.SetActive(false);
                var fwBinder = canvasTransform.gameObject.AddComponent<FacingWheelBinder>();
                fwBinder.Initialize(controller, facingWheel);
            }

            // MissionCompleteModal 선택적 초기화 (있을 때만)
            var missionComplete = canvasTransform.Find("MissionCompleteModal");
            if (missionComplete != null)
            {
                missionComplete.gameObject.SetActive(false);
                var mcBinder = canvasTransform.gameObject.AddComponent<MissionCompleteModalBinder>();
                mcBinder.Initialize(controller, missionComplete);
            }

            // 3단계 팝업 UI 초기화: AmmoSelectPanel → WeaponSelectPanel → ContextMenu (의존성 순서)
            // 먼저 캔버스 자식으로 찾기 (기존 네스팅된 경우 호환성)
            Transform ammoSelectTransform = canvasTransform.Find("AmmoSelectPanel");
            Transform weaponSelectTransform = canvasTransform.Find("WeaponSelectPanel");
            Transform contextMenuTransform = canvasTransform.Find("ContextMenu");

            // AmmoSelectPanel 인스턴시에이션 (없으면 프리팹에서 생성)
            if (ammoSelectTransform == null && ammoSelectPanelPrefab != null)
            {
                var instance = Instantiate(ammoSelectPanelPrefab, canvasTransform, false);
                instance.name = ammoSelectPanelPrefab.name;
                ammoSelectTransform = instance.transform;
            }
            else if (ammoSelectTransform == null)
            {
                Debug.LogWarning("[CRUX] BattleHUDBootstrap: AmmoSelectPanel이 캔버스 자식으로도 없고, ammoSelectPanelPrefab이 할당되지 않았습니다. 스킵합니다.");
            }

            // WeaponSelectPanel 인스턴시에이션
            if (weaponSelectTransform == null && weaponSelectPanelPrefab != null)
            {
                var instance = Instantiate(weaponSelectPanelPrefab, canvasTransform, false);
                instance.name = weaponSelectPanelPrefab.name;
                weaponSelectTransform = instance.transform;
            }
            else if (weaponSelectTransform == null)
            {
                Debug.LogWarning("[CRUX] BattleHUDBootstrap: WeaponSelectPanel이 캔버스 자식으로도 없고, weaponSelectPanelPrefab이 할당되지 않았습니다. 스킵합니다.");
            }

            // ContextMenu 인스턴시에이션
            if (contextMenuTransform == null && contextMenuPrefab != null)
            {
                var instance = Instantiate(contextMenuPrefab, canvasTransform, false);
                instance.name = contextMenuPrefab.name;
                contextMenuTransform = instance.transform;
            }
            else if (contextMenuTransform == null)
            {
                Debug.LogWarning("[CRUX] BattleHUDBootstrap: ContextMenu이 캔버스 자식으로도 없고, contextMenuPrefab이 할당되지 않았습니다. 스킵합니다.");
            }

            AmmoSelectPanelController ammoSelectController = null;
            WeaponSelectPanelController weaponSelectController = null;
            ContextMenuController contextMenuController = null;

            if (ammoSelectTransform != null)
            {
                ammoSelectTransform.gameObject.SetActive(false);
                ammoSelectController = canvasTransform.gameObject.AddComponent<AmmoSelectPanelController>();
            }

            if (weaponSelectTransform != null)
            {
                weaponSelectTransform.gameObject.SetActive(false);
                weaponSelectController = canvasTransform.gameObject.AddComponent<WeaponSelectPanelController>();
            }

            if (contextMenuTransform != null)
            {
                contextMenuTransform.gameObject.SetActive(false);
                contextMenuController = canvasTransform.gameObject.AddComponent<ContextMenuController>();
                Debug.Log("[CRUX] ContextMenuController: 초기화 완료");
            }

            // 초기화: 의존성 역순 (상위가 하위를 참조)
            if (ammoSelectController != null)
                ammoSelectController.Initialize(controller, ammoSelectTransform, weaponSelectController);

            if (weaponSelectController != null)
                weaponSelectController.Initialize(controller, weaponSelectTransform, ammoSelectController, contextMenuController);

            if (contextMenuController != null)
                contextMenuController.Initialize(controller, contextMenuTransform, weaponSelectController);

            Debug.Log("[CRUX] BattleHUDBootstrap: uGUI HUD 초기화 완료");
        }

        /// <summary>BattleHUDCanvas를 씬에서 찾는 보조 메서드</summary>
        private Transform FindCanvasTransform()
        {
            var canvas = GameObject.Find("BattleHUDCanvas");
            return canvas != null ? canvas.transform : null;
        }
    }
}
