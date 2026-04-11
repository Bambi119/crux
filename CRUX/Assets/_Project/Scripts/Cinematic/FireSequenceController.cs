using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Core;
using Crux.Combat;

namespace Crux.Cinematic
{
    /// <summary>전투 연출 씬 — 사격→비행→피격 판정별 상세 연출</summary>
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

        private void Start()
        {
            if (!FireActionContext.HasPendingAction)
            {
                SceneManager.LoadScene("StrategyScene");
                return;
            }

            data = FireActionContext.Current;

            cam = UnityEngine.Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.transform.position = new Vector3(-100f, -100f, -10f); // 서브시퀀스에서 즉시 재설정
            cam.transform.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);

            StartCoroutine(PlaySequence());
        }

        private void Update()
        {
            if (sequenceDone && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                // Clear는 StrategyScene의 ApplyPendingResult에서 처리
                SceneManager.LoadScene("StrategyScene");
            }
        }

        private IEnumerator PlaySequence()
        {
            // ===== 배치 =====
            // 전술맵 실제 좌표에서 사격 방향 계산
            Vector2 fireDir = ((Vector2)(data.targetWorldPos - data.attackerWorldPos)).normalized;

            // 연출용 초기 배치 — 서브시퀀스에서 즉시 재배치하므로 화면 밖에 생성
            Vector3 offscreen = new Vector3(-100f, -100f, 0);
            Vector3 attackerPos = offscreen;
            Vector3 targetPos = offscreen;

            attackerObj = CreateTankVisual(attackerPos, true);
            targetObj = CreateTankVisual(targetPos, false);

            // 서브시퀀스 배치 전까지 숨김
            attackerObj.SetActive(false);
            targetObj.SetActive(false);

            // 방향은 서브시퀀스에서 설정 (여기선 임시)
            float attackerAngle = data.attackerHullAngle;
            float targetFaceAngle = data.targetHullAngle;

            GameObject targetCoverObj = null;

            // 무기 분기
            if (data.weaponType == WeaponType.CoaxialMG || data.weaponType == WeaponType.MountedMG)
            {
                yield return PlayMGSequence(attackerPos, targetPos, fireDir);
                yield break;
            }

            // ===== 주포 시퀀스 — 엄폐/개활지 분기 =====

            if (data.attackerInCover)
            {
                yield return PlayCoverFireSequence(attackerPos, targetPos, fireDir, attackerAngle);
            }
            else
            {
                yield return PlayOpenFireSequence(attackerPos, targetPos, fireDir, attackerAngle);
            }

            // 포탑 위치 + 머즐 오프셋으로 포구 위치 계산
            Vector3 turretPos = attackerTurret != null ? attackerTurret.position : attackerObj.transform.position;
            Vector2 currentFireDir = ((Vector3)targetObj.transform.position - turretPos).normalized;
            // 머즐 오프셋을 포탑 회전 방향으로 변환
            float muzzleAngle = Mathf.Atan2(currentFireDir.y, currentFireDir.x);
            Vector2 muzzleOff = data.attackerMuzzleOffset;
            Vector3 rotatedOffset = new Vector3(
                muzzleOff.x * Mathf.Cos(muzzleAngle) - muzzleOff.y * Mathf.Sin(muzzleAngle),
                muzzleOff.x * Mathf.Sin(muzzleAngle) + muzzleOff.y * Mathf.Cos(muzzleAngle),
                0);
            Vector3 muzzlePos = turretPos + rotatedOffset;

            // ===== 사격! =====
            MuzzleFlash.Spawn(muzzlePos, currentFireDir);
            StartCoroutine(CameraShake(0.15f, 0.06f));
            ShowNarrative("사격!", new Color(1f, 0.9f, 0.3f));
            yield return new WaitForSeconds(0.15f);

            // ===== [4] 포탄 비행 =====
            Vector3 actualTargetPos = targetObj.transform.position;
            Vector3 impactPos = actualTargetPos;

            // 엄폐물 피격 시 착탄점을 벽 쪽으로 오프��� (비주얼은 배치 단계에서 이미 생성됨)
            if (data.targetCoverHit)
            {
                Vector2 toAttacker = ((Vector2)(attackerObj.transform.position - actualTargetPos)).normalized;
                impactPos = actualTargetPos + (Vector3)(toAttacker * 0.6f);
            }

            var projectile = CreateProjectile(muzzlePos);

            float flightTime = 0.15f;
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

            // ===== [5] 착탄 =====
            cam.transform.position = new Vector3(impactPos.x, impactPos.y, -10f);
            cam.orthographicSize = 2f;
            StartCoroutine(CameraShake(0.15f, 0.06f));

            if (data.targetCoverHit)
            {
                // 엄폐물 피격 연출
                HitEffects.SpawnCoverHit(impactPos);
                string coverLabel = string.IsNullOrEmpty(data.targetCoverName) ? "엄폐물" : data.targetCoverName;
                DamagePopup.SpawnCoverHit(impactPos, data.coverDamageDealt, coverLabel);
                ShowNarrative("엄폐물 피격!", new Color(0.3f, 0.85f, 0.9f));

                // 엄폐물 흔들림
                if (targetCoverObj != null)
                    StartCoroutine(ShakeObject(targetCoverObj, 0.2f, 0.05f));

                yield return new WaitForSeconds(1f);
            }
            else
            {
                // 기존 전차 착탄 로직
                HitEffects.Spawn(impactPos, data.result.outcome, currentFireDir);

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
            }

            yield return new WaitForSeconds(0.6f);
            sequenceDone = true;
        }

        // ===== 엄폐 상태 사격 연출 =====
        // 엄폐물 뒤 → 측면으로 빠져나와 → 포탑 회전 → 사격
        private IEnumerator PlayCoverFireSequence(Vector3 basePos, Vector3 targetPos,
                                                    Vector2 fireDir, float attackerAngle)
        {
            // 전술맵의 실제 차체 방향 사용 (나침반: 0°=북)
            float myAngle = data.attackerHullAngle;
            Vector2 hullDir = AngleUtil.ToDir(myAngle);

            // 차체 방향에 수직인 벡터 (엄폐물 벽 방향)
            Vector2 wallDir = new Vector2(-hullDir.y, hullDir.x);

            // 적 위치 — 전술맵 차체 방향 유지
            Vector3 enemyPos = (Vector3)(fireDir * 5f);
            float enemyAngle = data.targetHullAngle;

            // 엄폐물 위치 (전방 2.5유닛)
            Vector3 coverPos = -(Vector3)(hullDir * 2.5f);

            // 측면 방향 결정: wallDir 중 적에 가까운 쪽 선택
            Vector2 toEnemy = ((Vector2)(enemyPos - coverPos)).normalized;
            float sideSign = Vector2.Dot(wallDir, toEnemy) >= 0 ? 1f : -1f;
            Vector2 peekSide = wallDir * sideSign;

            // 전차 시작: 엄폐물 뒤
            Vector3 tankStart = coverPos - (Vector3)(hullDir * 2.5f);
            // peek 위치: 엄폐물 옆 (측면으로 빠져나옴)
            Vector3 peekPos = coverPos + (Vector3)(peekSide * 1.8f) - (Vector3)(hullDir * 0.3f);

            // 엄폐물 크기별 스케일
            float coverScale = data.attackerCoverSize switch
            {
                CoverSize.Small  => 1.0f,
                CoverSize.Medium => 1.5f,
                CoverSize.Large  => 2.0f,
                _ => 1.5f
            };

            // 배치
            attackerObj.transform.position = tankStart;
            attackerObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            targetObj.transform.position = enemyPos;
            targetObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(enemyAngle));

            if (attackerTurret != null)
                attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            // 엄폐물 배치 — 차체 전방에 수직으로 세운 벽
            var coverObj = new GameObject("CoverVisual");
            coverObj.transform.position = coverPos;
            var coverSr = coverObj.AddComponent<SpriteRenderer>();
            coverSr.sprite = TankSpriteGenerator.CreateCover();
            coverSr.sortingOrder = 4;
            coverSr.color = data.attackerCoverSize switch
            {
                CoverSize.Small  => new Color(0.7f, 0.65f, 0.55f),
                CoverSize.Medium => new Color(0.55f, 0.5f, 0.45f),
                CoverSize.Large  => new Color(0.4f, 0.38f, 0.35f),
                _ => new Color(0.6f, 0.55f, 0.5f)
            };
            float wallAngle = AngleUtil.FromDir(wallDir);
            coverObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(wallAngle));
            coverObj.transform.localScale = new Vector3(coverScale, 0.5f, 1f);

            // [1] 배치 완료 — 표시
            attackerObj.SetActive(true);
            targetObj.SetActive(true);

            cam.orthographicSize = 2f;
            cam.transform.position = new Vector3(tankStart.x, tankStart.y, -10f);
            ShowNarrative("엄폐 사격", new Color(0.3f, 1f, 0.5f));
            subText = data.attackerCoverName ?? "엄폐물";

            yield return new WaitForSeconds(0.5f);

            // [2] 측면으로 이동 — 엄폐물 옆으로 빠져나옴
            ShowNarrative("측면 전개!", Color.white);

            Vector3 camStart = new Vector3(tankStart.x, tankStart.y, -10f);
            Vector3 camEnd = new Vector3(peekPos.x, peekPos.y, -10f);

            float moveTime = 0.6f;
            float mt = 0;
            while (mt < 1f)
            {
                mt += Time.deltaTime / moveTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(mt));
                attackerObj.transform.position = Vector3.Lerp(tankStart, peekPos, t);
                cam.transform.position = Vector3.Lerp(camStart, camEnd, t);
                yield return null;
            }
            attackerObj.transform.position = peekPos;

            yield return new WaitForSeconds(0.15f);

            // [3] 포탑 회전 — 적 방향 (나침반 각도)
            Vector2 aimDir = ((Vector2)(enemyPos - peekPos)).normalized;
            float aimAngle = AngleUtil.FromDir(aimDir);

            if (attackerTurret != null)
            {
                float startAngle = myAngle;
                float elapsed = 0f;
                float rotDuration = 0.4f;

                ShowNarrative("조준...", new Color(1f, 0.9f, 0.5f));

                while (elapsed < rotDuration)
                {
                    elapsed += Time.deltaTime;
                    float angle = Mathf.LerpAngle(startAngle, aimAngle, elapsed / rotDuration);
                    attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(angle));

                    cam.orthographicSize = Mathf.Lerp(2f, 4f, elapsed / rotDuration);
                    Vector3 midCam = (peekPos + enemyPos) * 0.5f;
                    cam.transform.position = Vector3.Lerp(cam.transform.position,
                        new Vector3(midCam.x, midCam.y, -10f), 5f * Time.deltaTime);

                    yield return null;
                }
                attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(aimAngle));
            }

            yield return new WaitForSeconds(0.1f);
        }

        // ===== 개활지 사격 연출 =====
        // 공격자: hullDir 반대에서 출발 → 전진 → 정지 → 포탑 조준 → 사격
        private IEnumerator PlayOpenFireSequence(Vector3 basePos, Vector3 unusedTargetPos,
                                                   Vector2 fireDir, float attackerAngle)
        {
            float myAngle = data.attackerHullAngle;
            Vector2 hullDir = AngleUtil.ToDir(myAngle);

            // 공격자: 차체 방향 반대에서 출발 → 차체 방향으로 전진
            Vector3 tankStart = -(Vector3)(hullDir * 4f);
            Vector3 tankStop = -(Vector3)(hullDir * 1.5f);

            // 적: fireDir 전방에 배치, 전술맵 차체 방향 유지
            Vector3 enemyPos = (Vector3)(fireDir * 5f);
            float enemyAngle = data.targetHullAngle;

            attackerObj.transform.position = tankStart;
            attackerObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            targetObj.transform.position = enemyPos;
            targetObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(enemyAngle));

            if (attackerTurret != null)
                attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            // 대상 엄폐물
            if (data.targetInCover || data.targetCoverHit)
            {
                var coverObj = new GameObject("TargetCoverVisual");
                coverObj.transform.position = enemyPos;
                var cvSr = coverObj.AddComponent<SpriteRenderer>();
                float incDir = AngleUtil.FromDir(((Vector2)(tankStart - enemyPos)).normalized);
                cvSr.sprite = TankSpriteGenerator.CreateCoverTile(data.targetCoverSize, incDir);
                cvSr.sortingOrder = 3;
                coverObj.transform.localScale = Vector3.one * 1.5f;
            }

            // [1] 표시 + 카메라 (공격자 위치에서 시작)
            attackerObj.SetActive(true);
            targetObj.SetActive(true);

            cam.orthographicSize = 2.5f;
            cam.transform.position = new Vector3(tankStart.x, tankStart.y, -10f);

            // [2] 차체 방향으로 전진
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

            // [3] 정지
            ShowNarrative("정지!", Color.white);
            yield return new WaitForSeconds(0.2f);

            // [4] 포탑 회전 → 적 방향
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

            // 카메라 줌아웃
            cam.orthographicSize = 4f;
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
            // 즉시 판정 텍스트
            ShowNarrative($"◆ 도탄!", new Color(1f, 0.8f, 0.3f));
            subText = $"{data.result.hitZone} 장갑 — 관통 실패";

            // 즉시 튕김 포탄 생성 — 진행 방향 기준 랜덤 반사
            Vector2 normal = (Vector2.Perpendicular(fireDir) * (Random.value > 0.5f ? 1f : -1f)
                             + fireDir * -0.3f + Random.insideUnitCircle * 0.4f).normalized;
            Vector2 ricochetDir = Vector2.Reflect(fireDir, normal).normalized;

            var ricochetBullet = CreateProjectile(targetPos);
            var ricochetSr = ricochetBullet.GetComponent<SpriteRenderer>();
            if (ricochetSr != null) ricochetSr.color = new Color(1f, 0.5f, 0.2f, 0.8f);

            Vector3 ricochetEnd = targetPos + (Vector3)(ricochetDir * 4f);
            float rt = 0;
            float ricochetTime = 0.15f; // 포탄 비행과 같은 속도감

            while (rt < ricochetTime)
            {
                rt += Time.deltaTime;
                float t = rt / ricochetTime;
                ricochetBullet.transform.position = Vector3.Lerp(targetPos, ricochetEnd, t);

                // 카메라 살짝 추적
                cam.transform.position = Vector3.Lerp(cam.transform.position,
                    new Vector3(ricochetBullet.transform.position.x, ricochetBullet.transform.position.y, -10f),
                    8f * Time.deltaTime);
                yield return null;
            }

            // 지면 충돌
            SpawnGroundImpact(ricochetEnd);
            Destroy(ricochetBullet);

            subText += "\n포탄이 튕겨 나갔다";
            yield return new WaitForSeconds(0.3f);
        }

        // ===== 피격 연출 — 착탄 즉시 충격 =====
        private IEnumerator PlayHitSequence(Vector3 targetPos, Vector2 fireDir)
        {
            ShowNarrative($"■ 피격!", new Color(1f, 0.5f, 0.2f));
            subText = $"{data.result.hitZone} 부위 피격\n데미지: {data.result.damageDealt:F0}";

            // 즉시 반동 + 추가 쉐이크
            StartCoroutine(CameraShake(0.2f, 0.08f));
            StartCoroutine(TankRecoil(targetObj, fireDir, 0.12f));

            yield return new WaitForSeconds(0.25f);

            // 연기
            SpawnLingeringSmoke(targetPos);
            subText += "\n장갑이 움푹 들어갔다";

            yield return new WaitForSeconds(0.3f);
        }

        // ===== 관통 연출 — 관통+출구 포탄+내부 폭발 =====
        private IEnumerator PlayPenetrationSequence(Vector3 targetPos, Vector2 fireDir)
        {
            ShowNarrative($"▶ 관통!", new Color(1f, 0.15f, 0.1f));
            subText = $"{data.result.hitZone} 관통!\n데미지: {data.result.damageDealt:F0}";

            StartCoroutine(CameraShake(0.3f, 0.12f));
            StartCoroutine(TankRecoil(targetObj, fireDir, 0.2f));

            yield return new WaitForSeconds(0.1f);

            // 반대편으로 포탄 뚫고 나옴
            Vector3 exitPoint = targetPos + (Vector3)(fireDir * 0.6f);
            var exitBullet = CreateProjectile(exitPoint);
            var exitSr = exitBullet.GetComponent<SpriteRenderer>();
            if (exitSr != null) exitSr.color = new Color(1f, 0.3f, 0.1f);

            SpawnExitFlame(exitPoint, fireDir);

            Vector3 exitEnd = exitPoint + (Vector3)(fireDir * 2.5f);
            float et = 0;
            while (et < 0.12f)
            {
                et += Time.deltaTime;
                exitBullet.transform.position = Vector3.Lerp(exitPoint, exitEnd, et / 0.12f);
                yield return null;
            }
            Destroy(exitBullet);

            // 내부 폭발
            HitEffects.SpawnExplosion(targetPos);
            StartCoroutine(CameraShake(0.4f, 0.18f));

            SpawnLingeringSmoke(targetPos);
            SpawnLingeringSmoke(targetPos + Vector3.up * 0.2f);

            subText += "\n내부 폭발! 전차가 불타오른다!";
            yield return new WaitForSeconds(0.5f);
        }

        // ===== 기관총 버스트 연출 (일제사격 → 탄줄 비행+즉시 판정 → 결과) =====

        private IEnumerator PlayMGSequence(Vector3 attackerPosUnused, Vector3 targetPosUnused, Vector2 fireDir)
        {
            // 위치 재배치 — 원점 기준
            Vector2 hullDir = AngleUtil.ToDir(data.attackerHullAngle);
            Vector3 attackerPos = -(Vector3)(hullDir * 3f);
            Vector3 targetPos = (Vector3)(fireDir * 4f);

            attackerObj.transform.position = attackerPos;
            attackerObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(data.attackerHullAngle));
            targetObj.transform.position = targetPos;
            targetObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(data.targetHullAngle));

            // 배치 완료 — 표시
            attackerObj.SetActive(true);
            targetObj.SetActive(true);

            string mgName = data.mgData != null ? data.mgData.mgName : "기관총";
            int burstCount = data.mgResults != null ? data.mgResults.Length : 6;
            float spread = data.mgData != null ? data.mgData.spreadPerShot : 8f;
            Vector3 muzzle = attackerPos + (Vector3)(fireDir * 0.4f);

            // 카메라: 공격자 쪽에서 시작
            cam.transform.position = new Vector3(attackerPos.x, attackerPos.y, -10f);
            cam.orthographicSize = 4f;

            ShowNarrative($"{mgName} 사격!", new Color(1f, 0.7f, 0.3f));

            yield return new WaitForSeconds(0.2f);

            float totalDamage = 0;
            int hitCount = 0;
            int ricochetCount = 0;
            int penCount = 0;

            // ===== 발당 순차: 발사 → 비행 → 착탄(즉시 판정) =====
            for (int i = 0; i < burstCount; i++)
            {
                var shotResult = data.mgResults != null && i < data.mgResults.Length
                    ? data.mgResults[i]
                    : new ShotResult { hit = false, outcome = ShotOutcome.Miss };

                // 산포 적용한 목표점
                float spreadAngle = data.attackerHullAngle + Random.Range(-spread, spread);
                Vector3 shotTarget = targetPos + (Vector3)(Random.insideUnitCircle * 0.3f);

                // [1] 발사 — 포구 화염
                SpawnSmallFlash(muzzle);
                StartCoroutine(CameraShake(0.02f, 0.01f));

                // [2] 탄환 생성 + 비행 (빠르게, 0.08초)
                var bullet = CreateProjectile(muzzle);
                var bulletSr = bullet.GetComponent<SpriteRenderer>();
                if (bulletSr != null)
                {
                    bulletSr.color = new Color(1f, 0.9f, 0.4f);
                    bullet.transform.localScale = Vector3.one * 0.025f;
                }

                float flightTime = 0.08f;
                float ft = 0;
                while (ft < 1f)
                {
                    ft += Time.deltaTime / flightTime;
                    bullet.transform.position = Vector3.Lerp(muzzle, shotTarget, Mathf.Clamp01(ft));
                    yield return null;
                }
                Destroy(bullet);

                // [3] 착탄 — 즉시 판정 + 이펙트 + 데미지 폰트
                if (shotResult.hit)
                {
                    hitCount++;
                    totalDamage += shotResult.damageDealt;

                    switch (shotResult.outcome)
                    {
                        case ShotOutcome.Ricochet:
                            ricochetCount++;
                            SpawnSmallSpark(shotTarget);
                            DamagePopup.SpawnSmall(shotTarget, shotResult.damageDealt, false);
                            break;
                        case ShotOutcome.Hit:
                            SpawnSmallHit(shotTarget);
                            DamagePopup.SpawnSmall(shotTarget, shotResult.damageDealt, true);
                            StartCoroutine(CameraShake(0.03f, 0.02f));
                            break;
                        case ShotOutcome.Penetration:
                            penCount++;
                            SpawnSmallHit(shotTarget);
                            HitEffects.Spawn(shotTarget, ShotOutcome.Hit, fireDir);
                            DamagePopup.SpawnSmall(shotTarget, shotResult.damageDealt, true);
                            StartCoroutine(CameraShake(0.05f, 0.04f));
                            break;
                    }
                }
                else
                {
                    SpawnSmallDust(shotTarget + (Vector3)(Random.insideUnitCircle * 0.2f));
                }

                // 발당 간격 (따다다닥)
                yield return new WaitForSeconds(0.1f);
            }

            // 대상 밀림
            if (hitCount > 0)
                StartCoroutine(TankRecoil(targetObj, fireDir, Mathf.Min(0.05f * hitCount, 0.3f)));

            // ===== 결과 요약 =====
            yield return new WaitForSeconds(0.3f);

            ShowNarrative("사격 완료", new Color(1f, 0.7f, 0.3f));
            subText = $"{burstCount}발 중 {hitCount}발 명중";
            if (ricochetCount > 0) subText += $" (도탄 {ricochetCount})";
            if (penCount > 0) subText += $" (관통 {penCount})";
            subText += $"\n총 데미지: {totalDamage:F0}";

            if (totalDamage > 0)
                DamagePopup.Spawn(targetPos + Vector3.up * 0.5f, totalDamage, ShotOutcome.Hit);

            yield return new WaitForSeconds(1f);
            sequenceDone = true;
        }

        // ===== 기관총 보조 이펙트 (작고 가벼움) =====

        private void SpawnSmallFlash(Vector3 pos)
        {
            var obj = new GameObject("MGFlash");
            obj.transform.position = pos;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(1f, 0.9f, 0.4f, 0.8f);
            sr.sortingOrder = 60;
            obj.transform.localScale = Vector3.one * 0.08f;
            Destroy(obj, 0.05f);
        }

        private void SpawnSmallSpark(Vector3 pos)
        {
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject("MGSpark");
                obj.transform.position = pos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.9f, 0.4f);
                sr.sortingOrder = 55;
                obj.transform.localScale = Vector3.one * 0.02f;
                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0; rb.linearDamping = 5f;
                rb.linearVelocity = Random.insideUnitCircle * 3f;
                Destroy(obj, 0.15f);
            }
        }

        private void SpawnSmallHit(Vector3 pos)
        {
            var obj = new GameObject("MGHit");
            obj.transform.position = pos;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(1f, 0.5f, 0.2f, 0.7f);
            sr.sortingOrder = 55;
            obj.transform.localScale = Vector3.one * 0.06f;
            obj.AddComponent<FadeAndShrink>();
            Destroy(obj, 0.15f);
        }

        private void SpawnSmallDust(Vector3 pos)
        {
            var obj = new GameObject("MGDust");
            obj.transform.position = pos;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(0.5f, 0.45f, 0.35f, 0.3f);
            sr.sortingOrder = 40;
            obj.transform.localScale = Vector3.one * 0.04f;
            obj.AddComponent<FadeAndShrink>();
            Destroy(obj, 0.2f);
        }

        // ===== 보조 이펙트 =====

        /// <summary>지면 충돌 — 먼지+파편 (도탄 포탄 착지)</summary>
        private void SpawnGroundImpact(Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                var obj = new GameObject("Dust");
                obj.transform.position = pos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(0.5f, 0.45f, 0.35f, 0.5f);
                sr.sortingOrder = 40;
                obj.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = Random.insideUnitCircle * Random.Range(1f, 3f);
                obj.AddComponent<FadeAndShrink>();
                Destroy(obj, 0.6f);
            }
        }

        /// <summary>관통 출구 화염</summary>
        private void SpawnExitFlame(Vector3 pos, Vector2 dir)
        {
            for (int i = 0; i < 3; i++)
            {
                var obj = new GameObject("ExitFlame");
                obj.transform.position = pos;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.5f, 0.1f, 0.8f);
                sr.sortingOrder = 45;
                obj.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);

                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 3f;
                rb.linearVelocity = dir * Random.Range(2f, 5f) + Random.insideUnitCircle * 1f;
                obj.AddComponent<FadeAndShrink>();
                Destroy(obj, 0.4f);
            }
        }

        /// <summary>지속 연기 (피격/관통 후)</summary>
        private void SpawnLingeringSmoke(Vector3 pos)
        {
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject("Smoke");
                obj.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.1f);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(0.1f, 0.1f, 0.08f, 0.4f);
                sr.sortingOrder = 35;
                obj.transform.localScale = Vector3.one * Random.Range(0.12f, 0.25f);

                var rb = obj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDamping = 1f;
                rb.linearVelocity = Vector2.up * Random.Range(0.3f, 0.8f);
                obj.AddComponent<FadeAndShrink>();
                Destroy(obj, Random.Range(1f, 2f));
            }
        }

        // ===== 피격 밀림 =====
        private IEnumerator TankRecoil(GameObject tank, Vector2 dir, float intensity)
        {
            if (tank == null) yield break;
            Vector3 original = tank.transform.position;
            Vector3 pushed = original + (Vector3)(dir * intensity);

            float t = 0;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                tank.transform.position = Vector3.Lerp(original, pushed, t / 0.1f);
                yield return null;
            }
            t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                tank.transform.position = Vector3.Lerp(pushed, original, t / 0.15f);
                yield return null;
            }
            tank.transform.position = original;
        }

        // ===== 시각 요소 =====

        private GameObject CreateTankVisual(Vector3 pos, bool isAttacker)
        {
            var obj = new GameObject(isAttacker ? "Attacker" : "Target");
            obj.transform.position = pos;

            // 스프라이트 방향 보정 (전략맵과 동일 구조)
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

            // 포탑
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
                var tex = new Texture2D(4, 4);
                var pixels = new Color[16];
                for (int i = 0; i < 16; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                _cachedBullet = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            sr.sprite = _cachedBullet;
            sr.color = new Color(1f, 0.9f, 0.3f);
            sr.sortingOrder = 20;
            obj.transform.localScale = Vector3.one * 0.06f;
            return obj;
        }

        // ===== 카메라 =====

        private IEnumerator CameraShake(float duration, float magnitude)
        {
            Vector3 originalPos = cam.transform.position;
            float elapsed = 0;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                cam.transform.position = originalPos + new Vector3(x, y, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cam.transform.position = originalPos;
        }

        private IEnumerator ShakeObject(GameObject obj, float duration, float magnitude)
        {
            Vector3 originalPos = obj.transform.position;
            float elapsed = 0;
            while (elapsed < duration && obj != null)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                obj.transform.position = originalPos + new Vector3(x, y, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (obj != null)
                obj.transform.position = originalPos;
        }

        // ===== UI =====

        private void ShowNarrative(string text, Color color)
        {
            narrativeText = text;
            narrativeColor = color;
            showNarrative = true;
        }

        private void OnGUI()
        {
            if (!showNarrative) return;

            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = GetTex(new Color(0, 0, 0, 0.75f));

            // 메인 텍스트 (큰 글씨)
            var mainStyle = new GUIStyle(GUI.skin.label);
            mainStyle.fontSize = 26;
            mainStyle.fontStyle = FontStyle.Bold;
            mainStyle.alignment = TextAnchor.MiddleCenter;
            mainStyle.normal.textColor = narrativeColor;

            GUI.Box(new Rect(Screen.width / 2 - 180, 20, 360, 50), "", boxStyle);
            GUI.Label(new Rect(Screen.width / 2 - 180, 20, 360, 50), narrativeText, mainStyle);

            // 서브 텍스트 (설명)
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

            // 명중률
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

        private static Sprite _cachedCircle;
        private Sprite GetCircleSprite()
        {
            if (_cachedCircle != null) return _cachedCircle;
            int size = 8;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            int half = size / 2;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                    pixels[y * size + x] = dist <= half ? Color.white : Color.clear;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircle;
        }
    }
}
