using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Throwable", menuName = "Scriptable Objects/ThrowableData")]
public class ThrowableData : ItemData
{
    public List<NPCsData.Class> throwableBy;
    public NPCsData.DamageType damageType;
    public float intensity;
    public int maxTargetQtt;
}
