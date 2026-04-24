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

        [Header("승무원 (Inspector에서 연결 — 순서: Commander, Gunner, Loader, Driver, GunnerMech)")]
        [SerializeField] private CrewMemberSO[] playerCrew = new CrewMemberSO[5];

        [Header("맵 설정")]
        [Tooltip("켜면 12×12 지형 테스트 맵 사용. 꺼져있으면 표준 8×10 맵.")]
        [SerializeField] private bool useTerrainTestMap = false;
        [SerializeField] private int testMapWidth = 12;
        [SerializeField] private int testMapHeight = 12;

        [Header("디버그")]
        [Tooltip("F1 토글 — 각 셀에 지형 라벨 + 마우스 호버 시 상세 정보")]
        [SerializeField] private bool showTerrainDebug = true;

        // 사격 실행 (P-S4에서 추출)
        private Crux.Combat.FireExecutor fireExecutor;

        // 반응 사격 시퀀스 (P-S5에서 추출)
        private Crux.Combat.ReactionFireSequence reactionFireSeq;

        // 화재 사망 처리 (P-S7에서 추출)
        private Crux.Combat.FireKillHandler fireKillHandler;

        // 사기 라우터 (P3-b)
        private Crux.Combat.CombatMoraleRouter moraleRouter;

        // HUD
        private BattleHUD hud;

        // Phase 3 UI 명령 라우터
        private BattleCommandRouter commandRouter;

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
        private enum InputMode { Select, Move, MoveDirectionSelect, Fire, WeaponSelect, RotateMode }
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
        public enum InputModeEnum { Select, Move, MoveDirectionSelect, Fire, WeaponSelect, RotateMode }

        // BattleCommandRouter용 internal setter
        internal void SetInputModeInternal(InputModeEnum mode) => inputMode = (InputMode)(int)mode;
        internal void SetTargetUnitInternal(GridTankUnit t) => targetUnit = t;

        /// <summary>사격 범위 판정용 모든 유닛 반환 (플레이어 + 적) — GridTankUnit.HasAnyEnemyInFireRange() 등에서 사용</summary>
        public List<GridTankUnit> GetAllUnitsForRangeCheck()
        {
            var allUnits = new List<GridTankUnit>();
            if (playerUnit != null)
                allUnits.Add(playerUnit);
            if (enemyUnits != null)
                allUnits.AddRange(enemyUnits);
            return allUnits;
        }

        // ===== 상태 저장/복원 관리자 (P-S6) =====
        private BattleStateManager stateManager;

        // BattleStateManager용 internal 접근자
        internal GridManager GridRef => grid;
        internal GridTankUnit PlayerUnitRef => playerUnit;
        internal List<GridTankUnit> EnemyUnitsRef => enemyUnits;
        internal GridVisualizer VisualizerRef => visualizer;
        internal int TurnCountInternal { get => turnCount; set => turnCount = value; }
        internal TurnPhase CurrentPhaseInternal { get => currentPhase; set => currentPhase = value; }
        internal int CurrentEnemyIndexInternal { get => currentEnemyIndex; set => currentEnemyIndex = value; }
        internal void ReinitializeBattle() => InitializeBattle();
        internal void StartProcessEnemyTurnFrom(int startIdx) => StartCoroutine(ProcessEnemyTurn(startIdx));

        private void Start()
        {
            stateManager = new BattleStateManager(this);
            commandRouter = new BattleCommandRouter(this);

            if (FireActionContext.HasPendingAction)
            {
                // 연출 씬에서 복귀 — 상태 복원 + 데미지 적용
                stateManager.ApplyPendingResult();
            }
            else
            {
                // Hangar에서 편성 데이터 입수 (Scene-3 스켈레톤 — 수신 로그만)
                if (BattleEntryData.HasEntry)
                    UnityEngine.Debug.Log($"[Battle] Hangar 편성 입수: {BattleEntryData.SortieTanks.Count}대 (실 생성은 후속 커밋)");

                // 최초 시작
                InitializeBattle();
            }
        }

        private void InitializeBattle()
        {
            moraleRouter?.Detach();
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

            // Scene-5-Mini: 편성 탱크가 있으면 TankDataSO 복사본에 기본 스탯 주입
            mapSetup.playerTankData = SortieDataBuilder.BuildPlayerTankData(playerTankData) ?? playerTankData;
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

            // Phase 2 API 초기화 — 사격 범위 판정용 컨트롤러 참조 바인딩
            if (playerUnit != null)
                playerUnit.BindBattleController(this);
            foreach (var enemy in enemyUnits)
                if (enemy != null)
                    enemy.BindBattleController(this);

            // 화재 사망 처리 (P-S7: FireKillHandler 초기화)
            fireKillHandler = new Crux.Combat.FireKillHandler(grid, (t, c, d) => hud?.ShowBanner(t, c, d));

            // 화재 사망 이벤트 구독
            if (playerUnit != null)
                playerUnit.OnFireKilled += fireKillHandler.Handle;
            foreach (var e in enemyUnits)
                if (e != null) e.OnFireKilled += fireKillHandler.Handle;

            // 오버워치 트리거 — 적 이동 스텝마다 플레이어측 반응 사격 체크
            // (P-S5 추출: HandleEnemyMoveStep은 ReactionFireSequence로 이동됨, 초기화 이후 구독)

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

            // 사격 실행 초기화 (HUD 이전)
            fireExecutor = new Crux.Combat.FireExecutor(grid, enemyUnits, coaxialMGData, mountedMGData);

            // HUD 초기화
            var hudObj = new GameObject("BattleHUD");
            hud = hudObj.AddComponent<BattleHUD>();
            hud.Initialize(this);

            // CommandBox · TargetCycler 초기화 (Phase 3) — Router에 위임
            commandRouter.SetupCommandBox();
            commandRouter.SetupTargetCycler();

            // 반응 사격 시퀀스 초기화 (P-S5 추출)
            var rfsObj = new GameObject("ReactionFireSequence");
            reactionFireSeq = rfsObj.AddComponent<Crux.Combat.ReactionFireSequence>();
            reactionFireSeq.Initialize(
                grid, battleCam, fireExecutor, playerUnit,
                (text, col, dur) => hud?.ShowBanner(text, col, dur),
                (pos, dur) => hud?.ShowAlert(pos, dur));

            // 오버워치 이벤트 구독 (P-S5: ReactionFireSequence 초기화 이후)
            foreach (var e in enemyUnits)
                if (e != null) e.OnMoveStepComplete += reactionFireSeq.HandleEnemyMoveStep;

            // 승무원 부착 — 모든 유닛에 TankCrew 초기화
            BattleCrewBinder.AttachAll(playerUnit, enemyUnits, playerCrew);

            // 사기 라우터 초기화 (피격 → 사기 이벤트)
            moraleRouter = new Crux.Combat.CombatMoraleRouter();
            moraleRouter.Attach(playerUnit, enemyUnits);

            // 전투 시작 선공 판정
            var firstSide = InitiativeSetup.Resolve(playerUnit, enemyUnits);
            if (firstSide == PlayerSide.Enemy)
                StartEnemyTurn();
            else
                StartPlayerTurn();
        }

        private void Update()
        {
            if (currentPhase == TurnPhase.PlayerTurn)
            {
                inputHandler?.Tick();
            }
            // 방호 arc는 턴 구분 없이 갱신 — 적 턴 중 씬 복귀 시에도 플레이어 엄폐 상태가 보여야 함
            UpdateCoverArcDisplay();
            if (battleCam != null) battleCam.Tick(Crux.Combat.ReactionFireSequence.IsPlaying);
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
                BattleEntryData.LastResult = BattleResult.Defeat;
                return;
            }

            bool allEnemiesDead = true;
            foreach (var e in enemyUnits)
                if (e != null && !e.IsDestroyed) { allEnemiesDead = false; break; }
            if (allEnemiesDead)
            {
                currentPhase = TurnPhase.Victory;
                BattleEntryData.LastResult = BattleResult.Victory;
            }
        }

        /// <summary>플레이어 유닛의 엄폐 커버 범위 표시 (상태 변경 시만 갱신)</summary>
        /// <remarks>
        /// 플레이어 턴: selectedUnit(일반적으로 playerUnit)의 Select/Move 모드에서 표시
        /// 적 턴: 선택 상태 무관하게 playerUnit의 엄폐 상태 계속 표시 (사격 사이 복귀 시 가시성)
        /// P-S7: 판정 로직만 유지, 실제 갱신은 GridVisualizer.UpdateCoverArcFor에 위임
        /// </remarks>
        private void UpdateCoverArcDisplay()
        {
            // 표시 대상 유닛 결정
            GridTankUnit target = null;
            int modeKey = 0;

            if (currentPhase == TurnPhase.PlayerTurn
                && selectedUnit != null && !selectedUnit.IsDestroyed
                && (inputMode == InputMode.Select || inputMode == InputMode.Move))
            {
                target = selectedUnit;
                modeKey = (int)inputMode;
            }
            else if (playerUnit != null && !playerUnit.IsDestroyed)
            {
                // 적 턴이거나 공격 모드 — playerUnit 엄폐 상태 폴백 표시
                target = playerUnit;
                modeKey = 0; // Select 모드와 동등
            }

            visualizer.UpdateCoverArcFor(grid, target, modeKey);
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

            // 연막 턴 감소 (P-S7: GridManager로 이관)
            grid.TickSmoke(visualizer);

            // 플레이어 사기 턴 시작 처리 + AP 페널티
            if (playerUnit != null && !playerUnit.IsDestroyed)
            {
                playerUnit.Crew?.TickTurnStart();
                int apPenalty = playerUnit.Crew != null
                    ? MoraleSystem.TurnApPenalty(playerUnit.Crew.Band)
                    : 0;
                if (apPenalty > 0)
                {
                    playerUnit.DeductAP(apPenalty);
                    Debug.Log($"[CRUX] 플레이어 사기 패널티: -{apPenalty} AP (사기={playerUnit.Crew.Morale})");
                }
            }

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

            // 생존한 적 유닛 사기 턴 시작 처리 + AP 페널티
            foreach (var enemy in enemyUnits)
            {
                if (enemy == null || enemy.IsDestroyed) continue;
                enemy.Crew?.TickTurnStart();
                int apPenalty = enemy.Crew != null
                    ? MoraleSystem.TurnApPenalty(enemy.Crew.Band)
                    : 0;
                if (apPenalty > 0)
                {
                    enemy.DeductAP(apPenalty);
                    Debug.Log($"[CRUX] 적 사기 패널티: -{apPenalty} AP (사기={enemy.Crew.Morale})");
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
                        fireExecutor.Execute(enemy, decision.fireTarget, WeaponType.MainGun);
                        stateManager.Save();
                        SceneManager.LoadScene("FireActionScene");
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

        // ===== 오버워치 (반응 사격) — P-S5에서 ReactionFireSequence로 추출됨 =====

        // ===== PlayerInputHandler 공개 API =====

        public bool CanHandleInput => selectedUnit == null
            || (!selectedUnit.IsDestroyed && !selectedUnit.IsMoving);

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
            commandRouter.InitializeTargetCycler(selectedUnit, enemyUnits);
        }

        public void SelectWeapon(WeaponType weapon)
        {
            if (weapon == WeaponType.MainGun
                || (weapon == WeaponType.CoaxialMG && coaxialMGData != null)
                || (weapon == WeaponType.MountedMG && mountedMGData != null))
                selectedWeapon = weapon;

            // 무기 프리뷰 시 부위 바 깜빡임 표시 (Phase 4)
            if (pendingTarget != null && selectedUnit != null)
                commandRouter.UpdateWeaponPreview(selectedWeapon, pendingTarget, coaxialMGData, mountedMGData);
        }

        public void CommitWeaponSelection()
        {
            commandRouter.ClearWeaponPreview();
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
            Debug.Log($"[TRACE] HandleClickAt gridPos={gridPos} mode={CurrentInputMode}");
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
            fireExecutor.Execute(attacker, target, weapon);
            stateManager.Save();
            SceneManager.LoadScene("FireActionScene");
        }

        /// <summary>Select 모드에서 셀 클릭 — 유닛이면 정보 조회</summary>
        private void InspectCell(Vector2Int pos)
        {
            var cell = grid.GetCell(pos);
            Debug.Log($"[TRACE] InspectCell pos={pos} cell={cell?.Type.ToString() ?? "null"} occupant={cell?.Occupant?.name ?? "null"}");
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
            // 아군 클릭 → selectedUnit 갱신 + CommandBox 표시 / 적군 → inspectedUnit
            inspectedUnit = unit.side == PlayerSide.Player ? null : unit;
            if (unit.side == PlayerSide.Player) { selectedUnit = unit; ShowCommandBox(); }
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


        /// <summary>거리 기반 명중률 — FireExecutor의 wrapper (호환성 유지)</summary>
        public float CalculateHitChance(int distance, GridTankUnit target)
            => fireExecutor.CalculateHitChance(distance, target);

        /// <summary>유닛의 현재 상태 (개활지/엄폐) — FireExecutor의 wrapper (호환성 유지)</summary>
        public string GetUnitCoverStatus(GridTankUnit unit)
            => fireExecutor.GetUnitCoverStatus(unit);


        // ===== Command Box · Target Cycler · Rotate Mode · 무기 프리뷰 (Phase 3/4) — Router 위임 =====

        /// <summary>커맨드 박스 표시</summary>
        public void ShowCommandBox() => commandRouter.ShowCommandBox();

        /// <summary>커맨드 박스 숨김</summary>
        public void HideCommandBox() => commandRouter.HideCommandBox();

        /// <summary>축적된 회전각도 적용 및 RotateMode 종료</summary>
        public void CommitRotation() => commandRouter.CommitRotation();

        /// <summary>RotateMode 취소</summary>
        public void CancelRotateMode() => commandRouter.CancelRotateMode();

        /// <summary>화면 내 회전각 누적 (양수=시계, 음수=반시계)</summary>
        public void AccumulateRotation(float deltaDegrees) => commandRouter.AccumulateRotation(deltaDegrees);

        /// <summary>회전 모드: 누적된 회전각 조회</summary>
        public float GetPendingRotationDelta() => commandRouter.GetPendingRotationDelta();

        /// <summary>Fire 모드: 다음 목표로 순환</summary>
        public void CycleTargetNext() => commandRouter.CycleTargetNext();

        /// <summary>Fire 모드: 이전 목표로 순환</summary>
        public void CycleTargetPrevious() => commandRouter.CycleTargetPrevious();

        /// <summary>목표 유닛의 부위 바 프리뷰 갱신 (외부 호출용 — 시그니처 유지)</summary>
        public void UpdateTargetWeaponPreview(GridTankUnit target, HitZone zone, float expectedDamage)
            => commandRouter.UpdateTargetWeaponPreview(target, zone, expectedDamage);

        // ===== UI (OnGUI) =====

        private void OnGUI()
        {
            if (hud != null) hud.Draw();
        }
    }
}
