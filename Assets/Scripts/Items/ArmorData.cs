using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Armor", menuName = "Scriptable Objects/ArmorData")]
public class ArmorData : ItemData
{
    
    public List<NPCsData.Class> classe;
    public float resistanceBaseValue;

    [System.Serializable]
    public struct ResistanceBonus
    {
        public NPCsData.DamageType damageType;
        public float intensity;
    }
    public List<ResistanceBonus> perTypeResistanceBonus;
}
