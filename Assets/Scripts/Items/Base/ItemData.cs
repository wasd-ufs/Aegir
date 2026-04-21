using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemData : ScriptableObject
{
    public string itemName, description;
    public Sprite Icon;
    public List<NPCsData.Type> possibleTypes;
    public int maximumQttPerSlot, unitaryPrice;
}
