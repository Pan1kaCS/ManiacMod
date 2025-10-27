using System.Text.Json.Serialization;

namespace ManiacMod
{
    // DTO для конфигурации — чтобы PluginConfig мог ссылаться на этот тип.
    public class Maniac
    {
        public int ManiacCount { get; set; } = 0;
        public int PlayersCount { get; set; } = 0;

        public Maniac() { }

        public Maniac(int maniacCount, int playersCount)
        {
            ManiacCount = maniacCount;
            PlayersCount = playersCount;
        }
    }
}