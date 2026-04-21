public static class GameState
{
    public static bool isGameStarted = false;
    public static bool IsInBattle = false;
    public static int ChasersCount = 0;
    public static bool IsBeingChased => ChasersCount > 0;
    public static bool IsOnWater;
}