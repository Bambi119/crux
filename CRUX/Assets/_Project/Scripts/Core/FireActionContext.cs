using System.Collections.Generic;
using UnityEngine;
using Crux.Core;
using Crux.Data;
using Crux.Combat;

namespace Crux.Core
{
    /// <summary>연속 사격 연출 큐 — 공격→반격 등 다중 액션</summary>
    public static class FireActionContext
    {
        public static List<FireActionData> Actions = new();
        public static int CurrentIndex;

        /// <summary>현재 처리 중인 사격 데이터</summary>
        public static FireActionData Current =>
            Actions.Count > 0 && CurrentIndex < Actions.Count ? Actions[CurrentIndex] : default;

        /// <summary>대기 중인 사격이 있는가</summary>
        public static bool HasPendingAction => Actions.Count > 0;

        /// <summary>현재 다음 사격이 있는가</summary>
        public static bool HasNext => CurrentIndex + 1 < Actions.Count;

        /// <summary>사격 데이터를 큐에 추가</summary>
        public static void Enqueue(FireActionData data)
        {
            Actions.Add(data);
        }

        /// <summary>다음 사격으로 진행</summary>
        public static void Advance()
        {
            if (HasNext)
                CurrentIndex++;
        }

        /// <summary>큐 전체 초기화</summary>
        public static void Clear()
        {
            Actions.Clear();
            CurrentIndex = 0;
        }
    }

    /// <summary>씬 전환 시 전투 상태 보존 (static)</summary>
    public static class BattleStateStorage
    {
        public static bool HasSavedState;
        public static BattleSaveData SavedState;

        /// <summary>연출 씬 종료 후 복귀할 전략 씬 이름 (비어있으면 StrategyScene 기본)</summary>
        public static string SourceScene;

        public static void Save(BattleSaveData data)
        {
            SavedState = data;
            HasSavedState = true;
        }

        public static void Clear()
        {
            HasSavedState = false;
        }
    }

    /// <summary>전투 상태 스냅샷</summary>
    [System.Serializable]
    public struct UnitSaveData
    {
        public Vector2Int gridPosition;
        public float currentHP;
        public int currentAP;
        public float hullAngle;
        public bool isDestroyed;
        public Unit.ModuleSaveData[] moduleSaves;
        public bool isOnFire;
        public int remainingSmokeCharges;
        public int mainGunAmmoCount;
        public int mgAmmoLoaded;
        public int mgAmmoTotal;
        public bool isOverwatching;
        public bool isCounterImmune;           // 반격 면역 (오버워치 중)
        public bool hasCounteredThisExchange;  // 이번 교환에서 이미 반격 실행
        public bool counterConfirmed;          // 플레이어 반격 확정 여부
    }

    [System.Serializable]
    public struct BattleSaveData
    {
        public UnitSaveData playerState;
        public UnitSaveData[] enemyStates;
        public int turnCount;
        public TurnPhase phase;

        // 엄폐물 HP
        public float[] coverHPs;

        // 연막 셀 상태
        public int[] smokeTurns; // 전체 셀 순회, 연막 잔여 턴

        // 적 턴 중 사격 시 — 다음에 행동할 적 인덱스
        public int nextEnemyIndex;
    }

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
