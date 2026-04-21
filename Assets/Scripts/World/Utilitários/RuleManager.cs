using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia as regras de bloqueio entre tiles no sistema WFC.
/// <para>
/// No Awake, as regras definidas no Inspector são:
/// <list type="number">
///   <item>Espelhadas automaticamente (se A bloqueia B acima, B também bloqueia A abaixo).</item>
///   <item>Compiladas em um dicionário de HashSets (<see cref="fastRules"/>) para consulta O(1).</item>
/// </list>
/// </para>
/// <para>
/// O método público <see cref="IsBlocked"/> é chamado pelo <see cref="MapGenerator"/>
/// durante a construção do cache de compatibilidade (<c>compatible[a, b, dir]</c>).
/// </para>
/// </summary>
public class RuleManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Tipos de Dados
    // -------------------------------------------------------------------------

    /// <summary>
    /// Identifica um tile pela combinação de tipo visual e direção.
    /// Usado nas regras de bloqueio configuradas no Inspector.
    /// </summary>
    [Serializable]
    public struct TileIdentifier
    {
        public Tile.Type tipo;
        public Tile.Directions direcao;
    }

    /// <summary>
    /// Regra de bloqueio direcional para um tile de origem.
    /// Cada lista indica quais tiles não podem aparecer em cada direção adjacente.
    /// </summary>
    [Serializable]
    public class TileRule // Classe (não struct) para permitir busca segura com null
    {
        public TileIdentifier origem;
        public List<TileIdentifier> bloqueadosAcima    = new List<TileIdentifier>();
        public List<TileIdentifier> bloqueadosAbaixo   = new List<TileIdentifier>();
        public List<TileIdentifier> bloqueadosEsquerda = new List<TileIdentifier>();
        public List<TileIdentifier> bloqueadosDireita  = new List<TileIdentifier>();
    }

    // -------------------------------------------------------------------------
    // Campos Públicos
    // -------------------------------------------------------------------------

    /// <summary>Regras de bloqueio configuradas no Inspector. Podem ser incompletas — o espelhamento é automático.</summary>
    public List<TileRule> regrasDeBloqueio;

    /// <summary>Referência ao tileset para resolver <see cref="TileIdentifier"/> → <see cref="Tile"/>.</summary>
    public TilesetData tilesetData;

    // -------------------------------------------------------------------------
    // Cache Interno
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mapa de bloqueios rápidos: tile origem → array de 4 HashSets de tiles bloqueados,
    /// indexados por direção [0=cima, 1=baixo, 2=esquerda, 3=direita].
    /// Construído em <see cref="ProcessRules"/> e usado em <see cref="IsBlocked"/>.
    /// </summary>
    private Dictionary<Tile, HashSet<Tile>[]> fastRules;

    // -------------------------------------------------------------------------
    // Unity Callbacks
    // -------------------------------------------------------------------------

    private void Awake()
    {
        ProcessRules();
    }

    // -------------------------------------------------------------------------
    // Processamento de Regras
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ponto de entrada do processamento: espelha as regras originais e
    /// constrói o dicionário de consulta rápida <see cref="fastRules"/>.
    /// </summary>
    private void ProcessRules()
    {
        MirrorRules();
        BuildFastRules();
    }

    /// <summary>
    /// Para cada regra original, gera a regra inversa simétrica caso ela não exista.
    /// Ex.: "A bloqueia B acima" → adiciona "B bloqueia A abaixo" se ausente.
    /// </summary>
    private void MirrorRules()
    {
        List<TileRule> regrasEspelhadas = new List<TileRule>();
        var originais = regrasDeBloqueio.ToArray();

        foreach (var regra in originais)
        {
            AdicionarEspelho(regrasEspelhadas, regra.origem, regra.bloqueadosAcima,    "abaixo");
            AdicionarEspelho(regrasEspelhadas, regra.origem, regra.bloqueadosAbaixo,   "acima");
            AdicionarEspelho(regrasEspelhadas, regra.origem, regra.bloqueadosEsquerda, "direita");
            AdicionarEspelho(regrasEspelhadas, regra.origem, regra.bloqueadosDireita,  "esquerda");
        }

        regrasDeBloqueio.AddRange(regrasEspelhadas);
    }

    /// <summary>
    /// Compila todas as regras (originais + espelhadas) no dicionário
    /// <see cref="fastRules"/> para consulta O(1) durante a geração.
    /// </summary>
    private void BuildFastRules()
    {
        fastRules = new Dictionary<Tile, HashSet<Tile>[]>();

        foreach (var regra in regrasDeBloqueio)
        {
            Tile tileOrigem = FindTile(regra.origem);
            if (tileOrigem == null) continue;

            if (!fastRules.ContainsKey(tileOrigem))
                fastRules[tileOrigem] = new HashSet<Tile>[4]
                {
                    new HashSet<Tile>(), // 0: cima
                    new HashSet<Tile>(), // 1: baixo
                    new HashSet<Tile>(), // 2: esquerda
                    new HashSet<Tile>()  // 3: direita
                };

            FillSet(fastRules[tileOrigem][0], regra.bloqueadosAcima);
            FillSet(fastRules[tileOrigem][1], regra.bloqueadosAbaixo);
            FillSet(fastRules[tileOrigem][2], regra.bloqueadosEsquerda);
            FillSet(fastRules[tileOrigem][3], regra.bloqueadosDireita);
        }
    }

    // -------------------------------------------------------------------------
    // Espelhamento
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gera a regra inversa para um conjunto de tiles bloqueados em uma direção.
    /// Cada tile bloqueado recebe uma nova regra que bloqueia a origem na direção oposta,
    /// desde que essa regra ainda não exista nas regras originais.
    /// </summary>
    /// <param name="listaEspelhada">Lista de destino para as regras espelhadas.</param>
    /// <param name="origem">Tile que originou o bloqueio.</param>
    /// <param name="bloqueados">Tiles que são bloqueados pela origem.</param>
    /// <param name="dirInv">Direção oposta (onde o bloqueio inverso será aplicado).</param>
    private void AdicionarEspelho(List<TileRule> listaEspelhada, TileIdentifier origem, List<TileIdentifier> bloqueados, string dirInv)
    {
        if (bloqueados == null) return;

        foreach (var bloqueado in bloqueados)
        {
            // Pula se a regra inversa já existe nas originais
            if (ExisteNasOriginais(bloqueado, origem, dirInv)) continue;

            // Busca ou cria a regra espelhada para o tile bloqueado
            TileRule alvo = listaEspelhada.Find(r => r.origem.tipo == bloqueado.tipo && r.origem.direcao == bloqueado.direcao);
            if (alvo == null)
            {
                alvo = new TileRule { origem = bloqueado };
                listaEspelhada.Add(alvo);
            }

            // Adiciona a origem na lista da direção inversa do tile bloqueado
            var listaDestino = ObterLista(alvo, dirInv);
            if (listaDestino != null && !listaDestino.Exists(b => b.tipo == origem.tipo && b.direcao == origem.direcao))
                listaDestino.Add(origem);
        }
    }

    /// <summary>
    /// Verifica se já existe nas regras originais uma regra onde
    /// <paramref name="de"/> bloqueia <paramref name="bloqueia"/> na direção <paramref name="dir"/>.
    /// </summary>
    private bool ExisteNasOriginais(TileIdentifier de, TileIdentifier bloqueia, string dir)
    {
        return regrasDeBloqueio.Exists(r =>
            r.origem.tipo == de.tipo && r.origem.direcao == de.direcao &&
            ObterLista(r, dir).Exists(b => b.tipo == bloqueia.tipo && b.direcao == bloqueia.direcao));
    }

    // -------------------------------------------------------------------------
    // Consulta Pública
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifica se <paramref name="neighbor"/> está bloqueado de aparecer ao lado de
    /// <paramref name="current"/> na direção <paramref name="direction"/>.
    /// <para>
    /// A verificação tem duas etapas:
    /// <list type="number">
    ///   <item>Compatibilidade de sockets via <see cref="Tile.IsCompatibleWith"/>.</item>
    ///   <item>Regras de bloqueio explícitas em <see cref="fastRules"/>.</item>
    /// </list>
    /// Se qualquer etapa reprovar, retorna <c>true</c> (bloqueado).
    /// </para>
    /// </summary>
    /// <param name="current">Tile atual (origem).</param>
    /// <param name="neighbor">Tile candidato a vizinho.</param>
    /// <param name="direction">Direção do vizinho em relação ao atual.</param>
    /// <returns><c>true</c> se o par for incompatível ou bloqueado por regra.</returns>
    public bool IsBlocked(Tile current, Tile neighbor, Vector2Int direction)
    {
        // Etapa 1: verificação de sockets (compatibilidade geométrica)
        if (!current.IsCompatibleWith(neighbor, direction)) return true;

        // Etapa 2: verificação de regras explícitas de bloqueio
        if (fastRules.TryGetValue(current, out var dirs))
        {
            int idx = direction == Vector2Int.up    ? 0 :
                      direction == Vector2Int.down  ? 1 :
                      direction == Vector2Int.left  ? 2 : 3;
            return dirs[idx].Contains(neighbor);
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Helpers Privados
    // -------------------------------------------------------------------------

    /// <summary>Preenche um HashSet com os tiles resolvidos a partir de uma lista de identificadores.</summary>
    private void FillSet(HashSet<Tile> set, List<TileIdentifier> ids)
    {
        foreach (var id in ids)
        {
            Tile t = FindTile(id);
            if (t != null) set.Add(t);
        }
    }

    /// <summary>Resolve um <see cref="TileIdentifier"/> para o <see cref="Tile"/> correspondente no tileset.</summary>
    private Tile FindTile(TileIdentifier id)
        => tilesetData.tileset.Find(t => t.metadata.type == id.tipo && t.metadata.direction == id.direcao);

    /// <summary>Retorna a lista de bloqueados de uma <see cref="TileRule"/> para a direção textual informada.</summary>
    private List<TileIdentifier> ObterLista(TileRule r, string dir) => dir switch
    {
        "acima"    => r.bloqueadosAcima,
        "abaixo"   => r.bloqueadosAbaixo,
        "esquerda" => r.bloqueadosEsquerda,
        "direita"  => r.bloqueadosDireita,
        _          => new List<TileIdentifier>()
    };
}