using System.Text.Json.Serialization;

namespace ZEarth.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SceneMode
    {
        Scene3D,
        Scene2D,
        ColumbusView
    }
}
