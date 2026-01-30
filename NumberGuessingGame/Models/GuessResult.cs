namespace NumberGuessingGame.Models
{
    public class GuessResult
    {
        public string Guess { get; set; } = "";
        public int Bulls { get; set; }
        public int Cows { get; set; }
    }

}
