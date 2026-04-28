using System;
using System.Collections.Generic;
using UnityEngine;
using Crux.Data;
using Crux.Unit;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crux.UI.Deployment
{
    /// <summary>
    /// 편성 씬(Crew Deployment)의 중앙 오케스트레이터.
    /// 보유 전차·승무원 풀 관리 + 5슬롯별 배치 + 사기 미리보기 + 저장.
    /// </summary>
    public class CrewDeploymentController : MonoBehaviour
    {
        [SerializeField] private List<TankDataSO> ownedTanks = new();
        private List<CrewMemberSO> roster = new();
        private int selectedTankIndex = 0;

        // 각 전차별 5슬롯 배치 상태 — Dictionary<CrewClass, CrewMemberSO>
        private List<Dictionary<CrewClass, CrewMemberSO>> tanksDeployment = new();

        public IReadOnlyList<TankDataSO> OwnedTanks => ownedTanks;
        public IReadOnlyList<CrewMemberSO> Roster => roster;
        public int SelectedTankIndex => selectedTankIndex;
        public TankDataSO SelectedTank => selectedTankIndex >= 0 && selectedTankIndex < ownedTanks.Count ? ownedTanks[selectedTankIndex] : null;

        public event Action OnTankSelectionChanged;
        public event Action OnAssignmentChanged;

        private void Awake()
        {
            Debug.Log("[CRUX] CrewDeploymentController.Awake called");
            InitializeData();
        }

        /// <summary>보유 전차·승무원 풀 로드</summary>
        public void InitializeData()
        {
            // 임시: 리소스 로드 (추후 PlayerProfile 연동)
            LoadOwnedTanks();
            LoadRoster();

            // 각 전차별 배치 딕셔너리 초기화
            tanksDeployment.Clear();
            for (int i = 0; i < ownedTanks.Count; i++)
            {
                tanksDeployment.Add(new Dictionary<CrewClass, CrewMemberSO>());
            }

            // 저장된 편성이 있으면 복구
            TryLoadDeployment();

            Debug.Log($"[CRUX] CrewDeploymentController initialized — {ownedTanks.Count} tanks, {roster.Count} crew");
        }

        /// <summary>리소스에서 보유 전차 로드 (임시)</summary>
        private void LoadOwnedTanks()
        {
            ownedTanks.Clear();

            #if UNITY_EDITOR
            Debug.Log("[CRUX] LoadOwnedTanks in UNITY_EDITOR mode");
            // 에디터 모드: AssetDatabase에서 직접 로드
            var tankGuids = UnityEditor.AssetDatabase.FindAssets("t:TankDataSO", new[] { "Assets/_Project/Data/Tanks" });
            foreach (var guid in tankGuids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var tank = UnityEditor.AssetDatabase.LoadAssetAtPath<TankDataSO>(path);
                if (tank != null)
                {
                    ownedTanks.Add(tank);
                    Debug.Log($"[CRUX] Loaded tank from {path}: {tank.tankName}");
                }
            }
            Debug.Log($"[CRUX] LoadOwnedTanks loaded {ownedTanks.Count} tanks in UNITY_EDITOR mode");
            #else
            Debug.Log("[CRUX] LoadOwnedTanks in Runtime mode");
            // 런타임: Resources 폴더에서 로드 (프로덕션 빌드용)
            var tanks = Resources.LoadAll<TankDataSO>("Tanks");
            if (tanks != null && tanks.Length > 0)
            {
                ownedTanks.AddRange(tanks);
            }
            #endif
        }

        /// <summary>전체 승무원 풀 로드</summary>
        private void LoadRoster()
        {
            Debug.Log("[CRUX] LoadRoster called");
            roster.Clear();

            #if UNITY_EDITOR
            Debug.Log("[CRUX] LoadRoster in UNITY_EDITOR mode");
            // 에디터 모드: AssetDatabase에서 직접 로드 (Resources 폴더 불필요)
            var crewPaths = new[] {
                "Assets/_Project/Data/Crew/Members/Crew_astra.asset",
                "Assets/_Project/Data/Crew/Members/Crew_ririd.asset",
                "Assets/_Project/Data/Crew/Members/Crew_grin.asset",
                "Assets/_Project/Data/Crew/Members/Crew_pretena.asset",
                "Assets/_Project/Data/Crew/Members/Crew_iris.asset"
            };
            foreach (var path in crewPaths)
            {
                var crew = AssetDatabase.LoadAssetAtPath<CrewMemberSO>(path);
                if (crew != null)
                {
                    roster.Add(crew);
                    Debug.Log($"[CRUX] Loaded crew from {path}");
                }
                else
                {
                    Debug.LogWarning($"[CRUX] Failed to load crew from {path}");
                }
            }
            Debug.Log($"[CRUX] LoadRoster loaded {roster.Count} crew members in UNITY_EDITOR mode");
            #else
            Debug.Log("[CRUX] LoadRoster in Runtime mode");
            // 런타임: Resources 폴더에서 로드 (프로덕션 빌드용)
            var crewAssets = Resources.LoadAll<CrewMemberSO>("Crew");
            if (crewAssets != null && crewAssets.Length > 0)
            {
                roster.AddRange(crewAssets);
            }
            #endif
        }

        /// <summary>현재 선택 전차의 직책별 배치 조회</summary>
        public CrewMemberSO GetAssignment(int tankIndex, CrewClass slot)
        {
            if (tankIndex < 0 || tankIndex >= tanksDeployment.Count) return null;
            tanksDeployment[tankIndex].TryGetValue(slot, out var crew);
            return crew;
        }

        /// <summary>현재 선택 전차의 직책별 배치 조회 (selectedTankIndex 기반)</summary>
        public CrewMemberSO GetSelectedAssignment(CrewClass slot)
        {
            return GetAssignment(selectedTankIndex, slot);
        }

        /// <summary>특정 승무원이 다른 전차에 배치되어 있는가 (excludingTankIndex 제외)</summary>
        public bool IsAssignedElsewhere(CrewMemberSO crew, int excludingTankIndex)
        {
            if (crew == null) return false;
            for (int i = 0; i < tanksDeployment.Count; i++)
            {
                if (i == excludingTankIndex) continue;
                foreach (var assigned in tanksDeployment[i].Values)
                {
                    if (assigned == crew) return true;
                }
            }
            return false;
        }

        /// <summary>현재 선택 전차에 승무원 배치 시도. 중복 방지 (다른 전차에 이미 배치된 경우 false)</summary>
        public bool TryAssignCrew(CrewClass slot, CrewMemberSO member)
        {
            if (selectedTankIndex < 0 || selectedTankIndex >= tanksDeployment.Count) return false;

            // 중복 배치 방지 (다른 전차에 이미 배치됨)
            if (member != null && IsAssignedElsewhere(member, selectedTankIndex))
            {
                Debug.LogWarning($"[CRUX] Crew {member.displayName} already assigned to another tank");
                return false;
            }

            // 기존 배치 제거 (같은 슬롯에 다른 크루가 있었으면)
            tanksDeployment[selectedTankIndex].Remove(slot);

            // 새 배치
            if (member != null)
            {
                tanksDeployment[selectedTankIndex][slot] = member;
            }

            OnAssignmentChanged?.Invoke();
            return true;
        }

        /// <summary>현재 선택 전차에서 승무원 제거</summary>
        public void RemoveCrew(CrewClass slot)
        {
            if (selectedTankIndex < 0 || selectedTankIndex >= tanksDeployment.Count) return;
            tanksDeployment[selectedTankIndex].Remove(slot);
            OnAssignmentChanged?.Invoke();
        }

        /// <summary>전차 선택</summary>
        public void SelectTank(int index)
        {
            if (index < 0 || index >= ownedTanks.Count) return;
            selectedTankIndex = index;
            OnTankSelectionChanged?.Invoke();
        }

        /// <summary>현재 선택 전차가 5명 모두 배치됨</summary>
        public bool IsFullyCrewed(int tankIndex)
        {
            if (tankIndex < 0 || tankIndex >= tanksDeployment.Count) return false;
            var dict = tanksDeployment[tankIndex];
            return dict.ContainsKey(CrewClass.Commander)
                && dict.ContainsKey(CrewClass.Gunner)
                && dict.ContainsKey(CrewClass.Loader)
                && dict.ContainsKey(CrewClass.Driver)
                && dict.ContainsKey(CrewClass.GunnerMech);
        }

        /// <summary>현재 선택 전차의 배치 승무원 수</summary>
        public int CrewCount(int tankIndex)
        {
            if (tankIndex < 0 || tankIndex >= tanksDeployment.Count) return 0;
            return tanksDeployment[tankIndex].Count;
        }

        /// <summary>사기값 미리보기 (현재 선택 전차). 임시 TankCrew 사용 안 함, 직접 계산.</summary>
        public int PreviewMorale()
        {
            var breakdown = PreviewMoraleBreakdown();
            return breakdown.total;
        }

        /// <summary>사기값 세부 분석</summary>
        public MoraleBreakdown PreviewMoraleBreakdown()
        {
            var tank = SelectedTank;
            if (tank == null)
                return new MoraleBreakdown { baseVal = 50, commanderMark = 0, traitFloor = 0, total = 50 };

            var deployment = tanksDeployment[selectedTankIndex];

            // 전차장 마크 보너스
            int commanderMark = 0;
            if (deployment.TryGetValue(CrewClass.Commander, out var cmdr) && cmdr != null)
            {
                // 현재는 commanderHullClassAxis가 전차 스펙에 없으므로 0으로 처리
                // TODO: TankDataSO에 hullClassAxis 필드 추가 후 GetMark() 호출
                commanderMark = 0;
            }

            // 5인 trait moraleFloor 합산 (only axisType=None)
            int traitFloor = 0;
            var crewSlots = new CrewClass[] { CrewClass.Commander, CrewClass.Gunner, CrewClass.Loader, CrewClass.Driver, CrewClass.GunnerMech };
            foreach (var slot in crewSlots)
            {
                if (deployment.TryGetValue(slot, out var crew) && crew != null)
                {
                    var traitSum = TraitEffects.SumActiveAtInit(crew.traits);
                    traitFloor += traitSum.moraleFloor;
                }
            }

            int baseVal = 50;
            int total = Mathf.Clamp(baseVal + commanderMark * 3 + traitFloor, 0, 100);

            return new MoraleBreakdown
            {
                baseVal = baseVal,
                commanderMark = commanderMark,
                traitFloor = traitFloor,
                total = total
            };
        }

        /// <summary>편성 확정 — 저장 후 씬 전환</summary>
        public void ConfirmDeployment()
        {
            // 저장
            var saveData = new DeploymentSaveData();
            saveData.tanks = new List<TankDeployment>();

            for (int i = 0; i < ownedTanks.Count; i++)
            {
                var tank = ownedTanks[i];
                var deployment = tanksDeployment[i];

                var tankDeploy = new TankDeployment
                {
                    tankSOGuid = GetAssetGuid(tank),
                    commanderGuid = GetCrewGuid(deployment, CrewClass.Commander),
                    gunnerGuid = GetCrewGuid(deployment, CrewClass.Gunner),
                    loaderGuid = GetCrewGuid(deployment, CrewClass.Loader),
                    driverGuid = GetCrewGuid(deployment, CrewClass.Driver),
                    mgMechanicGuid = GetCrewGuid(deployment, CrewClass.GunnerMech)
                };
                saveData.tanks.Add(tankDeploy);
            }

            DeploymentStorage.Save(saveData);
            Debug.Log("[CRUX] Deployment confirmed and saved");

            // 씬 전환 (TODO: 추후 실제 씬 이름으로 변경)
            // UnityEngine.SceneManagement.SceneManager.LoadScene("StrategyScene");
        }

        /// <summary>취소 후 이전 씬 복귀</summary>
        public void Back()
        {
            Debug.Log("[CRUX] Deployment cancelled");
            // TODO: 이전 씬으로 복귀
            // UnityEngine.SceneManagement.SceneManager.LoadScene("HangarScene");
        }

        /// <summary>저장된 편성 복구 시도</summary>
        private void TryLoadDeployment()
        {
            var saveData = DeploymentStorage.Load();
            if (saveData == null) return;

            for (int i = 0; i < saveData.tanks.Count && i < tanksDeployment.Count; i++)
            {
                var tankDeploy = saveData.tanks[i];
                var crewSlots = new (CrewClass slot, string guid)[]
                {
                    (CrewClass.Commander, tankDeploy.commanderGuid),
                    (CrewClass.Gunner, tankDeploy.gunnerGuid),
                    (CrewClass.Loader, tankDeploy.loaderGuid),
                    (CrewClass.Driver, tankDeploy.driverGuid),
                    (CrewClass.GunnerMech, tankDeploy.mgMechanicGuid)
                };

                foreach (var (slot, guid) in crewSlots)
                {
                    if (string.IsNullOrEmpty(guid)) continue;
                    var crew = FindCrewByGuid(guid);
                    if (crew != null)
                    {
                        tanksDeployment[i][slot] = crew;
                    }
                }
            }

            Debug.Log("[CRUX] Deployment loaded from storage");
        }

        /// <summary>GUID로부터 CrewMemberSO 역검색</summary>
        private CrewMemberSO FindCrewByGuid(string guid)
        {
            #if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<CrewMemberSO>(path);
            }
            #endif
            return null;
        }

        /// <summary>ScriptableObject의 GUID 추출</summary>
        private string GetAssetGuid(UnityEngine.Object asset)
        {
            #if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                return UnityEditor.AssetDatabase.AssetPathToGUID(path);
            }
            #endif
            return "";
        }

        /// <summary>딕셔너리에서 특정 슬롯의 크루 GUID 추출</summary>
        private string GetCrewGuid(Dictionary<CrewClass, CrewMemberSO> dict, CrewClass slot)
        {
            if (dict.TryGetValue(slot, out var crew) && crew != null)
            {
                return GetAssetGuid(crew);
            }
            return "";
        }
    }

    /// <summary>사기값 세부 분석</summary>
    public struct MoraleBreakdown
    {
        public int baseVal;
        public int commanderMark;
        public int traitFloor;
        public int total;
    }
}
