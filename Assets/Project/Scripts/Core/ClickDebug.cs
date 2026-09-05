using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Ferramenta de depuração utilizada para identificar quais objetos da Interface de Usuário (UI)
/// estão interceptando e "roubando" cliques do mouse, útil para corrigir colisores ou imagens
/// ocultas com "Raycast Target" ativado incorretamente.
/// </summary>
public class ClickDebug : MonoBehaviour
{
    #region Ciclo de Vida
    void Update()
    {
        if (Mouse.current == null) return;

        // Dispara a checagem apenas quando o botão esquerdo do mouse é pressionado
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Debug.Log($"[ClickDebug] Clique detectado em {mousePos}");

            var resultado = new System.Collections.Generic.List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = mousePos };
            
            // Dispara um raio através da interface para pegar tudo o que está abaixo do cursor
            if (EventSystem.current != null)
            {
                EventSystem.current.RaycastAll(pointer, resultado);

                if (resultado.Count == 0)
                    Debug.Log("[ClickDebug] Nenhum objeto detectado pelo raycast");

                // Itera e exibe todos os elementos atingidos no local do clique e sua layer respectiva
                foreach (var r in resultado)
                    Debug.Log($"[ClickDebug] Raycast hit: {r.gameObject.name} | layer: {LayerMask.LayerToName(r.gameObject.layer)}");
            }
        }
    }
    #endregion
}