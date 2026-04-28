#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Crux.EditorTools.Automation
{
    /// <summary>
    /// 자동화 시나리오 단계 하나를 표현하는 데이터 클래스.
    /// CruxScenarioAsset의 steps 리스트 요소로 사용.
    /// </summary>
    [Serializable]
    public class CruxScenarioStep
    {
        [Tooltip("스텝 식별 레이블 (로그·파일명에 사용)")]
        public string label = "step";

        [Tooltip("이 스텝에서 실행할 액션")]
        public ScenarioAction action = ScenarioAction.Wait;

        [Tooltip("ClickCell 액션에서 사용하는 그리드 좌표")]
        public Vector2Int cellTarget = Vector2Int.zero;

        /// <summary>
        /// 숫자 또는 enum 문자열 인자 (RotateAngle → "60", SelectWeapon → "MainGun" 등).
        /// AssertState → expectedStateValue 와 동일 역할이라 통일.
        /// </summary>
        [Tooltip("액션 인자: 각도(float), WeaponType 이름, AssertState 기대값 등")]
        public string apiArg = "";

        [Tooltip("Wait 액션 또는 다음 스텝 진입 전 대기 시간(초)")]
        public float waitSeconds = 0.2f;

        [Tooltip("AssertState 액션에서 검사할 BattleController 프로퍼티 이름 (예: CurrentInputMode)")]
        public string expectedStateKey = "";

        [Tooltip("AssertState 액션에서 기대하는 값 문자열 (예: MoveDirectionSelect, True)")]
        public string expectedStateValue = "";

        [Tooltip("스크린샷 캡처 정책")]
        public CapturePolicy capture = CapturePolicy.OnFail;
    }

    /// <summary>시나리오 스텝에서 실행 가능한 액션 종류</summary>
    public enum ScenarioAction
    {
        /// <summary>지정 그리드 셀 클릭 — BattleController.HandleClickAt(cellTarget)</summary>
        ClickCell,
        /// <summary>BattleController.EndPlayerTurn()</summary>
        EndTurn,
        /// <summary>BattleController.ShowCommandBox()</summary>
        ShowCommandBox,
        /// <summary>BattleController.HideCommandBox()</summary>
        HideCommandBox,
        /// <summary>BattleController.SelectWeapon(WeaponType) — apiArg: "MainGun" | "CoaxialMG" | "MountedMG"</summary>
        SelectWeapon,
        /// <summary>BattleController.CommitWeaponSelection()</summary>
        CommitWeapon,
        /// <summary>BattleController.SetPendingFacingAngle(float) — apiArg: 각도 문자열</summary>
        RotateAngle,
        /// <summary>BattleController.CommitMoveDirection()</summary>
        CommitMoveDirection,
        /// <summary>BattleController.UndoMoveSnapshot()</summary>
        UndoMoveSnapshot,
        /// <summary>BattleController.CancelToSelect()</summary>
        CancelToSelect,
        /// <summary>BattleController 프로퍼티 값 검증 — expectedStateKey + expectedStateValue</summary>
        AssertState,
        /// <summary>waitSeconds 동안 대기만 (다음 스텝으로 자동 진행)</summary>
        Wait,
    }

    /// <summary>스텝별 스크린샷 캡처 정책</summary>
    public enum CapturePolicy
    {
        /// <summary>캡처 없음</summary>
        None,
        /// <summary>항상 캡처</summary>
        Always,
        /// <summary>스텝 실패(AssertState Fail) 시에만 캡처</summary>
        OnFail,
    }
}
#endif
