using UnityEngine;
using Crux.Grid;
using Crux.Unit;

namespace Crux.Combat
{
    /// <summary>화재 누적 사망 처리 — 전략맵 내 폭파 연출 + 배너 표시</summary>
    public class FireKillHandler
    {
        private readonly GridManager grid;
        private readonly System.Action<string, Color, float> onShowBanner;

        public FireKillHandler(GridManager grid, System.Action<string, Color, float> onShowBanner)
        {
            this.grid = grid;
            this.onShowBanner = onShowBanner;
        }

        /// <summary>화재로 인한 유닛 격파 처리</summary>
        public void Handle(GridTankUnit unit)
        {
            if (unit == null) return;
            Vector3 pos = unit.transform.position;

            // 폭파 이펙트 — 기존 HitEffects 재사용
            HitEffects.SpawnExplosion(pos);

            // 배너 표시
            onShowBanner?.Invoke($"화재로 인한 전소! — {unit.Data?.tankName}",
                                new Color(1f, 0.4f, 0.15f), 2.5f);

            // 유닛 외형 비활성화 (남은 처리는 기존 IsDestroyed 로직에 맡김)
            var cell = grid.GetCell(unit.GridPosition);
            if (cell != null && cell.Occupant == unit.gameObject)
                cell.Occupant = null;
            unit.gameObject.SetActive(false);

            Debug.Log($"[CRUX] {unit.Data?.tankName} 화재로 인한 전소");
        }
    }
}
