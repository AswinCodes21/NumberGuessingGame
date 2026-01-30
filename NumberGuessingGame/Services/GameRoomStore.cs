using NumberGuessingGame.Models;
using System.Collections.Concurrent;

namespace NumberGuessingGame.Services
{
    public static class GameRoomStore
    {
        public static ConcurrentDictionary<string, GameRoom> Rooms = new();
    }
}
