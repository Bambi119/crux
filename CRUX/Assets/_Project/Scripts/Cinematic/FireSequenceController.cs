using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Core;
using Crux.Combat;
using Crux.Grid;
using Crux.Unit;

namespace Crux.Cinematic
{
    /// <summary>전투 연출 씬 — 사격→비행→피격 판정별 상세 연출 오케스트레이션</summary>
    public class FireSequenceController : MonoBehaviour
    {
        private FireActionData data;
        private UnityEngine.Camera cam;
        private bool sequenceDone;

        private GameObject attackerObj;
        private GameObject targetObj;
        private Transform attackerTurret;

        // UI
        private string narrativeText = "";
        private Color narrativeColor = Color.white;
        private bool showNarrative;
        private string subText = "";
        private Dictionary<Color, Texture2D> texCache = new();

        // 분리된 서브시스템
        private FireCinematicFX fx;
        private FirePostImpactHandler postImpact;

        // ===== internal accessor — FirePostImpactHandler 사용 =====

        /// <summary>PostImpactHandler에서 내러티브 텍스트 설정</summary>
        internal void ShowNarrative(string text, Color color)
        {
            narrativeText = text;
            narrativeColor = color;
            showNarrative = true;
        }

        /// <summary>PostImpactHandler에서 서브텍스트 설정</summary>
        internal string SubText
        {
            get => subText;
            set => subText = value;
        }

        /// <summary>PostImpactHandler에서 대상 이름 참조</summary>
        internal string TargetName => data.targetName ?? "";

        // ===== MonoBehaviour 라이프사이클 =====

        private void Start()
        {
            if (!FireActionContext.HasPendingAction)
            {
                SceneManager.LoadScene(GetReturnScene());
                return;
            }

            cam = UnityEngine.Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.transform.position = new Vector3(-100f, -100f, -10f); // 서브시퀀스에서 즉시 재설정
            cam.transform.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);

            fx = new FireCinematicFX(this);
            fx.SetCamera(cam);
            postImpact = new FirePostImpactHandler(this, fx);

            InitializeForCurrentAction();
            StartCoroutine(PlaySequence());
        }

        private void Update()
        {
            if (sequenceDone && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                if (FireActionContext.HasNext)
                {
                    // 반격 연출 — 기존 전차 오브젝트 제거 후 다음 액션으로
                    if (attackerObj != null) Destroy(attackerObj);
                    if (targetObj != null) Destroy(targetObj);
                    attackerObj = null;
                    targetObj = null;
                    attackerTurret = null;

                    FireActionContext.Advance();
                    sequenceDone = false;
                    showNarrative = false;
                    narrativeText = "";
                    subText = "";

                    InitializeForCurrentAction();
                    StartCoroutine(PlaySequence());
                }
                else
                {
                    // Clear는 복귀 씬의 ApplyPendingResult에서 처리
                    SceneManager.LoadScene(GetReturnScene());
                }
            }
        }

        /// <summary>현재 FireActionContext.Current 기준으로 data 갱신</summary>
        private void InitializeForCurrentAction()
        {
            data = FireActionContext.Current;
        }

        /// <summary>연출 종료 후 복귀할 전략 씬 — BattleStateStorage에 기록된 이름 우선</summary>
        private static string GetReturnScene()
        {
            var s = BattleStateStorage.SourceScene;
            return string.IsNullOrEmpty(s) ? "StrategyScene" : s;
        }

        // ===== 주포 시퀀스 오케스트레이션 =====

        private IEnumerator PlaySequence()
        {
            // ===== 배치 =====
            Vector2 fireDir = ((Vector2)(data.targetWorldPos - data.attackerWorldPos)).normalized;

            Vector3 offscreen = new Vector3(-100f, -100f, 0);
            Vector3 attackerPos = offscreen;
            Vector3 targetPos = offscreen;

            attackerObj = CreateTankVisual(attackerPos, true);
            targetObj = CreateTankVisual(targetPos, false);

            attackerObj.SetActive(false);
            targetObj.SetActive(false);

            float attackerAngle = data.attackerHullAngle;
            float targetFaceAngle = data.targetHullAngle;

            GameObject targetCoverObj = null;

            // 무기 분기
            if (data.weaponType == WeaponType.CoaxialMG || data.weaponType == WeaponType.MountedMG)
            {
                yield return PlayMGSequence(attackerPos, targetPos, fireDir);
                yield break;
            }

            // ===== 주포 시퀀스 =====
            yield return PlayOpenFireSequence(attackerPos, targetPos, fireDir, attackerAngle);

            Vector3 turretPos = attackerTurret != null ? attackerTurret.position : attackerObj.transform.position;

            float turretZ = attackerTurret != null
                ? attackerTurret.eulerAngles.z
                : attackerObj.transform.eulerAngles.z;
            float turretRad = turretZ * Mathf.Deg2Rad;

            Vector2 muzzleOff = data.attackerMuzzleOffset;
            Vector3 rotatedOffset = new Vector3(
                muzzleOff.x * Mathf.Cos(turretRad) - muzzleOff.y * Mathf.Sin(turretRad),
                muzzleOff.x * Mathf.Sin(turretRad) + muzzleOff.y * Mathf.Cos(turretRad),
                0);
            Vector3 muzzlePos = turretPos + rotatedOffset;

            Vector2 currentFireDir = ((Vector3)targetObj.transform.position - muzzlePos).normalized;

            // ===== 사격! =====
            MuzzleFlash.Spawn(muzzlePos, currentFireDir);
            StartCoroutine(fx.CameraShake(0.15f, 0.06f));
            ShowNarrative("사격!", new Color(1f, 0.9f, 0.3f));
            yield return new WaitForSeconds(0.15f);

            // ===== 포탄 비행 =====
            Vector3 actualTargetPos = targetObj.transform.position;
            Vector3 impactPos = actualTargetPos;

            if (data.targetCoverHit)
            {
                Vector2 toMuzzle = ((Vector2)(muzzlePos - actualTargetPos)).normalized;
                impactPos = actualTargetPos + (Vector3)(toMuzzle * 0.6f);
            }

            var projectile = CreateProjectile(muzzlePos);
            float projAngle = Mathf.Atan2(currentFireDir.y, currentFireDir.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, projAngle);

            float flightTime = 0.12f;
            float ft = 0;

            Vector3 midPoint = (attackerObj.transform.position + impactPos) * 0.5f;

            while (ft < 1f)
            {
                ft += Time.deltaTime / flightTime;
                float clamped = Mathf.Clamp01(ft);
                projectile.transform.position = Vector3.Lerp(muzzlePos, impactPos, clamped);

                cam.transform.position = Vector3.Lerp(cam.transform.position,
                    new Vector3(midPoint.x, midPoint.y, -10f), 20f * Time.deltaTime);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, 4.5f, 10f * Time.deltaTime);

                yield return null;
            }
            Destroy(projectile);

            // ===== 착탄 =====
            cam.transform.position = new Vector3(impactPos.x, impactPos.y, -10f);
            cam.orthographicSize = 2f;
            StartCoroutine(fx.CameraShake(0.15f, 0.06f));

            if (data.targetCoverHit)
            {
                HitEffects.SpawnCoverHit(impactPos);
                string coverLabel = string.IsNullOrEmpty(data.targetCoverName) ? "엄폐물" : data.targetCoverName;
                DamagePopup.SpawnCoverHit(impactPos, data.coverDamageDealt, coverLabel);
                ShowNarrative("엄폐물 피격!", new Color(0.3f, 0.85f, 0.9f));

                if (targetCoverObj != null)
                    StartCoroutine(fx.ShakeObject(targetCoverObj, 0.2f, 0.05f));

                yield return new WaitForSeconds(1f);
            }
            else
            {
                float caliberScale = data.ammoData != null
                    ? HitEffects.CaliberScaleFromAmmoDamage(data.ammoData.damage)
                    : 1f;
                HitEffects.Spawn(impactPos, data.result.outcome, currentFireDir, caliberScale);

                if (data.result.hit && data.result.damageDealt > 0)
                    DamagePopup.Spawn(impactPos, data.result.damageDealt, data.result.outcome);

                switch (data.result.outcome)
                {
                    case ShotOutcome.Miss:
                        yield return PlayMissSequence(impactPos, currentFireDir);
                        break;
                    case ShotOutcome.Ricochet:
                        yield return PlayRicochetSequence(impactPos, currentFireDir);
                        break;
                    case ShotOutcome.Hit:
                        yield return PlayHitSequence(impactPos, currentFireDir);
                        break;
                    case ShotOutcome.Penetration:
                        yield return PlayPenetrationSequence(impactPos, currentFireDir);
                        break;
                }

                if (data.result.hit && data.result.damageDealt > 0)
                    yield return postImpact.PlayPostImpactNarratives(impactPos, data.mainOutcome);
            }

            yield return new WaitForSeconds(0.6f);
            sequenceDone = true;
        }

        // ===== 개활지 사격 연출 =====

        private IEnumerator PlayOpenFireSequence(Vector3 basePos, Vector3 unusedTargetPos,
                                                   Vector2 fireDir, float attackerAngle)
        {
            float myAngle = data.attackerHullAngle;
            Vector2 hullDir = AngleUtil.ToDir(myAngle);

            Vector3 tankStart = -(Vector3)(hullDir * 4f);
            Vector3 tankStop = -(Vector3)(hullDir * 1.5f);

            float gridDist = Vector2.Distance(
                (Vector2)data.attackerWorldPos, (Vector2)data.targetWorldPos);
            float cinematicDist = Mathf.Clamp(gridDist, 6f, 13f);
            Vector3 enemyPos = (Vector3)(fireDir * cinematicDist);
            float enemyAngle = data.targetHullAngle;

            attackerObj.transform.position = tankStart;
            attackerObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            targetObj.transform.position = enemyPos;
            targetObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(enemyAngle));

            if (attackerTurret != null)
                attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            if (data.targetInCover || data.targetCoverHit)
            {
                float cellSize = GameConstants.CellSize;
                const float visInflate = 1.8f;
                float edgeLength = cellSize * visInflate;
                float edgeDist = cellSize * visInflate * Mathf.Sqrt(3f) / 2f;

                float spriteWorldWidth = data.targetCoverSize switch
                {
                    CoverSize.Small  => 40f / 32f,
                    CoverSize.Medium => 48f / 32f,
                    CoverSize.Large  => 56f / 32f,
                    _ => 48f / 32f
                };
                float xScale = (edgeLength * 0.8f) / spriteWorldWidth;
                float yScale = data.targetCoverSize switch
                {
                    CoverSize.Small  => 0.9f,
                    CoverSize.Medium => 1.1f,
                    CoverSize.Large  => 1.35f,
                    _ => 1.1f
                };

                foreach (var facet in data.targetCoverFacets.Enumerate())
                {
                    Vector2 edgeNormal = HexCoord.DirToWorld(facet);
                    Vector3 wallPos = enemyPos + new Vector3(edgeNormal.x, edgeNormal.y, 0f) * edgeDist;
                    Vector2 tangent = new Vector2(-edgeNormal.y, edgeNormal.x);
                    float wallRotZ = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

                    var wallObj = new GameObject($"TargetCoverWall_{facet}");
                    wallObj.transform.position = wallPos;
                    wallObj.transform.rotation = Quaternion.Euler(0, 0, wallRotZ);
                    wallObj.transform.localScale = new Vector3(xScale, yScale, 1f);
                    var wsr = wallObj.AddComponent<SpriteRenderer>();
                    wsr.sprite = TankSpriteGenerator.CreateCinematicCover(data.targetCoverSize);
                    wsr.sortingOrder = 3;
                }
            }

            attackerObj.SetActive(true);
            targetObj.SetActive(true);

            cam.orthographicSize = 2.5f;
            cam.transform.position = new Vector3(tankStart.x, tankStart.y, -10f);

            ShowNarrative("전진 중...", new Color(0.8f, 0.8f, 0.8f));

            float moveTime = 0.5f;
            float mt = 0;
            while (mt < 1f)
            {
                mt += Time.deltaTime / moveTime;
                attackerObj.transform.position = Vector3.Lerp(tankStart, tankStop, Mathf.Clamp01(mt));
                cam.transform.position = Vector3.Lerp(cam.transform.position,
                    new Vector3(attackerObj.transform.position.x, attackerObj.transform.position.y, -10f),
                    8f * Time.deltaTime);
                yield return null;
            }
            attackerObj.transform.position = tankStop;

            ShowNarrative("정지!", Color.white);
            yield return new WaitForSeconds(0.2f);

            Vector2 aimDir = (enemyPos - tankStop).normalized;
            float aimAngle = AngleUtil.FromDir(aimDir);

            if (attackerTurret != null)
            {
                float startAngle = myAngle;
                float elapsed = 0f;
                while (elapsed < 0.35f)
                {
                    elapsed += Time.deltaTime;
                    float angle = Mathf.LerpAngle(startAngle, aimAngle, elapsed / 0.35f);
                    attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(angle));
                    yield return null;
                }
                attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(aimAngle));
            }

            cam.orthographicSize = Mathf.Clamp(cinematicDist * 0.55f, 3.5f, 7f);
            Vector3 midCam = (tankStop + enemyPos) * 0.5f;
            cam.transform.position = new Vector3(midCam.x, midCam.y, -10f);

            yield return new WaitForSeconds(0.1f);
        }

        // ===== 빗나감 연출 =====

        private IEnumerator PlayMissSequence(Vector3 targetPos, Vector2 fireDir)
        {
            ShowNarrative("빗나감!", new Color(0.6f, 0.6f, 0.6f));
            subText = "포탄이 빗나갔다";
            yield return new WaitForSeconds(0.3f);
        }

        // ===== 도탄 연출 — 착탄 즉시 스파크 + 튕김 =====

        private IEnumerator PlayRicochetSequence(Vector3 targetPos, Vector2 fireDir)
        {
            ShowNarrative($"◆ 도탄!", new Color(1f, 0.8f, 0.3f));
            subText = $"{data.result.hitZone} 장갑 — 관통 실패";

            Vector2 normal = (Vector2.Perpendicular(fireDir) * (Random.value > 0.5f ? 1f : -1f)
                             + fireDir * -0.3f + Random.insideUnitCircle * 0.4f).normalized;
            Vector2 ricochetDir = Vector2.Reflect(fireDir, normal).normalized;

            var ricochetBullet = CreateProjectile(targetPos);
            float ricochetAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
            ricochetBullet.transform.rotation = Quaternion.Euler(0, 0, ricochetAngle);
            var ricochetSr = ricochetBullet.GetComponent<SpriteRenderer>();
            if (ricochetSr != null) ricochetSr.color = new Color(1f, 0.5f, 0.2f, 0.8f);

            Vector3 ricochetEnd = targetPos + (Vector3)(ricochetDir * 4f);
            float rt = 0;
            float ricochetTime = 0.15f;

            while (rt < ricochetTime)
            {
                rt += Time.deltaTime;
                float t = rt / ricochetTime;
                ricochetBullet.transform.position = Vector3.Lerp(targetPos, ricochetEnd, t);

                cam.transform.position = Vector3.Lerp(cam.transform.position,
                    new Vector3(ricochetBullet.transform.position.x, ricochetBullet.transform.position.y, -10f),
                    8f * Time.deltaTime);
                yield return null;
            }

            fx.SpawnGroundImpact(ricochetEnd);
            Destroy(ricochetBullet);

            subText += "\n포탄이 튕겨 나갔다";
            yield return new WaitForSeconds(0.3f);
        }

        // ===== 피격 연출 — 착탄 즉시 충격 =====

        private IEnumerator PlayHitSequence(Vector3 targetPos, Vector2 fireDir)
        {
            ShowNarrative($"■ 피격!", new Color(1f, 0.5f, 0.2f));
            subText = $"{data.result.hitZone} 부위 피격\n데미지: {data.result.damageDealt:F0}";

            StartCoroutine(fx.CameraShake(0.2f, 0.08f));
            StartCoroutine(fx.TankRecoil(targetObj, fireDir, 0.12f));

            yield return new WaitForSeconds(0.25f);

            fx.SpawnLingeringSmoke(targetPos);
            subText += "\n장갑이 움푹 들어갔다";

            yield return new WaitForSeconds(0.3f);
        }

        // ===== 관통 연출 — 관통+출구 포탄+내부 폭발 =====

        private IEnumerator PlayPenetrationSequence(Vector3 targetPos, Vector2 fireDir)
        {
            ShowNarrative($"▶ 관통!", new Color(1f, 0.15f, 0.1f));
            subText = $"{data.result.hitZone} 관통!\n데미지: {data.result.damageDealt:F0}";

            StartCoroutine(fx.CameraShake(0.3f, 0.12f));
            StartCoroutine(fx.TankRecoil(targetObj, fireDir, 0.2f));

            yield return new WaitForSeconds(0.1f);

            Vector3 exitPoint = targetPos + (Vector3)(fireDir * 0.6f);
            var exitBullet = CreateProjectile(exitPoint);
            float exitAngle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;
            exitBullet.transform.rotation = Quaternion.Euler(0, 0, exitAngle);
            var exitSr = exitBullet.GetComponent<SpriteRenderer>();
            if (exitSr != null) exitSr.color = new Color(1f, 0.3f, 0.1f);

            fx.SpawnExitFlame(exitPoint, fireDir);

            Vector3 exitEnd = exitPoint + (Vector3)(fireDir * 2.5f);
            float et = 0;
            while (et < 0.12f)
            {
                et += Time.deltaTime;
                exitBullet.transform.position = Vector3.Lerp(exitPoint, exitEnd, et / 0.12f);
                yield return null;
            }
            Destroy(exitBullet);

            HitEffects.SpawnExplosion(targetPos);
            StartCoroutine(fx.CameraShake(0.4f, 0.18f));

            fx.SpawnLingeringSmoke(targetPos);
            fx.SpawnLingeringSmoke(targetPos + Vector3.up * 0.2f);

            yield return new WaitForSeconds(0.5f);
        }

        // ===== 기관총 버스트 연출 =====

        // MG 볼리 집계 — AnimateMGBullet 코루틴 간 공유
        private int mgHits;
        private int mgRicochets;
        private int mgPens;
        private float mgTotalDamage;

        private IEnumerator PlayMGSequence(Vector3 attackerPosUnused, Vector3 targetPosUnused, Vector2 fireDir)
        {
            yield return PlayOpenFireSequence(attackerPosUnused, targetPosUnused, fireDir, data.attackerHullAngle);

            Vector3 turretPos = attackerTurret != null ? attackerTurret.position : attackerObj.transform.position;
            float turretZ = attackerTurret != null
                ? attackerTurret.eulerAngles.z
                : attackerObj.transform.eulerAngles.z;
            float turretRad = turretZ * Mathf.Deg2Rad;
            Vector2 muzzleOff = data.attackerMuzzleOffset;
            Vector3 rotatedOffset = new Vector3(
                muzzleOff.x * Mathf.Cos(turretRad) - muzzleOff.y * Mathf.Sin(turretRad),
                muzzleOff.x * Mathf.Sin(turretRad) + muzzleOff.y * Mathf.Cos(turretRad),
                0);
            Vector3 muzzlePos = turretPos + rotatedOffset;

            Vector3 targetPos = targetObj.transform.position;
            Vector2 currentFireDir = ((Vector3)targetPos - muzzlePos).normalized;
            Vector2 perpDir = new Vector2(-currentFireDir.y, currentFireDir.x);

            string mgName = data.mgData != null ? data.mgData.mgName : "기관총";
            int burstCount = data.mgResults != null ? data.mgResults.Length : 6;

            ShowNarrative($"{mgName} 사격!", new Color(1f, 0.7f, 0.3f));
            yield return new WaitForSeconds(0.15f);

            mgHits = 0;
            mgRicochets = 0;
            mgPens = 0;
            mgTotalDamage = 0f;

            const float spawnInterval = 0.06f;
            const float flightTime = 0.2f;
            const float lateralSpread = 0.08f;

            for (int i = 0; i < burstCount; i++)
            {
                var shotResult = data.mgResults != null && i < data.mgResults.Length
                    ? data.mgResults[i]
                    : new ShotResult { hit = false, outcome = ShotOutcome.Miss };

                float lateral = Random.Range(-lateralSpread, lateralSpread);
                Vector3 bulletStart = muzzlePos + (Vector3)(perpDir * lateral);
                Vector3 shotTarget = targetPos + (Vector3)(Random.insideUnitCircle * 0.3f);

                fx.SpawnSmallFlash(muzzlePos);
                StartCoroutine(fx.CameraShake(0.02f, 0.01f));
                StartCoroutine(AnimateMGBullet(bulletStart, shotTarget, flightTime, shotResult, currentFireDir));

                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitForSeconds(flightTime + 0.1f);

            if (mgHits > 0)
                StartCoroutine(fx.TankRecoil(targetObj, fireDir, Mathf.Min(0.05f * mgHits, 0.3f)));

            yield return new WaitForSeconds(0.3f);

            ShowNarrative("사격 완료", new Color(1f, 0.7f, 0.3f));
            subText = $"{burstCount}발 중 {mgHits}발 명중";
            if (mgRicochets > 0) subText += $" (도탄 {mgRicochets})";
            if (mgPens > 0) subText += $" (관통 {mgPens})";
            subText += $"\n총 데미지: {mgTotalDamage:F0}";

            if (mgTotalDamage > 0)
                DamagePopup.Spawn(targetPos + Vector3.up * 0.5f, mgTotalDamage, ShotOutcome.Hit);

            yield return new WaitForSeconds(0.5f);

            if (mgTotalDamage > 0)
                yield return postImpact.PlayPostImpactNarratives(targetPos, data.mgAggregateOutcome);

            yield return new WaitForSeconds(0.5f);
            sequenceDone = true;
        }

        /// <summary>MG 탄환 1발 — 스트림 볼리에서 비동기 실행. 비행·착탄·판정 일원화.</summary>
        private IEnumerator AnimateMGBullet(Vector3 start, Vector3 end, float flightTime,
                                             ShotResult shotResult, Vector2 fireDir)
        {
            var bullet = CreateProjectile(start);
            Vector2 dir = ((Vector2)(end - start)).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
            var bulletSr = bullet.GetComponent<SpriteRenderer>();
            if (bulletSr != null)
            {
                bulletSr.color = new Color(1f, 0.9f, 0.4f);
                bullet.transform.localScale = new Vector3(0.2f, 0.04f, 1f);
            }

            float ft = 0;
            while (ft < 1f)
            {
                ft += Time.deltaTime / flightTime;
                bullet.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(ft));
                yield return null;
            }
            Destroy(bullet);

            if (shotResult.hit)
            {
                mgHits++;
                mgTotalDamage += shotResult.damageDealt;

                switch (shotResult.outcome)
                {
                    case ShotOutcome.Ricochet:
                        mgRicochets++;
                        fx.SpawnSmallSpark(end);
                        DamagePopup.SpawnSmall(end, shotResult.damageDealt, false);
                        break;
                    case ShotOutcome.Hit:
                        fx.SpawnSmallHit(end);
                        DamagePopup.SpawnSmall(end, shotResult.damageDealt, true);
                        StartCoroutine(fx.CameraShake(0.03f, 0.02f));
                        break;
                    case ShotOutcome.Penetration:
                        mgPens++;
                        fx.SpawnSmallHit(end);
                        HitEffects.Spawn(end, ShotOutcome.Hit, fireDir, 0.4f);
                        DamagePopup.SpawnSmall(end, shotResult.damageDealt, true);
                        StartCoroutine(fx.CameraShake(0.05f, 0.04f));
                        break;
                }
            }
            else
            {
                fx.SpawnSmallDust(end + (Vector3)(Random.insideUnitCircle * 0.2f));
            }
        }

        // ===== 비주얼 팩토리 =====

        private GameObject CreateTankVisual(Vector3 pos, bool isAttacker)
        {
            var obj = new GameObject(isAttacker ? "Attacker" : "Target");
            obj.transform.position = pos;

            float spriteRotOffset = isAttacker ? data.attackerSpriteRotOffset : data.targetSpriteRotOffset;

            var spriteContainer = new GameObject("SpriteContainer");
            spriteContainer.transform.SetParent(obj.transform);
            spriteContainer.transform.localPosition = Vector3.zero;
            spriteContainer.transform.localRotation = Quaternion.Euler(0, 0, spriteRotOffset);

            var sr = spriteContainer.AddComponent<SpriteRenderer>();
            Sprite hullSprite = isAttacker ? data.attackerHullSprite : data.targetHullSprite;
            if (hullSprite != null)
                sr.sprite = hullSprite;
            else
                sr.sprite = isAttacker ? TankSpriteGenerator.CreatePlayerHull()
                                       : TankSpriteGenerator.CreateHeavyEnemy();
            sr.sortingOrder = 5;

            Sprite turretSprite = isAttacker ? data.attackerTurretSprite : data.targetTurretSprite;
            if (turretSprite != null)
            {
                var turretObj = new GameObject("Turret");
                turretObj.transform.SetParent(obj.transform);
                turretObj.transform.localPosition = Vector3.zero;
                turretObj.transform.localRotation = Quaternion.Euler(0, 0, spriteRotOffset);
                var turretSr = turretObj.AddComponent<SpriteRenderer>();
                turretSr.sprite = turretSprite;
                turretSr.sortingOrder = 6;

                if (isAttacker)
                    attackerTurret = turretObj.transform;
            }

            return obj;
        }

        private static Sprite _cachedBullet;
        private GameObject CreateProjectile(Vector3 startPos)
        {
            var obj = new GameObject("Projectile");
            obj.transform.position = startPos;

            var sr = obj.AddComponent<SpriteRenderer>();
            if (_cachedBullet == null)
            {
                int w = 16, h = 3;
                var tex = new Texture2D(w, h);
                tex.filterMode = FilterMode.Point;
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float t = (float)x / w;
                        float alpha = t;
                        Color c = Color.Lerp(
                            new Color(1f, 0.6f, 0.1f, 0.3f),
                            new Color(1f, 1f, 0.8f, 1f),
                            t);
                        if (y == 0 || y == h - 1) c.a *= 0.4f;
                        pixels[y * w + x] = c;
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                _cachedBullet = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(1f, 0.5f), 16);
            }
            sr.sprite = _cachedBullet;
            sr.color = Color.white;
            sr.sortingOrder = 20;
            obj.transform.localScale = new Vector3(0.5f, 0.08f, 1f);
            return obj;
        }

        // ===== UI (OnGUI — FireActionScene 전용) =====

        private void OnGUI()
        {
            if (!showNarrative) return;

            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = GetTex(new Color(0, 0, 0, 0.75f));

            var mainStyle = new GUIStyle(GUI.skin.label);
            mainStyle.fontSize = 26;
            mainStyle.fontStyle = FontStyle.Bold;
            mainStyle.alignment = TextAnchor.MiddleCenter;
            mainStyle.normal.textColor = narrativeColor;

            GUI.Box(new Rect(Screen.width / 2 - 180, 20, 360, 50), "", boxStyle);
            GUI.Label(new Rect(Screen.width / 2 - 180, 20, 360, 50), narrativeText, mainStyle);

            if (!string.IsNullOrEmpty(subText))
            {
                var subStyle = new GUIStyle(GUI.skin.label);
                subStyle.fontSize = 13;
                subStyle.alignment = TextAnchor.UpperCenter;
                subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.8f);
                subStyle.wordWrap = true;

                GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height - 120, 400, 80), "", boxStyle);
                GUI.Label(new Rect(Screen.width / 2 - 195, Screen.height - 118, 390, 76), subText, subStyle);
            }

            if (sequenceDone)
            {
                var smallStyle = new GUIStyle(GUI.skin.label);
                smallStyle.fontSize = 12;
                smallStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                smallStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height - 35, 200, 20),
                    $"명중률: {data.result.hitChance:P0}  |  클릭하여 계속...", smallStyle);
            }
        }

        private Texture2D GetTex(Color col)
        {
            if (texCache.TryGetValue(col, out var cached)) return cached;
            var tex = new Texture2D(2, 2);
            var px = new Color[4];
            for (int i = 0; i < 4; i++) px[i] = col;
            tex.SetPixels(px); tex.Apply();
            texCache[col] = tex;
            return tex;
        }
    }
}
