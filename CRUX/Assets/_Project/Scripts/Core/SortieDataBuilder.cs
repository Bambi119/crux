using UnityEngine;
using Crux.Data;

namespace Crux.Core
{
    /// <summary>
    /// Hangar 편성 데이터를 BattleController가 소비할 형태로 변환.
    /// BattleController에서 P-S7 일환으로 추출.
    /// Scene-5 이후 확장될 예정 (파츠 armor/turret 등).
    /// </summary>
    public static class SortieDataBuilder
    {
        /// <summary>
        /// BattleEntryData.SortieTanks[0] 편성 정보를 TankDataSO 복사본에 반영.
        /// Inspector 원본 유지, 편성 값만 덮어쓰기. 편성 없으면 null 반환.
        /// </summary>
        public static TankDataSO BuildPlayerTankData(TankDataSO inspectorOriginal)
        {
            if (!BattleEntryData.HasEntry || inspectorOriginal == null) return null;
            var entry = BattleEntryData.SortieTanks[0];
            if (entry == null) return null;

            var copy = ScriptableObject.Instantiate(inspectorOriginal);
            copy.name = inspectorOriginal.name + " (Sortie)";
            copy.tankName = entry.tankName;
            copy.hullClass = entry.hullClass;
            copy.isRocinante = entry.isRocinante;

            // HP (장갑 파츠 기여 포함된 계산값)
            if (entry.MaxHP > 0)
                copy.maxHP = entry.MaxHP;

            // 주포 — 구경
            if (entry.mainGun?.data is MainGunPartSO mg)
                copy.mainGunCaliber = mg.caliber;

            // 포탑 — 회전 속도
            if (entry.turret?.data is TurretPartSO tr)
                copy.turretRotationSpeed = tr.rotationSpeed;

            // 탄약고 — 최대 적재량
            if (entry.ammoRack?.data is AmmoRackPartSO ar)
            {
                copy.maxMainGunAmmo = ar.maxMainGunAmmo;
                copy.maxMGAmmo = ar.maxMGAmmo;
            }

            // 장갑 — armor 리스트 순서로 front/side/rear/turret 매핑
            ApplyArmorProfile(entry, ref copy.armor);

            Debug.Log($"[Battle] 편성 TankData 주입: {copy.tankName} · HP={copy.maxHP} · 구경={copy.mainGunCaliber} · 포탑회전={copy.turretRotationSpeed:F0}° · 주포탄={copy.maxMainGunAmmo} · 장갑 F/S/R/T={copy.armor.front:F0}/{copy.armor.side:F0}/{copy.armor.rear:F0}/{copy.armor.turret:F0}");
            return copy;
        }

        /// <summary>
        /// TankInstance.armor 리스트를 TankDataSO.armor ArmorProfile에 매핑.
        /// 슬롯 순서 규약: [0]=front, [1]=side, [2]=rear, [3]=turret.
        /// 비어있는 슬롯은 Inspector 원본 값 유지.
        /// </summary>
        private static void ApplyArmorProfile(TankInstance entry, ref ArmorProfile profile)
        {
            if (entry.armor == null) return;
            for (int i = 0; i < entry.armor.Count; i++)
            {
                var part = entry.armor[i];
                if (part?.data is ArmorPartSO armor)
                {
                    switch (i)
                    {
                        case 0: profile.front = armor.baseProtection; break;
                        case 1: profile.side = armor.baseProtection; break;
                        case 2: profile.rear = armor.baseProtection; break;
                        case 3: profile.turret = armor.baseProtection; break;
                    }
                }
            }
        }
    }
}
