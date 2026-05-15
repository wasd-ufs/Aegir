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
    [SerializeField]
    private List<NPCsData.Class> _throwableByList = new();

    [Tooltip("Número máximo de alvos que a área de efeito do arremessável pode atingir.")]
    [SerializeField]
    private int _maxTargetQuantity;

    [Header("Dano")]
    [Tooltip("Tipo elemental do dano infligido no arremesso.")]
    [SerializeField]
    private NPCsData.DamageType _damageType;

    [Tooltip("Potência ou dano base infligido pelo item arremessável.")]
    [SerializeField]
    private float _intensity;
    #endregion

    #region Propriedades Públicas
    public List<NPCsData.Class> ThrowableByList => _throwableByList;
    public int MaxTargetQuantity => _maxTargetQuantity;
    public NPCsData.DamageType DamageType => _damageType;
    public float Intensity => _intensity;
    #endregion
}