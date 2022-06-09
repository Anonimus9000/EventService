using System;
using Newtonsoft.Json;

namespace Analytics
{
    [Serializable]
    public struct EventData
    {
        [JsonProperty("type")] public string Type { get; }
        [JsonProperty("data")] public string Data { get; }
        
        public EventData(string type, string data)
        {
            Type = type;
            Data = data;
        }
    }
}