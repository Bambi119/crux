using System.Collections.Generic;
using System.Linq;

namespace Crux.Data
{
    /// <summary>
    /// 부대 전체가 공유하는 파츠 재고(stash) + 승무원 풀.
    /// 편성 씬에서 TankInstance 간 파츠·승무원 이동을 중개.
    /// 카테고리별 리스트로 분류 저장 — 조회·장착 후보 필터링 용이.
    ///
    /// 세이브 직렬화는 후속 커밋. 이번은 in-memory 구조만.
    /// </summary>
    public class ConvoyInventory
    {
        // 카테고리별 분류 저장 — 조회 효율 + 인덱스 안정성
        private readonly Dictionary<PartCategory, List<PartInstance>> buckets;

        // 승무원 풀 — ID별 조회 가능하도록 저장
        public readonly List<CrewMemberRuntime> availableCrew = new();

        // 자원 관리 — 첫 빌드 임시값, docs/09 §5 자원 관리 기반으로 추후 수정
        public int Money { get; set; }
        public int Morale { get; set; }

        public ConvoyInventory()
        {
            buckets = new Dictionary<PartCategory, List<PartInstance>>();
            foreach (PartCategory cat in System.Enum.GetValues(typeof(PartCategory)))
                buckets[cat] = new List<PartInstance>();

            // 초기값
            Money = 1000;
            Morale = 80;
        }

        /// <summary>전체 재고 수량 (모든 카테고리 합)</summary>
        public int TotalCount => buckets.Values.Sum(list => list.Count);

        /// <summary>특정 카테고리 재고 수량</summary>
        public int CountOf(PartCategory category) =>
            buckets.TryGetValue(category, out var list) ? list.Count : 0;

        /// <summary>재고 추가 — 중복 instanceId 방지, null 거부</summary>
        public bool Add(PartInstance part)
        {
            if (part == null || part.data == null) return false;
            var cat = part.Category;
            if (!buckets.ContainsKey(cat)) return false;  // 미지 카테고리 방어
            if (buckets[cat].Any(p => p.instanceId == part.instanceId)) return false;  // 중복
            buckets[cat].Add(part);
            return true;
        }

        /// <summary>instanceId 기준 제거 — 성공 시 PartInstance 반환, 없으면 null</summary>
        public PartInstance Remove(string instanceId)
        {
            foreach (var list in buckets.Values)
            {
                int idx = list.FindIndex(p => p.instanceId == instanceId);
                if (idx >= 0)
                {
                    var removed = list[idx];
                    list.RemoveAt(idx);
                    return removed;
                }
            }
            return null;
        }

        /// <summary>카테고리 내 모든 파츠 조회 (읽기 전용)</summary>
        public IReadOnlyList<PartInstance> GetByCategory(PartCategory category) =>
            buckets.TryGetValue(category, out var list) ? list : System.Array.Empty<PartInstance>();

        /// <summary>instanceId로 파츠 조회 (미발견 null)</summary>
        public PartInstance FindById(string instanceId)
        {
            foreach (var list in buckets.Values)
            {
                var found = list.FirstOrDefault(p => p.instanceId == instanceId);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 재고에서 꺼내 지정 전차에 장착. 실패 시 원복 (재고에 다시 넣음).
        /// 장착 실패 이유를 CompatibilityResult.violations 로 반환.
        /// </summary>
        public CompatibilityResult EquipTo(
            TankInstance tank,
            string instanceId,
            PartCategory category,
            int slotIndex = 0)
        {
            var part = Remove(instanceId);
            if (part == null)
                return CompatibilityResult.Fail($"재고에 파츠 없음: {instanceId}");

            var result = tank.TryEquip(category, part, slotIndex);
            if (!result.isValid)
            {
                // 장착 실패 → 재고로 돌려보내기
                Add(part);
            }
            return result;
        }

        /// <summary>
        /// 전차에서 해제해 재고로 회수. 해당 슬롯이 비어있으면 no-op.
        /// </summary>
        public PartInstance ReturnFrom(TankInstance tank, PartCategory category, int slotIndex = 0)
        {
            var removed = tank.Unequip(category, slotIndex);
            if (removed != null) Add(removed);
            return removed;
        }

        /// <summary>자금 변동 — 양수는 획득, 음수는 소비</summary>
        public void AddMoney(int delta)
        {
            Money += delta;
        }

        /// <summary>사기 변동 — 0~100 범위로 자동 clamp</summary>
        public void ChangeMorale(int delta)
        {
            Morale += delta;
            if (Morale < 0) Morale = 0;
            if (Morale > 100) Morale = 100;
        }

        /// <summary>
        /// 풀에서 승무원을 지정 전차의 직책에 할당.
        /// 직책 일치 + 목표 직책 공석이면 할당 성공, 아니면 false.
        /// </summary>
        public bool AssignCrewTo(TankInstance tank, CrewClass klass, string crewId)
        {
            if (tank == null || tank.crew == null) return false;

            // 풀에서 crewId 검색
            var crew = availableCrew.Find(c => c != null && c.data != null && c.data.id == crewId);
            if (crew == null) return false;

            // 직책 일치 확인
            if (crew.Class != klass) return false;

            // 목표 직책 공석 확인
            if (!tank.crew.HasVacancy(klass)) return false;

            // 할당 — 풀에서 제거, 전차에 배치
            availableCrew.Remove(crew);
            tank.crew.Set(klass, crew);
            return true;
        }

        /// <summary>
        /// 지정 전차의 직책에서 승무원을 제거하고 풀로 돌려보냄.
        /// 공석이면 no-op.
        /// </summary>
        public CrewMemberRuntime UnassignCrewFrom(TankInstance tank, CrewClass klass)
        {
            if (tank == null || tank.crew == null) return null;

            var crew = tank.crew.Get(klass);
            if (crew == null) return null;

            // 전차에서 제거, 풀에 추가
            tank.crew.Set(klass, null);
            availableCrew.Add(crew);
            return crew;
        }
    }
}
