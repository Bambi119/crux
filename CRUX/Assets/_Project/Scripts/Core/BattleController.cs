using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;

namespace Crux.Core
{
    /// <summary>턴제 전투 컨트롤러 — 전략맵 씬 메인</summary>
    public class BattleController : MonoBehaviour
    {
        [Header("데이터 (Inspector에서 연결)")]
        public TankDataSO playerTankData;
        public TankDataSO lightEnemyData;
        public TankDataSO heavyEnemyData;
        public AmmoDataSO playerAmmo;
        public AmmoDataSO enemyAmmo;

        [Header("스프라이트 (Inspector에서 연결)")]
        public Sprite playerHullSprite;
        public Sprite playerTurretSprite;
        public Sprite lightEnemySprite;
        public Sprite heavyEnemySprite;
        public Sprite coverSprite;
        public Sprite bioCoreSprite;
        public Sprite defenseTurretSprite;

        [Header("기관총 데이터")]
        public Data.MachineGunDataSO coaxialMGData;
        public Data.MachineGunDataSO mountedMGData;

        // 시스템
        private GridManager grid;
        private GridVisualizer visualizer;
        private GridMapSetup mapSetup;

        // 유닛
        private GridTankUnit playerUnit;
        private List<GridTankUnit> enemyUnits;

        // 상태
        private TurnPhase currentPhase = TurnPhase.PlayerTurn;
        private GridTankUnit selectedUnit;
        private GridTankUnit targetUnit;
        private int turnCount = 1;
        private bool waitingForCinematic;
        private int currentEnemyIndex; // 적 턴 중 행동 중인 적 인덱스

        // UI 상태
        private enum InputMode { Select, Move, MoveDirectionSelect, Fire, WeaponSelect }
        private InputMode inputMode = InputMode.Select;
        private WeaponType selectedWeapon = WeaponType.MainGun;
        private GridTankUnit pendingTarget; // 무기 선택 대기 중인 대상
        private Vector2Int pendingMoveTarget; // 이동 목적지 (방향 선택 대기 중)
        private float pendingFacingAngle;     // 선택 중인 방향
        private int pendingMoveCost;          // 이동 AP 비용

        // 카메라
        private UnityEngine.Camera mainCam;
        private float camTargetSize;
        private Vector3 camTargetPos;
        private const float camZoomSpeed = 5f;
        private const float camMinSize = 3f;
        private const float camMaxSize = 8f;
        private const float camPanSpeed = 8f;
        private const int edgePanMargin = 15; // 가장자리 스크롤 픽셀 폭

        // UI 스케일
        private float uiScale = 1f;

        // UI 텍스처 캐시
        private Dictionary<Color, Texture2D> texCache = new();

        public TurnPhase CurrentPhase => currentPhase;
        public int TurnCount => turnCount;

        private void Start()
        {
            if (FireActionContext.HasPendingAction)
            {
                // 연출 씬에서 복귀 — 상태 복원 + 데미지 적용
                ApplyPendingResult();
            }
            else
            {
                // 최초 시작
                InitializeBattle();
            }
        }

        private void InitializeBattle()
        {
            mainCam = UnityEngine.Camera.main;

            // 그리드
            var gridObj = new GameObject("Grid");
            grid = gridObj.AddComponent<GridManager>();

            // 시각화
            var visObj = new GameObject("GridVisualizer");
            visualizer = visObj.AddComponent<GridVisualizer>();
            visualizer.Initialize(grid);

            // 맵 배치
            var setupObj = new GameObject("MapSetup");
            mapSetup = setupObj.AddComponent<GridMapSetup>();
            mapSetup.playerTankData = playerTankData;
            mapSetup.lightEnemyData = lightEnemyData;
            mapSetup.heavyEnemyData = heavyEnemyData;
            mapSetup.playerAmmo = playerAmmo;
            mapSetup.enemyAmmo = enemyAmmo;
            mapSetup.playerHullSprite = playerHullSprite;
            mapSetup.playerTurretSprite = playerTurretSprite;
            mapSetup.lightEnemySprite = lightEnemySprite;
            mapSetup.heavyEnemySprite = heavyEnemySprite;
            mapSetup.coverSprite = coverSprite;
            mapSetup.bioCoreSprite = bioCoreSprite;
            mapSetup.defenseTurretSprite = defenseTurretSprite;
            mapSetup.Setup(grid);

            playerUnit = mapSetup.PlayerUnit;
            enemyUnits = mapSetup.EnemyUnits;

            // 카메라 — 맵 전체가 보이도록 자동 조정
            if (mainCam != null)
            {
                mainCam.orthographic = true;
                float mapH = GameConstants.GridHeight * GameConstants.CellSize;
                float mapW = GameConstants.GridWidth * GameConstants.CellSize;
                float aspect = mainCam.aspect;
                // 세로/가로 중 넉넉한 쪽 기준
                float sizeByH = mapH * 0.55f;
                float sizeByW = (mapW * 0.55f) / aspect;
                camTargetSize = Mathf.Max(sizeByH, sizeByW);
                camTargetSize = Mathf.Clamp(camTargetSize, camMinSize, camMaxSize);
                mainCam.orthographicSize = camTargetSize;

                float cx = (GameConstants.GridWidth - 1) * GameConstants.CellSize * 0.5f;
                float cy = (GameConstants.GridHeight - 1) * GameConstants.CellSize * 0.5f;
                camTargetPos = new Vector3(cx, cy, -10f);
                mainCam.transform.position = camTargetPos;
            }

            // UI 스케일 — 기준 해상도 1080p
            uiScale = Screen.height / 1080f;

            // 플레이어 턴 시작
            StartPlayerTurn();
        }

        private InputMode lastArcMode = InputMode.Select;
        private Vector2Int lastArcPos;

        private void Update()
        {
            if (currentPhase == TurnPhase.PlayerTurn)
            {
                HandlePlayerInput();
                UpdateCoverArcDisplay();
            }
            HandleCamera();
        }

        private void HandleCamera()
        {
            if (mainCam == null) return;

            // 마우스 휠 줌
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                camTargetSize -= scroll * 0.5f;
                camTargetSize = Mathf.Clamp(camTargetSize, camMinSize, camMaxSize);
            }

            // 가장자리 스크롤 (마우스가 화면 가장자리에 있을 때)
            Vector3 panDir = Vector3.zero;
            Vector2 mp = Input.mousePosition;
            if (mp.x < edgePanMargin) panDir.x = -1f;
            else if (mp.x > Screen.width - edgePanMargin) panDir.x = 1f;
            if (mp.y < edgePanMargin) panDir.y = -1f;
            else if (mp.y > Screen.height - edgePanMargin) panDir.y = 1f;

            if (panDir != Vector3.zero)
            {
                camTargetPos += panDir * camPanSpeed * Time.deltaTime;
                // 맵 범위 제한
                float halfH = camTargetSize;
                float halfW = camTargetSize * mainCam.aspect;
                camTargetPos.x = Mathf.Clamp(camTargetPos.x, -halfW * 0.3f,
                    GameConstants.GridWidth * GameConstants.CellSize + halfW * 0.3f);
                camTargetPos.y = Mathf.Clamp(camTargetPos.y, -halfH * 0.3f,
                    GameConstants.GridHeight * GameConstants.CellSize + halfH * 0.3f);
                camTargetPos.z = -10f;
            }

            // 부드러운 보간
            mainCam.orthographicSize = Mathf.Lerp(mainCam.orthographicSize, camTargetSize, camZoomSpeed * Time.deltaTime);
            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, camTargetPos, camZoomSpeed * Time.deltaTime);

            // UI 스케일 갱신
            uiScale = Mathf.Max(0.5f, Screen.height / 1080f);
        }

        /// <summary>플레이어 유닛의 엄폐 커버 범위 표시 (상태 변경 시만 갱신)</summary>
        private void UpdateCoverArcDisplay()
        {
            if (selectedUnit == null || selectedUnit.IsDestroyed)
            {
                if (lastArcMode != InputMode.Select)
                {
                    visualizer.ClearCoverArcs();
                    lastArcMode = InputMode.Select;
                }
                return;
            }

            // 위치나 모드가 바뀌었을 때만 갱신
            if (lastArcPos == selectedUnit.GridPosition && lastArcMode == inputMode) return;
            lastArcPos = selectedUnit.GridPosition;
            lastArcMode = inputMode;

            visualizer.ClearCoverArcs();

            // Select 모드에서 아군 엄폐 표시
            if (inputMode == InputMode.Select || inputMode == InputMode.Move)
            {
                var cell = grid.GetCell(selectedUnit.GridPosition);
                if (cell != null && cell.HasCover && cell.Cover != null
                    && !cell.Cover.IsDestroyed)
                {
                    visualizer.ShowCoverArc(
                        selectedUnit.GridPosition,
                        cell.CoverDirection,
                        cell.Cover.CoverArc,
                        new Color(0.2f, 0.8f, 0.4f, 0.5f)); // 초록
                }
            }
        }

        // ===== 턴 관리 =====

        private void StartPlayerTurn()
        {
            currentPhase = TurnPhase.PlayerTurn;
            inputMode = InputMode.Select;
            selectedUnit = null;
            targetUnit = null;

            if (playerUnit != null && !playerUnit.IsDestroyed)
            {
                playerUnit.OnTurnStart();
                selectedUnit = playerUnit;
            }

            turnCount++;
            Debug.Log($"[CRUX] === 플레이어 턴 {turnCount} ===");
        }

        private void StartEnemyTurn()
        {
            currentPhase = TurnPhase.EnemyTurn;
            visualizer.ClearHighlights();
            Debug.Log("[CRUX] === 적 턴 ===");
            StartCoroutine(ProcessEnemyTurn());
        }

        private IEnumerator ProcessEnemyTurn(int startIndex = 0)
        {
            for (int i = startIndex; i < enemyUnits.Count; i++)
            {
                var enemy = enemyUnits[i];
                if (enemy == null || enemy.IsDestroyed) continue;

                // 복귀 시에는 OnTurnStart 중복 호출 방지 (이미 저장된 AP 사용)
                if (startIndex == 0)
                    enemy.OnTurnStart();

                if (playerUnit == null || playerUnit.IsDestroyed) continue;

                // 항상 플레이어를 향하도록
                enemy.FaceToward(playerUnit.GridPosition);

                // 사거리 확인
                int dist = grid.GetDistance(enemy.GridPosition, playerUnit.GridPosition);

                if (dist <= GameConstants.MaxFireRange && enemy.CanFire())
                {
                    // 다음 적 인덱스 기록 후 사격 → 연출 씬
                    currentEnemyIndex = i + 1;
                    ExecuteFire(enemy, playerUnit);
                    yield break; // 씬 전환됨
                }
                else if (enemy.CanMove())
                {
                    // 플레이어 방향으로 이동
                    var reachable = grid.GetReachableCells(enemy.GridPosition, enemy.CurrentAP);
                    Vector2Int bestPos = enemy.GridPosition;
                    float bestDist = dist;

                    foreach (var pos in reachable)
                    {
                        float d = grid.GetDistance(pos, playerUnit.GridPosition);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestPos = pos;
                        }
                    }

                    if (bestPos != enemy.GridPosition)
                    {
                        enemy.MoveTo(bestPos);
                        // 이동 완료 대기
                        while (enemy.IsMoving)
                            yield return null;
                    }

                    // 이동 후 사격 가능하면 사격
                    dist = grid.GetDistance(enemy.GridPosition, playerUnit.GridPosition);
                    if (dist <= GameConstants.MaxFireRange && enemy.CanFire())
                    {
                        currentEnemyIndex = i + 1;
                        ExecuteFire(enemy, playerUnit);
                        yield break;
                    }
                }

                yield return new WaitForSeconds(0.3f);
            }

            // 모든 적 행동 완료 → 플레이어 턴
            yield return new WaitForSeconds(0.5f);
            StartPlayerTurn();
        }

        // ===== 입력 처리 =====

        private void HandlePlayerInput()
        {
            if (selectedUnit == null || selectedUnit.IsDestroyed) return;
            if (selectedUnit.IsMoving) return; // 이동 애니메이션 중 입력 차단

            // ===== 취소 (Tab / ESC) — 모든 모드에서 최우선 =====
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
            {
                inputMode = InputMode.Select;
                visualizer.ClearHighlights();
                targetUnit = null;
                pendingTarget = null;
                return;
            }

            // ===== 무기 선택 모드 =====
            if (inputMode == InputMode.WeaponSelect && pendingTarget != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetMouseButtonDown(0))
                {
                    selectedWeapon = WeaponType.MainGun;
                    ExecuteFire(selectedUnit, pendingTarget);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Alpha2) && coaxialMGData != null)
                {
                    selectedWeapon = WeaponType.CoaxialMG;
                    ExecuteMGFire(selectedUnit, pendingTarget, coaxialMGData);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Alpha3) && mountedMGData != null)
                {
                    selectedWeapon = WeaponType.MountedMG;
                    ExecuteMGFire(selectedUnit, pendingTarget, mountedMGData);
                    return;
                }
                return; // 무기 선택 중 다른 입력 차단
            }

            // ===== 방향 선택 모드 =====
            if (inputMode == InputMode.MoveDirectionSelect)
            {
                // WASD / 화살표: 방향 선택 (나침반: 0=북)
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                    pendingFacingAngle = 0f;
                if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    pendingFacingAngle = 90f;
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                    pendingFacingAngle = 180f;
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                    pendingFacingAngle = 270f;

                // 대각선 (WASD 조합)
                if (Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.D)
                    || Input.GetKey(KeyCode.UpArrow) && Input.GetKeyDown(KeyCode.RightArrow))
                    pendingFacingAngle = 45f;
                if (Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.A)
                    || Input.GetKey(KeyCode.UpArrow) && Input.GetKeyDown(KeyCode.LeftArrow))
                    pendingFacingAngle = 315f;
                if (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.D)
                    || Input.GetKey(KeyCode.DownArrow) && Input.GetKeyDown(KeyCode.RightArrow))
                    pendingFacingAngle = 135f;
                if (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.A)
                    || Input.GetKey(KeyCode.DownArrow) && Input.GetKeyDown(KeyCode.LeftArrow))
                    pendingFacingAngle = 225f;

                // Space / Enter: 방향 확정
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    selectedUnit.MoveToWithFacing(pendingMoveTarget, pendingFacingAngle);
                    visualizer.ClearHighlights();
                    inputMode = InputMode.Select;
                    return;
                }

                // 좌클릭: 클릭 방향으로 즉시 확정
                if (Input.GetMouseButtonDown(0))
                {
                    pendingFacingAngle = GetSnappedDirectionFromMouse(pendingMoveTarget);
                    selectedUnit.MoveToWithFacing(pendingMoveTarget, pendingFacingAngle);
                    visualizer.ClearHighlights();
                    inputMode = InputMode.Select;
                    return;
                }
                return; // 방향 선택 중 다른 입력 차단
            }

            // ===== Select 모드: Q/M 이동, E/F 사격 =====
            if ((Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.M)) && selectedUnit.CanMove())
            {
                inputMode = InputMode.Move;
                var reachable = grid.GetReachableCells(selectedUnit.GridPosition,
                                                       selectedUnit.CurrentAP);
                visualizer.ShowMoveRange(reachable);
            }

            if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)) && selectedUnit.CanFire())
            {
                inputMode = InputMode.Fire;
                visualizer.ShowFireRange(selectedUnit.GridPosition, GameConstants.MaxFireRange);
            }

            // Space: 턴 종료 (Select 모드에서만)
            if (Input.GetKeyDown(KeyCode.Space) && inputMode == InputMode.Select)
            {
                visualizer.ClearHighlights();
                StartEnemyTurn();
            }

            // 좌클릭: 이동 목적지 / 사격 대상
            if (Input.GetMouseButtonDown(0))
            {
                var worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
                var clickedPos = grid.WorldToGrid(worldPos);

                if (!grid.IsInBounds(clickedPos)) return;

                switch (inputMode)
                {
                    case InputMode.Move:
                        TryMoveToCell(clickedPos);
                        break;
                    case InputMode.Fire:
                        TrySelectTarget(clickedPos);
                        break;
                }
            }
        }

        /// <summary>마우스 클릭 위치에서 셀 기준 8방향 스냅 각도 계산</summary>
        private float GetSnappedDirectionFromMouse(Vector2Int targetCell)
        {
            var clickWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            var cellWorld = grid.GridToWorld(targetCell);
            var diff = new Vector2(clickWorld.x - cellWorld.x, clickWorld.y - cellWorld.y);

            if (diff.sqrMagnitude < 0.01f)
                return pendingFacingAngle; // 셀 중심 클릭 시 현재 방향 유지

            // atan2 → 나침반 각도 (0=북, 90=동)
            float rad = Mathf.Atan2(diff.x, diff.y); // x,y 순서 → 북=0
            float deg = rad * Mathf.Rad2Deg;
            if (deg < 0) deg += 360f;

            // 45도 단위 스냅
            float snapped = Mathf.Round(deg / 45f) * 45f;
            if (snapped >= 360f) snapped = 0f;
            return snapped;
        }

        private void TryMoveToCell(Vector2Int pos)
        {
            // 경로 비용 계산
            var path = grid.FindPath(selectedUnit.GridPosition, pos);
            if (path == null || path.Count <= 1) return;

            int cost = (path.Count - 1) * selectedUnit.GetMoveCostPerCell();
            if (cost > selectedUnit.CurrentAP) return;

            // 방향 선택 단계로 전환
            pendingMoveTarget = pos;
            pendingMoveCost = cost;
            pendingFacingAngle = selectedUnit.HullAngle; // 기본: 현재 방향 유지
            inputMode = InputMode.MoveDirectionSelect;
            visualizer.ClearHighlights();
            visualizer.HighlightCell(pos, Color.cyan);
        }

        private void TrySelectTarget(Vector2Int pos)
        {
            var cell = grid.GetCell(pos);
            if (cell == null || cell.Occupant == null) return;

            var target = cell.Occupant.GetComponent<GridTankUnit>();
            if (target == null || target.side == PlayerSide.Player) return;

            targetUnit = target;
            pendingTarget = target;
            inputMode = InputMode.WeaponSelect;
            visualizer.ClearHighlights();
        }

        // ===== 사격 =====

        private void ExecuteFire(GridTankUnit attacker, GridTankUnit target)
        {
            attacker.ConsumeFireAP();

            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float hitChance = CalculateHitChanceWithCover(attacker, target);

            bool hit = Random.value <= hitChance;

            ShotResult result = new ShotResult { hit = false, outcome = ShotOutcome.Miss, hitChance = hitChance };
            bool hitCover = false;
            float coverDmgDealt = 0f;
            string hitCoverName = "";

            // 대상 엄폐 여부 — 방향 기반 판정
            var targetCellForCover = grid.GetCell(target.GridPosition);
            float attackAngle = AngleUtil.FromDir(
                new Vector2(target.GridPosition.x - attacker.GridPosition.x,
                            target.GridPosition.y - attacker.GridPosition.y).normalized);

            bool targetInCover = false;
            string targetCoverNameForVisual = "";

            if (targetCellForCover != null && targetCellForCover.HasCover
                && targetCellForCover.Cover != null
                && !targetCellForCover.Cover.IsDestroyed)
            {
                // 방향 판정: 공격 방향이 커버 범위 내인지
                float coverDir = targetCellForCover.CoverDirection;
                targetInCover = targetCellForCover.Cover.IsCovered(coverDir, attackAngle);
                if (targetInCover)
                    targetCoverNameForVisual = targetCellForCover.Cover.coverName;
            }

            if (!hit)
            {
                // 빗나감 — 기본값 유지
            }
            else
            {
                // ===== 엄폐 판정 — 방향 기반: 커버 범위 내일 때만 엄폐물이 막을 수 있음 =====
                var targetCell = grid.GetCell(target.GridPosition);

                if (targetInCover && targetCell != null && targetCell.Cover != null)
                {
                    float coverRate = targetCell.Cover.CoverRate;
                    if (Random.value < coverRate)
                    {
                        // 엄폐물이 피격됨!
                        hitCover = true;
                        float dmg = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                        coverDmgDealt = dmg;
                        hitCoverName = targetCell.Cover.coverName;

                        targetCell.Cover.TakeDamage(dmg);

                        result = new ShotResult
                        {
                            hit = true,
                            outcome = ShotOutcome.Hit,
                            hitZone = HitZone.Front,
                            effectiveArmor = 0,
                            damageDealt = 0, // 전차에 데미지 없음
                            hitChance = hitChance
                        };

                        float coverHP = targetCell.Cover.CurrentHP;
                        float coverMax = targetCell.Cover.maxHP;
                        Debug.Log($"[CRUX] 엄폐물 피격! {hitCoverName} ({targetCell.Cover.size}) HP: {coverHP:F0}/{coverMax:F0} 엄폐율: {targetCell.Cover.CoverRate:P0} 커버범위: {targetCell.Cover.CoverArc:F0}°");
                    }
                }

                if (!hitCover)
                {
                // ===== 전차 직접 피격 =====
                var hitZone = PenetrationCalculator.DetermineHitZone(
                    attacker.transform.position, target.transform.position, target.HullAngle);

                float baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, hitZone);
                float impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                    attacker.transform.position, target.transform.position, target.HullAngle, hitZone);
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

                result = new ShotResult
                {
                    hit = true,
                    outcome = outcome,
                    hitZone = hitZone,
                    effectiveArmor = effectiveArmor,
                    damageDealt = finalDmg,
                    hitChance = hitChance
                };
                } // if (!hitCover)
            } // else (hit)

            // 연출 씬으로 데이터 전달
            int targetIndex = enemyUnits.IndexOf(target);

            // 스프라이트 가져오기
            var attackerSr = attacker.GetComponentInChildren<SpriteRenderer>();
            var attackerTurretSr = attacker.transform.Find("Turret")?.GetComponent<SpriteRenderer>();
            var targetSr = target.GetComponentInChildren<SpriteRenderer>();

            // 공격자 엄폐 상태 확인
            var attackerCell = grid.GetCell(attacker.GridPosition);
            bool inCover = attackerCell != null && attackerCell.HasCover
                           && attackerCell.Cover != null && !attackerCell.Cover.IsDestroyed;
            string coverName = inCover ? attackerCell.Cover.coverName : "";

            FireActionContext.SetAction(new FireActionData
            {
                attackerWorldPos = attacker.transform.position,
                attackerHullAngle = attacker.HullAngle,
                attackerName = attacker.Data.tankName,
                attackerSide = attacker.side,
                attackerInCover = inCover,
                attackerCoverName = coverName,
                attackerCoverSize = inCover ? attackerCell.Cover.size : CoverSize.Medium,
                targetInCover = targetInCover,
                targetCoverHit = hitCover,
                coverDamageDealt = coverDmgDealt,
                targetCoverName = hitCover ? hitCoverName : targetCoverNameForVisual,
                targetCoverSize = targetInCover ? targetCellForCover.Cover.size : CoverSize.Medium,
                targetWorldPos = target.transform.position,
                targetHullAngle = target.HullAngle,
                targetName = target.Data.tankName,
                weaponType = WeaponType.MainGun,
                ammoData = attacker.currentAmmo,
                result = result,
                targetUnitIndex = targetIndex,
                targetSide = target.side,
                attackerHullSprite = attackerSr != null ? attackerSr.sprite : null,
                attackerTurretSprite = attackerTurretSr != null ? attackerTurretSr.sprite : null,
                attackerSpriteRotOffset = GetSpriteRotOffset(attacker.transform),
                attackerMuzzleOffset = attacker.Data.muzzleOffset,
                targetHullSprite = targetSr != null ? targetSr.sprite : null,
                targetTurretSprite = target.transform.Find("Turret")?.GetComponent<SpriteRenderer>()?.sprite,
                targetSpriteRotOffset = GetSpriteRotOffset(target.transform)
            });

            SaveBattleState();
            SceneManager.LoadScene("FireActionScene");
        }

        // ===== 기관총 사격 =====

        private void ExecuteMGFire(GridTankUnit attacker, GridTankUnit target, Data.MachineGunDataSO mgData)
        {
            attacker.ConsumeFireAP();

            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float baseHitChance = CalculateHitChance(distance, target)
                + mgData.accuracyModifier
                - attacker.Modules.GetMGAccuracyPenalty();

            // 기총 손상 시 버스트 감소
            int burstCount = Mathf.Max(1, mgData.burstCount - attacker.Modules.GetBurstPenalty());

            // 버스트 발당 결과 계산
            var results = new ShotResult[burstCount];
            for (int i = 0; i < burstCount; i++)
            {
                float shotChance = Mathf.Clamp01(baseHitChance - (i * 0.02f)); // 연사할수록 정확도 감소
                bool hit = Random.value <= shotChance;

                if (!hit)
                {
                    results[i] = new ShotResult { hit = false, outcome = ShotOutcome.Miss, hitChance = shotChance };
                }
                else
                {
                    var hitZone = PenetrationCalculator.DetermineHitZone(
                        attacker.transform.position, target.transform.position, target.HullAngle);
                    float baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, hitZone);
                    float impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                        attacker.transform.position, target.transform.position, target.HullAngle, hitZone);
                    float effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(baseArmor, impactAngle);
                    var outcome = PenetrationCalculator.JudgePenetration(mgData.penetration, effectiveArmor);

                    float dmg = outcome switch
                    {
                        ShotOutcome.Ricochet => mgData.damagePerShot * 0.03f,
                        ShotOutcome.Hit => mgData.damagePerShot,
                        ShotOutcome.Penetration => mgData.damagePerShot * 2f,
                        _ => 0f
                    };

                    results[i] = new ShotResult
                    {
                        hit = true, outcome = outcome, hitZone = hitZone,
                        effectiveArmor = effectiveArmor, damageDealt = dmg, hitChance = shotChance
                    };
                }
            }

            var attackerSr = attacker.GetComponentInChildren<SpriteRenderer>();
            var attackerTurretSr = attacker.transform.Find("Turret")?.GetComponent<SpriteRenderer>();
            var targetSr = target.GetComponentInChildren<SpriteRenderer>();
            int targetIndex = enemyUnits.IndexOf(target);

            FireActionContext.SetAction(new FireActionData
            {
                attackerWorldPos = attacker.transform.position,
                attackerHullAngle = attacker.HullAngle,
                attackerName = attacker.Data.tankName,
                attackerSide = attacker.side,
                targetWorldPos = target.transform.position,
                targetHullAngle = target.HullAngle,
                targetName = target.Data.tankName,
                weaponType = mgData.type,
                mgData = mgData,
                mgResults = results,
                targetUnitIndex = targetIndex,
                targetSide = target.side,
                attackerHullSprite = attackerSr != null ? attackerSr.sprite : null,
                attackerTurretSprite = attackerTurretSr != null ? attackerTurretSr.sprite : null,
                attackerSpriteRotOffset = GetSpriteRotOffset(attacker.transform),
                attackerMuzzleOffset = attacker.Data.muzzleOffset,
                targetHullSprite = targetSr != null ? targetSr.sprite : null,
                targetTurretSprite = target.transform.Find("Turret")?.GetComponent<SpriteRenderer>()?.sprite,
                targetSpriteRotOffset = GetSpriteRotOffset(target.transform)
            });

            SaveBattleState();
            SceneManager.LoadScene("FireActionScene");
        }

        /// <summary>SpriteContainer가 있으면 그 회전 오프셋을 반환</summary>
        private float GetSpriteRotOffset(Transform unitRoot)
        {
            var container = unitRoot.Find("SpriteContainer");
            if (container != null)
                return container.localEulerAngles.z > 180 ? container.localEulerAngles.z - 360 : container.localEulerAngles.z;
            return 0f;
        }

        private float CalculateHitChance(int distance, GridTankUnit target)
        {
            float chance = GameConstants.BaseAccuracy;
            chance -= distance * GameConstants.DistancePenaltyPerCell;

            return Mathf.Clamp01(chance);
        }

        /// <summary>엄폐 + 모듈 보정 포함 명중률</summary>
        private float CalculateHitChanceWithCover(GridTankUnit attacker, GridTankUnit target)
        {
            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float chance = CalculateHitChance(distance, target);

            // 포신 손상 패널티
            chance -= attacker.Modules.GetAccuracyPenalty();

            // 방향 기반 엄폐 보정
            var targetCell = grid.GetCell(target.GridPosition);
            if (targetCell != null && targetCell.HasCover && targetCell.Cover != null
                && !targetCell.Cover.IsDestroyed)
            {
                float atkAngle = AngleUtil.FromDir(
                    new Vector2(target.GridPosition.x - attacker.GridPosition.x,
                                target.GridPosition.y - attacker.GridPosition.y).normalized);
                float coverDir = targetCell.CoverDirection;

                if (targetCell.Cover.IsCovered(coverDir, atkAngle))
                    chance -= targetCell.Cover.CoverRate * 0.3f;
            }

            return Mathf.Clamp01(chance);
        }

        /// <summary>유닛의 현재 상태 (개활지/엄폐)</summary>
        public string GetUnitCoverStatus(GridTankUnit unit)
        {
            var cell = grid.GetCell(unit.GridPosition);
            if (cell == null) return "개활지";

            if (cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                float rate = cell.Cover.CoverRate;
                return $"엄폐 ({cell.Cover.coverName} 엄폐율:{rate:P0})";
            }

            return "개활지";
        }

        // ===== 연출 복귀 후 결과 적용 =====

        /// <summary>씬 전환 직전 — 전투 상태 저장</summary>
        private void SaveBattleState()
        {
            Debug.Log($"[CRUX] SaveBattleState — Player at ({playerUnit.GridPosition.x},{playerUnit.GridPosition.y})");
            var enemyStates = new UnitSaveData[enemyUnits.Count];
            for (int i = 0; i < enemyUnits.Count; i++)
                enemyStates[i] = enemyUnits[i].SaveState();

            // 엄폐물 HP 저장
            var coverHPs = new List<float>();
            for (int x = 0; x < GameConstants.GridWidth; x++)
                for (int y = 0; y < GameConstants.GridHeight; y++)
                {
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell != null && cell.Type == CellType.Cover && cell.Cover != null)
                        coverHPs.Add(cell.Cover.CurrentHP);
                }

            BattleStateStorage.Save(new BattleSaveData
            {
                playerState = playerUnit.SaveState(),
                enemyStates = enemyStates,
                turnCount = turnCount,
                phase = currentPhase,
                coverHPs = coverHPs.ToArray(),
                nextEnemyIndex = currentEnemyIndex
            });
        }

        /// <summary>연출 씬 복귀 — 상태 복원 + 데미지 적용</summary>
        private void ApplyPendingResult()
        {
            Debug.Log($"[CRUX] ApplyPendingResult — HasSavedState: {BattleStateStorage.HasSavedState}");

            // 먼저 씬 초기화 (그리드, 유닛 생성)
            InitializeBattle();

            // 저장된 상태 복원
            if (BattleStateStorage.HasSavedState)
            {
                var state = BattleStateStorage.SavedState;

                // 플레이어 복원
                playerUnit.RestoreState(grid, state.playerState);

                // 적 복원
                for (int i = 0; i < enemyUnits.Count && i < state.enemyStates.Length; i++)
                    enemyUnits[i].RestoreState(grid, state.enemyStates[i]);

                // 엄폐물 HP 복원
                int coverIdx = 0;
                for (int x = 0; x < GameConstants.GridWidth; x++)
                    for (int y = 0; y < GameConstants.GridHeight; y++)
                    {
                        var cell = grid.GetCell(new Vector2Int(x, y));
                        if (cell != null && cell.Type == CellType.Cover && cell.Cover != null)
                        {
                            if (coverIdx < state.coverHPs.Length)
                            {
                                float hp = state.coverHPs[coverIdx++];
                                float dmg = cell.Cover.CurrentHP - hp;
                                if (dmg > 0)
                                    cell.Cover.TakeDamage(dmg);
                            }
                        }
                    }

                turnCount = state.turnCount;
                currentPhase = state.phase;

                // 연출 결과 데미지 적용
                var actionData = FireActionContext.Current;
                GridTankUnit target = null;

                if (actionData.targetSide == PlayerSide.Enemy
                    && actionData.targetUnitIndex >= 0
                    && actionData.targetUnitIndex < enemyUnits.Count)
                    target = enemyUnits[actionData.targetUnitIndex];
                else if (actionData.targetSide == PlayerSide.Player)
                    target = playerUnit;

                if (target != null && !target.IsDestroyed)
                {
                    if (actionData.weaponType == WeaponType.MainGun)
                    {
                        // 주포 데미지
                        if (actionData.result.hit && actionData.result.damageDealt > 0)
                        {
                            target.TakeDamage(new DamageInfo
                            {
                                damage = actionData.result.damageDealt,
                                outcome = actionData.result.outcome
                            });
                        }
                    }
                    else if (actionData.mgResults != null)
                    {
                        // 기관총 데미지 합산
                        foreach (var r in actionData.mgResults)
                        {
                            if (r.hit && r.damageDealt > 0)
                            {
                                target.TakeDamage(new DamageInfo
                                {
                                    damage = r.damageDealt,
                                    outcome = r.outcome
                                });
                            }
                        }
                    }
                }

                // 먼저 클리어 — 적 턴 재개 시 새 데이터를 덮어쓸 수 있도록
                int nextEnemy = state.nextEnemyIndex;
                TurnPhase savedPhase = state.phase;

                BattleStateStorage.Clear();
                FireActionContext.Clear();

                // 적 턴 중이었으면 나머지 적 행동 이어서 진행
                if (savedPhase == TurnPhase.EnemyTurn)
                {
                    currentPhase = TurnPhase.EnemyTurn;
                    StartCoroutine(ProcessEnemyTurn(nextEnemy));
                }

                return;
            }

            FireActionContext.Clear();
        }

        // ===== UI (OnGUI) =====

        private void OnGUI()
        {
            // UI 스케일링 — 모든 좌표/크기에 자동 적용
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            DrawTurnInfo();
            DrawUnitInfo();
            DrawModuleStatus();
            DrawInputModeInfo();
            DrawControls();
            DrawGameResult();

            GUI.matrix = prevMatrix;
        }

        /// <summary>스케일 보정된 Screen 크기 (OnGUI 내에서 사용)</summary>
        private float ScaledW => Screen.width / uiScale;
        private float ScaledH => Screen.height / uiScale;

        private void DrawTurnInfo()
        {
            GUI.Box(new Rect(10, 10, 180, 35), "", GetBoxStyle());
            GUI.Label(new Rect(15, 14, 170, 25),
                $"턴 {turnCount}  |  {currentPhase}", GetLabelStyle());
        }

        private void DrawUnitInfo()
        {
            if (selectedUnit == null) return;

            GUI.Box(new Rect(10, 55, 250, 88), "", GetBoxStyle());
            var style = GetLabelStyle();
            style.fontSize = 18;

            GUI.Label(new Rect(15, 57, 240, 18),
                $"{selectedUnit.Data.tankName}", style);
            GUI.Label(new Rect(15, 75, 240, 18),
                $"HP: {selectedUnit.CurrentHP:F0}/{selectedUnit.Data.maxHP}  AP: {selectedUnit.CurrentAP}/{selectedUnit.MaxAP}", style);
            GUI.Label(new Rect(15, 93, 240, 18),
                $"탄종: {(selectedUnit.currentAmmo != null ? selectedUnit.currentAmmo.ammoName : "없음")}", style);

            // 엄폐 상태 표시
            string coverStatus = GetUnitCoverStatus(selectedUnit);
            Color coverColor = coverStatus == "개활지" ? new Color(1f, 0.5f, 0.3f) : new Color(0.3f, 1f, 0.5f);
            var coverStyle = new GUIStyle(style);
            coverStyle.normal.textColor = coverColor;
            coverStyle.fontSize = 17;
            GUI.Label(new Rect(15, 111, 240, 18), $"상태: {coverStatus}", coverStyle);

            // 타겟 정보
            if (targetUnit != null && (inputMode == InputMode.Fire || inputMode == InputMode.WeaponSelect))
            {
                int dist = grid.GetDistance(selectedUnit.GridPosition, targetUnit.GridPosition);
                float hitChance = CalculateHitChanceWithCover(selectedUnit, targetUnit);

                GUI.Box(new Rect(ScaledW - 260, 55, 250, 85), "", GetBoxStyle());
                GUI.Label(new Rect(ScaledW - 255, 57, 240, 18),
                    $"대상: {targetUnit.Data.tankName}", style);
                GUI.Label(new Rect(ScaledW - 255, 75, 240, 18),
                    $"거리: {dist}셀  명중률: {hitChance:P0}", style);

                // 엄폐 정보
                var tCell = grid.GetCell(targetUnit.GridPosition);
                if (tCell != null && tCell.HasCover && tCell.Cover != null
                    && !tCell.Cover.IsDestroyed)
                {
                    var cover = tCell.Cover;
                    float atkAngle = AngleUtil.FromDir(
                        new Vector2(targetUnit.GridPosition.x - selectedUnit.GridPosition.x,
                                    targetUnit.GridPosition.y - selectedUnit.GridPosition.y).normalized);
                    bool covered = cover.IsCovered(tCell.CoverDirection, atkAngle);

                    var tgtCoverStyle = new GUIStyle(style);
                    tgtCoverStyle.fontSize = 16;
                    tgtCoverStyle.normal.textColor = covered
                        ? new Color(0.3f, 1f, 0.5f)   // 초록 — 엄폐 유효
                        : new Color(1f, 0.5f, 0.3f);   // 주황 — 엄폐 무효

                    string sizeLabel = cover.size switch
                    {
                        CoverSize.Small => "소형",
                        CoverSize.Medium => "중형",
                        CoverSize.Large => "대형",
                        _ => ""
                    };

                    GUI.Label(new Rect(ScaledW - 255, 95, 240, 16),
                        $"엄폐: {cover.coverName} ({sizeLabel}) {(covered ? "유효" : "무효")}", tgtCoverStyle);
                    GUI.Label(new Rect(ScaledW - 255, 111, 240, 16),
                        $"엄폐율:{cover.CoverRate:P0} HP:{cover.CurrentHP:F0}/{cover.maxHP:F0} 범위:{cover.CoverArc:F0}°", tgtCoverStyle);
                }
                else
                {
                    var openStyle = new GUIStyle(style);
                    openStyle.fontSize = 16;
                    openStyle.normal.textColor = new Color(1f, 0.5f, 0.3f);
                    GUI.Label(new Rect(ScaledW - 255, 95, 240, 16), "엄폐: 없음 (개활지)", openStyle);
                }
            }
        }

        private void DrawInputModeInfo()
        {
            // 무기 선택 모드 — 특별 UI
            if (inputMode == InputMode.WeaponSelect && pendingTarget != null)
            {
                DrawWeaponSelectUI();
                return;
            }

            // 방향 선택 모드 — 특별 UI
            if (inputMode == InputMode.MoveDirectionSelect)
            {
                DrawMoveDirectionUI();
                return;
            }

            string modeText = inputMode switch
            {
                InputMode.Move => "[이동 모드] 셀 클릭으로 이동",
                InputMode.Fire => "[사격 모드] 적 유닛 클릭으로 사격",
                _ => "[대기] Q:이동  E:사격  Space:턴종료"
            };

            GUI.Box(new Rect(ScaledW / 2 - 150, ScaledH - 40, 300, 30), "", GetBoxStyle());
            var style = GetLabelStyle();
            style.fontSize = 17;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(ScaledW / 2 - 150, ScaledH - 40, 300, 30), modeText, style);
        }

        private void DrawMoveDirectionUI()
        {
            var box = GetBoxStyle();
            var label = GetLabelStyle();
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;

            float panelW = 280, panelH = 90;
            float px = ScaledW / 2 - panelW / 2;
            float py = ScaledH / 2 + 60;

            GUI.Box(new Rect(px, py, panelW, panelH), "", box);

            string dirName = pendingFacingAngle switch
            {
                0f => "↑ 북",
                45f => "↗ 북동",
                90f => "→ 동",
                135f => "↘ 남동",
                180f => "↓ 남",
                225f => "↙ 남서",
                270f => "← 서",
                315f => "↖ 북서",
                _ => $"{pendingFacingAngle:F0}°"
            };

            GUI.Label(new Rect(px, py + 5, panelW, 22),
                $"목적지: ({pendingMoveTarget.x},{pendingMoveTarget.y})  AP: {pendingMoveCost}", label);
            GUI.Label(new Rect(px, py + 28, panelW, 22),
                $"방향: {dirName}  [WASD 변경]", label);
            GUI.Label(new Rect(px, py + 55, panelW, 22),
                "[Space] 확정  [클릭] 방향+확정  [Tab] 취소", label);
        }

        private void DrawWeaponSelectUI()
        {
            var box = GetBoxStyle();
            var label = GetLabelStyle();

            int dist = grid.GetDistance(selectedUnit.GridPosition, pendingTarget.GridPosition);
            float mainHit = CalculateHitChanceWithCover(selectedUnit, pendingTarget);

            // 무기 선택 패널
            float panelW = 320, panelH = 130;
            float px = ScaledW / 2 - panelW / 2;
            float py = ScaledH / 2 + 40;

            GUI.Box(new Rect(px, py, panelW, panelH), "", box);

            var titleStyle = new GUIStyle(label);
            titleStyle.fontSize = 21;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(px, py + 5, panelW, 22), $"무기 선택 — 대상: {pendingTarget.Data.tankName}", titleStyle);

            var itemStyle = new GUIStyle(label);
            itemStyle.fontSize = 18;

            // 1. 주포
            string mainInfo = $"[1] 주포 ({selectedUnit.currentAmmo?.ammoName ?? "AP"})  명중: {mainHit:P0}  AP:{GameConstants.FireCost}";
            GUI.Label(new Rect(px + 10, py + 32, panelW - 20, 20), mainInfo, itemStyle);

            // 2. 동축 기관총
            if (coaxialMGData != null)
            {
                float mgHit = Mathf.Clamp01(mainHit + coaxialMGData.accuracyModifier);
                string mgInfo = $"[2] {coaxialMGData.mgName}  {coaxialMGData.burstCount}발  명중: {mgHit:P0}  AP:{coaxialMGData.apCost}";
                GUI.Label(new Rect(px + 10, py + 55, panelW - 20, 20), mgInfo, itemStyle);
            }

            // 3. 탑재 기관총
            if (mountedMGData != null)
            {
                float mgHit = Mathf.Clamp01(mainHit + mountedMGData.accuracyModifier);
                string mgInfo = $"[3] {mountedMGData.mgName}  {mountedMGData.burstCount}발  명중: {mgHit:P0}  AP:{mountedMGData.apCost}";
                GUI.Label(new Rect(px + 10, py + 78, panelW - 20, 20), mgInfo, itemStyle);
            }

            itemStyle.fontSize = 16;
            itemStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(px + 10, py + 105, panelW - 20, 18), "클릭: 주포  |  Tab: 취소", itemStyle);
        }

        private void DrawModuleStatus()
        {
            if (selectedUnit == null || selectedUnit.IsDestroyed) return;

            var modules = selectedUnit.Modules;
            float panelX = 10f, panelY = 148f, panelW = 250f, panelH = 58f;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", GetBoxStyle());

            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 15;

            var types = new (Unit.ModuleType type, string name)[]
            {
                (Unit.ModuleType.Engine, "엔진"),
                (Unit.ModuleType.Barrel, "포신"),
                (Unit.ModuleType.AmmoRack, "탄약"),
                (Unit.ModuleType.Loader, "장전"),
                (Unit.ModuleType.MachineGun, "기총"),
                (Unit.ModuleType.TurretRing, "포탑"),
                (Unit.ModuleType.CaterpillarLeft, "캐L"),
                (Unit.ModuleType.CaterpillarRight, "캐R"),
            };

            float x = panelX + 5;
            float y = panelY + 3;
            int col = 0;

            foreach (var (type, name) in types)
            {
                var m = modules.Get(type);
                if (m == null) continue;

                style.normal.textColor = m.state switch
                {
                    Unit.ModuleState.Normal => new Color(0.7f, 0.7f, 0.7f),
                    Unit.ModuleState.Damaged => new Color(1f, 0.9f, 0.2f),
                    Unit.ModuleState.Broken => new Color(1f, 0.3f, 0.2f),
                    Unit.ModuleState.Destroyed => new Color(0.4f, 0.4f, 0.4f),
                    _ => Color.white
                };

                string stateChar = m.state switch
                {
                    Unit.ModuleState.Normal => "",
                    Unit.ModuleState.Damaged => "!",
                    Unit.ModuleState.Broken => "X",
                    Unit.ModuleState.Destroyed => "#",
                    _ => ""
                };

                GUI.Label(new Rect(x + col * 62, y, 60, 14), $"{name}{stateChar}", style);
                col++;
                if (col >= 4)
                {
                    col = 0;
                    y += 15;
                }
            }
        }

        private void DrawControls()
        {
            var style = GetLabelStyle();
            style.fontSize = 16;
            style.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            GUI.Box(new Rect(10, ScaledH - 75, 250, 55), "", GetBoxStyle());
            GUI.Label(new Rect(15, ScaledH - 73, 240, 16), "Q: 이동 | E: 사격 | Tab: 취소", style);
            GUI.Label(new Rect(15, ScaledH - 55, 240, 16), "Space: 확정/턴종료 | 1/2/3: 무기", style);
        }

        private void DrawGameResult()
        {
            // 승리/패배 체크
            if (playerUnit != null && playerUnit.IsDestroyed)
                currentPhase = TurnPhase.GameOver;

            bool allEnemiesDead = true;
            foreach (var e in enemyUnits)
                if (e != null && !e.IsDestroyed) { allEnemiesDead = false; break; }
            if (allEnemiesDead)
                currentPhase = TurnPhase.Victory;

            if (currentPhase == TurnPhase.GameOver || currentPhase == TurnPhase.Victory)
            {
                string msg = currentPhase == TurnPhase.Victory
                    ? "승리! 적 전멸!"
                    : "패배... 로시난테 파괴됨";

                var bigStyle = GetLabelStyle();
                bigStyle.fontSize = 36;
                bigStyle.alignment = TextAnchor.MiddleCenter;

                GUI.Box(new Rect(ScaledW / 2 - 180, ScaledH / 2 - 35, 360, 70), "", GetBoxStyle());
                GUI.Label(new Rect(ScaledW / 2 - 180, ScaledH / 2 - 35, 360, 70), msg, bigStyle);

                if (Input.GetKeyDown(KeyCode.R))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        // ===== UI 유틸 =====

        private GUIStyle _boxStyle;
        private GUIStyle GetBoxStyle()
        {
            if (_boxStyle != null) return _boxStyle;
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = GetTex(new Color(0, 0, 0, 0.7f));
            return _boxStyle;
        }

        private GUIStyle _labelStyle;
        private GUIStyle GetLabelStyle()
        {
            if (_labelStyle != null) return _labelStyle;
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 20;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.fontStyle = FontStyle.Bold;
            return _labelStyle;
        }

        private Texture2D GetTex(Color col)
        {
            if (texCache.TryGetValue(col, out var cached)) return cached;
            var tex = new Texture2D(2, 2);
            var pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            texCache[col] = tex;
            return tex;
        }
    }
}
