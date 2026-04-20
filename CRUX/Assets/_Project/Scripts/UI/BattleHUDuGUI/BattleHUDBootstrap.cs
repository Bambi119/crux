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
            var controller = FindObjectOfType<BattleController>();
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

            // Binder 추가 및 초기화
            var binder = canvasTransform.gameObject.AddComponent<BattleHUDBinder>();
            binder.Initialize(controller, turnCounter, banner, ammo, unitCard);

            // ActionStackController 추가 및 초기화
            var actionController = canvasTransform.gameObject.AddComponent<ActionStackController>();
            actionController.Initialize(controller, actionStack);

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
