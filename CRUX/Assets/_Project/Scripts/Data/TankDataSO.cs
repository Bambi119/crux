using UnityEngine;

namespace Crux.Data
{
    [CreateAssetMenu(fileName = "NewTank", menuName = "CRUX/Tank Data")]
    public class TankDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string tankName;

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

        [Header("사격")]
        public int fireCost = 3;

        [Header("내구력")]
        public int maxHP = 100;

        [Header("모듈 내구력")]
        public ModuleHPProfile moduleHP;
    }

    [System.Serializable]
    public struct ArmorProfile
    {
        public float front;
        public float side;
        public float rear;
        public float turret;
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
