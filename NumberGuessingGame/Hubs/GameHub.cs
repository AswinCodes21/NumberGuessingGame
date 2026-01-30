using Microsoft.AspNetCore.SignalR;
using NumberGuessingGame.Models;
using NumberGuessingGame.Services;
using System.Collections.Concurrent;

namespace NumberGuessingGame.Hubs
{
    public class GameHub : Hub
    {
        public async Task SendChatMessage(string roomCode, string message)
        {
            var chatMessage = new ChatMessageDto
            {
                Message = message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = "text"
            };

            await Clients.OthersInGroup(roomCode)
                .SendAsync("ReceiveChatMessage", chatMessage);
        }

 
        public async Task SendVoiceMessage(string roomCode, byte[] audioData)
        {
            var chatMessage = new ChatMessageDto
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = "voice",
                AudioData = audioData
            };

            await Clients.OthersInGroup(roomCode)
                .SendAsync("ReceiveVoiceMessage", chatMessage);
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }


        public async Task CreateRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                throw new HubException("Invalid room code");

            if (GameRoomStore.Rooms.ContainsKey(roomCode))
                throw new HubException("Room already exists");

            var room = new GameRoom
            {
                RoomCode = roomCode,
                DigitCount = 4,
                CurrentTurn = "PLAYER1",
                IsGameOver = false,
                Player1 = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    Role = "PLAYER1"
                }
            };

            GameRoomStore.Rooms[roomCode] = room;

            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Caller.SendAsync("RoomCreated", "PLAYER1");
        }

        public async Task JoinRoom(string roomCode)
        {
            if (!GameRoomStore.Rooms.TryGetValue(roomCode, out var room))
                throw new HubException("Room not found");

            if (room.Player2 != null)
                throw new HubException("Room is already full");

            room.Player2 = new Player
            {
                ConnectionId = Context.ConnectionId,
                Role = "PLAYER2"
            };

            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Group(roomCode).SendAsync("OpponentJoined");
        }


        public async Task SubmitSecret(string roomCode, string secret)
        {
            if (!GameRoomStore.Rooms.TryGetValue(roomCode, out var room))
                throw new HubException("Room not found");

            Player player;

            if (room.Player1.ConnectionId == Context.ConnectionId)
            {
                player = room.Player1;
            }
            else if (room.Player2 != null &&
                     room.Player2.ConnectionId == Context.ConnectionId)
            {
                player = room.Player2;
            }
            else
            {
                throw new HubException("Player not part of this room");
            }

            player.SecretNumber = secret;

            if (!string.IsNullOrEmpty(room.Player1.SecretNumber) &&
                room.Player2 != null &&
                !string.IsNullOrEmpty(room.Player2.SecretNumber))
            {
                await Clients.Group(roomCode)
                    .SendAsync("GameStarted", room.CurrentTurn);
            }
        }


        public async Task MakeGuess(string roomCode, string guess)
        {
            if (!GameRoomStore.Rooms.TryGetValue(roomCode, out var room))
                throw new HubException("Room not found");

            if (room.IsGameOver)
                return;

            var isPlayer1 = room.Player1.ConnectionId == Context.ConnectionId;
            var currentPlayer = isPlayer1 ? "PLAYER1" : "PLAYER2";

            if (room.CurrentTurn != currentPlayer)
                throw new HubException("Not your turn");

            var opponent = isPlayer1 ? room.Player2 : room.Player1;

            if (opponent == null || string.IsNullOrEmpty(opponent.SecretNumber))
                throw new HubException("Opponent not ready");

            var (bulls, cows) = GameLogic.Calculate(opponent.SecretNumber, guess);

            var result = new GuessResult
            {
                Guess = guess,
                Bulls = bulls,
                Cows = cows
            };

            if (isPlayer1)
                room.Player1Guesses.Add(result);
            else
                room.Player2Guesses.Add(result);

            await Clients.Group(roomCode).SendAsync(
                "GuessResult",
                currentPlayer,
                result
            );

            if (bulls == room.DigitCount)
            {
                room.IsGameOver = true;
                await Clients.Group(roomCode)
                    .SendAsync("GameEnded", currentPlayer);
                return;
            }

            room.CurrentTurn = isPlayer1 ? "PLAYER2" : "PLAYER1";
            await Clients.Group(roomCode)
                .SendAsync("TurnChanged", room.CurrentTurn);
        }
    }
}
