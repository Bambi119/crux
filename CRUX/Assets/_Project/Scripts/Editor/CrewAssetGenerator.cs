using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>초기 5인 승무원 + 10개 특성 에셋 자동 생성 도구</summary>
public static class CrewAssetGenerator
{
    const string TraitsPath = "Assets/_Project/Data/Traits";
    const string MembersPath = "Assets/_Project/Data/Crew/Members";

    [MenuItem("Crux/Generate/Initial Crew Assets")]
    public static void GenerateInitialCrewAssets()
    {
        EnsureFolder(TraitsPath);
        EnsureFolder(MembersPath);

        var traits = new Dictionary<string, Crux.Data.TraitSO>();

        traits["donquixote_dream"] = CreateTrait("donquixote_dream", "돈키호테의 꿈", true,
            Crux.Data.CrewClass.Commander, "전투 시작 사기 +10 (마크 보너스와 별개)", "trait.donquixote_dream");

        traits["first_battle_fear"] = CreateTrait("first_battle_fear", "첫 전투의 공포", false,
            Crux.Data.CrewClass.Commander, "누적 전투 수 ≤5일 때 명중 -10", "trait.first_battle_fear");

        traits["hermit_eye"] = CreateTrait("hermit_eye", "은둔자의 눈", true,
            Crux.Data.CrewClass.Gunner, "전차 내부 사격 시 명중 +10 (상시)", "trait.hermit_eye");

        traits["dialogue_phobia"] = CreateTrait("dialogue_phobia", "대화 공포", false,
            Crux.Data.CrewClass.Gunner, "전차장 지휘 스킬 수혜 효과 절반", "trait.dialogue_phobia");

        traits["silent_worker"] = CreateTrait("silent_worker", "묵묵한 일꾼", true,
            Crux.Data.CrewClass.Loader, "탄종 관계없이 장전 AP 항상 -1 (기본 탄약수 보너스 중첩)", "trait.silent_worker");

        traits["wordless_comrade"] = CreateTrait("wordless_comrade", "말없는 동료", false,
            Crux.Data.CrewClass.Loader, "지휘 스킬 수혜 불가 — 음성 명령 불통", "trait.wordless_comrade");

        traits["rocinante_owner"] = CreateTrait("rocinante_owner", "로시난테의 주인", true,
            Crux.Data.CrewClass.Driver, "로시난테 탑승 시 이동 AP -1 추가", "trait.rocinante_owner");

        traits["spoiled"] = CreateTrait("spoiled", "응석받이", false,
            Crux.Data.CrewClass.Driver, "사기 ≤50일 때 이동 명령 1턴 1회 실패 10%", "trait.spoiled");

        traits["little_hand_prodigy"] = CreateTrait("little_hand_prodigy", "작은 손의 신동", true,
            Crux.Data.CrewClass.GunnerMech, "엔진·캐터필러 수리 턴 절반 추가", "trait.little_hand_prodigy");

        traits["brother_dependent"] = CreateTrait("brother_dependent", "오빠 의존", false,
            Crux.Data.CrewClass.GunnerMech, "아스트라 공석·부상 시 명중·Tech -10", "trait.brother_dependent");

        CreateMember("astra", "아스트라", Crux.Data.CrewClass.Commander, 50, 55, 45,
            Crux.Data.PreferredTagType.TankClass, "medium",
            traits["donquixote_dream"], traits["first_battle_fear"],
            new[] { "medium" }, new[] { 1 },
            "지하 벙커에서 자란 16세 소년. 돈키호테 책에서 로시난테의 이름 기원을 알고 있다. 일행의 리더이자 이리스의 오빠.");

        CreateMember("ririd", "리리드", Crux.Data.CrewClass.Gunner, 75, 60, 35,
            Crux.Data.PreferredTagType.MainGunType, "medium_caliber",
            traits["hermit_eye"], traits["dialogue_phobia"],
            new[] { "medium_caliber" }, new[] { 1 },
            "전차 안이 세상의 전부인 15세 히키코모리 소녀. 눈이 좋고 직감형. 수줍음 많음. 아스트라가 리리드의 첫 번째 친구.");

        CreateMember("grin", "그린", Crux.Data.CrewClass.Loader, 30, 40, 55,
            Crux.Data.PreferredTagType.AmmoType, "ap",
            traits["silent_worker"], traits["wordless_comrade"],
            new[] { "ap" }, new[] { 1 },
            "벙어리 17세 거한. 선하고 순박하고 성실한 먹보. 로시난테가 집이고 친구들이 가족. 리리드의 두 번째 친구.");

        CreateMember("pretena", "프리테나", Crux.Data.CrewClass.Driver, 40, 60, 70,
            Crux.Data.PreferredTagType.WeightClass, "medium",
            traits["rocinante_owner"], traits["spoiled"],
            new[] { "medium" }, new[] { 2 },
            "로시난테의 최초 발견자이자 이름을 붙인 19세. 이리스의 스승. 가장 연장자이지만 응석받이인 활달한 성격.");

        CreateMember("iris", "이리스", Crux.Data.CrewClass.GunnerMech, 30, 45, 75,
            Crux.Data.PreferredTagType.RepairModule, "engine",
            traits["little_hand_prodigy"], traits["brother_dependent"],
            new[] { "caterpillar", "engine" }, new[] { 2, 1 },
            "프리테나를 따라다니며 기계를 배운 13세 신동. 아스트라의 동생. 조용하지만 기계 앞에서는 집요하다.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CRUX] 초기 5인 + 10 특성 생성 완료");
    }

    static Crux.Data.TraitSO CreateTrait(string id, string displayName, bool isPositive,
        Crux.Data.CrewClass classRestriction, string description, string effectKey)
    {
        string assetPath = TraitsPath + "/Trait_" + id + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<Crux.Data.TraitSO>(assetPath);
        if (existing != null)
        {
            Debug.LogWarning("[CRUX] " + assetPath + " 이미 존재 — 스킵");
            return existing;
        }

        var trait = ScriptableObject.CreateInstance<Crux.Data.TraitSO>();
        trait.id = id;
        trait.displayName = displayName;
        trait.isPositive = isPositive;
        trait.classRestriction = classRestriction;
        trait.description = description;
        trait.effectKey = effectKey;

        AssetDatabase.CreateAsset(trait, assetPath);
        return trait;
    }

    static Crux.Data.CrewMemberSO CreateMember(string id, string displayName, Crux.Data.CrewClass klass,
        int aim, int react, int tech,
        Crux.Data.PreferredTagType preferredTagType, string preferredTag,
        Crux.Data.TraitSO traitPositive, Crux.Data.TraitSO traitNegative,
        string[] startingMarkKeys, int[] startingMarkValues,
        string storyRef)
    {
        string assetPath = MembersPath + "/Crew_" + id + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<Crux.Data.CrewMemberSO>(assetPath);
        if (existing != null)
        {
            Debug.LogWarning("[CRUX] " + assetPath + " 이미 존재 — 스킵");
            return existing;
        }

        var member = ScriptableObject.CreateInstance<Crux.Data.CrewMemberSO>();
        member.id = id;
        member.displayName = displayName;
        member.klass = klass;
        member.aim = aim;
        member.react = react;
        member.tech = tech;
        member.preferredTagType = preferredTagType;
        member.preferredTag = preferredTag;
        member.traitPositive = traitPositive;
        member.traitNegative = traitNegative;
        member.startingMarkKeys = startingMarkKeys;
        member.startingMarkValues = startingMarkValues;
        member.storyRef = storyRef;

        AssetDatabase.CreateAsset(member, assetPath);
        return member;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string currentPath = "";

        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
                continue;

            string nextPath = currentPath == "" ? parts[i] : currentPath + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath == "" ? "Assets" : currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}
