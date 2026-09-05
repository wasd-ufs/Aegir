using UnityEngine;

public class PlayerTransitionController : MonoBehaviour
{
    private WorldTileQuery _tileQuery;
    private ChunkLifecycleManager _lifecycleManager;
    private Camera _mainCamera;
    private float _cachedCellSize;

    public void Setup(WorldTileQuery tileQuery, ChunkLifecycleManager lifecycleManager, Camera mainCamera, float cachedCellSize)
    {
        _tileQuery = tileQuery;
        _lifecycleManager = lifecycleManager;
        _mainCamera = mainCamera;
        _cachedCellSize = cachedCellSize;
    }

    public void TryTransition(PlayerMovement boatMovement)
    {
        if (boatMovement == null) return;

        GameObject boatObject = boatMovement.gameObject;
        GameObject captainObject = boatMovement.captain; 

        if (boatMovement.isOnWater)
        {
            Vector3[] directionsArray = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (Vector3 direction in directionsArray)
            {
                Vector3 targetWorldPosition = boatObject.transform.position + (direction * _cachedCellSize);
                Tile tile = _tileQuery.GetTileAtWorldPosition(targetWorldPosition);

                if (tile != null && tile.Metadata.Layer == 1) // 1 = Costa
                {
                    boatMovement.isOnWater = false;
                    GameState.IsOnWater = false;
                    captainObject.SetActive(true);
                    captainObject.transform.position = targetWorldPosition;
                    
                    _lifecycleManager.SetPlayerTransform(captainObject.transform);
                    boatObject.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    
                    _mainCamera.orthographicSize = 3.5f; 
                    return;
                }
            }
        }
        else
        {
            float distanceToBoat = Vector3.Distance(captainObject.transform.position, boatObject.transform.position);
            if (distanceToBoat < _cachedCellSize * 1.5f)
            {
                boatMovement.isOnWater = true;
                GameState.IsOnWater = true;
                captainObject.SetActive(false);
                
                _lifecycleManager.SetPlayerTransform(boatObject.transform);
                _mainCamera.orthographicSize = 5f;
            }
        }
    }

    public void TryFindWaterTile(Transform playerTransform)
    {
        if (playerTransform == null) return;

        Vector3 startPosition = playerTransform.position;
        int searchRadius = 5;

        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int y = -searchRadius; y <= searchRadius; y++)
            {
                Vector3 checkPosition = startPosition + new Vector3(x * _cachedCellSize, y * _cachedCellSize, 0);
                Tile tile = _tileQuery.GetTileAtWorldPosition(checkPosition);

                if (tile != null && tile.Metadata.Layer == 0) // 0 = Água
                {
                    playerTransform.position = checkPosition;
                    Debug.Log("[PlayerTransition] Player moved to water!");
                    return;
                }
            }
        }
        Debug.LogWarning("[PlayerTransition] No water tile found nearby.");
    }
}