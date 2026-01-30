namespace NumberGuessingGame.Models
{
    public class GameRoom
    {
        public string RoomCode { get; set; } = "";
        public int DigitCount { get; set; }

        public Player Player1 { get; set; } = null!;
        public Player Player2 { get; set; } = null!;

        public string CurrentTurn { get; set; } = "PLAYER1";
        public bool IsGameOver { get; set; } = false;

        public List<GuessResult> Player1Guesses { get; set; } = new();
        public List<GuessResult> Player2Guesses { get; set; } = new();
    }

}
