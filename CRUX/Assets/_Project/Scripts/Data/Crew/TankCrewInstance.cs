using System.Collections.Generic;

namespace Crux.Data
{
    /// <summary>
    /// 편성 씬 TankInstance의 승무원 매핑 모델.
    /// 5직책 슬롯(Commander/Gunner/Loader/Driver/GunnerMech) + 공석 관리.
    ///
    /// 이름 규약: MonoBehaviour TankCrew와 구분하기 위해 Instance 붙임.
    /// Data 레이어 순수 모델 — 세이브 직렬화 예정.
    /// </summary>
    public class TankCrewInstance
    {
        public CrewMemberRuntime commander;
        public CrewMemberRuntime gunner;
        public CrewMemberRuntime loader;
        public CrewMemberRuntime driver;
        public CrewMemberRuntime gunnerMech;

        /// <summary>직책으로 승무원 조회. 공석은 null.</summary>
        public CrewMemberRuntime Get(CrewClass klass) => klass switch
        {
            CrewClass.Commander => commander,
            CrewClass.Gunner => gunner,
            CrewClass.Loader => loader,
            CrewClass.Driver => driver,
            CrewClass.GunnerMech => gunnerMech,
            _ => null
        };

        /// <summary>직책에 승무원 할당.</summary>
        public void Set(CrewClass klass, CrewMemberRuntime crew)
        {
            switch (klass)
            {
                case CrewClass.Commander:
                    commander = crew;
                    break;
                case CrewClass.Gunner:
                    gunner = crew;
                    break;
                case CrewClass.Loader:
                    loader = crew;
                    break;
                case CrewClass.Driver:
                    driver = crew;
                    break;
                case CrewClass.GunnerMech:
                    gunnerMech = crew;
                    break;
            }
        }

        /// <summary>모든 직책과 해당 승무원을 열거</summary>
        public IEnumerable<(CrewClass klass, CrewMemberRuntime crew)> All()
        {
            yield return (CrewClass.Commander, commander);
            yield return (CrewClass.Gunner, gunner);
            yield return (CrewClass.Loader, loader);
            yield return (CrewClass.Driver, driver);
            yield return (CrewClass.GunnerMech, gunnerMech);
        }

        /// <summary>배치된 승무원 수 (공석 제외)</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                if (commander != null) count++;
                if (gunner != null) count++;
                if (loader != null) count++;
                if (driver != null) count++;
                if (gunnerMech != null) count++;
                return count;
            }
        }

        /// <summary>공석 개수</summary>
        public int VacantCount => 5 - OccupiedCount;

        /// <summary>특정 직책이 공석 상태인지 확인</summary>
        public bool HasVacancy(CrewClass klass) => Get(klass) == null;
    }
}
