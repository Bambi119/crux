using UnityEngine;
using Crux.Data;
using Crux.Combat;

namespace Crux.Core
{
    /// <summary>사격 연출에 필요한 모든 데이터</summary>
    [System.Serializable]
    public struct FireActionData
    {
        // 공격자 정보
        public Vector3 attackerWorldPos;
        public float attackerHullAngle;
        public string attackerName;
        public PlayerSide attackerSide;

        // 대상 정보
        public Vector3 targetWorldPos;
        public float targetHullAngle;
        public string targetName;

        // 스프라이트 (연출용)
        public Sprite attackerHullSprite;
        public Sprite attackerTurretSprite;
        public float attackerSpriteRotOffset; // 스프라이트 방향 보정 (→ 기준)
        public Sprite targetHullSprite;
        public Sprite targetTurretSprite;
        public float targetSpriteRotOffset;

        // 머즐 오프셋
        public Vector2 attackerMuzzleOffset;

        // 엄폐 상태
        public bool attackerInCover;     // 공격자 엄폐 중
        public string attackerCoverName; // 엄폐물 이름
        public CoverSize attackerCoverSize; // 엄폐물 크기
        public Grid.HexFacet attackerCoverFacets; // 공격자 엄폐물 방호면

        // 대상 엄폐 상태
        public bool targetInCover;       // 대상이 엄폐 중인지 (비주얼 표시용)
        public bool targetCoverHit;      // 엄폐물이 대신 맞았는지
        public float coverDamageDealt;   // 엄폐물에 입힌 데미지
        public string targetCoverName;   // 피격된 엄폐물 이름
        public CoverSize targetCoverSize; // 대상 엄폐물 크기
        public Grid.HexFacet targetCoverFacets; // 대상 엄폐물 방호면

        // 무기
        public WeaponType weaponType;
        public AmmoDataSO ammoData;
        public MachineGunDataSO mgData;

        // 사전 계산된 결과 (주포용)
        public ShotResult result;
        public Unit.DamageOutcome mainOutcome; // 주포 데미지 사전 롤 (격파/화재/모듈/유폭)

        // 기관총 결과 (버스트)
        public ShotResult[] mgResults;
        public Unit.DamageOutcome mgAggregateOutcome; // 기총 전체 사전 롤 (격파/화재/모듈/유폭)

        // 대상 유닛 참조 (씬 복귀 후 데미지 적용)
        // 주의: 씬 전환 시 GameObject는 파괴되므로 인덱스로 참조
        public int targetUnitIndex;
        public PlayerSide targetSide;
    }
}
