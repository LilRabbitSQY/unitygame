namespace FinalDefense.Dialogue
{
    // User-selected distribution mode: this value is compiled into the game.
    public static class EmbeddedAiConfig
    {
        public static readonly string ApiKey = "";
        public const string Endpoint = "https://api.deepseek.com/chat/completions";
        public const string Model = "deepseek-flash";
    }
}
