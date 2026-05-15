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
    public CrewData aliados;
    [Tooltip("Referência para a tripulação adversária.")]
    public CrewData inimigos;
    #endregion

    #region Execução de Ação
    /// <summary>
    /// Envolve a função DoAction da classe base e dispara os feedbacks na UI da Batalha.
    /// </summary>
    /// <param name="action">Ação escolhida para executar.</param>
    /// <param name="alvos">A lista de alvos afetados.</param>
    /// <param name="ator">O GameObject que está realizando a ação.</param>
    public void ExecutarAção(Actions action, List<GameObject> alvos, GameObject ator)
    {
        DoAction(action, alvos, aliados, inimigos, ator);
        BattleManager.Instance.ExibirMensagem(ator.GetComponent<NPCsData>().NPC_Name + " usou " + action.nomeAção + "!!");
    }
    #endregion
}