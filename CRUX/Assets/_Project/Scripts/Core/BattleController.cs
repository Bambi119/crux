using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;
using Crux.UI;
using Crux.Camera;
using Crux.PlayerInput;
using TerrainData = Crux.Core.TerrainData;

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

        [Header("맵 설정")]
        [Tooltip("켜면 12×12 지형 테스트 맵 사용. 꺼져있으면 표준 8×10 맵.")]
        [SerializeField] private bool useTerrainTestMap = false;
        [SerializeField] private int testMapWidth = 12;
        [SerializeField] private int testMapHeight = 12;

        [Header("디버그")]
        [Tooltip("F1 토글 — 각 셀에 지형 라벨 + 마우스 호버 시 상세 정보")]
        [SerializeField] private bool showTerrainDebug = true;

        // 반응 사격 연출 상태 — 적 이동 코루틴이 이 플래그를 폴링해 시퀀스 종료까지 대기
        public static bool IsReactionPlaying;

        // HUD
        private BattleHUD hud;

        // 입력 핸들러
        private PlayerInputHandler inputHandler;

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
        private GridTankUnit inspectedUnit; // 정보 조회용 (아군/적 공용)
        private GridTankUnit hoveredTarget; // Fire 모드 호버 대상
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
        private BattleCamera battleCam;

        public TurnPhase CurrentPhase => currentPhase;
        public int TurnCount => turnCount;

        // HUD 접근용 getter
        public UnityEngine.Camera MainCam => battleCam != null ? battleCam.Cam : null;
        public GridManager Grid => grid;
        public bool ShowTerrainDebug => showTerrainDebug;
        public GridTankUnit SelectedUnit => selectedUnit;
        public GridTankUnit InspectedUnit => inspectedUnit;
        public GridTankUnit HoveredTarget => hoveredTarget;
        public GridTankUnit PendingTarget => pendingTarget;
        public Vector2Int PendingMoveTarget => pendingMoveTarget;
        public float PendingFacingAngle => pendingFacingAngle;
        public int PendingMoveCost => pendingMoveCost;
        public WeaponType SelectedWeapon => selectedWeapon;
        public InputModeEnum CurrentInputMode => (InputModeEnum)(int)inputMode;
        public Data.MachineGunDataSO CoaxialMGData => coaxialMGData;
        public Data.MachineGunDataSO MountedMGData => mountedMGData;

        // InputMode를 외부에서 참조할 수 있게 enum으로 노출
        public enum InputModeEnum { Select, Move, MoveDirectionSelect, Fire, WeaponSelect }

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
            // 그리드 — 테스트 맵일 경우 차원 확장
            var gridObj = new GameObject("Grid");
            grid = gridObj.AddComponent<GridManager>();
            if (useTerrainTestMap)
                grid.SetDimensions(testMapWidth, testMapHeight);

            // 시각화
            var visObj = new GameObject("GridVisualizer");
            visualizer = visObj.AddComponent<GridVisualizer>();
            visualizer.Initialize(grid);

            // 맵 배치 — 모드별 배치 클래스 선택
            var setupObj = new GameObject("MapSetup");
            mapSetup = useTerrainTestMap
                ? setupObj.AddComponent<TerrainTestMapSetup>()
                : setupObj.AddComponent<GridMapSetup>();
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

            // 화재 사망 이벤트 구독
            if (playerUnit != null)
                playerUnit.OnFireKilled += HandleFireKill;
            foreach (var e in enemyUnits)
                if (e != null) e.OnFireKilled += HandleFireKill;

            // 오버워치 트리거 — 적 이동 스텝마다 플레이어측 반응 사격 체크
            foreach (var e in enemyUnits)
                if (e != null) e.OnMoveStepComplete += HandleEnemyMoveStep;

            // BattleCamera 초기화
            var camObj = new GameObject("BattleCamera");
            battleCam = camObj.AddComponent<BattleCamera>();
            battleCam.Initialize();

            // 맵 프레이밍 계산 및 적용 — 맵 전체가 보이도록 자동 조정 (flat-top hex 실제 치수)
            // 주의: grid.Width/Height를 참조해야 테스트 맵(12×12 등)에도 대응
            float size = GameConstants.CellSize;
            float mapW = size * (1.5f * (grid.Width - 1) + 2f);
            float mapH = size * Mathf.Sqrt(3f) * (grid.Height + 0.5f);
            float aspect = battleCam.Cam != null ? battleCam.Cam.aspect : 16f / 9f;
            float sizeByH = mapH * 0.55f;
            float sizeByW = (mapW * 0.55f) / aspect;
            float initSize = Mathf.Max(sizeByH, sizeByW);

            // 맵 중심 (hex 월드 좌표 기반)
            Vector3 bl = HexCoord.OffsetToWorld(new Vector2Int(0, 0), size);
            Vector3 tr = HexCoord.OffsetToWorld(new Vector2Int(grid.Width - 1, grid.Height - 1), size);
            Vector3 center = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, -10f);
            battleCam.SetInitialFraming(center, initSize);
            battleCam.SetPanBounds(0, grid.Width * size, 0, grid.Height * size);

            // 입력 핸들러 초기화
            var inputObj = new GameObject("PlayerInputHandler");
            inputHandler = inputObj.AddComponent<PlayerInputHandler>();
            inputHandler.Initialize(this);

            // HUD 초기화
            var hudObj = new GameObject("BattleHUD");
            hud = hudObj.AddComponent<BattleHUD>();
            hud.Initialize(this);

            // 플레이어 턴 시작
            StartPlayerTurn();
        }

        private InputMode lastArcMode = InputMode.Select;
        private Vector2Int lastArcPos;

        private void Update()
        {
            if (currentPhase == TurnPhase.PlayerTurn)
            {
                inputHandler?.Tick();
            }
            // 방호 arc는 턴 구분 없이 갱신 — 적 턴 중 씬 복귀 시에도 플레이어 엄폐 상태가 보여야 함
            UpdateCoverArcDisplay();
            if (battleCam != null) battleCam.Tick(IsReactionPlaying);
            CheckVictoryDefeat();

            // 지형 디버그 토글
            if (Input.GetKeyDown(KeyCode.F1))
                showTerrainDebug = !showTerrainDebug;
        }

        /// <summary>승리/패배 판정 — 플레이어 파괴 시 패배, 전 적 파괴 시 승리</summary>
        private void CheckVictoryDefeat()
        {
            if (currentPhase == TurnPhase.GameOver || currentPhase == TurnPhase.Victory) return;

            if (playerUnit != null && playerUnit.IsDestroyed)
            {
                currentPhase = TurnPhase.GameOver;
                return;
            }

            bool allEnemiesDead = true;
            foreach (var e in enemyUnits)
                if (e != null && !e.IsDestroyed) { allEnemiesDead = false; break; }
            if (allEnemiesDead)
                currentPhase = TurnPhase.Victory;
        }

        /// <summary>플레이어 유닛의 엄폐 커버 범위 표시 (상태 변경 시만 갱신)</summary>
        /// <remarks>
        /// 플레이어 턴: selectedUnit(일반적으로 playerUnit)의 Select/Move 모드에서 표시
        /// 적 턴: 선택 상태 무관하게 playerUnit의 엄폐 상태 계속 표시 (사격 사이 복귀 시 가시성)
        /// </remarks>
        private void UpdateCoverArcDisplay()
        {
            // 표시 대상 유닛 결정
            GridTankUnit target = null;
            bool isPlayerSelectMode = false;
            if (currentPhase == TurnPhase.PlayerTurn
                && selectedUnit != null && !selectedUnit.IsDestroyed
                && (inputMode == InputMode.Select || inputMode == InputMode.Move))
            {
                target = selectedUnit;
                isPlayerSelectMode = true;
            }
            else if (playerUnit != null && !playerUnit.IsDestroyed)
            {
                // 적 턴이거나 공격 모드 — playerUnit 엄폐 상태 폴백 표시
                target = playerUnit;
            }

            if (target == null)
            {
                if (lastArcMode != InputMode.Select)
                {
                    visualizer.ClearCoverArcs();
                    lastArcMode = InputMode.Select;
                }
                return;
            }

            // 위치나 모드가 바뀌었을 때만 갱신
            var currentMode = isPlayerSelectMode ? inputMode : InputMode.Select;
            if (lastArcPos == target.GridPosition && lastArcMode == currentMode) return;
            lastArcPos = target.GridPosition;
            lastArcMode = currentMode;

            visualizer.ClearCoverArcs();

            var cell = grid.GetCell(target.GridPosition);
            if (cell != null && cell.HasCover && cell.Cover != null
                && !cell.Cover.IsDestroyed)
            {
                visualizer.ShowCoverFacets(
                    target.GridPosition,
                    cell.Cover.CurrentFacets,
                    new Color(0.2f, 0.8f, 0.4f, 0.8f));
            }
        }

        // ===== 턴 관리 =====

        private void StartPlayerTurn()
        {
            currentPhase = TurnPhase.PlayerTurn;
            inputMode = InputMode.Select;
            selectedUnit = null;
            targetUnit = null;

            // 사전 스윕: 턴 시작 효과(화재 등) 처리 → 격파 유닛 제거 후 행동 부여
            StartCoroutine(BeginPlayerTurnRoutine());
        }

        /// <summary>플레이어 턴 시작 루틴 — 상태 효과 처리 우선</summary>
        private IEnumerator BeginPlayerTurnRoutine()
        {
            // 플레이어 화재 등 턴 시작 효과 처리
            if (playerUnit != null && !playerUnit.IsDestroyed)
            {
                playerUnit.OnTurnStart();
                if (playerUnit.IsDestroyed)
                {
                    // 화재 전소 — 배너/폭발이 HandleFireKill에서 처리됨
                    // 게임오버 화면 표시를 위해 대기
                    yield return new WaitForSeconds(1.5f);
                    turnCount++;
                    yield break; // DrawGameResult가 감지
                }
                selectedUnit = playerUnit;
            }

            // 연막 턴 감소
            TickSmoke();

            turnCount++;
            Debug.Log($"[CRUX] === 플레이어 턴 {turnCount} ===");
        }

        private void StartEnemyTurn()
        {
            currentPhase = TurnPhase.EnemyTurn;
            visualizer.ClearHighlights();
            Debug.Log("[CRUX] === 적 턴 ===");
            StartCoroutine(BeginEnemyTurnRoutine());
        }

        /// <summary>적 턴 시작 루틴 — 사전 스윕으로 상태 효과 처리 후 행동 루프</summary>
        private IEnumerator BeginEnemyTurnRoutine()
        {
            // 사전 스윕: 모든 적 유닛에 턴 시작 효과(화재) 적용
            // 이 단계에서 격파되는 유닛은 HandleFireKill 이벤트로 처리됨
            foreach (var enemy in enemyUnits)
            {
                if (enemy == null || enemy.IsDestroyed) continue;
                enemy.OnTurnStart();
                if (enemy.IsDestroyed)
                {
                    // 폭발/배너 가시 시간 확보
                    yield return new WaitForSeconds(1.3f);
                }
            }
            yield return new WaitForSeconds(0.2f);

            // 메인 행동 루프 — 생존 유닛만 순차 진행
            yield return ProcessEnemyTurn(0);
        }

        private IEnumerator ProcessEnemyTurn(int startIndex = 0)
        {
            // foes 리스트 (적 입장에서는 플레이어 측) — 매 턴 동일하지만 일관성 위해 로컬로
            var foes = new List<GridTankUnit>();
            if (playerUnit != null) foes.Add(playerUnit);

            for (int i = startIndex; i < enemyUnits.Count; i++)
            {
                var enemy = enemyUnits[i];
                if (enemy == null || enemy.IsDestroyed) continue;

                // OnTurnStart는 BeginEnemyTurnRoutine 사전 스윕에서 처리됨
                // 연출 씬 복귀 시에도 중복 호출 안됨

                if (playerUnit == null || playerUnit.IsDestroyed) continue;

                // ===== AI 결정 위임 =====
                var ai = enemy.GetComponent<Crux.AI.EnemyAIController>();
                Crux.AI.AIDecision decision;
                if (ai != null)
                {
                    decision = ai.Decide(grid, enemyUnits, foes);
                    Debug.Log($"[AI] {enemy.Data?.tankName} state={decision.state} score={decision.score:F2} " +
                              $"move={(decision.moveTo.HasValue ? decision.moveTo.Value.ToString() : "none")} " +
                              $"fire={(decision.fireTarget != null ? decision.fireTarget.Data?.tankName : "none")}");
                }
                else
                {
                    // Fallback: AI 컨트롤러 없으면 대기만
                    decision = Crux.AI.AIDecision.Wait(Crux.AI.AIState.Engage);
                }

                // ===== 이동 실행 =====
                if (decision.moveTo.HasValue && decision.moveTo.Value != enemy.GridPosition && enemy.CanMove())
                {
                    enemy.FaceToward(decision.moveTo.Value);
                    enemy.MoveTo(decision.moveTo.Value);
                    while (enemy.IsMoving)
                        yield return null;

                    // 오버워치 반격으로 이동 중 격파됐다면 이 적 행동은 종결
                    if (enemy.IsDestroyed) continue;
                }

                // ===== 사격 실행 =====
                if (decision.fireTarget != null && !decision.fireTarget.IsDestroyed && enemy.CanFire())
                {
                    int distAfter = grid.GetDistance(enemy.GridPosition, decision.fireTarget.GridPosition);
                    if (distAfter <= GameConstants.MaxFireRange
                        && grid.HasLOS(enemy.GridPosition, decision.fireTarget.GridPosition))
                    {
                        enemy.FaceToward(decision.fireTarget.GridPosition);
                        currentEnemyIndex = i + 1;
                        ExecuteFire(enemy, decision.fireTarget);
                        yield break; // 씬 전환됨
                    }
                }

                yield return new WaitForSeconds(0.3f);
            }

            // 모든 적 행동 완료 → 플레이어 턴
            yield return new WaitForSeconds(0.5f);
            StartPlayerTurn();
        }

        // ===== 입력 처리 =====


        /// <summary>화재 누적 사망 — 전략맵 내 폭파 연출 + 배너 표시</summary>
        private void HandleFireKill(GridTankUnit unit)
        {
            if (unit == null) return;
            Vector3 pos = unit.transform.position;

            // 폭파 이펙트 — 기존 HitEffects 재사용
            Crux.Combat.HitEffects.SpawnExplosion(pos);

            // 배너 표시
            ShowBanner($"화재로 인한 전소! — {unit.Data?.tankName}",
                       new Color(1f, 0.4f, 0.15f), 2.5f);

            // 유닛 외형 비활성화 (남은 처리는 기존 IsDestroyed 로직에 맡김)
            var cell = grid.GetCell(unit.GridPosition);
            if (cell != null && cell.Occupant == unit.gameObject)
                cell.Occupant = null;
            unit.gameObject.SetActive(false);

            Debug.Log($"[CRUX] {unit.Data?.tankName} 화재로 인한 전소");
        }

        // ===== 오버워치 (반응 사격) =====

        private const float OverwatchArcHalfWidth = 25f; // 전방 50° (±25°)

        /// <summary>적이 한 셀 이동 완료할 때마다 호출 — 플레이어측 오버워치 트리거 판정</summary>
        private void HandleEnemyMoveStep(GridTankUnit movingEnemy, Vector2Int newPos)
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
            StartCoroutine(ExecuteReactionFireSequence(playerUnit, movingEnemy));
        }

        /// <summary>
        /// 반응 사격 연출 시퀀스 — 비트 분리형. 각 단계가 사용자에게 인지되도록 간격 확보.
        /// IsReactionPlaying 플래그로 이동 중인 적 코루틴을 일시 정지시킴.
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
        private IEnumerator ExecuteReactionFireSequence(GridTankUnit attacker, GridTankUnit target)
        {
            IsReactionPlaying = true;

            attacker.ConsumeOverwatchShot();
            if (!attacker.ConsumeMainGunRound())
            {
                Debug.LogWarning("[CRUX] 오버워치 트리거 시점 주포 잔탄 0 — 사격 무시");
                IsReactionPlaying = false;
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
            ShowAlertAt(attackerPos, 0.40f);
            ShowBanner($"⚠ 오버워치 — {attacker.Data?.tankName}",
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
            float hitChance = CalculateHitChanceWithCover(attacker, target);
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
                ShowBanner($"⌁ 반응 사격 빗나감 — {target.Data?.tankName}",
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
                ShowBanner($"⌁ 반응 사격 {outcomeLabel}! — {target.Data?.tankName}",
                           bannerCol, 1.8f);
                Debug.Log($"[CRUX] 오버워치 반격: {outcomeLabel} dmg={finalDmg:F0}");
            }

            // ===== [5-후] 판정 결과 인지 여운 =====
            yield return new WaitForSeconds(0.35f);

            // ===== 카메라 즉시 복귀 =====
            battleCam?.RestoreState();

            IsReactionPlaying = false;
        }

        /// <summary>경고 마커 예약 — BattleHUD에 위임</summary>
        private void ShowAlertAt(Vector3 worldPos, float duration)
        {
            hud?.ShowAlert(worldPos, duration);
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

        // ===== PlayerInputHandler 공개 API =====

        public bool CanHandleInput => selectedUnit != null && !selectedUnit.IsDestroyed && !selectedUnit.IsMoving;

        public void CancelToSelect()
        {
            inputMode = InputMode.Select;
            visualizer.ClearHighlights();
            targetUnit = null;
            pendingTarget = null;
            hoveredTarget = null;
        }

        public void TryEnterMoveMode()
        {
            if (selectedUnit == null || !selectedUnit.CanMove()) return;
            inputMode = InputMode.Move;
            visualizer.ShowMoveRange(grid.GetReachableCells(selectedUnit.GridPosition, selectedUnit.CurrentAP));
        }

        public void TryEnterFireMode()
        {
            if (selectedUnit == null || !selectedUnit.CanFire()) return;
            inputMode = InputMode.Fire;
            selectedWeapon = WeaponType.MainGun;
            visualizer.ShowFireRange(selectedUnit.GridPosition, GameConstants.MaxFireRange);
        }

        public void SelectWeapon(WeaponType weapon)
        {
            if (weapon == WeaponType.MainGun
                || (weapon == WeaponType.CoaxialMG && coaxialMGData != null)
                || (weapon == WeaponType.MountedMG && mountedMGData != null))
                selectedWeapon = weapon;
        }

        public void CommitWeaponSelection()
        {
            if (selectedUnit != null && pendingTarget != null)
                CommitFire(selectedUnit, pendingTarget, selectedWeapon);
        }

        public void SetPendingFacingAngle(float angle) => pendingFacingAngle = angle;

        public void CommitMoveDirection()
        {
            if (selectedUnit == null) return;
            selectedUnit.MoveToWithFacing(pendingMoveTarget, pendingFacingAngle);
            visualizer.ClearHighlights();
            inputMode = InputMode.Select;
        }

        public void CommitMoveDirectionFromMouse()
        {
            pendingFacingAngle = GetSnappedDirectionFromMouse(pendingMoveTarget);
            CommitMoveDirection();
        }

        public void TryExtinguishAction()
        {
            if (selectedUnit != null && selectedUnit.IsOnFire && inputMode == InputMode.Select)
                selectedUnit.TryExtinguish();
        }

        public void TryUseSmokeAction()
        {
            if (selectedUnit == null || !selectedUnit.CanUseSmoke() || inputMode != InputMode.Select) return;
            if (!selectedUnit.UseSmoke()) return;
            var cell = grid.GetCell(selectedUnit.GridPosition);
            if (cell != null)
            {
                cell.SmokeTurnsLeft = 2;
                visualizer.ShowSmoke(selectedUnit.GridPosition);
            }
        }

        public void TryActivateOverwatchAction()
        {
            if (selectedUnit == null || inputMode != InputMode.Select || !selectedUnit.CanActivateOverwatch()) return;
            if (selectedUnit.ActivateOverwatch())
                ShowBanner("⌁ 오버워치 설정 — 전방 50° 내 적 이동 시 반격", new Color(0.4f, 0.9f, 1f), 1.5f);
        }

        public void EndPlayerTurn()
        {
            if (inputMode != InputMode.Select) return;
            visualizer.ClearHighlights();
            StartEnemyTurn();
        }

        public void HandleClickAt(Vector2Int gridPos)
        {
            if (!grid.IsInBounds(gridPos)) return;
            switch (inputMode)
            {
                case InputMode.Move: TryMoveToCell(gridPos); break;
                case InputMode.Fire: TrySelectTarget(gridPos); break;
                case InputMode.Select: InspectCell(gridPos); break;
            }
        }

        /// <summary>전략맵 상단 배너 — duration 초 동안 표시</summary>
        public void ShowBanner(string text, Color color, float duration)
        {
            if (hud != null) hud.ShowBanner(text, color, duration);
        }

        public void ShowAlert(Vector3 worldPos, float duration)
        {
            if (hud != null) hud.ShowAlert(worldPos, duration);
        }

        /// <summary>Fire 모드: 마우스 위치의 적을 hoveredTarget에 기록</summary>
        public void UpdateHoveredTarget()
        {
            if (battleCam?.Cam == null) return;
            var worldPos = battleCam.Cam.ScreenToWorldPoint(Input.mousePosition);
            var pos = grid.WorldToGrid(worldPos);
            if (!grid.IsInBounds(pos))
            {
                hoveredTarget = null;
                return;
            }
            var cell = grid.GetCell(pos);
            if (cell == null || cell.Occupant == null)
            {
                hoveredTarget = null;
                return;
            }
            var unit = cell.Occupant.GetComponent<GridTankUnit>();
            hoveredTarget = (unit != null && unit.side == PlayerSide.Enemy && !unit.IsDestroyed)
                            ? unit : null;
        }

        /// <summary>현재 선택된 무기로 사격 — 주포/기총 분기</summary>
        private void CommitFire(GridTankUnit attacker, GridTankUnit target, WeaponType weapon)
        {
            if (weapon == WeaponType.MainGun)
            {
                ExecuteFire(attacker, target);
            }
            else if (weapon == WeaponType.CoaxialMG && coaxialMGData != null)
            {
                ExecuteMGFire(attacker, target, coaxialMGData);
            }
            else if (weapon == WeaponType.MountedMG && mountedMGData != null)
            {
                ExecuteMGFire(attacker, target, mountedMGData);
            }
            else
            {
                ExecuteFire(attacker, target);
            }
        }

        /// <summary>Select 모드에서 셀 클릭 — 유닛이면 정보 조회</summary>
        private void InspectCell(Vector2Int pos)
        {
            var cell = grid.GetCell(pos);
            if (cell == null || cell.Occupant == null)
            {
                inspectedUnit = null;
                return;
            }
            var unit = cell.Occupant.GetComponent<GridTankUnit>();
            if (unit == null || unit.IsDestroyed)
            {
                inspectedUnit = null;
                return;
            }
            // 아군 클릭 → 기본 상태 (selectedUnit 표시)
            inspectedUnit = unit.side == PlayerSide.Player ? null : unit;
        }

        /// <summary>마우스 클릭 위치에서 셀 기준 6방향 (60° 단위) 스냅 각도 계산</summary>
        private float GetSnappedDirectionFromMouse(Vector2Int targetCell)
        {
            var clickWorld = battleCam?.Cam.ScreenToWorldPoint(Input.mousePosition) ?? Vector3.zero;
            var cellWorld = grid.GridToWorld(targetCell);
            var diff = new Vector2(clickWorld.x - cellWorld.x, clickWorld.y - cellWorld.y);

            if (diff.sqrMagnitude < 0.01f)
                return pendingFacingAngle;

            float deg = AngleUtil.FromDir(diff);
            if (deg < 0) deg += 360f;
            return AngleUtil.SnapTo60(deg);
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
            if (target == null || target.IsDestroyed || target.side == PlayerSide.Player) return;

            targetUnit = target;
            pendingTarget = target;
            inputMode = InputMode.WeaponSelect;
            visualizer.ClearHighlights();
        }

        // ===== 사격 =====

        private void ExecuteFire(GridTankUnit attacker, GridTankUnit target)
        {
            attacker.ConsumeFireAP();
            attacker.ConsumeMainGunRound();

            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float hitChance = CalculateHitChanceWithCover(attacker, target);

            bool hit = Random.value <= hitChance;

            ShotResult result = new ShotResult { hit = false, outcome = ShotOutcome.Miss, hitChance = hitChance };
            bool hitCover = false;
            float coverDmgDealt = 0f;
            string hitCoverName = "";

            // 대상 엄폐 여부 — 6방향 슬롯 판정
            var targetCellForCover = grid.GetCell(target.GridPosition);
            var attackHexDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);

            bool targetInCover = false;
            string targetCoverNameForVisual = "";

            if (targetCellForCover != null && targetCellForCover.HasCover
                && targetCellForCover.Cover != null
                && !targetCellForCover.Cover.IsDestroyed)
            {
                targetInCover = targetCellForCover.Cover.IsCovered(attackHexDir);
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

                        var coverRef = targetCell.Cover; // TakeDamage 전에 참조 보존
                        coverRef.TakeDamage(dmg);

                        result = new ShotResult
                        {
                            hit = true,
                            outcome = ShotOutcome.Hit,
                            hitZone = HitZone.Front,
                            effectiveArmor = 0,
                            damageDealt = 0, // 전차에 데미지 없음
                            hitChance = hitChance
                        };

                        Debug.Log($"[CRUX] 엄폐물 피격! {hitCoverName} ({coverRef.size}) HP: {coverRef.CurrentHP:F0}/{coverRef.maxHP:F0} 엄폐율: {coverRef.CoverRate:P0} 방호면: {coverRef.CurrentFacets}");
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

            // 사전 롤: 전차 피해 시에만 (엄폐 피격이 아닌 경우)
            Unit.DamageOutcome mainOutcome = default;
            if (!hitCover && result.hit && result.damageDealt > 0)
            {
                mainOutcome = target.PreRollDamage(new DamageInfo
                {
                    damage = result.damageDealt,
                    outcome = result.outcome,
                    hitZone = result.hitZone
                });
            }

            FireActionContext.SetAction(new FireActionData
            {
                attackerWorldPos = attacker.transform.position,
                attackerHullAngle = attacker.HullAngle,
                attackerName = attacker.Data.tankName,
                attackerSide = attacker.side,
                attackerInCover = inCover,
                attackerCoverName = coverName,
                attackerCoverSize = inCover ? attackerCell.Cover.size : CoverSize.Medium,
                attackerCoverFacets = inCover ? attackerCell.Cover.CurrentFacets : HexFacet.None,
                targetInCover = targetInCover,
                targetCoverHit = hitCover,
                coverDamageDealt = coverDmgDealt,
                targetCoverName = hitCover ? hitCoverName : targetCoverNameForVisual,
                targetCoverSize = targetInCover ? targetCellForCover.Cover.size : CoverSize.Medium,
                targetCoverFacets = targetInCover ? targetCellForCover.Cover.CurrentFacets : HexFacet.None,
                targetWorldPos = target.transform.position,
                targetHullAngle = target.HullAngle,
                targetName = target.Data.tankName,
                weaponType = WeaponType.MainGun,
                ammoData = attacker.currentAmmo,
                result = result,
                mainOutcome = mainOutcome,
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
            attacker.ConsumeMGBurst(burstCount);

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

            // 기총 총 피해 집계 후 단일 사전 롤 (관통 발생 시에만 모듈/화재 롤)
            float totalMGDamage = 0f;
            bool anyPenetration = false;
            HitZone mgZone = HitZone.Front;
            foreach (var r in results)
            {
                if (r.hit && r.damageDealt > 0)
                {
                    totalMGDamage += r.damageDealt;
                    if (r.outcome == ShotOutcome.Penetration) anyPenetration = true;
                    mgZone = r.hitZone;
                }
            }
            Unit.DamageOutcome mgOutcome = default;
            if (totalMGDamage > 0)
            {
                mgOutcome = target.PreRollDamage(new DamageInfo
                {
                    damage = totalMGDamage,
                    outcome = anyPenetration ? ShotOutcome.Penetration : ShotOutcome.Hit,
                    hitZone = mgZone
                });
            }

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
                mgAggregateOutcome = mgOutcome,
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

        public float CalculateHitChance(int distance, GridTankUnit target)
        {
            float chance = GameConstants.BaseAccuracy;
            chance -= distance * GameConstants.DistancePenaltyPerCell;

            return Mathf.Clamp01(chance);
        }

        /// <summary>엄폐 + 모듈 + 지형(고도·은엄폐) 보정 포함 명중률</summary>
        private float CalculateHitChanceWithCover(GridTankUnit attacker, GridTankUnit target)
        {
            int distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);
            float chance = CalculateHitChance(distance, target);

            // 포신 손상 패널티
            chance -= attacker.Modules.GetAccuracyPenalty();

            var attackerCell = grid.GetCell(attacker.GridPosition);
            var targetCell = grid.GetCell(target.GridPosition);

            // 지형 고도 차 — 공격자 > 목표면 +5%/단계
            if (attackerCell != null && targetCell != null)
            {
                int elevDelta = TerrainData.Elevation(attackerCell.Terrain)
                              - TerrainData.Elevation(targetCell.Terrain);
                if (elevDelta > 0) chance += elevDelta * 0.05f;
            }

            // 6방향 슬롯 엄폐 보정
            if (targetCell != null && targetCell.HasCover && targetCell.Cover != null
                && !targetCell.Cover.IsDestroyed)
            {
                var atkDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);
                if (targetCell.Cover.IsCovered(atkDir))
                    chance -= targetCell.Cover.CoverRate * 0.3f;
            }

            // 지형 자체 엄폐 (파편지대·탄흔 등) — 엄폐물과 합산
            if (targetCell != null)
            {
                float intrinsicCover = TerrainData.IntrinsicCoverRate(targetCell.Terrain);
                if (intrinsicCover > 0f) chance -= intrinsicCover * 0.3f;
            }

            // 은엄폐 (수풀·파편) — 엄폐와 독립 감산
            if (targetCell != null)
            {
                int concealment = TerrainData.Concealment(targetCell.Terrain);
                if (concealment > 0) chance -= concealment * 0.01f;
            }

            // 연막 보정
            if (targetCell != null && targetCell.HasSmoke)
                chance -= 0.4f;

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
            // 연출 씬 복귀 대상 씬 기록 (TerrainTestScene 등에서 사격 시 원본으로 안 돌아가게)
            BattleStateStorage.SourceScene = SceneManager.GetActiveScene().name;
            Debug.Log($"[CRUX] SaveBattleState — Player at ({playerUnit.GridPosition.x},{playerUnit.GridPosition.y}), return → {BattleStateStorage.SourceScene}");
            var enemyStates = new UnitSaveData[enemyUnits.Count];
            for (int i = 0; i < enemyUnits.Count; i++)
                enemyStates[i] = enemyUnits[i].SaveState();

            // 엄폐물 HP 저장
            var coverHPs = new List<float>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell != null && cell.Type == CellType.Cover && cell.Cover != null)
                        coverHPs.Add(cell.Cover.CurrentHP);
                }

            // 연막 상태 저장
            var smokeTurns = new List<int>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var c = grid.GetCell(new Vector2Int(x, y));
                    smokeTurns.Add(c != null ? c.SmokeTurnsLeft : 0);
                }

            BattleStateStorage.Save(new BattleSaveData
            {
                playerState = playerUnit.SaveState(),
                enemyStates = enemyStates,
                turnCount = turnCount,
                phase = currentPhase,
                coverHPs = coverHPs.ToArray(),
                smokeTurns = smokeTurns.ToArray(),
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
                for (int x = 0; x < grid.Width; x++)
                    for (int y = 0; y < grid.Height; y++)
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

                // 연막 복원
                if (state.smokeTurns != null)
                {
                    int si = 0;
                    for (int x2 = 0; x2 < grid.Width; x2++)
                        for (int y2 = 0; y2 < grid.Height; y2++)
                        {
                            if (si < state.smokeTurns.Length)
                            {
                                var sc = grid.GetCell(new Vector2Int(x2, y2));
                                if (sc != null)
                                {
                                    sc.SmokeTurnsLeft = state.smokeTurns[si];
                                    if (sc.HasSmoke) visualizer.ShowSmoke(sc.Position);
                                }
                                si++;
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
                        // 주포 데미지 — 사전 롤된 결과 적용
                        if (actionData.result.hit && actionData.result.damageDealt > 0)
                        {
                            target.ApplyPrerolledDamage(new DamageInfo
                            {
                                damage = actionData.result.damageDealt,
                                outcome = actionData.result.outcome,
                                hitZone = actionData.result.hitZone
                            }, actionData.mainOutcome);
                        }
                    }
                    else if (actionData.mgResults != null)
                    {
                        // 기총 총 데미지 합산 + 사전 롤된 모듈/화재/격파 적용
                        float total = 0f;
                        bool anyPen = false;
                        HitZone zone = HitZone.Front;
                        foreach (var r in actionData.mgResults)
                        {
                            if (r.hit && r.damageDealt > 0)
                            {
                                total += r.damageDealt;
                                if (r.outcome == ShotOutcome.Penetration) anyPen = true;
                                zone = r.hitZone;
                            }
                        }
                        if (total > 0)
                        {
                            target.ApplyPrerolledDamage(new DamageInfo
                            {
                                damage = total,
                                outcome = anyPen ? ShotOutcome.Penetration : ShotOutcome.Hit,
                                hitZone = zone
                            }, actionData.mgAggregateOutcome);
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
            if (hud != null) hud.Draw();
        }
        /// <summary>모든 셀의 연막 턴 감소</summary>
        private void TickSmoke()
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell != null && cell.SmokeTurnsLeft > 0)
                    {
                        cell.SmokeTurnsLeft--;
                        if (cell.SmokeTurnsLeft <= 0)
                            visualizer.ClearSmoke(cell.Position);
                    }
                }
        }
    }
}
