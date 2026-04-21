using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class CombatBase : MonoBehaviour
{
    public enum TimeAlvo { Aliado, Inimigo }
    public enum Efeito { Cura, Dano, Força, Efeito }

    [Serializable]
    public struct Efeitos
    {
        public Efeito efeito;
        public int qtdMaximaDeAlvos;
        public float intensidade;
        public int turnosDuração;
        public List<TimeAlvo> timesAlvos;
        public NPCsData.DamageType damageType;
    }

    [Serializable]
    public struct Actions
    {
        public string nomeAção;
        public float peso;
        public List<NPCsData.Class> classesPermitidas; // quais classes podem usar essa ação
        public List<TimeAlvo> timesAlvos;
        public List<Efeitos> efeitos;
    }

    public List<Actions> actions;

    /// <summary>
    /// ator: o membro do crew que está executando a ação — sua força é usada no cálculo.
    /// </summary>
    public void DoAction(Actions action, List<GameObject> alvos, CrewData aliados, CrewData inimigos, GameObject ator)
    {
        float força = ator?.GetComponent<NPCsData>()?.força ?? 1f;

        foreach (TimeAlvo timeAlvo in action.timesAlvos)
        {
            CrewData crewAlvo = timeAlvo == TimeAlvo.Aliado ? aliados : inimigos;
            
            Debug.Log($"[CrewData] DoDamage chamado — alvos: {alvos.Count}, dano: {força}, crew: {crewAlvo.crew.Count}");
            foreach (Efeitos efeito in action.efeitos)
            {
                if (!efeito.timesAlvos.Contains(timeAlvo)) continue;

                switch (efeito.efeito)
                {
                    case Efeito.Cura:
                        crewAlvo.HealUnits(alvos, efeito.intensidade * força, efeito.qtdMaximaDeAlvos);
                        break;

                    case Efeito.Dano:
                        crewAlvo.DoDamage(alvos, efeito.intensidade * força, efeito.damageType, efeito.qtdMaximaDeAlvos);
                        break;

                    case Efeito.Força:
                    case Efeito.Efeito:
                        ApplyTimedEffect(alvos, crewAlvo, efeito);
                        break;
                }
            }
        }
    }

    private void ApplyTimedEffect(List<GameObject> alvos, CrewData crew, Efeitos efeito)
    {
        foreach (GameObject alvo in alvos)
        {
            if (!crew.crew.Contains(alvo)) continue;

            NPCsData npc = alvo.GetComponent<NPCsData>();
            if (npc == null) continue;

            npc.AddEffect(new NPCsData.ActiveEffect
            {
                tipo            = efeito.efeito,
                intensidade     = efeito.intensidade,
                turnosRestantes = efeito.turnosDuração,
                damageType      = efeito.damageType
            });
        }
    }
}