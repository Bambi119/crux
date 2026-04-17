using System.Collections.Generic;

namespace Crux.Data
{
    /// <summary>
    /// ConvoyInventory 직렬화 DTO — JsonUtility 호환 POCO.
    /// Phase 1: Money / Morale / 탱크 메타(name, hull, isRocinante, inSortie).
    /// Phase 2 (후속): 크루 ID 매핑 / 파츠 슬롯 ID 매핑 / availableCrew 풀.
    /// </summary>
    [System.Serializable]
    public class ConvoySaveData
    {
        public int money;
        public int morale;
        public List<TankSaveEntry> tanks = new();

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
            }
            return data;
        }

        /// <summary>
        /// 저장 데이터를 기존 convoy에 적용.
        /// Money/Morale은 덮어쓰기. 탱크 리스트는 이름/차체 매칭으로 inSortie만 갱신
        /// (신규 탱크 생성은 생성 규칙이 복잡해 Phase 2에서 처리).
        /// </summary>
        public void ApplyTo(ConvoyInventory convoy)
        {
            if (convoy == null) return;
            convoy.Money = money;
            convoy.Morale = morale;

            foreach (var entry in tanks)
            {
                if (entry == null) continue;
                var existing = convoy.tanks.Find(t =>
                    t != null && t.tankName == entry.tankName && (int)t.hullClass == entry.hullClass);
                if (existing != null)
                    existing.inSortie = entry.inSortie;
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
}
