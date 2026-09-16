namespace CreatureClimb
{
    /// <summary>
    /// HUD / flow surface used by <see cref="GameManager"/>.
    /// Keeps gameplay free of concrete menu wiring.
    /// </summary>
    public interface IGameUI
    {
        void ShowReady(int highScore);
        void ShowPlaying(int score, int highScore);
        void UpdateScore(int score);
        void ShowGameOver(int score, int highScore, string title = null);
        void ShowPaused(bool paused);
    }
}
