using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe base abstrata para o sistema de combate.
/// Define as estruturas de dados para ações e efeitos, e contém a lógica central
/// para aplicar dano, cura ou efeitos de status em membros da tripulação.
/// </summary>
public abstract class CombatBase : MonoBehaviour
{
    #region Data Structures

    public enum TargetTeam { Ally, Enemy }
    public enum EffectType { Heal, Damage, Strength, Status }

    /// <summary>
    /// Estrutura que define um efeito aplicado durante o combate.
    /// </summary>
    [Serializable]
    public struct EffectData
    {
        public EffectType effectType;
        public int maxTargets;
        public float intensity;
        public int durationTurns;
        public List<TargetTeam> targetTeams;
        public NPCsData.DamageType damageType;
    }

    /// <summary>
    /// Estrutura que define uma ação completa que um personagem pode realizar no turno.
    /// </summary>
    [Serializable]
    public struct ActionData
    {
        public string actionName;
        public float weight;
        public List<NPCsData.Class> allowedClasses;
        public List<TargetTeam> targetTeams;
        public List<EffectData> effects;
    }

    #endregion

    #region Fields

    [Header("Configured Actions")]
    [Tooltip("Lista de ações disponíveis para esta entidade ou equipe no combate.")]
    [SerializeField] private List<ActionData> _actions;

    public List<ActionData> Actions => _actions;

    #endregion

    #region Main Action Logic

    /// <summary>
    /// Executa uma ação de combate, iterando sobre os alvos e aplicando os efeitos.
    /// </summary>
    public void DoAction(ActionData action, List<GameObject> targets, CrewData allies, CrewData enemies, GameObject actor)
    {
        float strength = actor?.GetComponent<NPCsData>()?.força ?? 1f;

        foreach (TargetTeam targetTeam in action.targetTeams)
        {
            CrewData targetCrew = targetTeam == TargetTeam.Ally ? allies : enemies;

            Debug.Log($"[CrewData] DoDamage chamado — alvos: {targets.Count}, dano: {strength}, crew: {targetCrew.crew.Count}");

            foreach (EffectData effect in action.effects)
            {
                if (!effect.targetTeams.Contains(targetTeam)) continue;

                switch (effect.effectType)
                {
                    case EffectType.Heal:
                        targetCrew.HealUnits(targets, effect.intensity * strength, effect.maxTargets);
                        break;

                    case EffectType.Damage:
                        targetCrew.DoDamage(targets, effect.intensity * strength, effect.damageType, effect.maxTargets);
                        break;

                    case EffectType.Strength:
                    case EffectType.Status:
                        ApplyTimedEffect(targets, targetCrew, effect);
                        break;
                }
            }
        }
    }

    #endregion

    #region Timed Effects Helpers

    /// <summary>
    /// Aplica efeitos que duram múltiplos turnos (buff/debuff).
    /// </summary>
    private void ApplyTimedEffect(List<GameObject> targets, CrewData crew, EffectData effect)
    {
        foreach (GameObject target in targets)
        {
            if (!crew.crew.Contains(target)) continue;

            NPCsData npc = target.GetComponent<NPCsData>();
            if (npc == null) continue;

            npc.AddEffect(new NPCsData.ActiveEffect
            {
                tipo = effect.effectType,
                intensidade = effect.intensity,
                turnosRestantes = effect.durationTurns,
                damageType = effect.damageType
            });
        }
    }

    #endregion
}