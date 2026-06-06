using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Security.Cryptography;
using Unity.VisualScripting;

/// <summary>
/// O coração dos dados de qualquer entidade no jogo.
/// Gerencia os atributos principais (vida, força, nível), os modificadores ativos (buffs/debuffs),
/// os equipamentos, a tabela de resistências/fraquezas elementais e o sistema de espólios (loot).
/// </summary>
[DefaultExecutionOrder(-10)]
public class NPCsData : MonoBehaviour
{
    #region Estruturas e Enumerações
    public enum Class { Navegador, Canhoneiro, Atirador, Guerreiro, Cozinheiro, Medico, Capitao, Barco }
    public enum Type { Animal, Humano, Fantasma, Esqueleto, Monstro, Estrutura }
    public enum DamageType { Physical, Magical, Fire, Ice, Poison, Holy, Cursed }

    /// <summary>
    /// Tabela global que define os multiplicadores de dano baseados no Tipo da criatura
    /// cruzado com o Tipo de Dano recebido.
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<DamageType, float>> damageTable =
        new Dictionary<Type, Dictionary<DamageType, float>>()
        {
            { Type.Animal, new Dictionary<DamageType, float> {
                { DamageType.Physical, 1.0f },
                { DamageType.Magical, 1.2f },
                { DamageType.Fire, 1.5f },
                { DamageType.Ice, 0.8f },
                { DamageType.Poison, 1.3f },
                { DamageType.Holy, 1.0f },
                { DamageType.Cursed, 0.8f },
            }},
            { Type.Humano, new Dictionary<DamageType, float> {
                { DamageType.Physical, 1.0f },
                { DamageType.Magical, 1.0f },
                { DamageType.Fire, 1.0f },
                { DamageType.Ice, 1.0f },
                { DamageType.Poison, 1.0f },
                { DamageType.Holy, 1.0f },
                { DamageType.Cursed, 1.0f },
            }},
            { Type.Fantasma, new Dictionary<DamageType, float> {
                { DamageType.Physical, 0.0f },
                { DamageType.Magical, 1.0f },
                { DamageType.Fire, 0.5f },
                { DamageType.Ice, 0.5f },
                { DamageType.Poison, 0.0f },
                { DamageType.Holy, 2.0f },
                { DamageType.Cursed, 0.5f },
            }},
            { Type.Esqueleto, new Dictionary<DamageType, float> {
                { DamageType.Physical, 0.5f },
                { DamageType.Magical, 1.0f },
                { DamageType.Fire, 1.0f },
                { DamageType.Ice, 0.0f },
                { DamageType.Poison, 0.0f },
                { DamageType.Holy, 2.0f },
                { DamageType.Cursed, 0.5f },
            }},
            { Type.Monstro, new Dictionary<DamageType, float> {
                { DamageType.Physical, 0.8f },
                { DamageType.Magical, 0.8f },
                { DamageType.Fire, 1.2f },
                { DamageType.Ice, 1.2f },
                { DamageType.Poison, 0.5f },
                { DamageType.Holy, 1.5f },
                { DamageType.Cursed, 1.5f },
            }},
            { Type.Estrutura, new Dictionary<DamageType, float> {
                { DamageType.Physical, 1.0f },
                { DamageType.Magical, 0.5f },
                { DamageType.Fire, 1.5f },
                { DamageType.Ice, 0.8f },
                { DamageType.Poison, 0.0f },
                { DamageType.Holy, 0.0f },
                { DamageType.Cursed, 0.0f },
            }},
        };

    [Serializable]
    public struct PossibleDrop
    {
        public ItemData itemData;

        [Range(0.0f, 1.0f)]
        public float dropChance;

        public int maxQuantity;
    }

    [Serializable]
    public struct ActiveEffect
    {
        public CombatBase.EffectType effectType;
        public float intensity;
        public int remainingTurns;
        public DamageType damageType;
    }
    #endregion

    #region Identidade e Equipamento
    [Header("Identidade")]
    [SerializeField] private string _npcName;
    [SerializeField] private Type _creatureType;
    [SerializeField] private Class _creatureClass;
    [SerializeField] private WeaponData _equippedWeapon;
    [SerializeField] private ArmorData _equippedArmor;

    [Header("Ações de Combate")]
    [SerializeField] private int _maxActionsPerTurn = 1;

    [HideInInspector]
    public int remainingActions;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float _permanentDeathChance = 0.3f;
    #endregion

    #region Status e Experiência
    [Header("Status")]
    [SerializeField] private float _maxHealth;

    private float _currentHealth;

    [SerializeField] private float _strength;

    public bool isAlive = true;

    [Header("Evolução")]
    [SerializeField] private int _level = 1;
    [SerializeField] private float _cost = 0;
    [SerializeField] private float _currentXp = 0f;
    [SerializeField] private float _xpToNextLevel = 100f;
    [SerializeField] private float _xpReward = 20f;
    #endregion

    #region Propiedades Públicas Controladas
    public string NpcName
    {
        get => _npcName;
        set => _npcName = value;
    }
    public Type CreatureType => _creatureType;
    public Class CreatureClass
    {
        get => _creatureClass;
        set => _creatureClass = value;
    }

    public WeaponData EquippedWeapon
    {
        get => _equippedWeapon;
        set => _equippedWeapon = value;
    }

    public ArmorData EquippedArmor
    {
        get => _equippedArmor;
        set => _equippedArmor = value;
    }

    public int MaxActionsPerTurn => _maxActionsPerTurn;

    public float PermanentDeathChance => _permanentDeathChance;

    public float MaxHealth
    {
        get => _maxHealth;
        set => _maxHealth = value;
    }
    public float CurrentHealth => _currentHealth;

    public float Strength
    {
        get => _strength;
        set => _strength = value;
    }

    public int Level => _level;

    public float Cost
    {
        get => _cost;
        set => _cost = value;
    }

    public float CurrentXp
    {
        get => _currentXp;
        set => _currentXp = value;
    }

    public float XpToNextLevel => _xpToNextLevel;
    public float XpReward => _xpReward;
    #endregion

    #region Listas Dinâmicas (Efeitos e Drops)
    [Header("Inventário e Drops")]
    public List<PossibleDrop> possibleDrops = new();

    [Header("Efeitos Ativos")]
    public List<ActiveEffect> activeEffects = new();
    #endregion

    #region Eventos
    /// <summary>
    /// Disparado quando a vida do NPC chega a zero. CrewData escuta para processar a remoção.
    /// </summary>
    public event Action<NPCsData> OnDeath;
    #endregion

    #region Ciclo de Vida (Unity)
    private void Awake()
    {
        _currentHealth = _maxHealth;
    }
    #endregion

    #region Lógica de Vida e Dano
    /// <summary>
    /// Processa o recebimento de dano aplicando os devidos multiplicadores elementais e reduções por armadura.
    /// </summary>
    public void TakeDamage(float damage, DamageType damageType)
    {
        if (!isAlive) return;

        float multiplier = 1f;

        if (damageTable.TryGetValue(_creatureType, out var typeTable))
            typeTable.TryGetValue(damageType, out multiplier);

        float realDamage = Mathf.Max(
            0,
            (_equippedArmor != null)
            ? damage * multiplier - _equippedArmor.ResistanceBaseValue
            : damage * multiplier
        );

        _currentHealth -= realDamage;

        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            isAlive = false;
            OnDeath?.Invoke(this);
        }
    }

    public void Heal(float healAmount)
    {
        if (!isAlive) return;

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + healAmount);
    }

    public float GetCurrentHealth() => _currentHealth;

    public float GetMaxHealth() => _maxHealth;

    public void UpdateStrength(float strengthValue) => _strength = strengthValue;
    #endregion

        #region Gerenciamento de Efeitos
    /// <summary>
    /// Adiciona um novo efeito passivo ou imediato a entidade (Buffs, Debuffs, Cura).
    /// O efeito de força é acumulado se o tempo também acumular, caso contrário a intensidade é substituída.
    /// </summary>
    public void AddEffect(ActiveEffect newEffect)
    {
        // Aplicação imediata no ato da adição para cura, dano ou aumento de força base
        if (newEffect.effectType == CombatBase.EffectType.Heal)
        {
            Heal(newEffect.intensity);
            newEffect.remainingTurns -= 1;
        }
        else if (newEffect.effectType == CombatBase.EffectType.Status)
        {
            TakeDamage(newEffect.intensity, newEffect.damageType);
            newEffect.remainingTurns -= 1;
        }
        else if (newEffect.effectType == CombatBase.EffectType.Strength)
        {
            _strength += newEffect.intensity;
        }

        if (newEffect.remainingTurns <= 0 &&
            newEffect.effectType != CombatBase.EffectType.Strength)
        {
            return;
        }

        // Procura se o efeito já existe para acumular ou sobrescrever
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].effectType == newEffect.effectType &&
                activeEffects[i].damageType == newEffect.damageType)
            {
                var existing = activeEffects[i];

                // Retira a força anterior para aplicar a nova (Evita stack infinito)
                if (newEffect.effectType == CombatBase.EffectType.Strength)
                {
                    _strength -= existing.intensity;
                }

                existing.remainingTurns =
                    Mathf.Max(existing.remainingTurns, newEffect.remainingTurns);

                existing.intensity = newEffect.intensity;

                activeEffects[i] = existing;

                return;
            }
        }

        activeEffects.Add(newEffect);
    }

    /// <summary>
    /// Chamado ao final do turno pelo BattleManager. 
    /// Executa efeitos periódicos (como veneno) e limpa efeitos expirados (devolvendo a Força ao normal).
    /// </summary>
    public void TickEffects()
    {
        if (!isAlive) return;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];

            // Aplica o efeito do turno
            switch (effect.effectType)
            {
                case CombatBase.EffectType.Damage:
                    TakeDamage(effect.intensity, effect.damageType);
                    break;

                case CombatBase.EffectType.Heal:
                    Heal(effect.intensity);
                    break;

                // Força não aplica dano por turno, apenas expira
            }

            // Decrementa turno
            activeEffects[i] = new ActiveEffect
            {
                effectType = effect.effectType,
                intensity = effect.intensity,
                remainingTurns = effect.remainingTurns - 1,
                damageType = effect.damageType
            };

            // Remove efeito expirado
            if (activeEffects[i].remainingTurns <= 0)
            {
                if (effect.effectType == CombatBase.EffectType.Strength)
                    _strength -= effect.intensity; // reverte o buff

                activeEffects.RemoveAt(i);
            }
        }
    }
    #endregion

    #region Loot e Equipamentos
    /// <summary>
    /// Sorteia os itens que a criatura dropará ao morrer, baseado nas chances de cada PossibleDrop.
    /// </summary>
    public List<Inventory.Slot> GenerateLoot()
    {
        List<Inventory.Slot> drops = new();

        foreach (PossibleDrop drop in possibleDrops)
        {
            float randomNumber = UnityEngine.Random.Range(0.0f, 1.0f);

            if (randomNumber < drop.dropChance)
            {
                drops.Add(new Inventory.Slot()
                {
                    item = drop.itemData,
                    quantity = UnityEngine.Random.Range(1, drop.maxQuantity + 1)
                });
            }
        }

        return drops;
    }

    public float GetAttackPower()
    {
        if (_equippedWeapon != null)
            return _strength + _equippedWeapon.AttackBaseValue;

        return _strength;
    }

    public void ApplyConsumable(ConsumableData consumable)
    {
        switch (consumable.EffectType)
        {
            case ConsumableData.Effect.Strength:
                ActiveEffect strengthEffect = new();

                strengthEffect.effectType = CombatBase.EffectType.Strength;
                strengthEffect.intensity = consumable.Intensity;
                strengthEffect.remainingTurns = consumable.DurationInTurns;

                AddEffect(strengthEffect);
                break;

            case ConsumableData.Effect.Heal:
                ActiveEffect healEffect = new();

                healEffect.effectType = CombatBase.EffectType.Heal;
                healEffect.intensity = consumable.Intensity;
                healEffect.remainingTurns = consumable.DurationInTurns;

                AddEffect(healEffect);
                break;
        }
    }

    public WeaponData EquipWeapon(WeaponData newWeapon)
    {
        WeaponData oldWeapon = _equippedWeapon;

        _equippedWeapon = newWeapon;

        return oldWeapon;
    }

    public ArmorData EquipArmor(ArmorData newArmor)
    {
        ArmorData oldArmor = _equippedArmor;

        _equippedArmor = newArmor;

        return oldArmor;
    }
    #endregion

    #region Sistema de XP e Evolução
    public void GainXp(float amount)
    {
        _currentXp += amount;
    }

    public void LevelUp()
    {
        while (_currentXp >= _xpToNextLevel)
        {
            _currentXp -= _xpToNextLevel;

            _level++;

            _xpToNextLevel *= 1.5f;
            _maxHealth *= 1.2f;
            _strength *= 1.2f;

            Heal(_maxHealth);
        }
    }
    #endregion

    #region Gerenciamento de Ações de Batalha
    public void ResetActions() => remainingActions = _maxActionsPerTurn;

    public void ConsumeAction() => remainingActions--;

    public bool CanAct() => remainingActions > 0;
    #endregion
}