using System.Collections.Generic;
using UnityEngine;

public class CrewAttacks : CombatBase
{
    [Header("Crews")]
    public CrewData aliados;
    public CrewData inimigos;

    public void ExecutarAção(Actions action, List<GameObject> alvos, GameObject ator)
    {
        DoAction(action, alvos, aliados, inimigos, ator);
        BattleManager.Instance.ExibirMensagem(ator.GetComponent<NPCsData>().NPC_Name + " usou " + action.nomeAção + "!!");
    }
}