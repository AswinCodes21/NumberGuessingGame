namespace NumberGuessingGame.Models
{
    public class ChatMessageDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; } = ""; 
        public string Message { get; set; } = "";
        public long Timestamp { get; set; }
        public string Type { get; set; } = "text"; 
        public byte[]? AudioData { get; set; }
    }
}
