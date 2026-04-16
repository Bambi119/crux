using System;
using System.Collections.Generic;
using System.Linq;

namespace Crux.Data
{
    /// <summary>
    /// 호환성 3중 체계 검사기 — docs/05 §3.
    /// 정적 메서드만 제공. 상태 없음.
    ///
    /// 세 축: 하중(Weight) · 출력(Power) · 규격(Specification).
    /// 모두 통과해야 장착 성립.
    ///
    /// 런타임 장착 상태 관리(PartInstance, TankInstance, ConvoyInventory)는 P4-c에서 진행.
    /// 이 검사기는 순수 정적 유효성 판단만 담당.
    /// </summary>
    public static class CompatibilityChecker
    {
        /// <summary>
        /// 하중 검사 — 파츠 총중량이 차체 한도 초과 여부.
        /// docs/05 §3.1: 총중량 = 차체 + 엔진 + 포탑 + 주포 + 장갑 + 탄약 + 캐터필러 + 보조.
        /// </summary>
        /// <param name="hull">차체 종류</param>
        /// <param name="parts">장착할 파츠 목록. null이면 empty로 취급</param>
        /// <returns>Ok 또는 하중 초과 사유 포함 Fail</returns>
        public static CompatibilityResult CheckWeight(HullClass hull, IEnumerable<PartDataSO> parts)
        {
            if (parts == null) parts = System.Array.Empty<PartDataSO>();

            var partList = parts.ToList();
            float totalWeight = partList.Sum(p => p.weight);
            float capacity = HullClassDefaults.WeightCapacityFor(hull);

            if (totalWeight > capacity)
            {
                string msg = $"하중 초과: 총 {totalWeight:F1}kg / 용량 {capacity} — 초과 {totalWeight - capacity:F1}kg";
                return CompatibilityResult.Fail(msg);
            }

            return CompatibilityResult.Ok;
        }

        /// <summary>
        /// 출력 검사 — 엔진 출력이 차체 요구 + 파츠 수요를 충족하는지 검증.
        /// docs/05 §3.2: 엔진 출력 ≥ 차체 최소 출력 + 장착 파츠 합 출력 수요.
        ///
        /// parts에 포함된 EnginePartSO의 powerOutput을 모두 합산하고,
        /// 다른 파츠의 powerDraw를 모두 합산한 뒤 비교.
        /// </summary>
        /// <param name="hull">차체 종류</param>
        /// <param name="parts">장착할 파츠 목록 (engine 포함 가능). null이면 empty</param>
        /// <returns>Ok 또는 출력 부족 사유 포함 Fail</returns>
        public static CompatibilityResult CheckPower(HullClass hull, IEnumerable<PartDataSO> parts)
        {
            if (parts == null) parts = System.Array.Empty<PartDataSO>();

            var partList = parts.ToList();

            // 엔진 출력 합산
            float totalPowerOutput = 0f;
            foreach (var part in partList)
            {
                if (part is EnginePartSO engine)
                {
                    totalPowerOutput += engine.powerOutput;
                }
            }

            // 차체 기본 요구 + 다른 파츠 출력 수요 합산
            float hullPowerReq = HullClassDefaults.PowerRequirementFor(hull);
            float totalPowerDemand = hullPowerReq;
            foreach (var part in partList)
            {
                if (!(part is EnginePartSO))
                {
                    totalPowerDemand += part.powerDraw;
                }
            }

            if (totalPowerOutput < totalPowerDemand)
            {
                string msg = $"출력 부족: 공급 {totalPowerOutput:F0} / 요구 {totalPowerDemand:F1} — 부족 {totalPowerDemand - totalPowerOutput:F1}";
                return CompatibilityResult.Fail(msg);
            }

            return CompatibilityResult.Ok;
        }

        /// <summary>
        /// 규격 검사 — 포탑-주포 구경 호환성 및 차체별 파츠 제약 검증.
        /// docs/05 §3.3.
        ///
        /// 검사 항목:
        /// 1. 주포 구경 ≤ 포탑 수용 구경
        /// 2. 탄약고 차체 제약 (hullClassRestrictions 배열 체크)
        /// 3. specTags 교차 검사 — **규격 태그 매칭 규칙 미확정** (docs/05 §11)
        ///    이번 커밋에서는 비워두고 TODO.
        ///
        /// null safe: turret/mainGun/ammoRack이 null이면 해당 검사 스킵 후 Ok 반환.
        /// </summary>
        /// <param name="hull">차체 종류</param>
        /// <param name="turret">포탑 (null 가능)</param>
        /// <param name="mainGun">주포 (null 가능)</param>
        /// <param name="ammoRack">탄약고 (null 가능)</param>
        /// <returns>Ok 또는 규격 위반 사유 포함 Fail</returns>
        public static CompatibilityResult CheckSpec(
            HullClass hull,
            TurretPartSO turret,
            MainGunPartSO mainGun,
            AmmoRackPartSO ammoRack)
        {
            var violations = new List<string>();

            // 포탑-주포 구경 검사
            if (turret != null && mainGun != null)
            {
                if (mainGun.caliber > turret.caliberLimit)
                {
                    violations.Add($"주포 구경 {mainGun.caliber}mm > 포탑 허용 {turret.caliberLimit}mm");
                }
            }

            // 탄약고 차체 제약 검사
            if (ammoRack != null)
            {
                if (ammoRack.hullClassRestrictions != null && ammoRack.hullClassRestrictions.Length > 0)
                {
                    // hullClassRestrictions가 지정되면, 그 목록에 현재 hull이 포함되어야 함.
                    // 포함되지 않으면 위반.
                    if (!ammoRack.hullClassRestrictions.Contains(hull.ToString()))
                    {
                        string allowed = string.Join(", ", ammoRack.hullClassRestrictions);
                        violations.Add($"탄약고는 {allowed}에만 장착 가능 (현재 차체: {hull})");
                    }
                }
            }

            // TODO: specTags 교차 검사
            // docs/05 §11에서 규격 태그 매칭 규칙이 확정될 때까지 미구현.
            // 구현 시: turret.specTags, mainGun.specTags, ammoRack.specTags 간
            // 필수/금지 조합 검증 추가 예정.

            if (violations.Count > 0)
            {
                return CompatibilityResult.Fail(violations.ToArray());
            }

            return CompatibilityResult.Ok;
        }

        /// <summary>
        /// 호환성 3축 전체 검사 — CheckWeight, CheckPower, CheckSpec 결과 합산.
        /// docs/05 §3: 모든 축을 통과해야 장착 성립.
        ///
        /// violations 배열은 세 검사의 실패 사유를 모두 포함.
        /// (다중 축 동시 위반 가능성 고려)
        /// </summary>
        /// <param name="hull">차체</param>
        /// <param name="turret">포탑</param>
        /// <param name="mainGun">주포</param>
        /// <param name="ammoRack">탄약고</param>
        /// <param name="allEquippedParts">장착할 모든 파츠 목록</param>
        /// <returns>전체 호환성 결과 (violations 합산)</returns>
        public static CompatibilityResult CheckAll(
            HullClass hull,
            TurretPartSO turret,
            MainGunPartSO mainGun,
            AmmoRackPartSO ammoRack,
            IEnumerable<PartDataSO> allEquippedParts)
        {
            var allViolations = new List<string>();

            // 하중 검사
            var weightResult = CheckWeight(hull, allEquippedParts);
            if (!weightResult.isValid)
            {
                allViolations.AddRange(weightResult.violations);
            }

            // 출력 검사
            var powerResult = CheckPower(hull, allEquippedParts);
            if (!powerResult.isValid)
            {
                allViolations.AddRange(powerResult.violations);
            }

            // 규격 검사
            var specResult = CheckSpec(hull, turret, mainGun, ammoRack);
            if (!specResult.isValid)
            {
                allViolations.AddRange(specResult.violations);
            }

            if (allViolations.Count > 0)
            {
                return CompatibilityResult.Fail(allViolations.ToArray());
            }

            return CompatibilityResult.Ok;
        }
    }
}
