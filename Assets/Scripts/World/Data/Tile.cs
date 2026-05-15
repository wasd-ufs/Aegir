using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que representa um tile individual do WFC.
/// Cada tile possui metadados de camada, tipo visual e direção, além de
/// sockets de canto usados para verificar compatibilidade com vizinhos.
/// </summary>
[CreateAssetMenu(fileName = "New Tile", menuName = "WFC/Tile")]
public class Tile : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Enumerações
    // -------------------------------------------------------------------------

    /// <summary>
    /// Forma visual do tile. Determina como os sockets de canto são calculados.
    /// </summary>
    public enum Type : byte
    {
        Bloco,          // Tile plano — todos os cantos com o mesmo valor de camada
        Costa,          // Transição com uma borda inteira de água e a oposta de terra
        Quina,          // Quina convexa — três cantos de água, um de terra
        QuinaInterna    // Quina côncava — três cantos de terra, um de água
    }

    /// <summary>
    /// Direção ou orientação do tile.
    /// Usada em conjunto com <see cref="Type"/> para determinar os sockets.
    /// </summary>
    public enum Directions { N, S, O, L, NL, NO, SL, SO, None }

    // -------------------------------------------------------------------------
    // Structs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Os quatro cantos do tile, cada um armazenando o valor da camada adjacente.
    /// Cantos compartilhados entre dois tiles vizinhos devem ser iguais para que
    /// a conexão seja válida.
    /// </summary>
    [Serializable]
    public struct CornerSockets
    {
        public int NO; // Canto Noroeste
        public int NE; // Canto Nordeste
        public int SO; // Canto Sudoeste
        public int SE; // Canto Sudeste
    }

    /// <summary>
    /// Define uma criatura que pode nascer sobre este tile, com quantidade e chance.
    /// </summary>
    [Serializable]
    public struct SpawnableCreatures
    {
        public GameObject creature;
        public int quantity;
        [Range(0, 1f)] public float spawnChance;
    }

    /// <summary>
    /// Metadados que identificam o tile dentro do sistema WFC.
    /// A camada indica o bioma (0 = Água, 1 = Costa, 2 = Terra);
    /// <see cref="corners"/> é gerado automaticamente via <see cref="OnValidate"/>.
    /// </summary>
    [Serializable]
    public struct TileMetadata
    {
        public int camada;              // 0: Água | 1: Costa | 2: Terra
        public Type type;
        public Directions direction;
        [HideInInspector] public CornerSockets corners; // Gerado automaticamente
    }

    // -------------------------------------------------------------------------
    // Campos Públicos
    // -------------------------------------------------------------------------

    public UnityEngine.Tilemaps.TileBase tilemapTile;
    public float peso = 1f;
    public List<SpawnableCreatures> spawnableCreatures;
    public TileMetadata metadata;

    // -------------------------------------------------------------------------
    // Unity Callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Recalcula os sockets de canto sempre que o tile é editado no Inspector.
    /// </summary>
    private void OnValidate()
    {
        metadata.corners = GerarCorners(metadata.camada, metadata.type, metadata.direction);
    }

    // -------------------------------------------------------------------------
    // Lógica de Sockets
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gera os <see cref="CornerSockets"/> com base na camada, tipo e direção do tile.
    /// <para>
    /// Convenção de valores:
    /// <list type="bullet">
    ///   <item><c>a</c> = valor da própria camada</item>
    ///   <item><c>i = a - 1</c> = camada inferior (ex.: água em relação à costa)</item>
    ///   <item><c>s = a + 1</c> = camada superior (ex.: terra em relação à costa)</item>
    /// </list>
    /// </para>
    /// </summary>
    private CornerSockets GerarCorners(int a, Type type, Directions d)
    {
        int i = a - 1; // camada inferior (ex: água)
        int s = a + 1; // camada superior (ex: terra)

        // Bloco plano: todos os cantos com o mesmo valor
        if (type == Type.Bloco)
            return new CornerSockets { NO = a, NE = a, SO = a, SE = a };

        return (type, d) switch
        {
            // Costa: uma borda inteira é a camada inferior, a oposta é a camada superior
            (Type.Costa, Directions.N)  => new CornerSockets { NO = i, NE = i, SO = s, SE = s },
            (Type.Costa, Directions.S)  => new CornerSockets { NO = s, NE = s, SO = i, SE = i },
            (Type.Costa, Directions.L)  => new CornerSockets { NO = s, NE = i, SO = s, SE = i },
            (Type.Costa, Directions.O)  => new CornerSockets { NO = i, NE = s, SO = i, SE = s },

            // Quina convexa: três cantos inferiores (água), um canto superior (terra)
            (Type.Quina, Directions.NL) => new CornerSockets { NO = i, NE = i, SO = s, SE = i },
            (Type.Quina, Directions.NO) => new CornerSockets { NO = i, NE = i, SO = i, SE = s },
            (Type.Quina, Directions.SL) => new CornerSockets { NO = s, NE = i, SO = i, SE = i },
            (Type.Quina, Directions.SO) => new CornerSockets { NO = i, NE = s, SO = i, SE = i },

            // Quina interna côncava: três cantos superiores (terra), um canto inferior (água)
            (Type.QuinaInterna, Directions.NL) => new CornerSockets { NO = s, NE = s, SO = i, SE = s },
            (Type.QuinaInterna, Directions.NO) => new CornerSockets { NO = s, NE = s, SO = s, SE = i },
            (Type.QuinaInterna, Directions.SL) => new CornerSockets { NO = i, NE = s, SO = s, SE = s },
            (Type.QuinaInterna, Directions.SO) => new CornerSockets { NO = s, NE = i, SO = s, SE = s },

            _ => new CornerSockets()
        };
    }

    // -------------------------------------------------------------------------
    // Compatibilidade
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifica se este tile é compatível com um vizinho em uma dada direção,
    /// comparando os cantos compartilhados entre os dois tiles.
    /// <para>
    /// Cantos compartilhados por direção:
    /// <list type="bullet">
    ///   <item>Direita  → A.NE == B.NO  e  A.SE == B.SO</item>
    ///   <item>Esquerda → A.NO == B.NE  e  A.SO == B.SE</item>
    ///   <item>Acima    → A.NO == B.SO  e  A.NE == B.SE</item>
    ///   <item>Abaixo   → A.SO == B.NO  e  A.SE == B.NE</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="neighbor">Tile vizinho a ser verificado.</param>
    /// <param name="direction">Direção do vizinho em relação a este tile.</param>
    /// <returns><c>true</c> se os cantos compartilhados coincidem.</returns>
    public bool IsCompatibleWith(Tile neighbor, Vector2Int direction)
    {
        CornerSockets a = metadata.corners;
        CornerSockets b = neighbor.metadata.corners;

        if (direction == Vector2Int.right)
            return a.NE == b.NO && a.SE == b.SO;

        if (direction == Vector2Int.left)
            return a.NO == b.NE && a.SO == b.SE;

        if (direction == Vector2Int.up)
            return a.NO == b.SO && a.NE == b.SE;

        if (direction == Vector2Int.down)
            return a.SO == b.NO && a.SE == b.NE;

        return false;
    }
}