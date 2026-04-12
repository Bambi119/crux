using UnityEngine;

namespace Crux.Data
{
    /// <summary>전차 분류 — 무게/역할 기준</summary>
    public enum TankClass
    {
        Vehicle,    // 차량 (비전투차량, 경장갑)
        Light,      // 경전차
        Medium,     // 중형전차
        Heavy       // 중전차
    }

    [CreateAssetMenu(fileName = "NewTank", menuName = "CRUX/Tank Data")]
    public class TankDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string tankName;
        [Tooltip("전차 분류 (차량/경/중형/중전차)")]
        public TankClass tankClass = TankClass.Medium;

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
