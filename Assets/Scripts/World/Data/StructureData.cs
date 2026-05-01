using System;
using System.Collections.Generic;
using UnityEngine;

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

    public string StructureName => _structureName;
    public Vector2Int StructureDimensions => _structureDimensions;
    public GameObject StructurePrefab => _structurePrefab;
    public float SpawnChance => _spawnChance;
    public List<int> ValidBaseLayersList => _validBaseLayersList;
    public float IsolationRadius => _isolationRadius;
    public List<LayerOverride> LayerOverridesList => _layerOverridesList;

    [Serializable]
    public struct LayerOverride
    {
        [SerializeField] private List<Vector2Int> _localCoordinatesList;
        [SerializeField] private int _layer;

        public List<Vector2Int> LocalCoordinatesList => _localCoordinatesList;
        public int Layer => _layer;
    }
}