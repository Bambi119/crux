using UnityEngine;
using Crux.Unit;

namespace Crux.UI
{
    /// <summary>모듈 상태 8셀 패널 — BattleHUD에서 위임받아 렌더링</summary>
    public class BattleHUDModulePanel
    {
        private BattleHUD hud;
        private Crux.Core.BattleController controller;

        public BattleHUDModulePanel(BattleHUD hud, Crux.Core.BattleController controller)
        {
            this.hud = hud;
            this.controller = controller;
        }

        public void Draw()
        {
            // 정보 패널과 동일한 유닛 기준 (적 클릭 시 적 모듈 상태)
            var u = controller.InspectedUnit != null && !controller.InspectedUnit.IsDestroyed
                    ? controller.InspectedUnit : controller.SelectedUnit;
            if (u == null || u.IsDestroyed) return;

            var modules = u.Modules;
            float panelX = 10f, panelY = 240f, panelW = 350f, panelH = 58f;

            hud.DrawBox(new Rect(panelX, panelY, panelW, panelH));

            var style = hud.GetLabelStyleUI();
            style.fontSize = 15;

            var types = new (ModuleType type, string name)[]
            {
                (ModuleType.Engine, "엔진"),
                (ModuleType.Barrel, "포신"),
                (ModuleType.AmmoRack, "탄약"),
                (ModuleType.Loader, "장전"),
                (ModuleType.MachineGun, "기총"),
                (ModuleType.TurretRing, "포탑"),
                (ModuleType.CaterpillarLeft, "캐L"),
                (ModuleType.CaterpillarRight, "캐R"),
            };

            float x = panelX + 5;
            float y2 = panelY + 3;
            int col = 0;

            foreach (var (type, name) in types)
            {
                var m = modules.Get(type);
                if (m == null) continue;

                style.normal.textColor = m.state switch
                {
                    ModuleState.Normal => new Color(0.7f, 0.7f, 0.7f),
                    ModuleState.Damaged => new Color(1f, 0.9f, 0.2f),
                    ModuleState.Broken => new Color(1f, 0.3f, 0.2f),
                    ModuleState.Destroyed => new Color(0.4f, 0.4f, 0.4f),
                    _ => Color.white
                };

                string stateChar = m.state switch
                {
                    ModuleState.Normal => "",
                    ModuleState.Damaged => "!",
                    ModuleState.Broken => "X",
                    ModuleState.Destroyed => "#",
                    _ => ""
                };

                GUI.Label(new Rect(x + col * 85, y2, 80, 16), $"{name}{stateChar}", style);
                col++;
                if (col >= 4)
                {
                    col = 0;
                    y2 += 17;
                }
            }
        }
    }
}
