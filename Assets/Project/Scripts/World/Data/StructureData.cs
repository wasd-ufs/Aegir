using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Molde (Blueprint) utilizado para gerar estruturas e decorações maiores que 1x1 no mapa procedural.
/// Define as dimensões, o prefab a instanciar, as regras de sobreposição e o raio de isolamento mínimo.
/// </summary>
[CreateAssetMenu(fileName = "StructureData", menuName = "Scriptable Objects/StructureData")]
public class StructureData : ScriptableObject
{
    [SerializeField] private string _structureName;
    [SerializeField] private Vector2Int _structureDimensions;
    [SerializeField] private GameObject _structurePrefab;
    [SerializeField, Range(0.0f, 1.0f)] private float _spawnChance;
    [SerializeField] private List<int> _validBaseLayersList;
    [SerializeField] private float _isolationRadius;
    [SerializeField] private List<LayerOverride> _layerOverridesList;
    [Header("Identificação e Categoria")]
    [SerializeField] private StructureCategory _category = StructureCategory.Residential;
    [SerializeField] private StructurePivotMode _pivotMode = StructurePivotMode.BottomCenter;
    [SerializeField, Min(0.01f)] private float _weight = 1f;
    [SerializeField] private Vector2Int _doorLocalCoordinate;

    [Header("Limites")]
    [SerializeField, Min(1)] private int _maxPerChunk = 1;

    public string StructureName => _structureName;
    public Vector2Int StructureDimensions => _structureDimensions;
    public GameObject StructurePrefab => _structurePrefab;
    public float SpawnChance => _spawnChance;
    public List<int> ValidBaseLayersList => _validBaseLayersList;
    public float IsolationRadius => _isolationRadius;
    public int MaxPerChunk => _maxPerChunk;
    public List<LayerOverride> LayerOverridesList => _layerOverridesList;

    public StructureCategory Category => _category;
    public StructurePivotMode PivotMode => _pivotMode;
    public float Weight => _weight > 0 ? _weight : (_spawnChance > 0 ? _spawnChance * 100f : 1f);
    public Vector2Int DoorLocalCoordinate => _doorLocalCoordinate;

    /// <summary>
    /// Estrutura que define exceções ou regras estritas para camadas específicas.
    /// Permite forçar ou verificar se uma coordenada local da estrutura exige uma camada exata do terreno.
    /// </summary>
    [Serializable]
    public struct LayerOverride
    {
        [SerializeField] private List<Vector2Int> _localCoordinatesList;
        [SerializeField] private int _layer;

        public List<Vector2Int> LocalCoordinatesList => _localCoordinatesList;
        public int Layer => _layer;
    }
}

public enum StructureCategory
{
    Residential,
    Service,
    Harbor,
    Landmark
}

public enum StructurePivotMode
{
    BottomCenter,
    Center
}
