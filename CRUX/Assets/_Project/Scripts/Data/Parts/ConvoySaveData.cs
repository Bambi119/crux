using System.Collections.Generic;

namespace Crux.Data
{
    /// <summary>
    /// ConvoyInventory 직렬화 DTO — JsonUtility 호환 POCO.
    /// Phase 1: Money / Morale / 탱크 메타(name, hull, isRocinante, inSortie).
    /// Phase 2: 탱크별 크루 할당 (CrewMemberSO.id 기반 tankName+klass+crewId).
    /// Phase 3: 탱크별 단일 슬롯 파츠 할당 (partName 기준).
    /// 후속: availableCrew 풀 / Armor·Auxiliary 복수 슬롯 / 파츠 내구도 상태.
    /// </summary>
    [System.Serializable]
    public class ConvoySaveData
    {
        public int money;
        public int morale;
        public List<TankSaveEntry> tanks = new();
        public List<CrewAssignmentEntry> crewAssignments = new();
        public List<TankPartsEntry> tankParts = new();

        public static ConvoySaveData FromConvoy(ConvoyInventory convoy)
        {
            if (convoy == null) return new ConvoySaveData();
            var data = new ConvoySaveData
            {
                money = convoy.Money,
                morale = convoy.Morale,
            };

            foreach (var t in convoy.tanks)
            {
                if (t == null) continue;
                data.tanks.Add(new TankSaveEntry
                {
                    tankName = t.tankName,
                    hullClass = (int)t.hullClass,
                    isRocinante = t.isRocinante,
                    inSortie = t.inSortie,
                });

                // 크루 할당 수집 (Phase 2)
                if (t.crew != null)
                {
                    foreach (var (klass, crew) in t.crew.All())
                    {
                        if (crew == null || crew.data == null) continue;
                        data.crewAssignments.Add(new CrewAssignmentEntry
                        {
                            tankName = t.tankName,
                            klass = (int)klass,
                            crewId = crew.data.id,
                        });
                    }
                }

                // 파츠 슬롯 수집 (Phase 3) — 단일 슬롯 5종
                data.tankParts.Add(new TankPartsEntry
                {
                    tankName = t.tankName,
                    enginePartName = t.engine?.data?.partName ?? "",
                    turretPartName = t.turret?.data?.partName ?? "",
                    mainGunPartName = t.mainGun?.data?.partName ?? "",
                    ammoRackPartName = t.ammoRack?.data?.partName ?? "",
                    trackPartName = t.track?.data?.partName ?? "",
                });
            }
            return data;
        }

        /// <summary>
        /// 저장 데이터를 기존 convoy에 적용.
        /// 1) Money/Morale 덮어쓰기
        /// 2) 탱크 inSortie 복원 (이름·차체 매칭)
        /// 3) 크루 재배치 — 모든 tank.crew 초기화 후 저장된 매핑 적용
        /// </summary>
        public void ApplyTo(ConvoyInventory convoy)
        {
            if (convoy == null) return;
            convoy.Money = money;
            convoy.Morale = morale;

            // 1) 탱크 inSortie
            foreach (var entry in tanks)
            {
                if (entry == null) continue;
                var existing = convoy.tanks.Find(t =>
                    t != null && t.tankName == entry.tankName && (int)t.hullClass == entry.hullClass);
                if (existing != null)
                    existing.inSortie = entry.inSortie;
            }

            // 2) 크루 전면 재배치 — 저장된 매핑 있을 때만 수행 (빈 리스트면 현 상태 유지)
            if (crewAssignments != null && crewAssignments.Count > 0)
            {
                foreach (var t in convoy.tanks)
                {
                    if (t?.crew == null) continue;
                    foreach (CrewClass klass in System.Enum.GetValues(typeof(CrewClass)))
                    {
                        if (klass == CrewClass.None) continue;
                        var c = t.crew.Get(klass);
                        if (c != null)
                        {
                            t.crew.Set(klass, null);
                            if (!convoy.availableCrew.Contains(c))
                                convoy.availableCrew.Add(c);
                        }
                    }
                }
            }

            // 3) 저장된 매핑으로 재할당
            foreach (var entry in crewAssignments)
            {
                if (entry == null || string.IsNullOrEmpty(entry.crewId)) continue;
                var tank = convoy.tanks.Find(t => t != null && t.tankName == entry.tankName);
                if (tank?.crew == null) continue;
                var crew = convoy.availableCrew.Find(c => c?.data != null && c.data.id == entry.crewId);
                if (crew == null) continue;
                var klass = (CrewClass)entry.klass;
                if (crew.Class != klass) continue;  // 직책 불일치는 무시 (데이터 손상 방어)
                tank.crew.Set(klass, crew);
                convoy.availableCrew.Remove(crew);
            }

            // 4) 파츠 재배치 (Phase 3) — 저장된 매핑 있을 때만 수행
            if (tankParts == null || tankParts.Count == 0) return;

            var singleSlotCategories = new[] {
                PartCategory.Engine, PartCategory.Turret, PartCategory.MainGun,
                PartCategory.AmmoRack, PartCategory.Track
            };
            foreach (var t in convoy.tanks)
            {
                if (t == null) continue;
                foreach (var cat in singleSlotCategories)
                    convoy.ReturnFrom(t, cat);  // 장착 → buckets 회수
            }
            foreach (var entry in tankParts)
            {
                if (entry == null) continue;
                var tank = convoy.tanks.Find(t => t != null && t.tankName == entry.tankName);
                if (tank == null) continue;
                AssignPartByName(convoy, tank, PartCategory.Engine, entry.enginePartName);
                AssignPartByName(convoy, tank, PartCategory.Turret, entry.turretPartName);
                AssignPartByName(convoy, tank, PartCategory.MainGun, entry.mainGunPartName);
                AssignPartByName(convoy, tank, PartCategory.AmmoRack, entry.ammoRackPartName);
                AssignPartByName(convoy, tank, PartCategory.Track, entry.trackPartName);
            }
        }

        /// <summary>buckets에서 partName 일치 첫 파츠 찾아 tank에 장착. 이름 비면 no-op.</summary>
        private static void AssignPartByName(ConvoyInventory convoy, TankInstance tank, PartCategory cat, string partName)
        {
            if (string.IsNullOrEmpty(partName)) return;
            var bucket = convoy.GetByCategory(cat);
            for (int i = 0; i < bucket.Count; i++)
            {
                var p = bucket[i];
                if (p?.data != null && p.data.partName == partName)
                {
                    convoy.EquipTo(tank, p.instanceId, cat);
                    return;
                }
            }
        }
    }

    [System.Serializable]
    public class TankSaveEntry
    {
        public string tankName;
        public int hullClass;
        public bool isRocinante;
        public bool inSortie;
    }

    [System.Serializable]
    public class CrewAssignmentEntry
    {
        public string tankName;
        public int klass;    // CrewClass enum 값
        public string crewId; // CrewMemberSO.id
    }

    [System.Serializable]
    public class TankPartsEntry
    {
        public string tankName;
        public string enginePartName;
        public string turretPartName;
        public string mainGunPartName;
        public string ammoRackPartName;
        public string trackPartName;
    }
}
