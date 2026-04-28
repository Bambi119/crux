namespace Crux.Data
{
    /// <summary>승무원 직책 — 전차당 1명씩 배치, 변경 불가</summary>
    public enum CrewClass
    {
        Commander,      // 전차장
        Gunner,         // 포수
        Loader,         // 탄약수
        Driver,         // 조종수
        GunnerMech,     // 기총사수/수리병
        None            // 제약 없음 (특성·요구조건 용도)
    }

    /// <summary>스킬 타입 3종</summary>
    public enum SkillType
    {
        Passive,        // 항상 적용
        ActiveInstant,  // 즉시형 공격 스킬
        ActiveReactive  // 반응형 (턴 종료 시 예약, 트리거 조건 시 자동 발동)
    }

    /// <summary>반응형 스킬 트리거 타입</summary>
    public enum ReactiveTrigger
    {
        None,           // 반응형 아님
        Move,           // 적 이동 시 발동
        Fire,           // 적 사격 시 발동
        Sight           // 적 시야 진입 시 발동
    }

    /// <summary>스킬 요구조건 축</summary>
    public enum RequirementAxis
    {
        MainGunCaliber,  // 소 / 중 / 대
        MainGunMechanism, // 수동 / 반자동 / 자동 / 다연장
        AmmoType,        // AP / HE / 로켓 / 소이
        MGType,          // 경기총 / 중기총 / 개틀링
        HullClass        // 경 / 중 / 중 / 초중
    }

    /// <summary>요구조건 연산자 — AND / OR / None</summary>
    public enum RequirementOp
    {
        Any,    // OR — 값 중 하나라도 만족
        All,    // AND — 값 전부 만족
        None    // 요구조건 없음
    }

    /// <summary>사기 상태 대역</summary>
    public enum MoraleBand
    {
        High,    // 80~100 — 사기충천
        Normal,  // 50~79 — 정상
        Shaken,  // 25~49 — 흔들림
        Panic    // 0~24 — 공황
    }

    /// <summary>부상 단계</summary>
    public enum InjuryLevel
    {
        None,    // 부상 없음
        Minor,   // 경상
        Severe,  // 중상
        Fatal    // 치명상
    }

    /// <summary>선호 항목 태그 타입</summary>
    public enum PreferredTagType
    {
        TankClass,      // 전차 클래스 (경·중·중·초중)
        MainGunType,    // 주포 종류
        AmmoType,       // 탄종
        WeightClass,    // 중량대
        RepairModule    // 수리 모듈
    }

    /// <summary>트레잇 축 — 잠금 해제 조건. 누적 카운트로 활성화된다.</summary>
    public enum TraitAxis
    {
        BattleCount,                  // 누적 전투 횟수 (아스트라)
        BlindFireHits,                // 시야 외 사격 명중 횟수 (리리드)
        LoadCount,                    // 누적 장전 횟수 (그린)
        RocinanteBattles,             // 로시난테 탑승 전투 횟수 (프리테나)
        EngineCaterpillarRepairs,     // 엔진/캐터필러 수리 횟수 (이리스)
        None                          // 기본값 (트레잇 없음)
    }
}
