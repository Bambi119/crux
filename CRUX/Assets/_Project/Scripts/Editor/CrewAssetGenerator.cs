using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>초기 5인 승무원 + 6개 특성 에셋 자동 생성 도구 (누적 카운트 기반 unlock 모델)</summary>
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

        // 6개 trait: 누적 카운트 기반 unlock 모델
        traits["donquixote_dream"] = CreateTrait("donquixote_dream", "돈키호테의 꿈",
            Crux.Data.TraitAxis.BattleCount, "전투 수 누적으로 unlock");

        traits["hermit_eye"] = CreateTrait("hermit_eye", "은둔자의 눈",
            Crux.Data.TraitAxis.None, "항상 활성 (제한 없음)");

        traits["little_hand_prodigy"] = CreateTrait("little_hand_prodigy", "작은 손의 신동",
            Crux.Data.TraitAxis.EngineCaterpillarRepairs, "엔진·캐터필러 수리로 unlock");

        traits["wordless_comrade"] = CreateTrait("wordless_comrade", "말없는 동료",
            Crux.Data.TraitAxis.None, "항상 활성 (제한 없음)");

        traits["rocinante_owner"] = CreateTrait("rocinante_owner", "로시난테의 주인",
            Crux.Data.TraitAxis.None, "항상 활성 (제한 없음)");

        traits["silent_worker"] = CreateTrait("silent_worker", "묵묵한 일꾼",
            Crux.Data.TraitAxis.LoadCount, "장전 횟수로 unlock");

        // 5인 승무원: 각 traits[] 배열로 특성 할당
        CreateMember("astra", "아스트라", Crux.Data.CrewClass.Commander, 50, 55, 45,
            Crux.Data.PreferredTagType.TankClass, "medium",
            new[] { traits["donquixote_dream"] },
            new[] { "medium" }, new[] { 1 },
            "지하 벙커에서 자란 16세 소년. 돈키호테 책에서 로시난테의 이름 기원을 알고 있다. 일행의 리더이자 이리스의 오빠.");

        CreateMember("ririd", "리리드", Crux.Data.CrewClass.Gunner, 75, 60, 35,
            Crux.Data.PreferredTagType.MainGunType, "medium_caliber",
            new[] { traits["hermit_eye"] },
            new[] { "medium_caliber" }, new[] { 1 },
            "전차 안이 세상의 전부인 15세 히키코모리 소녀. 눈이 좋고 직감형. 수줍음 많음. 아스트라가 리리드의 첫 번째 친구.");

        CreateMember("grin", "그린", Crux.Data.CrewClass.Loader, 30, 40, 55,
            Crux.Data.PreferredTagType.AmmoType, "ap",
            new[] { traits["silent_worker"], traits["wordless_comrade"] },
            new[] { "ap" }, new[] { 1 },
            "벙어리 17세 거한. 선하고 순박하고 성실한 먹보. 로시난테가 집이고 친구들이 가족. 리리드의 두 번째 친구.");

        CreateMember("pretena", "프리테나", Crux.Data.CrewClass.Driver, 40, 60, 70,
            Crux.Data.PreferredTagType.WeightClass, "medium",
            new[] { traits["rocinante_owner"] },
            new[] { "medium" }, new[] { 2 },
            "로시난테의 최초 발견자이자 이름을 붙인 19세. 이리스의 스승. 가장 연장자이지만 응석받이인 활달한 성격.");

        CreateMember("iris", "이리스", Crux.Data.CrewClass.GunnerMech, 30, 45, 75,
            Crux.Data.PreferredTagType.RepairModule, "engine",
            new[] { traits["little_hand_prodigy"] },
            new[] { "caterpillar", "engine" }, new[] { 2, 1 },
            "프리테나를 따라다니며 기계를 배운 13세 신동. 아스트라의 동생. 조용하지만 기계 앞에서는 집요하다.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CRUX] 초기 5인 + 6 특성 생성 완료");
    }

    static Crux.Data.TraitSO CreateTrait(string id, string displayName, Crux.Data.TraitAxis axisType, string description)
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
        trait.axisType = axisType;
        trait.axisThreshold = 10;  // 기본값: 누적 카운트 10 이상으로 unlock
        trait.description = description;

        AssetDatabase.CreateAsset(trait, assetPath);
        return trait;
    }

    static Crux.Data.CrewMemberSO CreateMember(string id, string displayName, Crux.Data.CrewClass klass,
        int aim, int react, int tech,
        Crux.Data.PreferredTagType preferredTagType, string preferredTag,
        Crux.Data.TraitSO[] traits,
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
        member.traits = traits ?? new Crux.Data.TraitSO[0];
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
