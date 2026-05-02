using UnityEngine;
using UnityEngine.Tilemaps;

public class ChunkRenderer : MonoBehaviour
{
    [SerializeField] private Tilemap _tilemap;
    [SerializeField] private TilesetData _tilesetData;

    public Tilemap Tilemap => _tilemap;

    public void RenderMap(ChunkCellGrid grid, Vector2Int chunkSize)
    {
        for (int x = 1; x <= chunkSize.x; x++)
        {
            for (int y = 1; y <= chunkSize.y; y++)
            {
                Cell cell = grid.CellsArray[x, y];
                if (cell.IsCollapsed())
                {
                    int tileIndex = cell.CollapsedIndex();
                    _tilemap.SetTile(new Vector3Int(x - 1, y - 1, 0), _tilesetData.TilesetList[tileIndex].TilemapTile);}
            }
        }
    }

    public void SetTilemapEnabled(bool isEnabled)
    {
        _tilemap.enabled = isEnabled;
    }
}