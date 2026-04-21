using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Material", menuName = "Scriptable Objects/MaterialData")]
public class MaterialData : ItemData
{
    [System.Serializable]
    public struct NPCs
    {
        public GameObject NPC;
        public int MaxQtt;
    }
    public List<NPCs> canBeDroppedBy;
}
