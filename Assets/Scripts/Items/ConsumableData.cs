using System;
using UnityEngine;

/// <summary>
/// Define itens consumíveis (como poções ou comida) que aplicam efeitos temporários 
/// ou instantâneos (como cura ou bónus de força) em unidades durante o combate ou no mapa.
/// </summary>
[CreateAssetMenu(fileName = "New Consumable", menuName = "Scriptable Objects/ConsumableData")]
public class ConsumableData : ItemData
{
    #region Tipos e Efeitos
    public enum Effect { cura, força }

    [Header("Atributos do Consumível")]
    [Tooltip("A magnitude do efeito aplicado (quantidade de HP curado ou força adicionada).")]
    public float intensity;  
    
    [Tooltip("Qual o tipo de efeito que este item causa ao ser consumido.")]
    public Effect efeito;  
    
    [Tooltip("Duração do efeito em turnos de combate. (Use 1 para efeito imediato/único).")]
    public int durationInTurns = 1;
    #endregion
}