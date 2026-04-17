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
            if (entry.MaxHP > 0)
                copy.maxHP = entry.MaxHP;
            if (entry.mainGun?.data is MainGunPartSO mg)
                copy.mainGunCaliber = mg.caliber;

            Debug.Log($"[Battle] 편성 TankData 주입: {copy.tankName} (HP={copy.maxHP}, 구경={copy.mainGunCaliber})");
            return copy;
        }
    }
}
