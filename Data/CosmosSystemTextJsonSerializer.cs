using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Cosmos;

namespace zinfandel_movie_club.Data;

public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonObjectSerializer _systemSerializer;

    public CosmosSystemTextJsonSerializer()
    {
        var options = new JsonSerializerOptions
        {
            //PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _systemSerializer = new JsonObjectSerializer(options);
    }
    
    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream is { CanSeek: true, Length: 0 }) return default!;
            if (typeof(Stream).IsAssignableTo(typeof(T))) return (T)(object)stream;
            return (T) _systemSerializer.Deserialize(stream, typeof(T), default)!;  
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var ms = new MemoryStream();
        _systemSerializer.Serialize(ms, input, typeof(T), default);
        ms.Position = 0;
        return ms;
    }
}