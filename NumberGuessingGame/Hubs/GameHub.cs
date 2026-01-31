using Microsoft.AspNetCore.SignalR;
using NumberGuessingGame.Models;
using NumberGuessingGame.Services;
using System.Collections.Concurrent;

namespace NumberGuessingGame.Hubs
{
    public class GameHub : Hub

    {

        public async Task SetDifficulty(string roomCode, int digitCount)
        {
            if (!GameRoomStore.Rooms.TryGetValue(roomCode, out var room))
                throw new HubException("Room not found");

            // Only HOST (PLAYER1) can set difficulty
            if (room.Player1.ConnectionId != Context.ConnectionId)
                throw new HubException("Only host can set difficulty");

            if (digitCount != 3 && digitCount != 4)
                throw new HubException("Invalid digit count");

            room.DigitCount = digitCount;

            // Notify ONLY the guest
            await Clients.OthersInGroup(roomCode)
                .SendAsync("DifficultySet", digitCount);
        }

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


        public async Task SendVoiceMessage(
    string roomCode,
    string sender,
    string base64Audio
)
        {
            await Clients.OthersInGroup(roomCode)
                .SendAsync("VoiceMessage", new
                {
                    sender = sender,          // "HOST" or "GUEST"
                    audioData = base64Audio,  // 그대로 forward
                    mimeType = "audio/webm"
                });
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


        public async Task RestartGame(string roomCode)
        {
            if (!GameRoomStore.Rooms.TryGetValue(roomCode, out var room))
                throw new HubException("Room not found");

            if (room.Player1 == null || room.Player1.ConnectionId != Context.ConnectionId)
                throw new HubException("Only host can restart the game");


            if (room.Player1 == null || room.Player2 == null)
                throw new HubException("Both players are required to restart");

            // Reset core game state
            room.IsGameOver = false;
            room.CurrentTurn = "PLAYER1"; // Host always starts (consistent rule)

            // Clear secrets
            room.Player1.SecretNumber = null;
            room.Player2.SecretNumber = null;

            // Clear guesses
            room.Player1Guesses.Clear();
            room.Player2Guesses.Clear();

            // Notify BOTH players to reset UI and go back to secret submission
            await Clients.Group(roomCode)
                .SendAsync("GameRestarted", new GameStateDto
                {
                    DigitCount = room.DigitCount,
                    CurrentTurn = room.CurrentTurn,
                    IsGameStarted = false,
                    IsGameOver = false,

                    YourGuesses = new List<GuessResult>(),
                    OpponentGuesses = new List<GuessResult>()
                });
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

            bool joined = false;

            // Host reconnect
            if (room.Player1 != null &&
                room.Player1.ConnectionId != Context.ConnectionId &&
                room.Player2 != null)
            {
                room.Player1.ConnectionId = Context.ConnectionId;
                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
                joined = true;
            }
            //  Guest reconnect
            else if (room.Player2 != null &&
                     room.Player2.ConnectionId != Context.ConnectionId)
            {
                room.Player2.ConnectionId = Context.ConnectionId;
                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
                joined = true;
            }
            //  New guest joining
            else if (room.Player2 == null)
            {
                room.Player2 = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    Role = "PLAYER2"
                };

                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

                // Notify host
                await Clients.OthersInGroup(roomCode)
                    .SendAsync("OpponentJoined");

                joined = true;
            }

            if (!joined)
                throw new HubException("Room is already full");

            // ALWAYS send game state after join/rejoin
            var isPlayer1 = room.Player1?.ConnectionId == Context.ConnectionId;

            await Clients.Caller.SendAsync("GameState", new GameStateDto
            {
                DigitCount = room.DigitCount,
                CurrentTurn = room.CurrentTurn,

                IsGameStarted = !string.IsNullOrEmpty(room.Player1?.SecretNumber) && !string.IsNullOrEmpty(room.Player2?.SecretNumber),

                IsGameOver = room.IsGameOver,

                YourSecret = room.IsGameOver ? (isPlayer1 ? room.Player1.SecretNumber : room.Player2.SecretNumber) : null, 
                OpponentSecret = room.IsGameOver ? (isPlayer1 ? room.Player2.SecretNumber : room.Player1.SecretNumber): null,

                YourGuesses = isPlayer1 ? room.Player1Guesses : room.Player2Guesses,

                OpponentGuesses = isPlayer1 ? room.Player2Guesses : room.Player1Guesses
            });

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

            bool isPlayer1 = room.Player1.ConnectionId == Context.ConnectionId;
            string currentPlayer = isPlayer1 ? "PLAYER1" : "PLAYER2";

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

            // Broadcast guess result (safe, no secrets)
            await Clients.Group(roomCode)
                .SendAsync("GuessResult", currentPlayer, result);

            //  GAME OVER
            if (bulls == room.DigitCount)
            {
                room.IsGameOver = true;

                var player1 = room.Player1;
                var player2 = room.Player2!;

                // Send personalized GameState to PLAYER1
                await Clients.Client(player1.ConnectionId)
                    .SendAsync("GameState", new GameStateDto
                    {
                        DigitCount = room.DigitCount,
                        CurrentTurn = room.CurrentTurn,
                        IsGameStarted = true,
                        IsGameOver = true,

                        YourSecret = player1.SecretNumber,
                        OpponentSecret = player2.SecretNumber,

                        YourGuesses = room.Player1Guesses,
                        OpponentGuesses = room.Player2Guesses
                    });

                // Send personalized GameState to PLAYER2
                await Clients.Client(player2.ConnectionId)
                    .SendAsync("GameState", new GameStateDto
                    {
                        DigitCount = room.DigitCount,
                        CurrentTurn = room.CurrentTurn,
                        IsGameStarted = true,
                        IsGameOver = true,

                        YourSecret = player2.SecretNumber,
                        OpponentSecret = player1.SecretNumber,

                        YourGuesses = room.Player2Guesses,
                        OpponentGuesses = room.Player1Guesses
                    });

                return;
            }

            // Next turn
            room.CurrentTurn = isPlayer1 ? "PLAYER2" : "PLAYER1";

            await Clients.Group(roomCode)
                .SendAsync("TurnChanged", room.CurrentTurn);
        }
    }
    }
