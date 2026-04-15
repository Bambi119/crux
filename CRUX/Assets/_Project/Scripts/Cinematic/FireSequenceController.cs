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
                SceneManager.LoadScene(GetReturnScene());
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
                // Clear는 복귀 씬의 ApplyPendingResult에서 처리
                SceneManager.LoadScene(GetReturnScene());
            }
        }

        /// <summary>연출 종료 후 복귀할 전략 씬 — BattleStateStorage에 기록된 이름 우선</summary>
        private static string GetReturnScene()
        {
            var s = BattleStateStorage.SourceScene;
            return string.IsNullOrEmpty(s) ? "StrategyScene" : s;
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

            // ===== 주포 시퀀스 =====
            // 공격자 엄폐 연출은 폐기 — 항상 기본 사격 연출 사용
            // (엄폐 효과는 판정/UI로 표현하고, 연출은 추후 스킬 시스템에서 차별화)
            yield return PlayOpenFireSequence(attackerPos, targetPos, fireDir, attackerAngle);

            // 포탑이 향하는 방향 = 포탄이 나가는 방향 (직선 정렬)
            Vector3 turretPos = attackerTurret != null ? attackerTurret.position : attackerObj.transform.position;

            // 포탑의 Unity Z 회전에서 정확한 전방 벡터 추출
            float turretZ = attackerTurret != null
                ? attackerTurret.eulerAngles.z
                : attackerObj.transform.eulerAngles.z;
            float turretRad = turretZ * Mathf.Deg2Rad;

            // 머즐 위치: 포탑 회전 기준으로 오프셋
            Vector2 muzzleOff = data.attackerMuzzleOffset;
            Vector3 rotatedOffset = new Vector3(
                muzzleOff.x * Mathf.Cos(turretRad) - muzzleOff.y * Mathf.Sin(turretRad),
                muzzleOff.x * Mathf.Sin(turretRad) + muzzleOff.y * Mathf.Cos(turretRad),
                0);
            Vector3 muzzlePos = turretPos + rotatedOffset;

            // 사격 방향: 머즐 → 대상
            Vector2 currentFireDir = ((Vector3)targetObj.transform.position - muzzlePos).normalized;

            // ===== 사격! =====
            MuzzleFlash.Spawn(muzzlePos, currentFireDir);
            StartCoroutine(CameraShake(0.15f, 0.06f));
            ShowNarrative("사격!", new Color(1f, 0.9f, 0.3f));
            yield return new WaitForSeconds(0.15f);

            // ===== [4] 포탄 비행 =====
            Vector3 actualTargetPos = targetObj.transform.position;
            Vector3 impactPos = actualTargetPos;

            if (data.targetCoverHit)
            {
                Vector2 toMuzzle = ((Vector2)(muzzlePos - actualTargetPos)).normalized;
                impactPos = actualTargetPos + (Vector3)(toMuzzle * 0.6f);
            }

            var projectile = CreateProjectile(muzzlePos);
            // 트레이서를 진행 방향으로 회전
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
                // 기존 전차 착탄 로직 — ammo 데미지로 구경 스케일 도출
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

                // 사후 상태: 격파 / 유폭 / 화재 / 모듈 손상
                if (data.result.hit && data.result.damageDealt > 0)
                    yield return PlayPostImpactNarratives(impactPos, data.mainOutcome);
            }

            yield return new WaitForSeconds(0.6f);
            sequenceDone = true;
        }

        /// <summary>사격 결과 이후 상태 표시 시퀀스 — 격파/유폭/화재/모듈</summary>
        private IEnumerator PlayPostImpactNarratives(Vector3 impactPos, DamageOutcome outcome)
        {
            yield return new WaitForSeconds(0.3f);

            // 1. 유폭 → 별도 그래픽 + 격파
            if (outcome.ammoExploded)
            {
                ShowNarrative("▲ 탄약고 유폭!", new Color(1f, 0.9f, 0.2f));
                subText = "내부 탄약 연쇄 폭발";

                // 대형 폭발 그래픽 — 2단계
                HitEffects.SpawnExplosion(impactPos);
                StartCoroutine(CameraShake(0.5f, 0.22f));
                SpawnAmmoCookoffEffect(impactPos);

                yield return new WaitForSeconds(0.6f);

                HitEffects.SpawnExplosion(impactPos + Vector3.up * 0.2f);
                StartCoroutine(CameraShake(0.4f, 0.18f));
                yield return new WaitForSeconds(0.5f);

                ShowNarrative("◆ 격파!", new Color(1f, 0.2f, 0.15f));
                subText = $"{data.targetName} 대파";
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            // 2. 격파 (유폭 제외)
            if (outcome.killed)
            {
                ShowNarrative("◆ 격파!", new Color(1f, 0.2f, 0.15f));
                subText = $"{data.targetName} 전투 불능";
                HitEffects.SpawnExplosion(impactPos);
                StartCoroutine(CameraShake(0.35f, 0.15f));
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            // 3. 격파가 아닌 경우: 상태이상
            if (outcome.fireStarted)
            {
                ShowNarrative("🔥 화재 발생!", new Color(1f, 0.5f, 0.15f));
                subText = "매 턴 HP 감소 / AP 비용 +1";
                SpawnFireIndicator(impactPos);
                yield return new WaitForSeconds(0.7f);
            }

            // 4. 모듈 파괴 판정
            if (outcome.moduleHit && outcome.stateChanged)
            {
                string moduleName = ModuleManager.GetModuleName(outcome.damagedModule);
                string stateName = ModuleManager.GetStateName(outcome.newState);

                Color col = outcome.newState switch
                {
                    ModuleState.Damaged => new Color(1f, 0.9f, 0.3f),
                    ModuleState.Broken => new Color(1f, 0.5f, 0.2f),
                    ModuleState.Destroyed => new Color(1f, 0.25f, 0.2f),
                    _ => Color.white
                };
                ShowNarrative($"⚙ {moduleName} {stateName}!", col);
                subText = GetModulePenaltyDescription(outcome.damagedModule, outcome.newState);
                yield return new WaitForSeconds(0.7f);
            }
        }

        /// <summary>모듈 상태별 효과 설명</summary>
        private static string GetModulePenaltyDescription(ModuleType type, ModuleState state)
        {
            if (state == ModuleState.Destroyed)
            {
                return type switch
                {
                    ModuleType.Engine => "이동 불가",
                    ModuleType.Barrel => "주포 사용 불가",
                    ModuleType.MachineGun => "기총 사용 불가",
                    ModuleType.AmmoRack => "장전 불가",
                    ModuleType.Loader => "장전기 작동 불능",
                    ModuleType.TurretRing => "포탑 회전 불가",
                    ModuleType.CaterpillarLeft => "좌측 궤도 완파",
                    ModuleType.CaterpillarRight => "우측 궤도 완파",
                    _ => "완파"
                };
            }
            if (state == ModuleState.Broken)
            {
                return type switch
                {
                    ModuleType.Engine => "이동 불가 (수리 시 복구)",
                    ModuleType.Barrel => "주포 사용 불가",
                    ModuleType.AmmoRack => "사격 AP +2",
                    ModuleType.Loader => "사격 AP +2",
                    ModuleType.TurretRing => "사격 AP +2",
                    _ => "기능 정지"
                };
            }
            return type switch
            {
                ModuleType.Engine => "이동 AP +1",
                ModuleType.Barrel => "명중률 -15%",
                ModuleType.MachineGun => "버스트 -2, 명중 -10%",
                ModuleType.Loader or ModuleType.AmmoRack or ModuleType.TurretRing => "사격 AP +1",
                ModuleType.CaterpillarLeft or ModuleType.CaterpillarRight => "이동 AP +1",
                _ => "성능 저하"
            };
        }

        /// <summary>탄약고 유폭 시 추가 대형 폭발 그래픽 (수직 파편 분출)</summary>
        private void SpawnAmmoCookoffEffect(Vector3 pos)
        {
            // 수직 화염 기둥
            for (int i = 0; i < 20; i++)
            {
                var p = new GameObject("Cookoff");
                p.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.15f);

                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                float t = Random.value;
                sr.color = t < 0.4f ? new Color(1f, 0.95f, 0.4f, 1f)
                        : t < 0.75f ? new Color(1f, 0.55f, 0.1f, 0.95f)
                                    : new Color(0.9f, 0.2f, 0.05f, 0.9f);
                sr.sortingOrder = 65;
                p.transform.localScale = Vector3.one * Random.Range(0.15f, 0.3f);

                var rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = -0.5f; // 위로 솟구침
                rb.linearDamping = 0.8f;
                rb.linearVelocity = new Vector2(
                    Random.Range(-1.5f, 1.5f),
                    Random.Range(3f, 6f));

                p.AddComponent<Combat.FadeAndShrink>();
                Destroy(p, Random.Range(0.8f, 1.3f));
            }

            // 외곽 쇼크웨이브 링
            var ring = new GameObject("CookoffRing");
            ring.transform.position = pos;
            var rs = ring.AddComponent<SpriteRenderer>();
            rs.sprite = GetCircleSprite();
            rs.color = new Color(1f, 0.9f, 0.5f, 0.7f);
            rs.sortingOrder = 55;
            ring.transform.localScale = Vector3.one * 0.3f;
            StartCoroutine(ExpandRing(ring.transform, rs, 2.5f, 0.5f));
        }

        private IEnumerator ExpandRing(Transform t, SpriteRenderer sr, float maxScale, float duration)
        {
            float el = 0f;
            Color c0 = sr.color;
            while (el < duration)
            {
                el += Time.deltaTime;
                float f = el / duration;
                t.localScale = Vector3.one * Mathf.Lerp(0.3f, maxScale, f);
                sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - f));
                yield return null;
            }
            Destroy(t.gameObject);
        }

        /// <summary>화재 시작 지시자 — 대상 위 작은 불꽃</summary>
        private void SpawnFireIndicator(Vector3 pos)
        {
            for (int i = 0; i < 6; i++)
            {
                var p = new GameObject("FireInd");
                p.transform.position = pos + Vector3.up * 0.15f + (Vector3)(Random.insideUnitCircle * 0.2f);
                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = GetCircleSprite();
                sr.color = new Color(1f, 0.6f, 0.15f, 0.85f);
                sr.sortingOrder = 50;
                p.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                var rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = -0.3f;
                rb.linearDamping = 1.5f;
                rb.linearVelocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.5f));

                p.AddComponent<Combat.FadeAndShrink>();
                Destroy(p, 0.8f);
            }
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

            // 적: 실제 전술맵 거리에 맞춰 배치 (근접전은 가깝게, 원거리는 멀게)
            // 기존 5u 고정은 원거리 사격에서도 적이 전차 바로 앞에 있는 것처럼 보이는 문제 있음
            float gridDist = Vector2.Distance(
                (Vector2)data.attackerWorldPos, (Vector2)data.targetWorldPos);
            // 최소 6u — 아주 가까운 거리도 연출이 너무 꽉 차지 않도록
            // 최대 13u — 아주 먼 거리도 카메라 한 화면에 공격자/대상 모두 프레이밍 가능
            float cinematicDist = Mathf.Clamp(gridDist, 6f, 13f);
            Vector3 enemyPos = (Vector3)(fireDir * cinematicDist);
            float enemyAngle = data.targetHullAngle;

            attackerObj.transform.position = tankStart;
            attackerObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            targetObj.transform.position = enemyPos;
            targetObj.transform.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(enemyAngle));

            if (attackerTurret != null)
                attackerTurret.rotation = Quaternion.Euler(0, 0, AngleUtil.ToUnity(myAngle));

            // 대상 엄폐물 — hex 방위 규칙 그대로, 월드 고정 렌더
            // targetCoverFacets의 모든 보호면을 "확대 hex"의 각 edge 중점에 배치
            // 공격 방향에 따라 회전/위치 변하지 않음 (고정)
            if (data.targetInCover || data.targetCoverHit)
            {
                float cellSize = GameConstants.CellSize;
                // 연출 전용 — 실제 셀 크기보다 1.8배 확대해 벽과 전차 사이 여유 확보
                // (전차 스프라이트가 약 1u 폭이라 기본 셀(1u)에 붙이면 벽이 전차와 겹쳐 보임)
                const float visInflate = 1.8f;
                float edgeLength = cellSize * visInflate;            // 확대된 변 길이
                float edgeDist = cellSize * visInflate * Mathf.Sqrt(3f) / 2f; // 확대된 apothem

                // 연출 벽 스프라이트의 월드 폭 (ppu 32 기준)
                // Small 40px=1.25u, Medium 48px=1.5u, Large 56px=1.75u
                float spriteWorldWidth = data.targetCoverSize switch
                {
                    CoverSize.Small => 40f / 32f,
                    CoverSize.Medium => 48f / 32f,
                    CoverSize.Large => 56f / 32f,
                    _ => 48f / 32f
                };
                // 벽 길이는 확대된 변의 80% — 모서리(vertex)에 10% 여백씩 생기게 해 인접 벽 간 시각 분리
                float xScale = (edgeLength * 0.8f) / spriteWorldWidth;
                float yScale = data.targetCoverSize switch
                {
                    CoverSize.Small => 0.9f,
                    CoverSize.Medium => 1.1f,
                    CoverSize.Large => 1.35f,
                    _ => 1.1f
                };

                foreach (var facet in data.targetCoverFacets.Enumerate())
                {
                    Vector2 edgeNormal = HexCoord.DirToWorld(facet);
                    Vector3 wallPos = enemyPos + new Vector3(edgeNormal.x, edgeNormal.y, 0f) * edgeDist;
                    // 벽 스프라이트의 x축 = edge 접선 (edgeNormal을 90° 회전)
                    Vector2 tangent = new Vector2(-edgeNormal.y, edgeNormal.x);
                    float wallRotZ = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

                    var wallObj = new GameObject($"TargetCoverWall_{facet}");
                    wallObj.transform.position = wallPos;
                    wallObj.transform.rotation = Quaternion.Euler(0, 0, wallRotZ);
                    wallObj.transform.localScale = new Vector3(xScale, yScale, 1f);
                    var wsr = wallObj.AddComponent<SpriteRenderer>();
                    wsr.sprite = TankSpriteGenerator.CreateCinematicCover(data.targetCoverSize);
                    // 전차 hull(5)/turret(6)보다 낮게 — 전차가 벽 앞에 명확히 보이도록
                    wsr.sortingOrder = 3;
                }
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

            // 카메라 줌아웃 — 거리에 비례해 시야 확대 (근접 3.5 ~ 원거리 7)
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
            // 즉시 판정 텍스트
            ShowNarrative($"◆ 도탄!", new Color(1f, 0.8f, 0.3f));
            subText = $"{data.result.hitZone} 장갑 — 관통 실패";

            // 즉시 튕김 포탄 생성 — 진행 방향 기준 랜덤 반사
            Vector2 normal = (Vector2.Perpendicular(fireDir) * (Random.value > 0.5f ? 1f : -1f)
                             + fireDir * -0.3f + Random.insideUnitCircle * 0.4f).normalized;
            Vector2 ricochetDir = Vector2.Reflect(fireDir, normal).normalized;

            var ricochetBullet = CreateProjectile(targetPos);
            // 트레이서를 도탄 방향으로 회전
            float ricochetAngle = Mathf.Atan2(ricochetDir.y, ricochetDir.x) * Mathf.Rad2Deg;
            ricochetBullet.transform.rotation = Quaternion.Euler(0, 0, ricochetAngle);
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
            // 관통 탄환을 진행 방향으로 회전
            float exitAngle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;
            exitBullet.transform.rotation = Quaternion.Euler(0, 0, exitAngle);
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

            yield return new WaitForSeconds(0.5f);
        }

        // ===== 기관총 버스트 연출 (일제사격 → 탄줄 비행+즉시 판정 → 결과) =====

        // MG 볼리 집계 — 스트림 코루틴 간 공유
        private int mgHits;
        private int mgRicochets;
        private int mgPens;
        private float mgTotalDamage;

        private IEnumerator PlayMGSequence(Vector3 attackerPosUnused, Vector3 targetPosUnused, Vector2 fireDir)
        {
            // 주포와 동일한 프레이밍 — 전진·정지·포탑 회전·카메라 중점 줌아웃
            yield return PlayOpenFireSequence(attackerPosUnused, targetPosUnused, fireDir, data.attackerHullAngle);

            // 머즐 위치 — 주포와 동일한 포탑 회전 반영 오프셋 계산
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

            // 포탑 정렬 이후 실제 사격 방향 재계산
            Vector3 targetPos = targetObj.transform.position;
            Vector2 currentFireDir = ((Vector3)targetPos - muzzlePos).normalized;
            Vector2 perpDir = new Vector2(-currentFireDir.y, currentFireDir.x);

            string mgName = data.mgData != null ? data.mgData.mgName : "기관총";
            int burstCount = data.mgResults != null ? data.mgResults.Length : 6;

            ShowNarrative($"{mgName} 사격!", new Color(1f, 0.7f, 0.3f));
            yield return new WaitForSeconds(0.15f);

            // 볼리 집계 리셋
            mgHits = 0;
            mgRicochets = 0;
            mgPens = 0;
            mgTotalDamage = 0f;

            const float spawnInterval = 0.06f; // 발당 간격 — 따다다닥
            const float flightTime = 0.2f;     // 비행 시간 — 약 3~4발 동시 재공
            const float lateralSpread = 0.08f; // 머즐 측면 분산 폭

            // ===== 스트림 볼리: 각 탄환을 비동기 코루틴으로 흐르게 =====
            for (int i = 0; i < burstCount; i++)
            {
                var shotResult = data.mgResults != null && i < data.mgResults.Length
                    ? data.mgResults[i]
                    : new ShotResult { hit = false, outcome = ShotOutcome.Miss };

                // 머즐 측면 미세 분산 — 탄환 행렬감
                float lateral = Random.Range(-lateralSpread, lateralSpread);
                Vector3 bulletStart = muzzlePos + (Vector3)(perpDir * lateral);

                // 착탄점 흩어짐
                Vector3 shotTarget = targetPos + (Vector3)(Random.insideUnitCircle * 0.3f);

                SpawnSmallFlash(muzzlePos);
                StartCoroutine(CameraShake(0.02f, 0.01f));
                StartCoroutine(AnimateMGBullet(bulletStart, shotTarget, flightTime, shotResult, currentFireDir));

                yield return new WaitForSeconds(spawnInterval);
            }

            // 마지막 탄환 착탄까지 대기
            yield return new WaitForSeconds(flightTime + 0.1f);

            // 대상 밀림
            if (mgHits > 0)
                StartCoroutine(TankRecoil(targetObj, fireDir, Mathf.Min(0.05f * mgHits, 0.3f)));

            // ===== 결과 요약 =====
            yield return new WaitForSeconds(0.3f);

            ShowNarrative("사격 완료", new Color(1f, 0.7f, 0.3f));
            subText = $"{burstCount}발 중 {mgHits}발 명중";
            if (mgRicochets > 0) subText += $" (도탄 {mgRicochets})";
            if (mgPens > 0) subText += $" (관통 {mgPens})";
            subText += $"\n총 데미지: {mgTotalDamage:F0}";

            if (mgTotalDamage > 0)
                DamagePopup.Spawn(targetPos + Vector3.up * 0.5f, mgTotalDamage, ShotOutcome.Hit);

            yield return new WaitForSeconds(0.5f);

            // 사후 상태: 격파 / 유폭 / 화재 / 모듈
            if (mgTotalDamage > 0)
                yield return PlayPostImpactNarratives(targetPos, data.mgAggregateOutcome);

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
                        SpawnSmallSpark(end);
                        DamagePopup.SpawnSmall(end, shotResult.damageDealt, false);
                        break;
                    case ShotOutcome.Hit:
                        SpawnSmallHit(end);
                        DamagePopup.SpawnSmall(end, shotResult.damageDealt, true);
                        StartCoroutine(CameraShake(0.03f, 0.02f));
                        break;
                    case ShotOutcome.Penetration:
                        mgPens++;
                        SpawnSmallHit(end);
                        HitEffects.Spawn(end, ShotOutcome.Hit, fireDir, 0.4f);
                        DamagePopup.SpawnSmall(end, shotResult.damageDealt, true);
                        StartCoroutine(CameraShake(0.05f, 0.04f));
                        break;
                }
            }
            else
            {
                SpawnSmallDust(end + (Vector3)(Random.insideUnitCircle * 0.2f));
            }
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
                // 트레이서 형태: 가로로 긴 선 (16x3)
                int w = 16, h = 3;
                var tex = new Texture2D(w, h);
                tex.filterMode = FilterMode.Point;
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // 선두(밝음) → 꼬리(어두움) 그라데이션
                        float t = (float)x / w;
                        float alpha = t; // 선두가 밝고 꼬리가 투명
                        Color c = Color.Lerp(
                            new Color(1f, 0.6f, 0.1f, 0.3f),  // 꼬리: 주황 반투명
                            new Color(1f, 1f, 0.8f, 1f),       // 선두: 백열
                            t);
                        // 중앙 라인이 밝고 가장자리 어두움
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
            obj.transform.localScale = new Vector3(0.5f, 0.08f, 1f); // 길고 얇게
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
