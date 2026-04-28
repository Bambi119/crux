#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using Crux.Core;

namespace Crux.EditorTools.Automation
{
    /// <summary>
    /// 시나리오 스텝 실행 헬퍼 — BattleController API 디스패치 + 상태 반영 읽기.
    /// static 유틸리티, 인스턴스 없음.
    /// </summary>
    public static class CruxScenarioInputHelper
    {
        // ──────────────────────────────────────────────
        //  컨트롤러 접근
        // ──────────────────────────────────────────────

        /// <summary>씬에서 BattleController를 찾아 반환. 없으면 null.</summary>
        public static BattleController GetController()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<BattleController>();
#else
#pragma warning disable CS0618
            return UnityEngine.Object.FindObjectOfType<BattleController>();
#pragma warning restore CS0618
#endif
        }

        // ──────────────────────────────────────────────
        //  액션 디스패치
        // ──────────────────────────────────────────────

        /// <summary>
        /// 스텝의 action에 따라 BattleController API를 호출한다.
        /// </summary>
        /// <exception cref="InvalidOperationException">BattleController 없음 또는 인자 파싱 실패</exception>
        public static void ApplyAction(CruxScenarioStep step)
        {
            var bc = GetController();
            if (bc == null)
                throw new InvalidOperationException("[ScenarioRunner] BattleController가 씬에 없음");

            switch (step.action)
            {
                case ScenarioAction.ClickCell:
                    bc.HandleClickAt(step.cellTarget);
                    break;

                case ScenarioAction.EndTurn:
                    bc.EndPlayerTurn();
                    break;

                case ScenarioAction.ShowCommandBox:
                    bc.ShowCommandBox();
                    break;

                case ScenarioAction.HideCommandBox:
                    bc.HideCommandBox();
                    break;

                case ScenarioAction.SelectWeapon:
                    bc.SelectWeapon(ParseWeaponType(step.apiArg));
                    break;

                case ScenarioAction.CommitWeapon:
                    bc.CommitWeaponSelection();
                    break;

                case ScenarioAction.RotateAngle:
                    bc.SetPendingFacingAngle(ParseFloat(step.apiArg, "RotateAngle"));
                    break;

                case ScenarioAction.CommitMoveDirection:
                    bc.CommitMoveDirection();
                    break;

                case ScenarioAction.UndoMoveSnapshot:
                    bc.UndoMoveSnapshot();
                    break;

                case ScenarioAction.CancelToSelect:
                    bc.CancelToSelect();
                    break;

                case ScenarioAction.AssertState:
                    // AssertState 자체는 ApplyAction에서 실행하지 않음.
                    // Runner가 AssertEquals를 직접 호출한다.
                    break;

                case ScenarioAction.Wait:
                    // 대기만 — Runner가 waitSeconds 처리
                    break;

                default:
                    throw new InvalidOperationException($"[ScenarioRunner] 미처리 액션: {step.action}");
            }
        }

        // ──────────────────────────────────────────────
        //  상태 읽기
        // ──────────────────────────────────────────────

        /// <summary>
        /// Reflection으로 BattleController의 public 프로퍼티/필드를 읽어 ToString() 반환.
        /// 대소문자 무감.
        /// </summary>
        /// <exception cref="InvalidOperationException">키 미발견 또는 컨트롤러 없음</exception>
        public static string ReadState(string key)
        {
            var bc = GetController();
            if (bc == null)
                throw new InvalidOperationException("[ScenarioRunner] BattleController 없음 — ReadState 실패");

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var type = bc.GetType();

            var prop = type.GetProperty(key, flags);
            if (prop != null)
                return prop.GetValue(bc)?.ToString() ?? "null";

            var field = type.GetField(key, flags);
            if (field != null)
                return field.GetValue(bc)?.ToString() ?? "null";

            throw new InvalidOperationException(
                $"[ScenarioRunner] BattleController에 '{key}' 프로퍼티/필드 없음");
        }

        /// <summary>실제 값과 기대 값을 공백·대소문자 정규화 후 비교한다.</summary>
        public static bool AssertEquals(string actual, string expected)
        {
            var a = actual?.Trim() ?? "";
            var e = expected?.Trim() ?? "";
            return string.Equals(a, e, StringComparison.OrdinalIgnoreCase);
        }

        // ──────────────────────────────────────────────
        //  파싱 유틸
        // ──────────────────────────────────────────────

        static WeaponType ParseWeaponType(string arg)
        {
            if (Enum.TryParse<WeaponType>(arg, ignoreCase: true, out var result))
                return result;
            throw new InvalidOperationException(
                $"[ScenarioRunner] WeaponType 파싱 실패: '{arg}'. 유효값: MainGun | CoaxialMG | MountedMG");
        }

        static float ParseFloat(string arg, string context)
        {
            if (float.TryParse(arg, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            throw new InvalidOperationException(
                $"[ScenarioRunner] float 파싱 실패 ({context}): '{arg}'");
        }
    }
}
#endif
