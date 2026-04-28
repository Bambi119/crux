using UnityEngine;
using Crux.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CreateRocinante
{
    #if UNITY_EDITOR
    public static void Execute()
    {
        Debug.Log("[CRUX] Creating Tank_rocinante.asset");

        // 폴더 확인 및 생성
        string folderPath = "Assets/_Project/Data/Tanks";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentFolder = "Assets/_Project/Data";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }
            AssetDatabase.CreateFolder(parentFolder, "Tanks");
        }

        // TankDataSO 생성
        TankDataSO tank = ScriptableObject.CreateInstance<TankDataSO>();
        tank.tankName = "Rocinante";
        tank.hullClass = HullClass.Assault;
        tank.isRocinante = true;
        tank.weightCapacity = 100;
        tank.powerRequirement = 100;
        tank.slotTable = HullSlotTable.ForHull(HullClass.Assault);
        tank.maxAP = 6;
        tank.moveSpeed = 3f;
        tank.turretRotationSpeed = 60f;
        tank.muzzleOffset = new Vector2(0.8f, 0f);
        tank.mainGunCaliber = 75;
        tank.fireCost = 3;
        tank.maxHP = 100;
        tank.smokeCharges = 2;
        tank.maxMainGunAmmo = 42;
        tank.maxMGAmmo = 1200;
        tank.mgLoadedAmmo = 120;
        tank.fireResistancePercent = 10f;

        // ArmorProfile 초기화 (기본값)
        tank.armor = new ArmorProfile();

        // ModuleHPProfile 초기화 (기본값)
        tank.moduleHP = new ModuleHPProfile();

        string assetPath = folderPath + "/Tank_rocinante.asset";
        AssetDatabase.CreateAsset(tank, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CRUX] Created asset at {assetPath}");
    }
    #endif
}
