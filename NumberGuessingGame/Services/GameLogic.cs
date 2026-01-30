namespace NumberGuessingGame.Services
{
    public static class GameLogic
    {
        public static (int bulls, int cows) Calculate(string secret, string guess)
        {
            int bulls = 0, cows = 0;

            for (int i = 0; i < guess.Length; i++)
            {
                if (guess[i] == secret[i])
                    bulls++;
                else if (secret.Contains(guess[i]))
                    cows++;
            }

            return (bulls, cows);
        }
    }

}
