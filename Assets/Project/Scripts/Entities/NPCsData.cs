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
    public enum Class { Navegador, Canhoneiro, Atirador, Guerreiro, Cozinheiro, Médico, Capitão, Barco }
    public enum Type { Animal, Humano, Fantasma, Esqueleto, Monstro, Estrutura }
    public enum DamageType { Físico, Mágico, Fogo, Gelo, Veneno, Sagrado, Amaldiçoado }

    /// <summary>
    /// Tabela global que define os multiplicadores de dano baseados no Tipo da criatura
    /// cruzado com o Tipo de Dano recebido.
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<DamageType, float>> damageTable =
        new Dictionary<Type, Dictionary<DamageType, float>>()
        {
            { Type.Animal, new Dictionary<DamageType, float> {
                { DamageType.Físico,      1.0f },
                { DamageType.Mágico,      1.2f },
                { DamageType.Fogo,        1.5f },
                { DamageType.Gelo,        0.8f },
                { DamageType.Veneno,      1.3f },
                { DamageType.Sagrado,     1.0f },
                { DamageType.Amaldiçoado, 0.8f },
            }},
            { Type.Humano, new Dictionary<DamageType, float> {
                { DamageType.Físico,      1.0f },
                { DamageType.Mágico,      1.0f },
                { DamageType.Fogo,        1.0f },
                { DamageType.Gelo,        1.0f },
                { DamageType.Veneno,      1.0f },
                { DamageType.Sagrado,     1.0f },
                { DamageType.Amaldiçoado, 1.0f },
            }},
            { Type.Fantasma, new Dictionary<DamageType, float> {
                { DamageType.Físico,      0.0f },
                { DamageType.Mágico,      1.0f },
                { DamageType.Fogo,        0.5f },
                { DamageType.Gelo,        0.5f },
                { DamageType.Veneno,      0.0f },
                { DamageType.Sagrado,     2.0f },
                { DamageType.Amaldiçoado, 0.5f },
            }},
            { Type.Esqueleto, new Dictionary<DamageType, float> {
                { DamageType.Físico,      0.5f },
                { DamageType.Mágico,      1.0f },
                { DamageType.Fogo,        1.0f },
                { DamageType.Gelo,        0.0f },
                { DamageType.Veneno,      0.0f },
                { DamageType.Sagrado,     2.0f },
                { DamageType.Amaldiçoado, 0.5f },
            }},
            { Type.Monstro, new Dictionary<DamageType, float> {
                { DamageType.Físico,      0.8f },
                { DamageType.Mágico,      0.8f },
                { DamageType.Fogo,        1.2f },
                { DamageType.Gelo,        1.2f },
                { DamageType.Veneno,      0.5f },
                { DamageType.Sagrado,     1.5f },
                { DamageType.Amaldiçoado, 1.5f },
            }},
            { Type.Estrutura, new Dictionary<DamageType, float> {
                { DamageType.Físico,      1.0f },
                { DamageType.Mágico,      0.5f },
                { DamageType.Fogo,        1.5f },
                { DamageType.Gelo,        0.8f },
                { DamageType.Veneno,      0.0f },
                { DamageType.Sagrado,     0.0f },
                { DamageType.Amaldiçoado, 0.0f },
            }},
        };

    [Serializable]
    public struct PossibleDrop
    {
        public ItemData itemData;
        [Range(0.0f, 1.0f)] public float dropChance;
        public int maxDuantity;
    }

    [Serializable]
    public struct ActiveEffect
    {
        public CombatBase.EffectType tipo;
        public float intensidade;
        public int turnosRestantes;
        public DamageType damageType;
    }
    #endregion

    #region Identidade e Equipamento
    [Header("Identidade")]
    public String NPC_Name;
    public Type creatureType;
    public Class creatureClass;
    public WeaponData armaEquipada;
    public ArmorData armaduraEquipada;
    
    [Header("Ações de Combate")]
    public int maxAcoesPorTurno = 1;
    [HideInInspector] public int acoesRestantes;

    [Range(0.0f, 1.0f)] public float chanceDeMortePermanente = 0.3f;
    #endregion

    #region Status e Experiência
    [Header("Status")]
    public float vidaMáxima;
    private float vidaAtual;
    public float força;
    public bool isAlive = true;
    
    [Header("Evolução")]
    public int level = 1;
    public float custo = 0;
    public float currentXP = 0f;
    public float xpToNextLevel = 100f;
    public float xpReward = 20f;
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
    public event System.Action<NPCsData> OnMorte;
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        vidaAtual = vidaMáxima;
    }
    #endregion

    #region Lógica de Vida e Dano
    /// <summary>
    /// Processa o recebimento de dano aplicando os devidos multiplicadores elementais e reduções por armadura.
    /// </summary>
    public void TakeDamage(float dano, DamageType damageType)
    {
        if (!isAlive) return;

        float multiplier = 1f;
        if (damageTable.TryGetValue(creatureType, out var typeTable))
            typeTable.TryGetValue(damageType, out multiplier);

        float damageReal = Mathf.Max(0, (armaduraEquipada != null)? dano * multiplier - armaduraEquipada.resistanceBaseValue: dano * multiplier);
        vidaAtual -= damageReal;
        
        if (vidaAtual <= 0)
        {
            vidaAtual = 0;
            isAlive = false;
            OnMorte?.Invoke(this);
        }
    }

    public void Heal(float healAmount)
    {
        if (!isAlive) return;
        vidaAtual = Mathf.Min(vidaMáxima, vidaAtual + healAmount);
    }

    public float GetVidaAtual() => vidaAtual;
    public float GetVidaMaxima() => vidaMáxima;
    public void UpdateStrength(float strength) => força = strength;
    #endregion

    #region Gerenciamento de Efeitos
    /// <summary>
    /// Adiciona um novo efeito passivo ou imediato a entidade (Buffs, Debuffs, Cura).
    /// O efeito de força é acumulado se o tempo também acumular, caso contrário a intensidade é substituída.
    /// </summary>
    public void AddEffect(ActiveEffect newEffect)
    {
        // Aplicação imediata no ato da adição para cura, dano ou aumento de força base
        if (newEffect.tipo == CombatBase.EffectType.Heal)
        {
            Heal(newEffect.intensidade);
            newEffect.turnosRestantes -= 1; 
        }
        else if (newEffect.tipo == CombatBase.EffectType.Status)
        {
            TakeDamage(newEffect.intensidade, newEffect.damageType);
            newEffect.turnosRestantes -= 1; 
        }
        else if (newEffect.tipo == CombatBase.EffectType.Strength)
        {
            força += newEffect.intensidade;
        }

        if (newEffect.turnosRestantes <= 0 && newEffect.tipo != CombatBase.EffectType.Strength)
        {
            return; 
        }

        // Procura se o efeito já existe para acumular ou sobrescrever
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].tipo == newEffect.tipo && activeEffects[i].damageType == newEffect.damageType)
            {
                var existing = activeEffects[i];

                // Retira a força anterior para aplicar a nova (Evita stack infinito)
                if (newEffect.tipo == CombatBase.EffectType.Strength)
                {
                    força -= existing.intensidade; 
                }

                existing.turnosRestantes = Mathf.Max(existing.turnosRestantes, newEffect.turnosRestantes);
                existing.intensidade = newEffect.intensidade;
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
            var e = activeEffects[i];

            // Aplica o efeito do turno
            switch (e.tipo)
            {
                case CombatBase.EffectType.Damage:
                    TakeDamage(e.intensidade, e.damageType);
                    break;
                case CombatBase.EffectType.Heal: 
                    Heal(e.intensidade); 
                    break;
                // Força não aplica dano por turno, apenas expira
            }

            // Decrementa turno
            activeEffects[i] = new ActiveEffect
            {
                tipo            = e.tipo,
                intensidade     = e.intensidade,
                turnosRestantes = e.turnosRestantes - 1,
                damageType      = e.damageType
            };

            // Remove efeito expirado
            if (activeEffects[i].turnosRestantes <= 0)
            {
                if (e.tipo == CombatBase.EffectType.Strength)
                    força -= e.intensidade; // reverte o buff

                activeEffects.RemoveAt(i);
            }
        }
    }
    #endregion

    #region Loot e Equipamentos
    /// <summary>
    /// Sorteia os itens que a criatura dropará ao morrer, baseado nas chances de cada PossibleDrop.
    /// </summary>
    public List<Inventory.Slot> GerarLoot()
    {
        List<Inventory.Slot> drops = new();

        foreach (PossibleDrop drop in possibleDrops)
        {
            float randomNumber = UnityEngine.Random.Range(0.0f, 1.0f);
            if (randomNumber < drop.dropChance)
            {
                drops.Add(new Inventory.Slot() {item = drop.itemData, quantity = UnityEngine.Random.Range(1, drop.maxDuantity + 1)});
            }
        }

        return drops;
    }

    public float GetPoderDeAtaque()
    {
        if (armaEquipada != null)
            return força + armaEquipada.attackBaseValue;    
        else
            return força;    
    }

    public void AplicarConsumivel(ConsumableData consumable)
    {
        switch (consumable.efeito)
        {
            case ConsumableData.Effect.força:
                ActiveEffect effectF = new();
                effectF.tipo = CombatBase.EffectType.Strength;
                effectF.intensidade = consumable.intensity;
                effectF.turnosRestantes = consumable.durationInTurns;
                AddEffect(effectF);
                break;
            case ConsumableData.Effect.cura:
                ActiveEffect effectC = new();
                effectC.tipo = CombatBase.EffectType.Heal;
                effectC.intensidade = consumable.intensity;
                effectC.turnosRestantes = consumable.durationInTurns;
                AddEffect(effectC);
                break;
        }
    }

    public WeaponData EquiparArma(WeaponData novaArma)
    {
        WeaponData armaAntiga = armaEquipada;
        armaEquipada = novaArma;
        return armaAntiga;
    }

    public ArmorData EquiparArmadura(ArmorData novaArmadura)
    {
        ArmorData armaduraAntiga = armaduraEquipada;
        armaduraEquipada = novaArmadura;
        return armaduraAntiga;
    }
    #endregion

    #region Sistema de XP e Evolução
    public void GanharXP(float quantidade)
    {
        currentXP += quantidade;
    }

    public void SubirDeNivel()
    {
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            level++;
            xpToNextLevel *= 1.5f;
            vidaMáxima *= 1.2f;
            força *= 1.2f;
            Heal(vidaMáxima);
        }
    }
    #endregion

    #region Gerenciamento de Ações de Batalha
    public void ResetarAcoes() => acoesRestantes = maxAcoesPorTurno;
    public void ConsumirAcao() => acoesRestantes--;
    public bool PodeAgir() => acoesRestantes > 0;
    #endregion
}