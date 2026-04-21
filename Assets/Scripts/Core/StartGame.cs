using UnityEngine;
using UnityEngine.UI;

public class StartGame : MonoBehaviour
{
    public Button StartBtn, OptionsBtn;
    public GameObject startScreen;
    public GameBoyTransition transition;

    void Awake()
    {
        StartBtn.onClick.AddListener(() => StartG());
    }
    public void StartG()
    {
        if (transition != null)
        {
            transition.StartTransition(onMidpointCallback: () => 
            {
                startScreen.SetActive(false);
                GameState.isGameStarted = true;
            });
        }
    }
}
