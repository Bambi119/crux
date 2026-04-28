using System.Collections.Generic;
using Crux.Combat;

namespace Crux.Core
{
    /// <summary>연속 사격 연출 큐 — 공격→반격 등 다중 액션</summary>
    public static class FireActionContext
    {
        public static List<FireActionData> Actions = new();
        public static int CurrentIndex;

        /// <summary>현재 처리 중인 사격 데이터</summary>
        public static FireActionData Current =>
            Actions.Count > 0 && CurrentIndex < Actions.Count ? Actions[CurrentIndex] : default;

        /// <summary>대기 중인 사격이 있는가</summary>
        public static bool HasPendingAction => Actions.Count > 0;

        /// <summary>현재 다음 사격이 있는가</summary>
        public static bool HasNext => CurrentIndex + 1 < Actions.Count;

        /// <summary>
        /// 반격 WeaponSelect 필요 플래그.
        /// 적이 플레이어를 공격하고 플레이어가 반격 가능한 상황 — FireActionScene 내부에서 처리.
        /// </summary>
        public static bool PendingCounterSelect;

        /// <summary>
        /// 반격 무기 선택 완료 콜백 — BattleController 측이 등록, CounterFireUIPanel이 호출.
        /// 선택된 WeaponType을 인자로 전달.
        /// </summary>
        public static System.Action<WeaponType> OnCounterWeaponSelected;

        /// <summary>사격 데이터를 큐에 추가</summary>
        public static void Enqueue(FireActionData data)
        {
            Actions.Add(data);
        }

        /// <summary>다음 사격으로 진행</summary>
        public static void Advance()
        {
            if (HasNext)
                CurrentIndex++;
        }

        /// <summary>큐 전체 초기화</summary>
        public static void Clear()
        {
            Actions.Clear();
            CurrentIndex = 0;
            PendingCounterSelect = false;
            OnCounterWeaponSelected = null;
        }
    }
}
