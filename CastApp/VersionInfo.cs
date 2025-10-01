using System.Text.Json.Serialization;

namespace CastApp
{
    public class VersionInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("updateUrl")]
        public string UpdateUrl { get; set; } = string.Empty;

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; } = string.Empty;
    }
}
