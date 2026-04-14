using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Crux.Grid;
using Crux.Unit;
using Crux.Data;
using Crux.Combat;
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

        // 전략맵 배너 (화재 전소 등 이벤트 알림)
        private string bannerText;
        private Color bannerColor;
        private float bannerEndTime;

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

            // 카메라 — 맵 전체가 보이도록 자동 조정 (flat-top hex 실제 치수)
            // 주의: grid.Width/Height를 참조해야 테스트 맵(12×12 등)에도 대응
            if (mainCam != null)
            {
                mainCam.orthographic = true;
                // flat-top hex: width = 1.5*(w-1)*size + 2*size, height = sqrt(3)*(h-0.5)*size + sqrt(3)*size
                float size = GameConstants.CellSize;
                float mapW = size * (1.5f * (grid.Width - 1) + 2f);
                float mapH = size * Mathf.Sqrt(3f) * (grid.Height + 0.5f);
                float aspect = mainCam.aspect;
                float sizeByH = mapH * 0.55f;
                float sizeByW = (mapW * 0.55f) / aspect;
                camTargetSize = Mathf.Max(sizeByH, sizeByW);
                camTargetSize = Mathf.Clamp(camTargetSize, camMinSize, camMaxSize);
                mainCam.orthographicSize = camTargetSize;

                // 맵 중심 (hex 월드 좌표 기반)
                Vector3 bl = HexCoord.OffsetToWorld(new Vector2Int(0, 0), size);
                Vector3 tr = HexCoord.OffsetToWorld(new Vector2Int(grid.Width - 1, grid.Height - 1), size);
                float cx = (bl.x + tr.x) * 0.5f;
                float cy = (bl.y + tr.y) * 0.5f;
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
            }
            // 방호 arc는 턴 구분 없이 갱신 — 적 턴 중 씬 복귀 시에도 플레이어 엄폐 상태가 보여야 함
            UpdateCoverArcDisplay();
            HandleCamera();

            // 지형 디버그 토글
            if (Input.GetKeyDown(KeyCode.F1))
                showTerrainDebug = !showTerrainDebug;
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
                    grid.Width * GameConstants.CellSize + halfW * 0.3f);
                camTargetPos.y = Mathf.Clamp(camTargetPos.y, -halfH * 0.3f,
                    grid.Height * GameConstants.CellSize + halfH * 0.3f);
                camTargetPos.z = -10f;
            }

            // 부드러운 보간
            mainCam.orthographicSize = Mathf.Lerp(mainCam.orthographicSize, camTargetSize, camZoomSpeed * Time.deltaTime);
            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, camTargetPos, camZoomSpeed * Time.deltaTime);

            // UI 스케일 갱신
            uiScale = Mathf.Max(0.5f, Screen.height / 1080f);
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

            // ===== 무기 선택 모드 — 1/2/3은 선택만, Space/Enter/Click으로 확정 =====
            if (inputMode == InputMode.WeaponSelect && pendingTarget != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    selectedWeapon = WeaponType.MainGun;
                else if (Input.GetKeyDown(KeyCode.Alpha2) && coaxialMGData != null)
                    selectedWeapon = WeaponType.CoaxialMG;
                else if (Input.GetKeyDown(KeyCode.Alpha3) && mountedMGData != null)
                    selectedWeapon = WeaponType.MountedMG;

                // Space / Enter / 좌클릭: 현재 선택 무기로 사격 확정
                bool commit = Input.GetKeyDown(KeyCode.Space)
                              || Input.GetKeyDown(KeyCode.Return)
                              || Input.GetMouseButtonDown(0);
                if (commit)
                {
                    CommitFire(selectedUnit, pendingTarget, selectedWeapon);
                    return;
                }
                return; // 무기 선택 중 다른 입력 차단
            }

            // ===== 방향 선택 모드 (6방향) =====
            if (inputMode == InputMode.MoveDirectionSelect)
            {
                // 6방향 육각 매핑 (QWE 상단 + ASD 하단, 2행 키보드 레이아웃)
                //   Q = NW (300°)   W = N  (0°)    E = NE (60°)
                //   A = SW (240°)   S = S  (180°)  D = SE (120°)
                if (Input.GetKeyDown(KeyCode.Q)) pendingFacingAngle = 300f;   // NW
                if (Input.GetKeyDown(KeyCode.W)) pendingFacingAngle = 0f;     // N
                if (Input.GetKeyDown(KeyCode.E)) pendingFacingAngle = 60f;    // NE
                if (Input.GetKeyDown(KeyCode.A)) pendingFacingAngle = 240f;   // SW
                if (Input.GetKeyDown(KeyCode.S)) pendingFacingAngle = 180f;   // S
                if (Input.GetKeyDown(KeyCode.D)) pendingFacingAngle = 120f;   // SE

                // Space / Enter: 방향 확정
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    selectedUnit.MoveToWithFacing(pendingMoveTarget, pendingFacingAngle);
                    visualizer.ClearHighlights();
                    inputMode = InputMode.Select;
                    return;
                }

                // 좌클릭: 클릭 방향을 가장 가까운 6방향으로 스냅
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
                selectedWeapon = WeaponType.MainGun;
                visualizer.ShowFireRange(selectedUnit.GridPosition, GameConstants.MaxFireRange);
            }

            // Fire 모드: 마우스 호버 대상 갱신 + 1/2/3으로 무기 전환
            if (inputMode == InputMode.Fire)
            {
                UpdateHoveredTarget();
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    selectedWeapon = WeaponType.MainGun;
                else if (Input.GetKeyDown(KeyCode.Alpha2) && coaxialMGData != null)
                    selectedWeapon = WeaponType.CoaxialMG;
                else if (Input.GetKeyDown(KeyCode.Alpha3) && mountedMGData != null)
                    selectedWeapon = WeaponType.MountedMG;
            }
            else
            {
                hoveredTarget = null;
            }

            // C: 소화
            if (Input.GetKeyDown(KeyCode.C) && selectedUnit.IsOnFire && inputMode == InputMode.Select)
            {
                selectedUnit.TryExtinguish();
            }

            // V: 연막
            if (Input.GetKeyDown(KeyCode.V) && selectedUnit.CanUseSmoke() && inputMode == InputMode.Select)
            {
                if (selectedUnit.UseSmoke())
                {
                    var cell = grid.GetCell(selectedUnit.GridPosition);
                    if (cell != null)
                    {
                        cell.SmokeTurnsLeft = 2; // 현재 턴 + 다음 턴
                        visualizer.ShowSmoke(selectedUnit.GridPosition);
                    }
                }
            }

            // O: 오버워치 (반응 사격)
            if (Input.GetKeyDown(KeyCode.O) && inputMode == InputMode.Select
                && selectedUnit.CanActivateOverwatch())
            {
                if (selectedUnit.ActivateOverwatch())
                {
                    ShowBanner("⌁ 오버워치 설정 — 전방 50° 내 적 이동 시 반격",
                               new Color(0.4f, 0.9f, 1f), 1.5f);
                }
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
                    case InputMode.Select:
                        InspectCell(clickedPos);
                        break;
                }
            }
        }

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

            // 트리거! — 즉시 반응 사격 (연출 씬 전환 없음)
            ExecuteReactionFire(playerUnit, movingEnemy);
        }

        /// <summary>경량 반응 사격 — 씬 전환 없이 맵 상에서 즉시 데미지 + VFX</summary>
        /// <remarks>오버워치 AP는 활성화 시점에 선지불되었으므로 추가 AP 소모 없음.
        /// 1회 발사 후 isOverwatching 해제. ExecuteFire의 경량 버전 — 엄폐/연출 생략.</remarks>
        private void ExecuteReactionFire(GridTankUnit attacker, GridTankUnit target)
        {
            attacker.ConsumeOverwatchShot();
            if (!attacker.ConsumeMainGunRound())
            {
                Debug.LogWarning("[CRUX] 오버워치 트리거 시점 주포 잔탄 0 — 사격 무시");
                return;
            }

            float hitChance = CalculateHitChanceWithCover(attacker, target);
            bool hit = Random.value <= hitChance;

            Vector3 attackerPos = attacker.transform.position;
            Vector3 targetPos = target.transform.position;
            Vector2 fireDir = ((Vector2)(targetPos - attackerPos)).normalized;

            // 공격자 쪽 머즐 플래시
            MuzzleFlash.Spawn(attackerPos + (Vector3)(fireDir * 0.3f), fireDir);

            if (!hit)
            {
                HitEffects.Spawn(targetPos + (Vector3)(Random.insideUnitCircle * 0.2f),
                                  ShotOutcome.Miss, fireDir);
                ShowBanner($"⌁ 반응 사격 빗나감 — {target.Data?.tankName}",
                           new Color(0.8f, 0.8f, 0.4f), 1.5f);
                Debug.Log($"[CRUX] 오버워치 빗나감");
                return;
            }

            // 명중 — 기존 PenetrationCalculator로 관통 판정
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

            // VFX
            float caliberScale = attacker.currentAmmo != null
                ? HitEffects.CaliberScaleFromAmmoDamage(attacker.currentAmmo.damage)
                : 1f;
            HitEffects.Spawn(targetPos, outcome, fireDir, caliberScale);

            // 데미지 적용 — PreRoll → ApplyPrerolledDamage
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
            ShowBanner($"⌁ 반응 사격 {outcomeLabel}! — {target.Data?.tankName}",
                       new Color(1f, 0.6f, 0.2f), 1.8f);
            Debug.Log($"[CRUX] 오버워치 반격: {outcomeLabel} dmg={finalDmg:F0}");
        }

        /// <summary>전략맵 상단 배너 — duration 초 동안 표시</summary>
        private void ShowBanner(string text, Color color, float duration)
        {
            bannerText = text;
            bannerColor = color;
            bannerEndTime = Time.time + duration;
        }

        private void DrawBanner()
        {
            if (string.IsNullOrEmpty(bannerText)) return;
            float remaining = bannerEndTime - Time.time;
            if (remaining <= 0)
            {
                bannerText = null;
                return;
            }

            float alpha = Mathf.Clamp01(remaining / 0.6f);
            float bw = 520, bh = 56;
            float bx = ScaledW / 2 - bw / 2;
            float by = 70;

            var bg = new GUIStyle(GetBoxStyle());
            bg.normal.background = GetTex(new Color(0, 0, 0, 0.75f * alpha));
            GUI.Box(new Rect(bx, by, bw, bh), "", bg);

            var s = new GUIStyle(GetLabelStyle());
            s.fontSize = 26;
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha);
            GUI.Label(new Rect(bx, by, bw, bh), bannerText, s);
        }

        /// <summary>Fire 모드: 마우스 위치의 적을 hoveredTarget에 기록</summary>
        private void UpdateHoveredTarget()
        {
            if (mainCam == null) return;
            var worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
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
            var clickWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
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

        private float CalculateHitChance(int distance, GridTankUnit target)
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
            // UI 스케일링 — 모든 좌표/크기에 자동 적용
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            DrawTurnInfo();
            DrawUnitInfo();
            DrawModuleStatus();
            DrawInputModeInfo();
            DrawControls();
            DrawBanner();
            DrawGameResult();

            if (showTerrainDebug)
            {
                DrawTerrainOverlay();
                DrawTerrainHoverInfo();
            }

            GUI.matrix = prevMatrix;
        }

        // ===== 지형 디버그 오버레이 =====

        private static string TerrainLetter(TerrainType t) => t switch
        {
            TerrainType.Road             => "로",
            TerrainType.Mud              => "진",
            TerrainType.Woods            => "숲",
            TerrainType.Rubble           => "편",
            TerrainType.Crater           => "탄",
            TerrainType.Hill             => "언",
            TerrainType.Building         => "건",
            TerrainType.ElevatedBuilding => "고",
            TerrainType.Water            => "물",
            _ => ""
        };

        private static Color TerrainLabelColor(TerrainType t) => t switch
        {
            TerrainType.Road             => new Color(0.95f, 0.95f, 0.75f),
            TerrainType.Mud              => new Color(0.95f, 0.75f, 0.45f),
            TerrainType.Woods            => new Color(0.50f, 1.00f, 0.50f),
            TerrainType.Rubble           => new Color(0.90f, 0.80f, 0.65f),
            TerrainType.Crater           => new Color(1.00f, 0.80f, 0.40f),
            TerrainType.Hill             => new Color(1.00f, 0.95f, 0.50f),
            TerrainType.Building         => new Color(0.65f, 0.75f, 1.00f),
            TerrainType.ElevatedBuilding => new Color(1.00f, 0.70f, 1.00f),
            TerrainType.Water            => new Color(0.50f, 0.80f, 1.00f),
            _ => Color.white
        };

        /// <summary>F1 토글: 각 셀에 지형 한 글자 라벨 그리기 (Open 제외)</summary>
        private void DrawTerrainOverlay()
        {
            if (grid == null || mainCam == null) return;

            var style = new GUIStyle();
            style.fontSize = 13;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cell = grid.GetCell(new Vector2Int(x, y));
                    if (cell == null || cell.Terrain == TerrainType.Open) continue;

                    Vector3 world = grid.GridToWorld(new Vector2Int(x, y));
                    Vector3 screen = mainCam.WorldToScreenPoint(world);
                    if (screen.z < 0) continue;

                    float sx = screen.x / uiScale;
                    float sy = (Screen.height - screen.y) / uiScale;
                    var rect = new Rect(sx - 13, sy - 10, 26, 20);

                    var prev = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.7f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = prev;

                    style.normal.textColor = TerrainLabelColor(cell.Terrain);
                    GUI.Label(rect, TerrainLetter(cell.Terrain), style);
                }
            }
        }

        /// <summary>마우스가 올라간 셀의 지형 상세 정보 박스 — 화면 상단 중앙</summary>
        private void DrawTerrainHoverInfo()
        {
            if (grid == null || mainCam == null) return;

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cellPos = grid.WorldToGrid(mouseWorld);
            if (!grid.IsInBounds(cellPos)) return;

            var cell = grid.GetCell(cellPos);
            if (cell == null) return;

            var terrain = cell.Terrain;
            int moveCost = TerrainData.MoveCost(terrain);
            int elev = TerrainData.Elevation(terrain);
            int concealment = TerrainData.Concealment(terrain);
            float intrinsicCov = TerrainData.IntrinsicCoverRate(terrain);
            bool groundPass = TerrainData.GroundPassable(terrain);
            bool blocksLOS = TerrainData.BlocksLOS(terrain);
            string moveCostStr = moveCost == int.MaxValue ? "∞" : moveCost.ToString();

            float boxW = 260;
            float boxH = 112;
            float bx = (ScaledW - boxW) * 0.5f;
            float by = 50;

            GUI.Box(new Rect(bx, by, boxW, boxH), "", GetBoxStyle());

            var title = new GUIStyle(GetLabelStyle());
            title.fontSize = 15;
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = TerrainLabelColor(terrain);
            GUI.Label(new Rect(bx + 12, by + 6, boxW - 24, 20),
                $"{TerrainData.Label(terrain)} @ ({cellPos.x},{cellPos.y})", title);

            var row = new GUIStyle(GetLabelStyle());
            row.fontSize = 13;
            row.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            float ly = by + 28;
            GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                $"이동 {moveCostStr} AP  |  고도 {(elev >= 0 ? "+" : "")}{elev}", row);
            ly += 17;
            GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                $"은엄폐 {concealment}%  |  지형엄폐 {intrinsicCov:P0}", row);
            ly += 17;
            string pass = groundPass ? "지상 통과" : "지상 차단";
            string los = blocksLOS ? "LOS 차단" : "LOS 통과";
            GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18), $"{pass}  |  {los}", row);

            ly += 17;
            if (cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                var covRow = new GUIStyle(row);
                covRow.normal.textColor = new Color(1f, 0.8f, 0.4f);
                GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                    $"엄폐물: {cell.Cover.coverName} ({cell.Cover.CoverRate:P0})", covRow);
            }
            else if (cell.HasSmoke)
            {
                var sm = new GUIStyle(row);
                sm.normal.textColor = new Color(0.7f, 0.9f, 1f);
                GUI.Label(new Rect(bx + 12, ly, boxW - 24, 18),
                    $"연막 {cell.SmokeTurnsLeft}턴 잔존", sm);
            }
        }

        /// <summary>스케일 보정된 Screen 크기 (OnGUI 내에서 사용)</summary>
        private float ScaledW => Screen.width / uiScale;
        private float ScaledH => Screen.height / uiScale;

        private void DrawTurnInfo()
        {
            string phaseKR = currentPhase switch
            {
                TurnPhase.PlayerTurn => "플레이어",
                TurnPhase.EnemyTurn => "적 턴",
                TurnPhase.Cinematic => "연출 중",
                TurnPhase.GameOver => "패배",
                TurnPhase.Victory => "승리",
                _ => currentPhase.ToString()
            };
            var tstyle = new GUIStyle(GetLabelStyle());
            tstyle.fontSize = 20;
            tstyle.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 240, 35), "", GetBoxStyle());
            GUI.Label(new Rect(15, 14, 230, 25),
                $"턴 {turnCount}  |  {phaseKR}", tstyle);
        }

        private void DrawUnitInfo()
        {
            // 조회 대상: 적을 클릭했으면 적, 아니면 선택된 아군
            var info = inspectedUnit != null && !inspectedUnit.IsDestroyed
                       ? inspectedUnit : selectedUnit;
            if (info != null)
                DrawUnitInfoPanel(10f, 55f, info);

            // Fire 모드 호버 / WeaponSelect 모드 → 사격 프리뷰
            GridTankUnit previewTarget = null;
            if (inputMode == InputMode.WeaponSelect && pendingTarget != null)
                previewTarget = pendingTarget;
            else if (inputMode == InputMode.Fire && hoveredTarget != null)
                previewTarget = hoveredTarget;

            if (previewTarget != null)
                DrawFireTargetPreview(previewTarget, selectedWeapon);
        }

        /// <summary>통합 유닛 정보 패널 — 아군/적 동일 레이아웃 (조회용, 프리뷰 제외)</summary>
        private void DrawUnitInfoPanel(float x, float y, GridTankUnit u)
        {
            float w = 350, h = 180;
            GUI.Box(new Rect(x, y, w, h), "", GetBoxStyle());

            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 17;

            float lineH = 20f;
            float cy = y + 5;
            float cx = x + 10;
            float innerW = w - 20;

            // 1행: 이름 + 전차타입 + 위치·방향
            string cls = GetTankClassLabel(u.Data != null ? u.Data.tankClass : TankClass.Medium);
            string facing = GetCompassLabel(u.HullAngle);
            string sideTag = u.side == PlayerSide.Player ? "" : "[적] ";
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"{sideTag}{u.Data?.tankName}  ({cls})", style);
            var posStyle = new GUIStyle(style);
            posStyle.alignment = TextAnchor.UpperRight;
            posStyle.fontSize = 15;
            posStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"({u.GridPosition.x},{u.GridPosition.y}) {facing}", posStyle);
            cy += lineH;

            // 2행: HP / AP
            float hpRatio = u.Data != null && u.Data.maxHP > 0 ? u.CurrentHP / u.Data.maxHP : 0f;
            var hpColor = hpRatio > 0.6f ? new Color(0.4f, 1f, 0.5f)
                       : hpRatio > 0.3f ? new Color(1f, 0.9f, 0.2f)
                                        : new Color(1f, 0.3f, 0.2f);
            var hpStyle = new GUIStyle(style);
            hpStyle.normal.textColor = hpColor;
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"HP {u.CurrentHP:F0}/{u.Data?.maxHP}   AP {u.CurrentAP}/{u.MaxAP}", hpStyle);
            cy += lineH;

            // 3행: 이동 거리 (셀, 패널티 반영)
            int moveCost = u.GetMoveCostPerCell();
            int moveCells = moveCost > 0 ? u.CurrentAP / moveCost : 0;
            int baseMove = GameConstants.MoveCostPerCell;
            int penalty = moveCost - baseMove;
            string penStr = penalty > 0 ? $"-{penalty}" : "";
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"이동 {moveCells}셀 (비용 {moveCost}/셀{penStr})", style);
            cy += lineH;

            // 4행: 주포 — {caliber}mm {AmmoCode} {count}/{max}
            int cal = u.Data != null ? u.Data.mainGunCaliber : 0;
            string ammoCode = u.currentAmmo != null
                ? (!string.IsNullOrEmpty(u.currentAmmo.shortCode) ? u.currentAmmo.shortCode : u.currentAmmo.ammoName)
                : "-";
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"주포 {cal}mm  {ammoCode} {u.MainGunAmmoCount}/{u.MaxMainGunAmmo}", style);
            cy += lineH;

            // 5행: 기관총 — {caliber}mm {loaded}/{total}
            if (u.MGAmmoTotal > 0)
            {
                float mgCal = coaxialMGData != null ? coaxialMGData.caliber : 7.92f;
                GUI.Label(new Rect(cx, cy, innerW, lineH),
                    $"MG {mgCal:0.##}mm  {u.MGAmmoLoaded}/{u.MGAmmoTotal}", style);
            }
            else
            {
                GUI.Label(new Rect(cx, cy, innerW, lineH), "MG —", style);
            }
            cy += lineH;

            // 6행: 엄폐 상태 (이름(크기), 방호범위 N/E, -명중보정)
            var cell = grid.GetCell(u.GridPosition);
            var coverStyle = new GUIStyle(style);
            coverStyle.fontSize = 16;
            if (cell != null && cell.HasCover && cell.Cover != null && !cell.Cover.IsDestroyed)
            {
                var cov = cell.Cover;
                string sizeLabel = cov.size switch
                {
                    CoverSize.Small => "소",
                    CoverSize.Medium => "중",
                    CoverSize.Large => "대",
                    _ => ""
                };
                string dirs = GetFacetLabel(cov.CurrentFacets);
                int hitPenalty = Mathf.RoundToInt(cov.CoverRate * 30f); // 명중률 보정치 %
                coverStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
                GUI.Label(new Rect(cx, cy, innerW, lineH),
                    $"엄폐 {cov.coverName}({sizeLabel}) {dirs}  명중-{hitPenalty}%", coverStyle);
            }
            else
            {
                coverStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
                GUI.Label(new Rect(cx, cy, innerW, lineH), "엄폐 — 개활지", coverStyle);
            }
            cy += lineH;

            // 7행: 상태이상
            string statusExtra = "";
            if (u.IsOnFire) statusExtra += "[화재] ";
            if (u.RemainingSmokeCharges > 0) statusExtra += $"연막:{u.RemainingSmokeCharges} ";
            if (cell != null && cell.HasSmoke) statusExtra += "[연막중] ";
            if (u.IsOverwatching) statusExtra += "[⌁반응대기]";
            if (statusExtra.Length == 0) statusExtra = "정상";
            var stStyle = new GUIStyle(style);
            stStyle.fontSize = 15;
            stStyle.normal.textColor = u.IsOnFire ? new Color(1f, 0.5f, 0.2f) : new Color(0.8f, 0.85f, 0.9f);
            GUI.Label(new Rect(cx, cy, innerW, lineH), $"상태: {statusExtra}", stStyle);
        }

        // ===== 사격 프리뷰 =====

        private struct FirePreview
        {
            public int distance;
            public float baseHit;      // 거리 패널티 적용 후
            public float coverPenalty; // 엄폐에 의한 차감
            public float smokePenalty; // 연막에 의한 차감
            public float finalHit;
            public HitZone hitZone;
            public float baseArmor;
            public float impactAngle;
            public float effectiveArmor;
            public float penetration;
            public ShotOutcome outcome;
            public float expectedDamagePerShot; // 판정 반영 데미지 (명중 시)
            public int shotsPerAction;           // 주포 1, 버스트 N
            public float totalExpected;          // shotsPerAction × finalHit × damagePerShot
            public bool coveredFromThisAngle;    // 현재 공격각에서 엄폐 유효
            public bool isMG;
        }

        /// <summary>선택 무기 기준 사격 결과 기대값 계산</summary>
        private FirePreview ComputeFirePreview(GridTankUnit attacker, GridTankUnit target, WeaponType weapon)
        {
            var p = new FirePreview();
            p.distance = grid.GetDistance(attacker.GridPosition, target.GridPosition);

            // 명중률 분해
            float chance = CalculateHitChance(p.distance, target);
            chance -= attacker.Modules.GetAccuracyPenalty();

            // 지형 고도 차 (공격자 > 목표면 보너스)
            var aCell = grid.GetCell(attacker.GridPosition);
            var tCell = grid.GetCell(target.GridPosition);
            if (aCell != null && tCell != null)
            {
                int elevDelta = TerrainData.Elevation(aCell.Terrain)
                              - TerrainData.Elevation(tCell.Terrain);
                if (elevDelta > 0) chance += elevDelta * 0.05f;
            }
            p.baseHit = Mathf.Clamp01(chance);

            // 엄폐 보정 — 엄폐물 + 지형 고유 엄폐 합산
            p.coverPenalty = 0f;
            if (tCell != null && tCell.HasCover && tCell.Cover != null && !tCell.Cover.IsDestroyed)
            {
                var atkDir = HexCoord.AttackDir(attacker.GridPosition, target.GridPosition, GameConstants.CellSize);
                if (tCell.Cover.IsCovered(atkDir))
                {
                    p.coveredFromThisAngle = true;
                    p.coverPenalty = tCell.Cover.CoverRate * 0.3f;
                }
            }
            if (tCell != null)
            {
                float intrinsic = TerrainData.IntrinsicCoverRate(tCell.Terrain);
                if (intrinsic > 0f) p.coverPenalty += intrinsic * 0.3f;
            }

            // 은엄폐 (수풀·파편)
            int concealmentPct = tCell != null ? TerrainData.Concealment(tCell.Terrain) : 0;
            float concealmentPenalty = concealmentPct * 0.01f;

            // 연막 보정
            p.smokePenalty = (tCell != null && tCell.HasSmoke) ? 0.4f : 0f;
            // 은엄폐를 연막 페널티에 합산 (별도 필드 없이 기존 구조 유지)
            p.smokePenalty += concealmentPenalty;

            // 기총 기본 명중률 보정
            if (weapon == WeaponType.CoaxialMG && coaxialMGData != null)
            {
                p.baseHit = Mathf.Clamp01(p.baseHit + coaxialMGData.accuracyModifier
                                         - attacker.Modules.GetMGAccuracyPenalty());
            }
            else if (weapon == WeaponType.MountedMG && mountedMGData != null)
            {
                p.baseHit = Mathf.Clamp01(p.baseHit + mountedMGData.accuracyModifier
                                         - attacker.Modules.GetMGAccuracyPenalty());
            }

            p.finalHit = Mathf.Clamp01(p.baseHit - p.coverPenalty - p.smokePenalty);

            // 피격 위치·장갑 — 현재 위치 기준
            p.hitZone = PenetrationCalculator.DetermineHitZone(
                attacker.transform.position, target.transform.position, target.HullAngle);
            p.baseArmor = PenetrationCalculator.GetBaseArmor(target.Data.armor, p.hitZone);
            p.impactAngle = PenetrationCalculator.CalculateImpactAngleFromPositions(
                attacker.transform.position, target.transform.position, target.HullAngle, p.hitZone);
            p.effectiveArmor = PenetrationCalculator.CalculateEffectiveArmor(p.baseArmor, p.impactAngle);

            // 관통력 / 데미지 — 무기에 따라
            float basePenetration;
            float baseDamage;
            if (weapon == WeaponType.MainGun)
            {
                basePenetration = attacker.currentAmmo != null ? attacker.currentAmmo.penetration : 100f;
                baseDamage = attacker.currentAmmo != null ? attacker.currentAmmo.damage : 10f;
                p.shotsPerAction = 1;
                p.isMG = false;

                // 거리 감쇠
                if (attacker.currentAmmo != null && attacker.currentAmmo.penetrationDropPerCell > 0)
                    basePenetration = Mathf.Max(1f, basePenetration - attacker.currentAmmo.penetrationDropPerCell * p.distance);
            }
            else
            {
                var mg = weapon == WeaponType.CoaxialMG ? coaxialMGData : mountedMGData;
                basePenetration = mg != null ? mg.penetration : 15f;
                baseDamage = mg != null ? mg.damagePerShot : 2f;
                p.shotsPerAction = mg != null
                    ? Mathf.Max(1, mg.burstCount - attacker.Modules.GetBurstPenalty())
                    : 1;
                p.isMG = true;
            }

            p.penetration = basePenetration;

            // 판정 예측 — 확률 경계는 기대값으로 대체 (ratio 기반 결정론 표기)
            p.outcome = PredictOutcome(basePenetration, p.effectiveArmor);

            // 데미지 계산 (판정별)
            float outcomeMult = p.outcome switch
            {
                ShotOutcome.Penetration => p.isMG ? 2f : 2.5f,
                ShotOutcome.Hit => 1f,
                ShotOutcome.Ricochet => 0.03f,
                _ => 0f
            };
            p.expectedDamagePerShot = baseDamage * outcomeMult;
            p.totalExpected = p.shotsPerAction * p.finalHit * p.expectedDamagePerShot;
            return p;
        }

        /// <summary>결정론적 판정 예측 — JudgePenetration의 확률 구간을 단일값으로</summary>
        private static ShotOutcome PredictOutcome(float penetration, float effectiveArmor)
        {
            if (effectiveArmor >= float.MaxValue) return ShotOutcome.Ricochet;
            float ratio = penetration / effectiveArmor;
            if (ratio > 1.2f) return ShotOutcome.Penetration;
            if (ratio > 0.8f) return ShotOutcome.Hit;
            return ShotOutcome.Ricochet;
        }

        /// <summary>사격 프리뷰 패널</summary>
        private void DrawFireTargetPreview(GridTankUnit target, WeaponType weapon)
        {
            if (target == null || target.IsDestroyed || selectedUnit == null) return;

            var p = ComputeFirePreview(selectedUnit, target, weapon);

            float w = 360, h = 260;
            float x = ScaledW - w - 10;
            float y = 55;
            GUI.Box(new Rect(x, y, w, h), "", GetBoxStyle());

            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 17;
            float cx = x + 10;
            float cy = y + 6;
            float lineH = 20f;
            float innerW = w - 20;

            // 제목
            var titleStyle = new GUIStyle(style);
            titleStyle.fontSize = 19;
            titleStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
            string cls = GetTankClassLabel(target.Data != null ? target.Data.tankClass : TankClass.Medium);
            GUI.Label(new Rect(cx, cy, innerW, 22),
                $"대상: {target.Data?.tankName}  [{cls}]", titleStyle);
            cy += 24;

            // HP + 예상 결과
            float hpRatio = target.Data != null && target.Data.maxHP > 0
                            ? target.CurrentHP / target.Data.maxHP : 0f;
            var hpColor = hpRatio > 0.6f ? new Color(0.4f, 1f, 0.5f)
                       : hpRatio > 0.3f ? new Color(1f, 0.9f, 0.2f)
                                        : new Color(1f, 0.3f, 0.2f);
            var hpStyle = new GUIStyle(style);
            hpStyle.normal.textColor = hpColor;
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"HP  {target.CurrentHP:F0}/{target.Data?.maxHP}", hpStyle);
            cy += lineH;

            // 구분선 — 거리·명중률
            var sepStyle = new GUIStyle(style);
            sepStyle.normal.textColor = new Color(0.6f, 0.6f, 0.7f);
            GUI.Label(new Rect(cx, cy, innerW, lineH), "────────────────────────", sepStyle);
            cy += lineH;

            // 거리
            GUI.Label(new Rect(cx, cy, innerW, lineH), $"거리  {p.distance}셀", style);
            cy += lineH;

            // 명중률 + 분해
            var hitStyle = new GUIStyle(style);
            hitStyle.fontSize = 18;
            hitStyle.normal.textColor = new Color(1f, 0.95f, 0.4f);
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"명중률  {p.finalHit:P0}", hitStyle);
            cy += lineH;

            var breakStyle = new GUIStyle(style);
            breakStyle.fontSize = 14;
            breakStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            string brk = $"   기본 {p.baseHit:P0}";
            if (p.coverPenalty > 0) brk += $"  −엄폐 {(p.coverPenalty * 100f):F0}%";
            if (p.smokePenalty > 0) brk += $"  −연막 {(p.smokePenalty * 100f):F0}%";
            GUI.Label(new Rect(cx, cy, innerW, 18), brk, breakStyle);
            cy += 18;

            // 피격 위치·장갑 (6섹터)
            string zoneLabel = p.hitZone switch
            {
                HitZone.Front => "전면",
                HitZone.FrontRight => "우전",
                HitZone.RearRight => "우후",
                HitZone.Rear => "후면",
                HitZone.RearLeft => "좌후",
                HitZone.FrontLeft => "좌전",
                HitZone.Turret => "포탑",
                _ => ""
            };
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"피격  {zoneLabel}  장갑 {p.baseArmor:F0}mm (유효 {p.effectiveArmor:F0}mm)", style);
            cy += lineH;

            // 관통력 vs 장갑 + 판정
            string outcomeLabel; Color outcomeColor;
            switch (p.outcome)
            {
                case ShotOutcome.Penetration:
                    outcomeLabel = "관통"; outcomeColor = new Color(0.4f, 1f, 0.5f); break;
                case ShotOutcome.Hit:
                    outcomeLabel = "명중"; outcomeColor = new Color(1f, 0.95f, 0.3f); break;
                case ShotOutcome.Ricochet:
                    outcomeLabel = "도탄"; outcomeColor = new Color(1f, 0.4f, 0.3f); break;
                default:
                    outcomeLabel = "실패"; outcomeColor = Color.gray; break;
            }
            var resStyle = new GUIStyle(style);
            resStyle.normal.textColor = outcomeColor;
            resStyle.fontSize = 18;
            GUI.Label(new Rect(cx, cy, innerW, lineH),
                $"관통력 {p.penetration:F0}mm  →  {outcomeLabel}", resStyle);
            cy += lineH;

            // 예상 피해
            string dmgLine;
            if (p.isMG)
                dmgLine = $"예상 피해  {p.totalExpected:F0}  ({p.shotsPerAction}발 기준)";
            else
                dmgLine = $"예상 피해  {p.expectedDamagePerShot:F0}  (명중 시)";
            var dmgStyle = new GUIStyle(style);
            dmgStyle.fontSize = 18;
            GUI.Label(new Rect(cx, cy, innerW, lineH), dmgLine, dmgStyle);
            cy += lineH;

            // 사격 후 예상 HP
            float remainHP = Mathf.Max(0f, target.CurrentHP - p.totalExpected);
            bool kill = remainHP <= 0f && p.finalHit > 0.01f;
            var afterStyle = new GUIStyle(style);
            afterStyle.fontSize = 16;
            afterStyle.normal.textColor = kill ? new Color(1f, 0.3f, 0.3f)
                                                : (remainHP / Mathf.Max(1f, target.Data.maxHP) < 0.5f
                                                    ? new Color(1f, 0.7f, 0.3f)
                                                    : new Color(0.85f, 0.85f, 0.9f));
            string afterLine = kill
                ? $"사격 후  {target.CurrentHP:F0} → 0  (격파)"
                : $"사격 후  {target.CurrentHP:F0} → {remainHP:F0}";
            GUI.Label(new Rect(cx, cy, innerW, lineH), afterLine, afterStyle);
            cy += lineH;

            // 엄폐
            var coverStyle = new GUIStyle(style);
            coverStyle.fontSize = 15;
            if (p.coveredFromThisAngle)
            {
                var tc = grid.GetCell(target.GridPosition);
                var cv = tc.Cover;
                string sz = cv.size switch
                {
                    CoverSize.Small => "소",
                    CoverSize.Medium => "중",
                    CoverSize.Large => "대",
                    _ => ""
                };
                string dirs = GetFacetLabel(cv.CurrentFacets);
                coverStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
                GUI.Label(new Rect(cx, cy, innerW, lineH),
                    $"엄폐 {cv.coverName}({sz}) {dirs}  유효", coverStyle);
            }
            else
            {
                coverStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
                GUI.Label(new Rect(cx, cy, innerW, lineH), "엄폐  현재 각도에 무효", coverStyle);
            }
        }

        /// <summary>전차 분류 라벨</summary>
        private static string GetTankClassLabel(TankClass cls) => cls switch
        {
            TankClass.Vehicle => "차량",
            TankClass.Light => "경전차",
            TankClass.Medium => "중형전차",
            TankClass.Heavy => "중전차",
            _ => ""
        };

        /// <summary>나침반 각도 → N/NE/E/SE/S/SW/W/NW</summary>
        private static string GetCompassLabel(float compassAngle)
        {
            float a = ((compassAngle % 360f) + 360f) % 360f;
            int idx = Mathf.RoundToInt(a / 45f) % 8;
            return idx switch
            {
                0 => "↑N",
                1 => "↗NE",
                2 => "→E",
                3 => "↘SE",
                4 => "↓S",
                5 => "↙SW",
                6 => "←W",
                7 => "↖NW",
                _ => ""
            };
        }

        /// <summary>6면 방호 플래그 → "북/북동/남동" 식 라벨</summary>
        private static string GetFacetLabel(HexFacet facets)
        {
            if (facets == HexFacet.None) return "—";
            var list = new List<string>();
            foreach (var d in facets.Enumerate())
                list.Add(HexCoord.DirLabel(d));
            return string.Join("/", list);
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

            float iw = 360;
            GUI.Box(new Rect(ScaledW / 2 - iw / 2, ScaledH - 42, iw, 32), "", GetBoxStyle());
            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 17;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(ScaledW / 2 - iw / 2, ScaledH - 42, iw, 32), modeText, style);
        }

        private void DrawMoveDirectionUI()
        {
            var box = GetBoxStyle();
            var label = new GUIStyle(GetLabelStyle());
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;

            float panelW = 440, panelH = 96;
            float px = ScaledW / 2 - panelW / 2;
            float py = ScaledH / 2 + 60;

            GUI.Box(new Rect(px, py, panelW, panelH), "", box);

            // 6방향 라벨 (60° 단위, QWE/ASD 매핑)
            string dirName = pendingFacingAngle switch
            {
                0f => "↑ 북 (W)",
                60f => "↗ 북동 (E)",
                120f => "↘ 남동 (D)",
                180f => "↓ 남 (S)",
                240f => "↙ 남서 (A)",
                300f => "↖ 북서 (Q)",
                _ => $"{pendingFacingAngle:F0}°"
            };

            GUI.Label(new Rect(px, py + 5, panelW, 22),
                $"목적지: ({pendingMoveTarget.x},{pendingMoveTarget.y})  AP: {pendingMoveCost}", label);
            GUI.Label(new Rect(px, py + 30, panelW, 22),
                $"방향: {dirName}", label);
            var hintStyle = new GUIStyle(label);
            hintStyle.fontSize = 16;
            hintStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            GUI.Label(new Rect(px, py + 60, panelW, 22),
                "[Space] 확정   [클릭] 방향+확정   [Tab] 취소", hintStyle);
        }

        private void DrawWeaponSelectUI()
        {
            var box = GetBoxStyle();
            var label = GetLabelStyle();

            // 무기 선택 패널
            float panelW = 420, panelH = 138;
            float px = ScaledW / 2 - panelW / 2;
            float py = ScaledH / 2 + 40;

            GUI.Box(new Rect(px, py, panelW, panelH), "", box);

            var titleStyle = new GUIStyle(label);
            titleStyle.fontSize = 21;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(px, py + 5, panelW, 22), $"무기 선택 — 대상: {pendingTarget.Data.tankName}", titleStyle);

            var normalCol = new Color(0.75f, 0.75f, 0.8f);
            var selCol = new Color(1f, 0.95f, 0.3f);

            void DrawItem(int slot, WeaponType wt, string text)
            {
                var st = new GUIStyle(label);
                st.fontSize = 18;
                st.normal.textColor = (selectedWeapon == wt) ? selCol : normalCol;
                string mark = (selectedWeapon == wt) ? "▶ " : "  ";
                GUI.Label(new Rect(px + 10, py + 30 + (slot - 1) * 23, panelW - 20, 20),
                    $"{mark}[{slot}] {text}", st);
            }

            // 1. 주포
            string mainAmmo = selectedUnit.currentAmmo != null
                ? (!string.IsNullOrEmpty(selectedUnit.currentAmmo.shortCode)
                    ? selectedUnit.currentAmmo.shortCode : selectedUnit.currentAmmo.ammoName)
                : "AP";
            int mainCal = selectedUnit.Data != null ? selectedUnit.Data.mainGunCaliber : 0;
            DrawItem(1, WeaponType.MainGun,
                $"주포 {mainCal}mm {mainAmmo}  AP:{GameConstants.FireCost}");

            // 2. 동축 기관총
            if (coaxialMGData != null)
                DrawItem(2, WeaponType.CoaxialMG,
                    $"{coaxialMGData.mgName} {coaxialMGData.burstCount}발  AP:{coaxialMGData.apCost}");

            // 3. 탑재 기관총
            if (mountedMGData != null)
                DrawItem(3, WeaponType.MountedMG,
                    $"{mountedMGData.mgName} {mountedMGData.burstCount}발  AP:{mountedMGData.apCost}");

            var hintStyle = new GUIStyle(label);
            hintStyle.fontSize = 15;
            hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            hintStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(px, py + 110, panelW, 18),
                "1/2/3: 선택   Space/클릭: 사격   Tab: 취소", hintStyle);
        }

        private void DrawModuleStatus()
        {
            // 정보 패널과 동일한 유닛 기준 (적 클릭 시 적 모듈 상태)
            var u = inspectedUnit != null && !inspectedUnit.IsDestroyed
                    ? inspectedUnit : selectedUnit;
            if (u == null || u.IsDestroyed) return;

            var modules = u.Modules;
            float panelX = 10f, panelY = 240f, panelW = 350f, panelH = 58f;

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
            float y2 = panelY + 3;
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

                GUI.Label(new Rect(x + col * 85, y2, 80, 16), $"{name}{stateChar}", style);
                col++;
                if (col >= 4)
                {
                    col = 0;
                    y2 += 17;
                }
            }
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

        private void DrawControls()
        {
            var style = new GUIStyle(GetLabelStyle());
            style.fontSize = 16;
            style.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            GUI.Box(new Rect(10, ScaledH - 115, 340, 98), "", GetBoxStyle());
            GUI.Label(new Rect(15, ScaledH - 112, 330, 18), "Q: 이동  |  E: 사격  |  Tab: 취소", style);
            GUI.Label(new Rect(15, ScaledH - 93, 330, 18), "Space: 확정/턴종료  |  1/2/3: 무기", style);
            GUI.Label(new Rect(15, ScaledH - 74, 330, 18), "C: 소화  |  V: 연막", style);
            GUI.Label(new Rect(15, ScaledH - 55, 330, 18),
                      $"O: 오버워치 (AP -{GameConstants.OverwatchCost})", style);
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

                var bigStyle = new GUIStyle(GetLabelStyle());
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
