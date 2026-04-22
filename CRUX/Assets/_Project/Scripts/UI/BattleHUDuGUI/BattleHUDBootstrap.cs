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
