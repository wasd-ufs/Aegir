/// <summary>
/// Classe estática que atua como um barramento central de estado para o jogo.
/// Armazena flags globais críticas que definem a situação atual da gameplay, 
/// evitando o acoplamento excessivo (dependências diretas) entre diferentes sistemas.
/// </summary>
public static class GameState
{
    #region Flags de Estado Global
    /// <summary>Indica se a tela inicial já foi passada e o jogador está explorando o mundo.</summary>
    public static bool isGameStarted = false;
    
    /// <summary>Indica se o jogador está ativamente dentro da tela de combate por turnos.</summary>
    public static bool IsInBattle = false;
    
    /// <summary>Contador de entidades inimigas agressivas que estão ativamente perseguindo o jogador no mapa.</summary>
    public static int ChasersCount = 0;
    
    /// <summary>Propriedade derivada: retorna true se houver pelo menos um inimigo no encalço do jogador.</summary>
    public static bool IsBeingChased => ChasersCount > 0;
    
    /// <summary>Indica se o jogador está no comando do barco (true) ou andando em terra firme como capitão (false).</summary>
    public static bool IsOnWater;
    #endregion
}