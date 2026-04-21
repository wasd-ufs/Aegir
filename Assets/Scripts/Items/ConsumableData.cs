using System;
using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Scriptable Objects/ConsumableData")]
public class ConsumableData : ItemData
{
    public enum Effect {cura, força}
    public float intensity;  
    public Effect efeito;  
    public int durationInTurns = 1;
}
