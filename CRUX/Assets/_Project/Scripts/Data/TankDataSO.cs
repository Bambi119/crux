using UnityEngine;

namespace Crux.Data
{
    /// <summary>차체 분류 — docs/05 §1. 5 차체 고정.</summary>
    public enum HullClass
    {
        Scout,     // 경전차 — 고속·경장·소구경 — 정찰·측면 기동
        Assault,   // 중형전차 — 범용 밸런스 — 주전투 라인 (로시난테 기본값)
        Support,   // 지원전차 — 수리·보조 장비 풍부 — 수리·보급
        Heavy,     // 중전차 — 대구경·후장 — 관통 우위·요새화
        Siege      // 초중전차 — 다연장·고정형 — 거점 공격·대보스
    }

    /// <summary>
    /// 차체별 파츠 슬롯 수. docs/05 §1.1.
    /// 특정 전차 인스턴스가 기본값을 벗어나야 하면 TankDataSO 인스펙터에서 개별 조정.
    /// </summary>
    [System.Serializable]
    public struct HullSlotTable
    {
        public int engine;
        public int turret;
        public int mainGun;
        [Tooltip("장갑 면별 독립 장착 슬롯 수")]
        public int armor;
        public int ammoRack;
        public int track;
        public int auxiliary;

        public static HullSlotTable ForHull(HullClass cls) => cls switch
        {
            HullClass.Scout   => new HullSlotTable { engine = 1, turret = 1, mainGun = 1, armor = 3, ammoRack = 1, track = 1, auxiliary = 1 },
            HullClass.Assault => new HullSlotTable { engine = 1, turret = 1, mainGun = 1, armor = 4, ammoRack = 1, track = 1, auxiliary = 2 },
            HullClass.Support => new HullSlotTable { engine = 1, turret = 1, mainGun = 1, armor = 4, ammoRack = 1, track = 1, auxiliary = 4 },
            HullClass.Heavy   => new HullSlotTable { engine = 1, turret = 1, mainGun = 1, armor = 6, ammoRack = 1, track = 1, auxiliary = 3 },
            HullClass.Siege   => new HullSlotTable { engine = 1, turret = 1, mainGun = 2, armor = 6, ammoRack = 2, track = 1, auxiliary = 3 },
            _ => new HullSlotTable { engine = 1, turret = 1, mainGun = 1, armor = 4, ammoRack = 1, track = 1, auxiliary = 2 }
        };

        /// <summary>모든 슬롯이 0이면 미초기화 상태로 간주</summary>
        public bool IsEmpty() => engine == 0 && turret == 0 && mainGun == 0 && armor == 0 && ammoRack == 0 && track == 0 && auxiliary == 0;
    }

    /// <summary>차체별 하중/출력 기본값 — docs/05 §7.1</summary>
    public static class HullClassDefaults
    {
        public static int WeightCapacityFor(HullClass cls) => cls switch
        {
            HullClass.Scout => 60,
            HullClass.Assault => 100,
            HullClass.Support => 110,
            HullClass.Heavy => 160,
            HullClass.Siege => 220,
            _ => 100
        };

        public static int PowerRequirementFor(HullClass cls) => cls switch
        {
            HullClass.Scout => 80,
            HullClass.Assault => 100,
            HullClass.Support => 95,
            HullClass.Heavy => 120,
            HullClass.Siege => 150,
            _ => 100
        };

        /// <summary>
        /// 이니셔티브(선공 판정)용 차체 속도 보정 — docs/03 §2.3.
        /// 이동 AP 계산용 moveSpeed와는 별개. 전투 시작 한 번만 반영되는 상수 보정.
        /// 범위: Scout +4 ~ Siege -2.
        /// </summary>
        public static int InitiativeSpeedFor(HullClass cls) => cls switch
        {
            HullClass.Scout => +4,
            HullClass.Assault => +2,
            HullClass.Support => +1,
            HullClass.Heavy => 0,
            HullClass.Siege => -2,
            _ => 0
        };
    }

    [CreateAssetMenu(fileName = "NewTank", menuName = "CRUX/Tank Data")]
    public class TankDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string tankName;
        [Tooltip("차체 분류 (Scout/Assault/Support/Heavy/Siege)")]
        public HullClass hullClass = HullClass.Assault;
        [Tooltip("로시난테 특례 전차 플래그 — 폐기 불가, 전용 파츠 장착 가능")]
        public bool isRocinante = false;

        [Header("차체 스펙 (docs/05 §3 호환성 3중 체계)")]
        [Tooltip("하중 한도. 장착 파츠 총 중량이 이 값을 초과하면 장착 거부")]
        public int weightCapacity = 100;
        [Tooltip("차체 기준 출력 요구. 엔진 출력이 이 값 + 파츠 출력 수요 합보다 작으면 장착 거부")]
        public int powerRequirement = 100;
        [Tooltip("파츠 카테고리별 슬롯 수 — 비어 있으면 hullClass 기본값으로 자동 채움")]
        public HullSlotTable slotTable;

        [Header("AP")]
        public int maxAP = 6;

        [Header("이동")]
        public float moveSpeed = 3f; // 시각적 이동 속도

        [Header("장갑 (mm)")]
        public ArmorProfile armor;

        [Header("포탑")]
        public float turretRotationSpeed = 60f;
        [Tooltip("포구 위치 오프셋 (차체 로컬 기준, 스프라이트 → 방향)")]
        public Vector2 muzzleOffset = new Vector2(0.8f, 0f);

        [Header("주포")]
        [Tooltip("주포 구경 (mm)")]
        public int mainGunCaliber = 75;

        [Header("사격")]
        public int fireCost = 3;

        [Header("내구력")]
        public int maxHP = 100;

        [Header("연막")]
        [Tooltip("연막 발사기 장탄수 (0=미장착)")]
        public int smokeCharges = 2;

        [Header("탄약 적재량")]
        [Tooltip("주포 최대 적재 탄수")]
        public int maxMainGunAmmo = 42;
        [Tooltip("기관총 최대 적재 탄수 (벨트 포함 총량)")]
        public int maxMGAmmo = 1200;
        [Tooltip("기관총 1벨트 장전량 (한 번에 장전된 수량)")]
        public int mgLoadedAmmo = 120;

        [Header("모듈 내구력")]
        public ModuleHPProfile moduleHP;

        [Header("화재 저항")]
        [Tooltip("모듈 관통 시 화재 발생 확률 경감 (0~100%). 기본값 10")]
        public float fireResistancePercent = 10f;

        [Header("편성 파츠 반영값 (런타임)")]
        [Tooltip("편성 엔진 powerOutput — 편성 진입 시 주입, Inspector 원본은 0")]
        public float partsEnginePowerOutput = 0f;

        [Tooltip("편성 궤도 mobilityBonus — 편성 진입 시 주입")]
        public int partsTrackMobilityBonus = 0;

        [Tooltip("편성 파츠 총 중량이 차체 하중 한도 초과 여부 — 편성 진입 시 주입")]
        public bool partsIsOverweight = false;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 하중/출력이 미설정(0 이하)이면 hullClass 기본값으로 자동 채움
            if (weightCapacity <= 0) weightCapacity = HullClassDefaults.WeightCapacityFor(hullClass);
            if (powerRequirement <= 0) powerRequirement = HullClassDefaults.PowerRequirementFor(hullClass);
            // 슬롯 테이블이 미설정이면 hullClass 기본값으로 자동 채움
            if (slotTable.IsEmpty()) slotTable = HullSlotTable.ForHull(hullClass);
        }
#endif
    }

    [System.Serializable]
    public struct ArmorProfile
    {
        [Tooltip("전면 (0°)")]
        public float front;
        [Tooltip("측면 평균 (FrontL/R, RearL/R 기본값) — 세분화 필요 시 아래 4개 필드 사용")]
        public float side;
        [Tooltip("후면 (180°)")]
        public float rear;
        [Tooltip("포탑")]
        public float turret;

        [Header("6섹터 세분화 (선택 — 0이면 side/front 값 사용)")]
        public float frontLeft;
        public float frontRight;
        public float rearLeft;
        public float rearRight;

        /// <summary>섹터별 장갑 값 조회 — 세분화 미설정 시 기본값(front/side/rear)으로 폴백</summary>
        public float GetArmor(Crux.Core.HitZone zone)
        {
            float fb = front > 0 ? front : 80f;
            float sb = side > 0 ? side : 40f;
            float rb = rear > 0 ? rear : 30f;
            return zone switch
            {
                Crux.Core.HitZone.Front => fb,
                Crux.Core.HitZone.FrontLeft => frontLeft > 0 ? frontLeft : (fb + sb) * 0.5f,
                Crux.Core.HitZone.FrontRight => frontRight > 0 ? frontRight : (fb + sb) * 0.5f,
                Crux.Core.HitZone.RearLeft => rearLeft > 0 ? rearLeft : (rb + sb) * 0.5f,
                Crux.Core.HitZone.RearRight => rearRight > 0 ? rearRight : (rb + sb) * 0.5f,
                Crux.Core.HitZone.Rear => rb,
                Crux.Core.HitZone.Turret => turret > 0 ? turret : 60f,
                _ => sb
            };
        }
    }

    [System.Serializable]
    public struct ModuleHPProfile
    {
        public float engine;
        public float barrel;
        public float machineGun;
        public float ammoRack;
        public float loader;
        public float caterpillarLeft;
        public float caterpillarRight;
        public float turretRing;

        /// <summary>기본 프로파일 (Inspector 미설정 시 사용)</summary>
        public static ModuleHPProfile Default => new ModuleHPProfile
        {
            engine = 40f,
            barrel = 35f,
            machineGun = 25f,
            ammoRack = 20f,
            loader = 30f,
            caterpillarLeft = 30f,
            caterpillarRight = 30f,
            turretRing = 35f
        };
    }
}
