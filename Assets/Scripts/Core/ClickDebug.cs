using UnityEngine;
using UnityEngine.EventSystems;

public class ClickDebug : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[ClickDebug] Clique detectado em {Input.mousePosition}");

            var resultado = new System.Collections.Generic.List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            EventSystem.current.RaycastAll(pointer, resultado);

            if (resultado.Count == 0)
                Debug.Log("[ClickDebug] Nenhum objeto detectado pelo raycast");

            foreach (var r in resultado)
                Debug.Log($"[ClickDebug] Raycast hit: {r.gameObject.name} | layer: {LayerMask.LayerToName(r.gameObject.layer)}");
        }
    }
}