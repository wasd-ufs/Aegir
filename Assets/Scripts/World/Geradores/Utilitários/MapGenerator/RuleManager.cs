using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Processa as regras de bloqueio (quem não se pode ligar a quem) e gera regras espelhadas
/// de compatibilidade rápida para o algoritmo WFC.
/// </summary>
public class RuleManager : MonoBehaviour
{
    [Serializable]
    public struct TileIdentifier
    {
        [SerializeField] private Tile.TileType _type;
        [SerializeField] private Tile.TileDirection _direction;

        public Tile.TileType Type => _type;
        public Tile.TileDirection Direction => _direction;
        
        public TileIdentifier(Tile.TileType type, Tile.TileDirection direction)
        {
            _type = type;
            _direction = direction;
        }
    }

    [Serializable]
    public class TileRule 
    {
        [SerializeField] private TileIdentifier _origin;
        [SerializeField] private List<TileIdentifier> _blockedAbove = new List<TileIdentifier>();
        [SerializeField] private List<TileIdentifier> _blockedBelow = new List<TileIdentifier>();
        [SerializeField] private List<TileIdentifier> _blockedLeft = new List<TileIdentifier>();
        [SerializeField] private List<TileIdentifier> _blockedRight = new List<TileIdentifier>();

        public TileIdentifier Origin 
        { 
            get => _origin; 
            set => _origin = value; 
        }
        public List<TileIdentifier> BlockedAbove => _blockedAbove;
        public List<TileIdentifier> BlockedBelow => _blockedBelow;
        public List<TileIdentifier> BlockedLeft => _blockedLeft;
        public List<TileIdentifier> BlockedRight => _blockedRight;
    }

    [SerializeField] private List<TileRule> _blockingRulesList;
    [SerializeField] private TilesetData _tilesetData;

    private Dictionary<Tile, HashSet<Tile>[]> _fastRulesDictionary;

    private void Awake()
    {
        ProcessRules();
    }

    private void ProcessRules()
    {
        MirrorRules();
        BuildFastRules();
    }

    /// <summary>
    /// Lê as regras definidas no Inspector e cria automaticamente as regras inversas (ex: se o Tile A bloqueia o Tile B acima, então o Tile B bloqueia o Tile A abaixo).
    /// </summary>
    private void MirrorRules()
    {
        List<TileRule> mirroredRulesList = new List<TileRule>();
        TileRule[] originalsArray = _blockingRulesList.ToArray();

        foreach (var rule in originalsArray)
        {
            AddMirrorRule(mirroredRulesList, rule.Origin, rule.BlockedAbove, "below");
            AddMirrorRule(mirroredRulesList, rule.Origin, rule.BlockedBelow, "above");
            AddMirrorRule(mirroredRulesList, rule.Origin, rule.BlockedLeft, "right");
            AddMirrorRule(mirroredRulesList, rule.Origin, rule.BlockedRight, "left");
        }

        _blockingRulesList.AddRange(mirroredRulesList);
    }

    /// <summary>
    /// Converte a lista de regras para um Dicionário de HashSets, otimizando a velocidade de leitura para O(1) durante a execução do algoritmo de geração pesada.
    /// </summary>
    private void BuildFastRules()
    {
        _fastRulesDictionary = new Dictionary<Tile, HashSet<Tile>[]>();

        foreach (var rule in _blockingRulesList)
        {
            Tile originTile = FindTile(rule.Origin);
            if (originTile == null) continue;

            if (!_fastRulesDictionary.ContainsKey(originTile))
            {
                _fastRulesDictionary[originTile] = new HashSet<Tile>[4]
                {
                    new HashSet<Tile>(), // 0: up
                    new HashSet<Tile>(), // 1: down
                    new HashSet<Tile>(), // 2: left
                    new HashSet<Tile>()  // 3: right
                };
            }

            FillSet(_fastRulesDictionary[originTile][0], rule.BlockedAbove);
            FillSet(_fastRulesDictionary[originTile][1], rule.BlockedBelow);
            FillSet(_fastRulesDictionary[originTile][2], rule.BlockedLeft);
            FillSet(_fastRulesDictionary[originTile][3], rule.BlockedRight);
        }
    }

    private void AddMirrorRule(List<TileRule> mirroredList, TileIdentifier origin, List<TileIdentifier> blockedTiles, string inverseDirection)
    {
        if (blockedTiles == null) return;

        foreach (var blockedTile in blockedTiles)
        {
            if (ExistsInOriginals(blockedTile, origin, inverseDirection)) continue;

            TileRule targetRule = mirroredList.Find(rule => rule.Origin.Type == blockedTile.Type && rule.Origin.Direction == blockedTile.Direction);
            
            if (targetRule == null)
            {
                targetRule = new TileRule { Origin = blockedTile };
                mirroredList.Add(targetRule);
            }

            List<TileIdentifier> targetList = GetList(targetRule, inverseDirection);
            
            if (targetList != null && !targetList.Exists(identifier => identifier.Type == origin.Type && identifier.Direction == origin.Direction))
            {
                targetList.Add(origin);
            }
        }
    }

    private bool ExistsInOriginals(TileIdentifier fromTile, TileIdentifier blocksTile, string direction)
    {
        return _blockingRulesList.Exists(rule =>
            rule.Origin.Type == fromTile.Type && rule.Origin.Direction == fromTile.Direction &&
            GetList(rule, direction).Exists(blocked => blocked.Type == blocksTile.Type && blocked.Direction == blocksTile.Direction));
    }

    ///<summary>
    /// Valida se dois tiles vizinhos estão a violar alguma das regras de adjacência estabelecidas
    ///</summary>
    public bool IsBlocked(Tile current, Tile neighbor, Vector2Int direction)
    {
        if (!current.IsCompatibleWith(neighbor, direction)) return true;

        if (_fastRulesDictionary.TryGetValue(current, out var directionSets))
        {
            int index = direction == Vector2Int.up ? 0 :
                        direction == Vector2Int.down ? 1 :
                        direction == Vector2Int.left ? 2 : 3;
                        
            return directionSets[index].Contains(neighbor);
        }

        return false;
    }

    private void FillSet(HashSet<Tile> set, List<TileIdentifier> identifiers)
    {
        foreach (var identifier in identifiers)
        {
            Tile tile = FindTile(identifier);
            if (tile != null) set.Add(tile);
        }
    }

    private Tile FindTile(TileIdentifier identifier)
    {
        return _tilesetData.TilesetList.Find(tile => tile.Metadata.Type == identifier.Type && tile.Metadata.Direction == identifier.Direction);
    }

    private List<TileIdentifier> GetList(TileRule rule, string direction)
    {
        return direction switch
        {
            "above" => rule.BlockedAbove,
            "below" => rule.BlockedBelow,
            "left" => rule.BlockedLeft,
            "right" => rule.BlockedRight,
            _ => new List<TileIdentifier>()
        };
    }
}