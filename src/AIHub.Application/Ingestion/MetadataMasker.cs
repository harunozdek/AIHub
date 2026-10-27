using System.Text.Json;
using System.Text.Json.Nodes;
using AIHub.Domain.Masking;

namespace AIHub.Application.Ingestion;

/// <summary>
/// Metadata JSON ağacını dolaşıp hassas değerleri maskeler (BR-3). Anahtar adı hassassa değerin
/// tamamı, değilse her string değer metin olarak maskelenir; sayı olarak gelen geçerli bir TCKN de
/// string yer tutucuya çevrilir (plan 0001, Q-3 ve Q-4).
/// </summary>
public static class MetadataMasker
{
    public static string? Mask(JsonObject? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var copy = (JsonObject)metadata.DeepClone();
        MaskObject(copy);
        return copy.ToJsonString();
    }

    private static void MaskObject(JsonObject obj)
    {
        foreach (var key in obj.Select(p => p.Key).ToArray())
        {
            if (obj[key] is not null && SensitiveDataMasker.TryGetPlaceholderForKey(key, out var placeholder))
            {
                obj[key] = placeholder;
                continue;
            }

            obj[key] = MaskNode(obj[key]);
        }
    }

    private static JsonNode? MaskNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject nested:
                MaskObject(nested);
                return nested;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    array[i] = MaskNode(array[i]);
                }

                return array;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                return JsonValue.Create(SensitiveDataMasker.Mask(value.GetValue<string>()));
            case JsonValue value when value.GetValueKind() == JsonValueKind.Number:
                var raw = value.ToJsonString();
                return TcknValidator.IsValid(raw) ? JsonValue.Create(MaskKinds.Placeholder(MaskKind.Tckn)) : value;
            default:
                return node;
        }
    }
}
