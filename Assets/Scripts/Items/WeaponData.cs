using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ItemData
{
    public List<NPCsData.Class> classe;
    public float attackBaseValue;
    [System.Serializable]
    public struct AttackBonus
    {
        public NPCsData.DamageType damageType;
        public float intensity;
    }
    public List<AttackBonus> perTypeAttackBonus;

}
