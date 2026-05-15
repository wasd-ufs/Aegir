using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define os dados de uma arma equipável.
/// Contém o valor base de ataque, bónus específicos por tipo de dano e as restrições de classe.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ItemData
{
    #region Estruturas
    /// <summary>
    /// Define um bónus de ataque aplicado a um tipo específico de dano.
    /// </summary>
    [System.Serializable]
    public struct AttackBonus
    {
        public NPCsData.DamageType damageType;
        public float intensity;
    }
    #endregion

    #region Atributos de Combate
    [Header("Restrições")]
    [Tooltip("Lista de classes da tripulação permitidas a equipar esta arma.")]
    [SerializeField]
    private List<NPCsData.Class> _allowedClassList = new();

    [Header("Status de Ataque")]
    [Tooltip("Poder de ataque base adicionado à força da entidade utilizadora.")]
    [SerializeField]
    private float _attackBaseValue;

    [Tooltip("Bónus adicionais aplicados a diferentes tipos de dano (ex: Fogo, Gelo).")]
    [SerializeField]
    private List<AttackBonus> _perTypeAttackBonusList = new();
    #endregion

    #region Propriedades Públicas
    public List<NPCsData.Class> AllowedClassList => _allowedClassList;
    public float AttackBaseValue => _attackBaseValue;
    public List<AttackBonus> PerTypeAttackBonusList => _perTypeAttackBonusList;
    #endregion
}