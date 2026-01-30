namespace NumberGuessingGame.Models
{
    public class GameStateDto
    {
        public int DigitCount { get; set; }
        public string CurrentTurn { get; set; }
        public bool IsGameStarted { get; set; }
        public bool IsGameOver { get; set; }

        // History
        public List<GuessResult> YourGuesses { get; set; } = new();
        public List<GuessResult> OpponentGuesses { get; set; } = new();
    }

}
