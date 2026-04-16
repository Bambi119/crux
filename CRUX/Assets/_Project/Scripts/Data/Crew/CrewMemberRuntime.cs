using System.Collections.Generic;
using Crux.Data;

namespace Crux.Data
{
    /// <summary>
    /// 승무원 런타임 상태 래퍼. CrewMemberSO(불변 기본값) + 가변 마크·쿨다운·부상 상태.
    /// 전차 1대당 최대 5명(직책별 1명)이 TankCrewInstance에 할당된다.
    /// </summary>
    public class CrewMemberRuntime
    {
        public readonly CrewMemberSO data;

        // 축별 현재 마크 (0~5). 축 ID는 직책 종속 문자열 (예: "caliber.large", "hull.medium")
        public readonly Dictionary<string, int> currentMarks = new Dictionary<string, int>();

        // 기여도 반영 누적 킬 (float — 0.5·1.0 카운트)
        public readonly Dictionary<string, float> killCounters = new Dictionary<string, float>();

        // 축별 참전 전투 수
        public readonly Dictionary<string, int> battleCounters = new Dictionary<string, int>();

        // 스킬 보유 풀 / 장착 슬롯
        public readonly List<CrewSkillSO> ownedSkills = new List<CrewSkillSO>();
        public readonly CrewSkillSO[] equippedPassives = new CrewSkillSO[2];
        public readonly CrewSkillSO[] equippedActives = new CrewSkillSO[2];

        // 스킬 ID → 남은 쿨다운 턴
        public readonly Dictionary<string, int> cooldowns = new Dictionary<string, int>();

        // 부상 상태 — 전투 종료 시 확률로 변동 (docs/04 §8)
        public InjuryLevel injuryState = InjuryLevel.None;

        public CrewMemberRuntime(CrewMemberSO data)
        {
            this.data = data;
            LoadStartingMarks();
        }

        private void LoadStartingMarks()
        {
            if (data == null || data.startingMarkKeys == null) return;
            int count = data.startingMarkKeys.Length;
            for (int i = 0; i < count; i++)
            {
                if (i >= data.startingMarkValues.Length) break;
                string key = data.startingMarkKeys[i];
                if (string.IsNullOrEmpty(key)) continue;
                currentMarks[key] = data.startingMarkValues[i];
            }
        }

        // 기본 스탯 — SO 값 그대로
        public int BaseAim => data != null ? data.aim : 50;
        public int BaseReact => data != null ? data.react : 50;
        public int BaseTech => data != null ? data.tech : 50;
        public CrewClass Class => data != null ? data.klass : CrewClass.None;
        public string DisplayName => data != null ? data.displayName : "(vacant)";

        // 마크 접근
        public int GetMark(string axis)
            => currentMarks.TryGetValue(axis, out var v) ? v : 0;

        /// <summary>현재 보유 마크 중 최대값 — 전차장 기본 보너스 계산 등에 사용</summary>
        public int MaxMark()
        {
            int max = 0;
            foreach (var m in currentMarks.Values)
                if (m > max) max = m;
            return max;
        }

        /// <summary>마크 보너스 테이블 — docs/04 §3.2</summary>
        public static int MarkBonus(int mark) => mark switch
        {
            1 => 3,
            2 => 6,
            3 => 10,
            4 => 14,
            5 => 20,
            _ => 0
        };

        // 쿨다운
        public bool IsOnCooldown(string skillId)
            => cooldowns.TryGetValue(skillId, out var t) && t > 0;

        public int GetCooldown(string skillId)
            => cooldowns.TryGetValue(skillId, out var t) ? t : 0;

        public void SetCooldown(string skillId, int turns)
        {
            if (turns <= 0) cooldowns.Remove(skillId);
            else cooldowns[skillId] = turns;
        }

        public void TickCooldowns()
        {
            if (cooldowns.Count == 0) return;
            // 키 스냅샷 후 감소 (사전 순회 중 변경 방지)
            var keys = new List<string>(cooldowns.Keys);
            foreach (var k in keys)
            {
                int v = cooldowns[k] - 1;
                if (v <= 0) cooldowns.Remove(k);
                else cooldowns[k] = v;
            }
        }

        /// <summary>부상 상태에서 전투 가능 여부 — 중상/치명상은 공석 취급 (docs/04 §8.1)</summary>
        public bool IsCombatReady
            => injuryState == InjuryLevel.None || injuryState == InjuryLevel.Minor;
    }
}
