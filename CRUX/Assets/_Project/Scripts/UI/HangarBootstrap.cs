using UnityEngine;
using System.Collections.Generic;
using Crux.Data;

namespace Crux.UI
{
    /// <summary>
    /// Hangar 씬 MVP 샘플 부대 시드.
    /// TankInstance·CrewMemberRuntime·PartInstance를 생성해 ConvoyInventory에 채우는 순수 팩토리.
    /// 임시 MVP 용도. 세이브 시스템 도입 시 제거 또는 NewGame 경로로 이동.
    /// </summary>
    public static class HangarBootstrap
    {
        const string KeyMoney = "Convoy.Money";      // legacy Save-Minimal
        const string KeyMorale = "Convoy.Morale";    // legacy Save-Minimal
        const string KeyConvoyJson = "Convoy.Json";  // Save-Full P1
        const int DefaultMoney = 1000;
        const int DefaultMorale = 80;

        /// <summary>
        /// 샘플 부대 생성: 로시난테 1대(출격) + T-34, 셔먼 2대(보관) + 크루 5명 + 파츠 10개.
        /// crewRoster가 null/empty면 Editor에서 AssetDatabase 폴백 로드 시도.
        /// Money/Morale은 PlayerPrefs에서 복원 (Save-Minimal).
        /// </summary>
        public static ConvoyInventory BuildSampleConvoy(ref CrewMemberSO[] crewRoster)
        {
            var convoy = new ConvoyInventory();

            // PlayerPrefs 복원 — Save-Full JSON 우선, 없으면 legacy int 키, 둘 다 없으면 기본값
            convoy.Money = PlayerPrefs.GetInt(KeyMoney, DefaultMoney);
            convoy.Morale = PlayerPrefs.GetInt(KeyMorale, DefaultMorale);

#if UNITY_EDITOR
            // Editor fallback — Inspector 미할당 시 AssetDatabase로 5명 자동 로드 (MVP 편의)
            if (crewRoster == null || crewRoster.Length == 0)
            {
                string[] ids = { "astra", "ririd", "grin", "pretena", "iris" };
                var list = new List<CrewMemberSO>();
                foreach (var id in ids)
                {
                    var path = $"Assets/_Project/Data/Crew/Members/Crew_{id}.asset";
                    var so = UnityEditor.AssetDatabase.LoadAssetAtPath<CrewMemberSO>(path);
                    if (so != null) list.Add(so);
                }
                crewRoster = list.ToArray();
            }
#endif

            // 1) 승무원 풀 시드 — Inspector 할당 에셋으로
            if (crewRoster != null)
            {
                foreach (var so in crewRoster)
                {
                    if (so == null) continue;
                    convoy.availableCrew.Add(new CrewMemberRuntime(so));
                }
            }

            // 2) 샘플 탱크 3대
            var rocinante = new TankInstance("로시난테", HullClass.Assault);
            rocinante.isRocinante = true;
            rocinante.inSortie = true;
            convoy.tanks.Add(rocinante);

            var t34 = new TankInstance("T-34", HullClass.Scout);
            t34.inSortie = false;
            convoy.tanks.Add(t34);

            var sherman = new TankInstance("셔먼", HullClass.Support);
            sherman.inSortie = false;
            convoy.tanks.Add(sherman);

            // 3) 5명 자동 할당 — 풀에 있는 승무원의 Class로 직책 판정
            var classes = new[] {
                CrewClass.Commander,
                CrewClass.Gunner,
                CrewClass.Loader,
                CrewClass.Driver,
                CrewClass.GunnerMech
            };
            foreach (var klass in classes)
            {
                var c = convoy.availableCrew.Find(cr => cr.Class == klass);
                if (c != null && c.data != null)
                    convoy.AssignCrewTo(rocinante, klass, c.data.id);
            }

            // 4) 샘플 파츠 시드 + 로시난테 기본 장착
            SeedSampleParts(convoy);
            EquipSamplePartsToTank(convoy, rocinante);

            // 5) JSON 세이브 있으면 덮어쓰기 (Money/Morale/탱크 inSortie)
            var json = PlayerPrefs.GetString(KeyConvoyJson, null);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var save = JsonUtility.FromJson<ConvoySaveData>(json);
                    save?.ApplyTo(convoy);
                    Debug.Log($"[Hangar] Save-Full 복원: money={convoy.Money} morale={convoy.Morale} tanks={save?.tanks.Count}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Hangar] Save-Full 복원 실패 (무시): {e.Message}");
                }
            }

            return convoy;
        }

        private static void SeedSampleParts(ConvoyInventory convoy)
        {
#if UNITY_EDITOR
            // 1순위: Assets/_Project/Data/Parts/Samples/ 아래 에셋 로드 (PartAssetGenerator 산출물)
            if (TrySeedFromAssets(convoy)) return;
#endif

            // 폴백: 런타임 CreateInstance — 에셋 없거나 빌드 모드
            // 엔진 2개
            var engine1 = ScriptableObject.CreateInstance<EnginePartSO>();
            engine1.partName = "V8 디젤";
            engine1.weight = 500f;
            engine1.powerOutput = 400f;
            convoy.Add(new PartInstance(engine1));

            var engine2 = ScriptableObject.CreateInstance<EnginePartSO>();
            engine2.partName = "V6 가솔린";
            engine2.weight = 380f;
            engine2.powerOutput = 280f;
            convoy.Add(new PartInstance(engine2));

            // 터렛 2개
            var turret1 = ScriptableObject.CreateInstance<TurretPartSO>();
            turret1.partName = "중형 터렛";
            turret1.weight = 300f;
            turret1.caliberLimit = 75;
            convoy.Add(new PartInstance(turret1));

            var turret2 = ScriptableObject.CreateInstance<TurretPartSO>();
            turret2.partName = "대형 터렛";
            turret2.weight = 450f;
            turret2.caliberLimit = 120;
            convoy.Add(new PartInstance(turret2));

            // 주포 2개
            var mainGun1 = ScriptableObject.CreateInstance<MainGunPartSO>();
            mainGun1.partName = "76mm 장포신";
            mainGun1.weight = 250f;
            mainGun1.caliber = 75;
            mainGun1.basePenetration = 120f;
            convoy.Add(new PartInstance(mainGun1));

            var mainGun2 = ScriptableObject.CreateInstance<MainGunPartSO>();
            mainGun2.partName = "88mm 단포신";
            mainGun2.weight = 350f;
            mainGun2.caliber = 88;
            mainGun2.basePenetration = 150f;
            convoy.Add(new PartInstance(mainGun2));

            // 탄약고 2개
            var ammoRack1 = ScriptableObject.CreateInstance<AmmoRackPartSO>();
            ammoRack1.partName = "표준 탄약고";
            ammoRack1.weight = 100f;
            ammoRack1.maxMainGunAmmo = 30;
            convoy.Add(new PartInstance(ammoRack1));

            var ammoRack2 = ScriptableObject.CreateInstance<AmmoRackPartSO>();
            ammoRack2.partName = "대용량 탄약고";
            ammoRack2.weight = 160f;
            ammoRack2.maxMainGunAmmo = 50;
            convoy.Add(new PartInstance(ammoRack2));

            // 궤도 2개
            var track1 = ScriptableObject.CreateInstance<TrackPartSO>();
            track1.partName = "표준궤";
            track1.weight = 200f;
            convoy.Add(new PartInstance(track1));

            var track2 = ScriptableObject.CreateInstance<TrackPartSO>();
            track2.partName = "광궤";
            track2.weight = 280f;
            convoy.Add(new PartInstance(track2));
        }

#if UNITY_EDITOR
        /// <summary>
        /// PartAssetGenerator가 만든 에셋 10개 로드 시도.
        /// 하나라도 로드되면 true. 아무 것도 없으면 false.
        /// </summary>
        private static bool TrySeedFromAssets(ConvoyInventory convoy)
        {
            string[] ids = {
                "engine_v8_diesel", "engine_v6_gasoline",
                "turret_medium", "turret_large",
                "maingun_76mm", "maingun_88mm",
                "ammorack_standard", "ammorack_large",
                "track_standard", "track_wide"
            };
            int loaded = 0;
            foreach (var id in ids)
            {
                var path = $"Assets/_Project/Data/Parts/Samples/{id}.asset";
                var so = UnityEditor.AssetDatabase.LoadAssetAtPath<PartDataSO>(path);
                if (so != null)
                {
                    convoy.Add(new PartInstance(so));
                    loaded++;
                }
            }
            if (loaded > 0)
                Debug.Log($"[Hangar] 파츠 에셋 로드: {loaded}/10");
            return loaded == 10;
        }
#endif

        /// <summary>
        /// Money/Morale을 PlayerPrefs에 저장. HangarUI.OnDisable에서 호출.
        /// </summary>
        public static void SaveConvoyStats(ConvoyInventory convoy)
        {
            if (convoy == null) return;

            // Save-Full P1: JSON 직렬화로 Money/Morale + 탱크 메타 저장
            var save = ConvoySaveData.FromConvoy(convoy);
            string json = JsonUtility.ToJson(save);
            PlayerPrefs.SetString(KeyConvoyJson, json);

            // legacy Int 키도 유지 (타 로직 의존 가능)
            PlayerPrefs.SetInt(KeyMoney, convoy.Money);
            PlayerPrefs.SetInt(KeyMorale, convoy.Morale);
            PlayerPrefs.Save();
        }

        /// <summary>PlayerPrefs 저장값 삭제 — 디버그/초기화용</summary>
        public static void ResetSavedStats()
        {
            PlayerPrefs.DeleteKey(KeyMoney);
            PlayerPrefs.DeleteKey(KeyMorale);
            PlayerPrefs.DeleteKey(KeyConvoyJson);
            PlayerPrefs.Save();
        }

        private static void EquipSamplePartsToTank(ConvoyInventory convoy, TankInstance tank)
        {
            var categories = new[] {
                PartCategory.Engine,
                PartCategory.Turret,
                PartCategory.MainGun,
                PartCategory.AmmoRack,
                PartCategory.Track,
            };
            foreach (var cat in categories)
            {
                var parts = convoy.GetByCategory(cat);
                if (parts.Count > 0)
                    convoy.EquipTo(tank, parts[0].instanceId, cat);
            }
        }
    }
}
