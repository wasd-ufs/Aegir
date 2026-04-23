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
    #region Estruturas de Dados
    public enum TimeAlvo { Aliado, Inimigo }
    public enum Efeito { Cura, Dano, Força, Efeito }

    /// <summary>
    /// Estrutura que define um efeito aplicado durante o combate.
    /// </summary>
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

    /// <summary>
    /// Estrutura que define uma ação completa que um personagem pode realizar no turno.
    /// Pode conter múltiplos efeitos (ex: causa dano ao inimigo e cura a si mesmo).
    /// </summary>
    [Serializable]
    public struct Actions
    {
        public string nomeAção;
        public float peso;
        public List<NPCsData.Class> classesPermitidas; // quais classes podem usar essa ação
        public List<TimeAlvo> timesAlvos;
        public List<Efeitos> efeitos;
    }
    #endregion

    #region Campos
    [Header("Ações Configuradas")]
    [Tooltip("Lista de ações disponíveis para esta entidade ou equipe no combate.")]
    public List<Actions> actions;
    #endregion

    #region Lógica Principal de Ações
    /// <summary>
    /// Executa uma ação de combate, iterando sobre os alvos e aplicando os efeitos neles.
    /// </summary>
    /// <param name="action">A ação selecionada para execução.</param>
    /// <param name="alvos">A lista de GameObjects alvos da ação.</param>
    /// <param name="aliados">Dados da tripulação aliada.</param>
    /// <param name="inimigos">Dados da tripulação inimiga.</param>
    /// <param name="ator">O membro do crew que está executando a ação — sua força atua como multiplicador.</param>
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
    #endregion

    #region Helpers de Efeitos Temporais
    /// <summary>
    /// Aplica efeitos que duram por múltiplos turnos (como buff de Força ou debuff) aos alvos.
    /// </summary>
    /// <param name="alvos">Lista de alvos que receberão o efeito.</param>
    /// <param name="crew">O grupo de tripulação ao qual os alvos pertencem.</param>
    /// <param name="efeito">As propriedades do efeito a ser aplicado.</param>
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
    #endregion
}