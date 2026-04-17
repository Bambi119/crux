using System;
using System.Collections.Generic;
using System.Linq;

namespace Crux.Data
{
    /// <summary>
    /// 편성 씬에서 한 대의 전차 장착 상태를 표현.
    /// 슬롯별 PartInstance 보유 + CompatibilityChecker 연동.
    ///
    /// 실전투 GridTankUnit과는 별개 — 편성 확정 시 이 모델을 기반으로 TankDataSO를 합성.
    ///
    /// P4-c: 편성 모델용 탱크 인스턴스.
    /// </summary>
    public class TankInstance
    {
        public string tankName;
        public HullClass hullClass;
        public bool isRocinante;
        public HullSlotTable slotTable;

        // 슬롯별 장착 상태 — 단일 슬롯 (Engine, Turret, MainGun, AmmoRack, Track)
        public PartInstance engine;
        public PartInstance turret;
        public PartInstance mainGun;
        public PartInstance ammoRack;
        public PartInstance track;

        // 복수 슬롯 (Armor, Auxiliary) — null 포함 가능
        public List<PartInstance> armor;
        public List<PartInstance> auxiliary;

        // HP 관리 — 차체 기본값(HullClass) + 장갑 파츠 기여
        public int CurrentHP { get; set; }
        public int MaxHP { get; private set; }

        // 승무원 매핑 — D 옵션 (하이브리드)
        public TankCrewInstance crew;

        // 편성 상태 — true: 출격 슬롯, false: 보관 슬롯
        public bool inSortie;

        /// <summary>
        /// TankInstance 생성.
        /// </summary>
        /// <param name="tankName">전차 이름</param>
        /// <param name="hullClass">차체 종류</param>
        public TankInstance(string tankName, HullClass hullClass)
        {
            this.tankName = tankName;
            this.hullClass = hullClass;
            this.isRocinante = false;

            // 차체별 슬롯 자동 배정
            slotTable = HullSlotTable.ForHull(hullClass);

            // 모든 슬롯 null로 초기화
            engine = null;
            turret = null;
            mainGun = null;
            ammoRack = null;
            track = null;

            // 복수 슬롯 초기화 — 카테고리별 슬롯 개수만큼 null 원소
            armor = new List<PartInstance>(new PartInstance[slotTable.armor]);
            auxiliary = new List<PartInstance>(new PartInstance[slotTable.auxiliary]);

            // 승무원 매핑 초기화
            crew = new TankCrewInstance();

            // HP 초기화 — 차체 기본값으로 시작
            RecalculateMaxHP();
            CurrentHP = MaxHP;
        }

        /// <summary>
        /// 모든 장착 파츠를 일괄 열거 (null 필터).
        /// </summary>
        public IEnumerable<PartInstance> AllEquipped()
        {
            if (engine != null) yield return engine;
            if (turret != null) yield return turret;
            if (mainGun != null) yield return mainGun;
            if (ammoRack != null) yield return ammoRack;
            if (track != null) yield return track;

            foreach (var a in armor)
            {
                if (a != null) yield return a;
            }

            foreach (var aux in auxiliary)
            {
                if (aux != null) yield return aux;
            }
        }

        /// <summary>
        /// 장착 시도 — CompatibilityChecker.CheckAll 결과 반환.
        /// 실패 시 장착하지 않고 violations 포함 결과 반환.
        /// 성공 시 상태 유지 + Ok 반환.
        /// </summary>
        public CompatibilityResult TryEquip(PartCategory category, PartInstance part, int slotIndex = 0)
        {
            if (part == null || part.data == null)
                return CompatibilityResult.Fail("파츠 데이터 null");

            if (part.data.category != category)
                return CompatibilityResult.Fail($"카테고리 불일치: {part.data.category} != {category}");

            // 슬롯 위치 결정 + 기존 값 백업
            PartInstance oldSingleSlot = null;
            PartInstance oldMultiSlot = null;

            switch (category)
            {
                case PartCategory.Engine:
                    oldSingleSlot = engine;
                    engine = part;
                    break;
                case PartCategory.Turret:
                    oldSingleSlot = turret;
                    turret = part;
                    break;
                case PartCategory.MainGun:
                    oldSingleSlot = mainGun;
                    mainGun = part;
                    break;
                case PartCategory.AmmoRack:
                    oldSingleSlot = ammoRack;
                    ammoRack = part;
                    break;
                case PartCategory.Track:
                    oldSingleSlot = track;
                    track = part;
                    break;
                case PartCategory.Armor:
                    if (slotIndex < 0 || slotIndex >= armor.Count)
                        return CompatibilityResult.Fail($"Armor 슬롯 인덱스 범위 초과: {slotIndex} / {armor.Count}");
                    oldMultiSlot = armor[slotIndex];
                    armor[slotIndex] = part;
                    break;
                case PartCategory.Auxiliary:
                    if (slotIndex < 0 || slotIndex >= auxiliary.Count)
                        return CompatibilityResult.Fail($"Auxiliary 슬롯 인덱스 범위 초과: {slotIndex} / {auxiliary.Count}");
                    oldMultiSlot = auxiliary[slotIndex];
                    auxiliary[slotIndex] = part;
                    break;
                default:
                    return CompatibilityResult.Fail($"알 수 없는 카테고리: {category}");
            }

            // 호환성 검증 (필수 슬롯 체크 제외)
            var result = ValidateCompatibilityOnly();
            if (!result.isValid)
            {
                // 원복 — 단일 슬롯
                if (category == PartCategory.Engine) engine = oldSingleSlot;
                else if (category == PartCategory.Turret) turret = oldSingleSlot;
                else if (category == PartCategory.MainGun) mainGun = oldSingleSlot;
                else if (category == PartCategory.AmmoRack) ammoRack = oldSingleSlot;
                else if (category == PartCategory.Track) track = oldSingleSlot;
                // 복수 슬롯
                else if (category == PartCategory.Armor && slotIndex >= 0 && slotIndex < armor.Count)
                    armor[slotIndex] = oldMultiSlot;
                else if (category == PartCategory.Auxiliary && slotIndex >= 0 && slotIndex < auxiliary.Count)
                    auxiliary[slotIndex] = oldMultiSlot;

                return result;
            }

            // 장착 성공 — MaxHP 재계산
            RecalculateMaxHP();

            return CompatibilityResult.Ok;
        }

        /// <summary>
        /// 장착 해제 — 해당 슬롯의 PartInstance를 반환하고 null로 대체.
        /// 빈 슬롯이면 null 반환.
        /// </summary>
        public PartInstance Unequip(PartCategory category, int slotIndex = 0)
        {
            PartInstance removed = null;

            switch (category)
            {
                case PartCategory.Engine:
                    removed = engine;
                    engine = null;
                    break;
                case PartCategory.Turret:
                    removed = turret;
                    turret = null;
                    break;
                case PartCategory.MainGun:
                    removed = mainGun;
                    mainGun = null;
                    break;
                case PartCategory.AmmoRack:
                    removed = ammoRack;
                    ammoRack = null;
                    break;
                case PartCategory.Track:
                    removed = track;
                    track = null;
                    break;
                case PartCategory.Armor:
                    if (slotIndex >= 0 && slotIndex < armor.Count)
                    {
                        removed = armor[slotIndex];
                        armor[slotIndex] = null;
                    }
                    break;
                case PartCategory.Auxiliary:
                    if (slotIndex >= 0 && slotIndex < auxiliary.Count)
                    {
                        removed = auxiliary[slotIndex];
                        auxiliary[slotIndex] = null;
                    }
                    break;
            }

            // 장착 해제 성공 — MaxHP 재계산
            if (removed != null)
                RecalculateMaxHP();

            return removed;
        }

        /// <summary>
        /// 호환성 검증만 수행 (필수 슬롯 null 체크 제외) — TryEquip 내부용.
        /// 부분 장착 상태에서도 현재 장착분의 무게·출력·규격 호환성만 검사.
        /// </summary>
        private CompatibilityResult ValidateCompatibilityOnly()
        {
            // CompatibilityChecker.CheckAll 호출 (필수 슬롯 체크 제외)
            var equippedParts = AllEquipped().Select(p => p.data);
            var result = CompatibilityChecker.CheckAll(
                hullClass,
                turret?.data as TurretPartSO,
                mainGun?.data as MainGunPartSO,
                ammoRack?.data as AmmoRackPartSO,
                equippedParts);

            return result;
        }

        /// <summary>
        /// 현재 상태 전체 검증 — 필수 슬롯 + 호환성 3축 검사.
        /// 편성 확정 전 마지막 체크.
        /// </summary>
        public CompatibilityResult Validate()
        {
            var violations = new List<string>();

            // 필수 슬롯 체크
            if (engine == null) violations.Add("Engine 슬롯 비어있음");
            if (turret == null) violations.Add("Turret 슬롯 비어있음");
            if (mainGun == null) violations.Add("MainGun 슬롯 비어있음");
            if (ammoRack == null) violations.Add("AmmoRack 슬롯 비어있음");

            if (violations.Count > 0)
                return CompatibilityResult.Fail(violations.ToArray());

            // 호환성 검증 (필수 슬롯은 이미 확인)
            return ValidateCompatibilityOnly();
        }

        /// <summary>모든 장착 파츠 총 무게</summary>
        public float TotalWeight => AllEquipped().Sum(p => p.data?.weight ?? 0f);

        /// <summary>엔진 공급 출력 합 (EnginePartSO만)</summary>
        public float TotalPowerSupply
        {
            get
            {
                float supply = 0f;
                foreach (var part in AllEquipped())
                {
                    if (part.data is EnginePartSO engine)
                        supply += engine.powerOutput;
                }
                return supply;
            }
        }

        /// <summary>차체 기본 요구 + 장착 파츠 출력 수요 합</summary>
        public float TotalPowerDemand
        {
            get
            {
                float demand = HullClassDefaults.PowerRequirementFor(hullClass);
                foreach (var part in AllEquipped())
                {
                    if (!(part.data is EnginePartSO))
                        demand += part.data?.powerDraw ?? 0f;
                }
                return demand;
            }
        }

        /// <summary>차체 하중 용량</summary>
        public int WeightCapacity => HullClassDefaults.WeightCapacityFor(hullClass);

        /// <summary>
        /// MaxHP 재계산 — 차체 기본값 + 장갑 파츠 기여분.
        /// 차체 기본값: Scout=60, Assault=100, Support=110, Heavy=160, Siege=220
        /// 장갑 기여: 차체별 슬롯별로 소정 값 가산 (첫 빌드 임시값)
        /// </summary>
        private void RecalculateMaxHP()
        {
            // 차체 기본 HP — 하중 용량의 일부로 임시 계산
            MaxHP = HullClassDefaults.WeightCapacityFor(hullClass) / 2;

            // 장갑 슬롯별 기여 — 장갑 파츠 개수 × 기본 기여도
            int armorCount = armor.Count(a => a != null);
            if (armorCount > 0)
                MaxHP += armorCount * 5;  // 슬롯당 +5 HP 임시값

            // CurrentHP가 최대값 초과 시 cap (파츠 제거로 MaxHP 감소할 경우)
            if (CurrentHP > MaxHP)
                CurrentHP = MaxHP;
        }
    }
}
