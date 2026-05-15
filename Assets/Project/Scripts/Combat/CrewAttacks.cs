using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente concreto derivado de CombatBase. 
/// Encapsula a lógica de execução de ataque associando as referências exatas
/// de tripulação aliada e inimiga.
/// </summary>
public class CrewAttacks : CombatBase
{
    #region Referências de Equipes

    [Header("Crews")]
    [Tooltip("Referência para a tripulação que usará as ações.")]
    [SerializeField] private CrewData _allies;

    [Tooltip("Referência para a tripulação adversária.")]
    [SerializeField] private CrewData _enemies;

    // Mantendo acesso público controlado (caso outros scripts dependam)
    public CrewData allies
    {
        get => _allies;
        set => _allies = setValueSafe(value);
    }

    public CrewData enemies
    {
        get => _enemies;
        set => _enemies = setValueSafe(value);
    }

    // Pequeno helper pra evitar null silencioso
    private CrewData setValueSafe(CrewData value) => value;

    #endregion

    #region Execução de Ação

    /// <summary>
    /// Envolve a função DoAction da classe base e dispara os feedbacks na UI da Batalha.
    /// </summary>
    /// <param name="action">Ação escolhida para executar.</param>
    /// <param name="targets">A lista de alvos afetados.</param>
    /// <param name="actor">O GameObject que está realizando a ação.</param>
    public void ExecuteAction(ActionData action, List<GameObject> targets, GameObject actor)
    {
        DoAction(action, targets, _allies, _enemies, actor);

        BattleManager.Instance.DisplayMessage(
            actor.GetComponent<NPCsData>().NPC_Name + " usou " + action.actionName + "!!"
        );
    }

    #endregion
}