using UnityEngine;

/// <summary>
/// Controla a interação do jogador com estruturas específicas no mundo, 
/// acionando a interface de usuário correspondente.
/// </summary>
public class InteractableStructures : MonoBehaviour
{
    [SerializeField] private StructureData _structureData;
    
    private PlayerInputActions _inputActions;
    private StructuresUI _structuresUI; 
    private bool _isPlayerNearby;

    private void Awake()
    {
        _inputActions = new PlayerInputActions();
    }

    private void Start()
    {
        _structuresUI = FindFirstObjectByType<StructuresUI>();
    }

    private void Update()
    {
        if (_isPlayerNearby)
        {
            if (_inputActions.Player.Contatar.WasPressedThisFrame())
            {
                _structuresUI.ShowScreen(this, _structureData.StructureName);
            }

            if (_inputActions.Player.CancelarSelecao.WasPressedThisFrame())
            {
                _structuresUI.CloseScreen();
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            _isPlayerNearby = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            _isPlayerNearby = false;
            _structuresUI.CloseScreen(); 
        }
    }

    private void OnEnable()
    {
        _inputActions.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Disable();
    }
}