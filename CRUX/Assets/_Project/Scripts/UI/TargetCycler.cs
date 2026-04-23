using System.Collections.Generic;
using UnityEngine;
using Crux.Unit;

namespace Crux.UI
{
    /// <summary>
    /// 사격 모드에서 유효한 목표들 사이 순환 선택.
    ///
    /// Tab 키로 다음 목표로 넘기거나 Shift+Tab으로 이전 목표로 돌아감.
    /// 현재 플레이어 유닛의 사격 범위 내의 적 유닛만 순환.
    ///
    /// 참고: docs/10c §3.2 — 목표 순환 입력
    /// </summary>
    public class TargetCycler : MonoBehaviour
    {
        /// <summary>현재 순환 목표 리스트</summary>
        private List<GridTankUnit> validTargets = new();

        /// <summary>현재 선택된 목표 인덱스</summary>
        private int currentTargetIndex = -1;

        /// <summary>목표 변경 콜백</summary>
        public event System.Action<GridTankUnit> OnTargetChanged;

        /// <summary>유효한 목표 초기화</summary>
        public void SetValidTargets(List<GridTankUnit> targets)
        {
            validTargets = targets != null ? new List<GridTankUnit>(targets) : new List<GridTankUnit>();
            currentTargetIndex = -1;

            if (validTargets.Count > 0)
            {
                currentTargetIndex = 0;
                OnTargetChanged?.Invoke(validTargets[0]);
            }
        }

        /// <summary>다음 목표로 순환</summary>
        public void CycleToNext()
        {
            if (validTargets.Count == 0) return;

            currentTargetIndex = (currentTargetIndex + 1) % validTargets.Count;
            OnTargetChanged?.Invoke(validTargets[currentTargetIndex]);
        }

        /// <summary>이전 목표로 순환</summary>
        public void CycleToPrevious()
        {
            if (validTargets.Count == 0) return;

            currentTargetIndex = (currentTargetIndex - 1 + validTargets.Count) % validTargets.Count;
            OnTargetChanged?.Invoke(validTargets[currentTargetIndex]);
        }

        /// <summary>현재 선택된 목표 조회</summary>
        public GridTankUnit GetCurrentTarget()
        {
            if (currentTargetIndex >= 0 && currentTargetIndex < validTargets.Count)
                return validTargets[currentTargetIndex];
            return null;
        }

        /// <summary>유효한 목표 개수</summary>
        public int GetValidTargetCount() => validTargets.Count;

        /// <summary>목표 리스트 초기화</summary>
        public void Clear()
        {
            validTargets.Clear();
            currentTargetIndex = -1;
        }
    }
}
