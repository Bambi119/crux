using System.Collections.Generic;

namespace Crux.Data
{
    /// <summary>
    /// ConvoyInventory 직렬화 DTO — JsonUtility 호환 POCO.
    /// Phase 1: Money / Morale / 탱크 메타(name, hull, isRocinante, inSortie).
    /// Phase 2: 탱크별 크루 할당 (CrewMemberSO.id 기반 tankName+klass+crewId).
    /// Phase 3: 탱크별 단일 슬롯 파츠 할당 (partName 기준).
    /// Phase 4: 파츠 내구도(5종 + Armor/Auxiliary) + 승무원 부상 상태 (crewId + injuryState).
    /// </summary>
    [System.Serializable]
    public class ConvoySaveData
    {
        public int money;
        public int morale;
        public List<TankSaveEntry> tanks = new();
        public List<CrewAssignmentEntry> crewAssignments = new();
        public List<TankPartsEntry> tankParts = new();
        public List<CrewStateEntry> crewStates = new();

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

                // 파츠 슬롯 수집 (Phase 3+4) — 단일 슬롯 5종 + 내구도
                var partsEntry = new TankPartsEntry
                {
                    tankName = t.tankName,
                    enginePartName = t.engine?.data?.partName ?? "",
                    turretPartName = t.turret?.data?.partName ?? "",
                    mainGunPartName = t.mainGun?.data?.partName ?? "",
                    ammoRackPartName = t.ammoRack?.data?.partName ?? "",
                    trackPartName = t.track?.data?.partName ?? "",
                    engineDurability = t.engine?.durability ?? 1f,
                    turretDurability = t.turret?.durability ?? 1f,
                    mainGunDurability = t.mainGun?.durability ?? 1f,
                    ammoRackDurability = t.ammoRack?.durability ?? 1f,
                    trackDurability = t.track?.durability ?? 1f,
                };
                // Armor 복수 슬롯
                foreach (var a in t.armor)
                {
                    partsEntry.armorPartNames.Add(a?.data?.partName ?? "");
                    partsEntry.armorDurabilities.Add(a?.durability ?? 1f);
                }
                // Auxiliary 복수 슬롯
                foreach (var aux in t.auxiliary)
                {
                    partsEntry.auxiliaryPartNames.Add(aux?.data?.partName ?? "");
                    partsEntry.auxiliaryDurabilities.Add(aux?.durability ?? 1f);
                    partsEntry.auxiliaryCharges.Add(aux?.chargesRemaining ?? -1);
                }
                data.tankParts.Add(partsEntry);
            }

            // 승무원 부상 상태 수집 (P4) — 탱크 배치 승무원
            foreach (var t in convoy.tanks)
            {
                if (t?.crew == null) continue;
                foreach (var (klass, crew) in t.crew.All())
                {
                    if (crew == null || crew.data == null) continue;
                    data.crewStates.Add(new CrewStateEntry
                    {
                        crewId = crew.data.id,
                        injuryState = (int)crew.injuryState,
                    });
                }
            }

            // availableCrew 풀 부상 상태 수집 (P4)
            foreach (var crew in convoy.availableCrew)
            {
                if (crew == null || crew.data == null) continue;
                data.crewStates.Add(new CrewStateEntry
                {
                    crewId = crew.data.id,
                    injuryState = (int)crew.injuryState,
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

            // 4) 파츠 재배치 (Phase 3+4) — 저장된 매핑 있을 때만 수행
            if (tankParts != null && tankParts.Count > 0)
            {
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

                    // 단일 슬롯 내구도 복원 (P4)
                    if (tank.engine != null) tank.engine.durability = entry.engineDurability;
                    if (tank.turret != null) tank.turret.durability = entry.turretDurability;
                    if (tank.mainGun != null) tank.mainGun.durability = entry.mainGunDurability;
                    if (tank.ammoRack != null) tank.ammoRack.durability = entry.ammoRackDurability;
                    if (tank.track != null) tank.track.durability = entry.trackDurability;

                    // Armor 복수 슬롯 복원 (P4)
                    for (int i = 0; i < entry.armorPartNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(entry.armorPartNames[i])) continue;
                        AssignMultiPartByName(convoy, tank, PartCategory.Armor, i, entry.armorPartNames[i]);
                        float dur = i < entry.armorDurabilities.Count ? entry.armorDurabilities[i] : 1f;
                        if (i < tank.armor.Count && tank.armor[i] != null)
                            tank.armor[i].durability = dur;
                    }

                    // Auxiliary 복수 슬롯 복원 (P4)
                    for (int i = 0; i < entry.auxiliaryPartNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(entry.auxiliaryPartNames[i])) continue;
                        AssignMultiPartByName(convoy, tank, PartCategory.Auxiliary, i, entry.auxiliaryPartNames[i]);
                        float dur = i < entry.auxiliaryDurabilities.Count ? entry.auxiliaryDurabilities[i] : 1f;
                        int charges = i < entry.auxiliaryCharges.Count ? entry.auxiliaryCharges[i] : -1;
                        if (i < tank.auxiliary.Count && tank.auxiliary[i] != null)
                        {
                            tank.auxiliary[i].durability = dur;
                            if (charges >= 0) tank.auxiliary[i].chargesRemaining = charges;
                        }
                    }
                }
            }

            // 5) 승무원 부상 상태 복원 (P4)
            if (crewStates != null && crewStates.Count > 0)
            {
                foreach (var stateEntry in crewStates)
                {
                    if (string.IsNullOrEmpty(stateEntry.crewId)) continue;
                    // 탱크 배치 승무원에서 찾기
                    CrewMemberRuntime found = null;
                    foreach (var t in convoy.tanks)
                    {
                        if (t?.crew == null) continue;
                        foreach (var (_, crew) in t.crew.All())
                        {
                            if (crew?.data?.id == stateEntry.crewId) { found = crew; break; }
                        }
                        if (found != null) break;
                    }
                    // 풀에서 찾기
                    if (found == null)
                        found = convoy.availableCrew.Find(c => c?.data?.id == stateEntry.crewId);
                    if (found != null)
                        found.injuryState = (InjuryLevel)stateEntry.injuryState;
                }
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

        /// <summary>복수 슬롯(Armor/Auxiliary)에 partName 일치 파츠 할당. 이름 비면 no-op.</summary>
        private static void AssignMultiPartByName(ConvoyInventory convoy, TankInstance tank, PartCategory cat, int slotIndex, string partName)
        {
            if (string.IsNullOrEmpty(partName)) return;
            var bucket = convoy.GetByCategory(cat);
            for (int i = 0; i < bucket.Count; i++)
            {
                var p = bucket[i];
                if (p?.data != null && p.data.partName == partName)
                {
                    convoy.EquipTo(tank, p.instanceId, cat, slotIndex);
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

        // 단일 슬롯 내구도 (P4)
        public float engineDurability = 1f;
        public float turretDurability = 1f;
        public float mainGunDurability = 1f;
        public float ammoRackDurability = 1f;
        public float trackDurability = 1f;

        // 복수 슬롯 — Armor (이름 + 내구도) (P4)
        public List<string> armorPartNames = new();
        public List<float> armorDurabilities = new();

        // 복수 슬롯 — Auxiliary (이름 + 내구도 + 남은 사용 횟수) (P4)
        public List<string> auxiliaryPartNames = new();
        public List<float> auxiliaryDurabilities = new();
        public List<int> auxiliaryCharges = new();
    }

    [System.Serializable]
    public class CrewStateEntry
    {
        public string crewId;
        public int injuryState;  // InjuryLevel enum int 값
    }
}
