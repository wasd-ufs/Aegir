using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// ScriptableObject que descreve uma estrutura (construção, ruína, etc.) que pode
/// ser gerada sobre o mapa durante a decoração de chunks.
/// <para>
/// O sistema de geração usa <see cref="validBaseLayers"/> para verificar se o terreno
/// suporta a estrutura, e <see cref="layerOverrides"/> para permitir exceções em
/// coordenadas locais específicas (ex.: um píer que pode tocar a água).
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "StructureData", menuName = "Scriptable Objects/StructureData")]
public class StructureData : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Campos Principais
    // -------------------------------------------------------------------------

    /// <summary>Identificador textual da estrutura. Usado no save de estruturas geradas.</summary>
    public string structureName;

    /// <summary>Tamanho da estrutura em tiles (largura × altura).</summary>
    public Vector2Int structureDimensions;

    /// <summary>Prefab a ser instanciado quando a estrutura for gerada.</summary>
    public GameObject structurePrefab;

    /// <summary>Probabilidade de tentativa de geração por chunk (0 = nunca, 1 = sempre).</summary>
    [Range(0.0f, 1.0f)] public float spawnChance;

    /// <summary>
    /// Camadas de tile aceitas como base para todos os tiles da estrutura,
    /// exceto os cobertos por <see cref="layerOverrides"/>.
    /// Ex.: [2] = somente terra.
    /// </summary>
    public List<int> validBaseLayers;

    /// <summary>
    /// Distância mínima (em unidades de mundo) que deve existir entre esta estrutura
    /// e qualquer outra já gerada. Evita sobreposição de construções.
    /// </summary>
    public float raioDeIsolamento;

    // -------------------------------------------------------------------------
    // Override de Camadas por Coordenada
    // -------------------------------------------------------------------------

    /// <summary>
    /// Define uma exceção de camada válida para um conjunto de coordenadas locais da estrutura.
    /// Permite que partes específicas da planta baixa aceitem camadas diferentes da regra geral.
    /// </summary>
    [Serializable]
    public struct LayerOverride
    {
        /// <summary>Coordenadas locais (relativas ao canto inferior-esquerdo da estrutura) que recebem a exceção.</summary>
        public List<Vector2Int> localCoordinates;

        /// <summary>Camada aceita nestas coordenadas em substituição a <see cref="validBaseLayers"/>.</summary>
        public int layer;
    }

    /// <summary>Lista de exceções de camada por região da planta baixa.</summary>
    public List<LayerOverride> layerOverrides;
}