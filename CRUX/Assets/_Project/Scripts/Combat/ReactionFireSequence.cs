using System;
using System.Collections;
using UnityEngine;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Core;
using Crux.Camera;
using Crux.Cinematic;
using Random = UnityEngine.Random;

namespace Crux.Combat
{
    /// <summary>오버워치 반응 사격 연출 시퀀스 — 코루틴 기반, 카메라 독점</summary>
    public class ReactionFireSequence : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }

        private const float OverwatchArcHalfWidth = 25f; // 전방 50° (±25°)

        private GridManager grid;
        private BattleCamera battleCam;
        private FireExecutor fireExecutor;
        private GridTankUnit playerUnit;
        private Action<string, Color, float> showBanner;
        private Action<Vector3, float> showAlert;

        /// <summary>의존성 주입. BattleHUD는 Action delegate로 우회 (Combat → UI 역참조 회피).</summary>
        public void Initialize(
            GridManager grid,
            BattleCamera battleCam,
            FireExecutor fireExecutor,
            GridTankUnit playerUnit,
            Action<string, Color, float> showBanner,
            Action<Vector3, float> showAlert)
        {
            this.grid = grid;
            this.battleCam = battleCam;
            this.fireExecutor = fireExecutor;
            this.playerUnit = playerUnit;
            this.showBanner = showBanner;
            this.showAlert = showAlert;
        }

        /// <summary>적이 한 셀 이동 완료할 때마다 호출 — 플레이어측 오버워치 트리거 판정</summary>
        public void HandleEnemyMoveStep(GridTankUnit movingEnemy, Vector2Int newPos)
        {
            if (movingEnemy == null || movingEnemy.IsDestroyed) return;
            if (playerUnit == null || playerUnit.IsDestroyed) return;
            if (!playerUnit.IsOverwatching) return;

            // 사거리 체크
            int dist = grid.GetDistance(playerUnit.GridPosition, newPos);
            if (dist <= 0 || dist > GameConstants.MaxFireRange) return;

            // 각도 체크 — 플레이어 차체 방향 vs 대상 방향의 차이가 ±25° 이내
            Vector3 attackerWorld = grid.GridToWorld(playerUnit.GridPosition);
            Vector3 targetWorld = grid.GridToWorld(newPos);
            Vector2 dir = ((Vector2)(targetWorld - attackerWorld)).normalized;
            float dirAngle = AngleUtil.FromDir(dir);
            float delta = Mathf.Abs(Mathf.DeltaAngle(playerUnit.HullAngle, dirAngle));
            if (delta > OverwatchArcHalfWidth) return;

            // 트리거! — 반응 사격 시퀀스 시작 (카메라 팬 + 경고 마커 + 탄환 트레이서)
            // 이동 중인 적을 오버워치 면역으로 표시 (반격 불가 — §06 §3.4 조건 7)
            movingEnemy.SetCounterImmune(true);
            StartCoroutine(Execute(playerUnit, movingEnemy));
        }

        /// <summary>
        /// 반응 사격 연출 시퀀스 — 비트 분리형. 각 단계가 사용자에게 인지되도록 간격 확보.
        /// IsPlaying 플래그로 이동 중인 적 코루틴을 일시 정지시킴.
        /// </summary>
        /// <remarks>
        /// 6개 비트로 분리:
        ///   [1] 0.00s 카메라 즉시 점프 → 공격자 타이트 줌인 (사격자 표현)
        ///   [2] 0.30s "!" 마커 + 발동 배너 (띠링! 인지)
        ///   [3] 0.65s 줌 아웃 애니메이션 0.15s (공격자+목표 한 화면)
        ///  [3.5] 0.80s 조준 여유 0.20s (줌 아웃 후 발사 전 breath)
        ///   [4] 1.00s 머즐 + 레이저 트레이서 0.08s (빠른 탄환)
        ///   [5] 1.08s 강타 VFX + DamagePopup + 데미지 + 결과 배너
        ///        +0.35s 판정 결과 인지 여운 → 카메라 즉시 복귀
        /// 총 ≈1.43초.
        /// </remarks>
        private IEnumerator Execute(GridTankUnit attacker, GridTankUnit target)
        {
            IsPlaying = true;

            attacker.ConsumeOverwatchShot();
            if (!attacker.ConsumeMainGunRound())
            {
                Debug.LogWarning("[CRUX] 오버워치 트리거 시점 주포 잔탄 0 — 사격 무시");
                IsPlaying = false;
                yield break;
            }

            Vector3 attackerPos = attacker.transform.position;
            Vector3 targetPos = target.transform.position;
            battleCam?.SaveState();

            // ===== [1] 카메라 즉시 점프 — 공격자 타이트 줌인 =====
            const float closeupSize = 2.5f; // battleCam.MinSize(3) 우회 — 더 타이트하게
            Vector3 closeupPos = new Vector3(attackerPos.x, attackerPos.y, -10f);
            battleCam?.SnapTo(closeupPos, closeupSize);

            // 사격자 인지 시간
            yield return new WaitForSeconds(0.30f);

            // ===== [2] 머리 위 "!" 마커 + 발동 배너 =====
            showAlert?.Invoke(attackerPos, 0.40f);
            showBanner?.Invoke($"⚠ 오버워치 — {attacker.Data?.tankName}",
                       new Color(1f, 0.4f, 0.2f), 1.2f);

            // 느낌표/배너 인지 시간
            yield return new WaitForSeconds(0.35f);

            // ===== [3] 줌 아웃 — 공격자+목표 한 화면으로 (짧은 eased 애니메이션) =====
            Vector3 midPos = (attackerPos + targetPos) * 0.5f;
            float dx = Mathf.Abs(attackerPos.x - targetPos.x);
            float dy = Mathf.Abs(attackerPos.y - targetPos.y);
            float aspect = battleCam?.Cam.aspect ?? (16f / 9f);
            const float margin = 2.0f;
            float halfByH = dy * 0.5f + margin;
            float halfByW = (dx * 0.5f + margin) / Mathf.Max(0.1f, aspect);
            float wideSize = Mathf.Max(halfByH, halfByW);
            wideSize = Mathf.Max(wideSize, closeupSize + 0.5f);
            Vector3 widePos = new Vector3(midPos.x, midPos.y, -10f);

            const float zoomOutDur = 0.15f;
            float zoomT = 0f;
            while (zoomT < zoomOutDur)
            {
                zoomT += Time.deltaTime;
                float u = Mathf.Clamp01(zoomT / zoomOutDur);
                // Ease-out quad
                float e = 1f - (1f - u) * (1f - u);
                battleCam.Cam.transform.position = Vector3.Lerp(closeupPos, widePos, e);
                battleCam.Cam.orthographicSize = Mathf.Lerp(closeupSize, wideSize, e);
                yield return null;
            }
            battleCam?.SnapTo(widePos, wideSize);

            // ===== [3.5] 조준 여유 — 줌 아웃 후 발사 전 breath =====
            yield return new WaitForSeconds(0.20f);

            // ===== [4] 명중 판정 + 머즐 + 레이저 트레이서 =====
            float hitChance = fireExecutor.CalculateHitChanceWithCover(attacker, target);
            bool hit = Random.value <= hitChance;

            Vector2 fireDir = ((Vector2)(targetPos - attackerPos)).normalized;
            Vector3 muzzlePos = attackerPos + (Vector3)(fireDir * 0.3f);

            MuzzleFlash.Spawn(muzzlePos, fireDir);
            yield return null; // 머즐 인지 1프레임
            yield return StartCoroutine(AnimateReactionTracer(muzzlePos, targetPos, 0.08f));

            // ===== [5] 강타 — 명중/빗나감 판정 실행 + DamagePopup 피드백 =====
            if (!hit)
            {
                Vector3 missPos = targetPos + (Vector3)(Random.insideUnitCircle * 0.2f);
                HitEffects.Spawn(missPos, ShotOutcome.Miss, fireDir);
                // MISS 팝업 — 목표 머리 위
                Crux.Cinematic.DamagePopup.Spawn(targetPos, 0f, ShotOutcome.Miss);
                showBanner?.Invoke($"⌁ 반응 사격 빗나감 — {target.Data?.tankName}",
                           new Color(0.8f, 0.8f, 0.4f), 1.5f);
                Debug.Log($"[CRUX] 오버워치 빗나감");
            }
            else
            {
                var hitZone = PenetrationCalculator.DetermineHitZone(
                    attackerPos, targetPos, target.HullAngle);
                float baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, hitZone);
                float impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                    attackerPos, targetPos, target.HullAngle, hitZone);
                float effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(baseArmor, impactAngle);

                float pen = attacker.currentAmmo != null ? attacker.currentAmmo.penetration : 100f;
                var outcome = PenetrationCalculator.JudgePenetration(pen, effectiveArmor);

                float dmg = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                float finalDmg = outcome switch
                {
                    ShotOutcome.Ricochet => dmg * 0.03f,
                    ShotOutcome.Hit => dmg,
                    ShotOutcome.Penetration => dmg * 2.5f,
                    _ => 0f
                };

                float caliberScale = attacker.currentAmmo != null
                    ? HitEffects.CaliberScaleFromAmmoDamage(attacker.currentAmmo.damage)
                    : 1f;
                HitEffects.Spawn(targetPos, outcome, fireDir, caliberScale);

                // 데미지 팝업 — 목표 머리 위 (outcome별 색/텍스트 자동)
                Crux.Cinematic.DamagePopup.Spawn(targetPos, finalDmg, outcome);

                var info = new DamageInfo
                {
                    damage = finalDmg,
                    outcome = outcome,
                    hitZone = hitZone,
                    penetrationValue = pen,
                    effectiveArmor = effectiveArmor,
                    impactAngle = impactAngle
                };
                var prerolled = target.PreRollDamage(info);
                target.ApplyPrerolledDamage(info, prerolled);

                string outcomeLabel = outcome == ShotOutcome.Penetration ? "관통"
                                      : outcome == ShotOutcome.Hit ? "명중" : "도탄";
                Color bannerCol = outcome switch
                {
                    ShotOutcome.Penetration => new Color(1f, 0.25f, 0.15f), // 빨강
                    ShotOutcome.Hit         => new Color(1f, 0.55f, 0.15f), // 주황
                    ShotOutcome.Ricochet    => new Color(1f, 0.85f, 0.25f), // 노랑
                    _ => new Color(1f, 0.6f, 0.2f)
                };
                showBanner?.Invoke($"⌁ 반응 사격 {outcomeLabel}! — {target.Data?.tankName}",
                           bannerCol, 1.8f);
                Debug.Log($"[CRUX] 오버워치 반격: {outcomeLabel} dmg={finalDmg:F0}");
            }

            // ===== [5-후] 판정 결과 인지 여운 =====
            yield return new WaitForSeconds(0.35f);

            // ===== 카메라 즉시 복귀 =====
            battleCam?.RestoreState();

            // 오버워치 면역 해제 (적이 생존했으면 다음 턴에 반격 재개 가능)
            if (!target.IsDestroyed)
            {
                target.SetCounterImmune(false);
            }

            IsPlaying = false;
        }

        /// <summary>반응 사격 레이저식 트레이서 — 전체 선을 한 프레임에 그리고 즉시 페이드</summary>
        /// <remarks>점진 연장은 리니어/느리게 보이므로 폐기. 레이저 플래시 → 빠른 페이드아웃으로
        /// "순간적으로 때린다" 느낌을 연출.</remarks>
        private IEnumerator AnimateReactionTracer(Vector3 start, Vector3 end, float duration)
        {
            var obj = new GameObject("ReactionTracer");
            var lr = obj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            // 굵은 선 — 레이저 느낌 강조
            lr.widthCurve = AnimationCurve.Linear(0f, 0.18f, 1f, 0.06f);
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.sortingOrder = 25;
            lr.numCapVertices = 4;

            // 전체 선 즉시 그림 (점진 연장 아님)
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            // 페이드만 duration 동안 수행 — 순간 플래시 → 빠르게 사라짐
            var colKeys = new[]
            {
                new GradientColorKey(new Color(1f, 0.98f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.2f), 1f)
            };
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(t / duration);
                // 가파른 페이드 커브 — 시작 순간이 가장 밝음
                float alpha = a * a;
                var grad = new Gradient();
                grad.SetKeys(colKeys,
                    new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.85f, 1f) });
                lr.colorGradient = grad;
                yield return null;
            }

            Destroy(obj);
        }
    }
}
