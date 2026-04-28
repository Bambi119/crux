using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Crux.Data;
using Crux.UI.Deployment;

/// <summary>
/// P7 Crew Deployment 편성 씬 백엔드 검증.
/// Editor 메뉴 Crux/Test/P7 Crew Deployment 실행 또는 execute_script(methodName=Execute).
/// 성공 조건: 전 단계 "OK" 로그, 실패 시 "FAIL" 로그 + 상세.
/// </summary>
public static class P7_CrewDeploymentTest
{
    [MenuItem("Crux/Test/P7 Crew Deployment")]
    public static void Execute()
    {
        void Log(string msg) => Debug.Log($"[P7] {msg}");
        void Fail(string msg) => Debug.LogError($"[P7] FAIL — {msg}");

        int passed = 0;
        int failed = 0;

        void Assert(bool cond, string name)
        {
            if (cond) { passed++; Log($"OK {name}"); }
            else      { failed++; Fail(name); }
        }

        // ===== 1. 자산 로드 =====
        var astra = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_astra.asset");
        var ririd = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_ririd.asset");
        var grin = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_grin.asset");
        var pretena = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_pretena.asset");
        var iris = AssetDatabase.LoadAssetAtPath<CrewMemberSO>("Assets/_Project/Data/Crew/Members/Crew_iris.asset");

        Assert(astra != null && astra.klass == CrewClass.Commander, "load astra (Commander)");
        Assert(ririd != null && ririd.klass == CrewClass.Gunner, "load ririd (Gunner)");
        Assert(grin != null && grin.klass == CrewClass.Loader, "load grin (Loader)");
        Assert(pretena != null && pretena.klass == CrewClass.Driver, "load pretena (Driver)");
        Assert(iris != null && iris.klass == CrewClass.GunnerMech, "load iris (GunnerMech)");

        if (astra == null || ririd == null || grin == null || pretena == null || iris == null)
        {
            Debug.LogError($"[P7] test aborted — crew assets not found. passed={passed} failed={failed + 5}");
            return;
        }

        // ===== 2. CrewDeploymentController 생성 =====
        var testGO = new GameObject("P7_CrewDeploymentTest");
        CrewDeploymentController ctrl = null;
        try
        {
            ctrl = testGO.AddComponent<CrewDeploymentController>();
            Assert(ctrl != null, "create controller");
            // Note: AddComponent in EditMode doesn't call Awake(), so manually initialize
            ctrl.InitializeData();
            Assert(ctrl.OwnedTanks.Count == 0, "init ownedTanks empty");
            Assert(ctrl.Roster.Count > 0, "init roster loaded");
            Assert(ctrl.SelectedTankIndex == 0, "init selectedTankIndex=0");
        }
        catch (System.Exception ex)
        {
            Fail($"controller creation: {ex.Message}");
            Object.DestroyImmediate(testGO);
            return;
        }

        // ===== 3. 탱크 선택 후 모롤 계산 검증 =====
        // 참고: PreviewMoraleBreakdown은 selectedTank 기반이므로, TryAssignCrew로 배치한 후 호출
        // 임시: 전차가 없으므로 기본값 50만 확인
        var breakdown = ctrl.PreviewMoraleBreakdown();
        Assert(breakdown.baseVal == 50, $"base morale=50 (got {breakdown.baseVal})");
        Assert(breakdown.commanderMark == 0, $"commander mark=0 (got {breakdown.commanderMark})");
        Assert(breakdown.total >= 50 && breakdown.total <= 100, $"final morale in [50,100] (got {breakdown.total})");

        // ===== 4. 중복 배치 방지 =====
        // astra를 한 명의 탱크 승무원으로만 배치 가능해야 함
        // (실제 탱크 SO가 없으므로 IsAssignedElsewhere 메서드 직접 테스트 불가)
        // TODO: TankDataSO 목(mock) 생성 시 확장 가능

        // ===== 5. 부분 배치 여부 검사 (테스트용 탱크 인덱스 0) =====
        // 초기 상태: 탱크가 없으므로 IsFullyCrewed(0) = false
        bool isFullyCrewed = ctrl.IsFullyCrewed(0);
        Assert(!isFullyCrewed, "empty tank not fully crewed");

        // ===== 6. CrewCount 검증 =====
        int count = ctrl.CrewCount(0);
        Assert(count == 0, $"crew count=0 for empty tank (got {count})");

        // ===== 7. 저장/복구 라운드트립 =====
        DeploymentStorage.Clear();
        Assert(!DeploymentStorage.HasSavedDeployment, "storage cleared");

        // 샘플 저장 데이터 구성
        var saveData = new DeploymentSaveData();
        var tankDep = new TankDeployment
        {
            tankSOGuid = "mock-tank-guid-001",
            commanderGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(astra)),
            gunnerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ririd)),
            loaderGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(grin)),
            driverGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pretena)),
            mgMechanicGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(iris))
        };
        saveData.tanks.Add(tankDep);

        DeploymentStorage.Save(saveData);
        Assert(DeploymentStorage.HasSavedDeployment, "save() sets HasSavedDeployment");

        DeploymentSaveData loaded = DeploymentStorage.Load();
        Assert(loaded != null, "load() returns non-null");
        Assert(loaded.tanks.Count == 1, $"loaded tanks count=1 (got {loaded.tanks.Count})");
        Assert(loaded.tanks[0].tankSOGuid == "mock-tank-guid-001", "tank GUID preserved");
        Assert(!string.IsNullOrEmpty(loaded.tanks[0].commanderGuid), "commander GUID persisted");

        // ===== 8. GUID → SO 역방향 매핑 =====
        string astraGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(astra));
        string astraPath = AssetDatabase.GUIDToAssetPath(astraGuid);
        var astraReloaded = AssetDatabase.LoadAssetAtPath<CrewMemberSO>(astraPath);
        Assert(astraReloaded == astra, "GUID round-trip maps to same SO");

        // ===== 9. 빈 배치 저장 =====
        var emptyData = new DeploymentSaveData();
        DeploymentStorage.Save(emptyData);
        DeploymentSaveData loaded2 = DeploymentStorage.Load();
        Assert(loaded2 != null && loaded2.tanks.Count == 0, "save/load empty tanks list");

        // ===== 10. 저장소 삭제 =====
        DeploymentStorage.Clear();
        Assert(!DeploymentStorage.HasSavedDeployment, "clear() resets HasSavedDeployment");

        // ===== 결과 =====
        Object.DestroyImmediate(testGO);
        if (failed == 0)
            Log($"=== ALL PASS ({passed}/{passed}) ===");
        else
            Debug.LogError($"[P7] === FAILED {failed} / {passed + failed} ===");
    }
}
