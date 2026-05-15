using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define itens arremessáveis (como bombas ou facas) que podem causar dano direto 
/// aos inimigos sem necessidade de equipar uma arma.
/// </summary>
[CreateAssetMenu(fileName = "New Throwable", menuName = "Scriptable Objects/ThrowableData")]
public class ThrowableData : ItemData
{
    #region Atributos de Arremesso
    [Header("Restrições e Alvos")]
    [Tooltip("Lista de classes que têm permissão para usar e atirar este item.")]
    public List<NPCsData.Class> throwableBy;
    
    [Tooltip("Número máximo de alvos que a área de efeito do arremessável pode atingir.")]
    public int maxTargetQtt;

    [Header("Dano")]
    [Tooltip("Tipo elemental do dano infligido no arremesso.")]
    public NPCsData.DamageType damageType;
    
    [Tooltip("Potência ou dano base infligido pelo item arremessável.")]
    public float intensity;
    #endregion
}