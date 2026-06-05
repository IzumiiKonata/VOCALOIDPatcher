using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VOCALOIDPatcher.Config;

public class ConfigManager
{
    private readonly string filePath;

    private readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true
    };

    private Dictionary<string, object> data = new();

    private readonly Dictionary<string, object?> cache = new();

    public ConfigManager(string filePath)
    {
        this.filePath = filePath;
        Load();
    }

    private void Load()
    {
        if (!File.Exists(filePath))
        {
            data = new Dictionary<string, object>();
            Save();
            return;
        }

        var json = File.ReadAllText(filePath);
        data = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options)
               ?? new Dictionary<string, object>();
        cache.Clear();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(filePath, json);
    }

    public void Set<T>(string key, T value)
    {
        data[key] = value!;
        cache[key] = value;
        Save();
    }

    public T Get<T>(string key, T defaultValue = default!)
    {
        if (cache.TryGetValue(key, out var cached))
            return cached is T typed ? typed : defaultValue;

        if (!data.TryGetValue(key, out var value))
            return defaultValue;

        try
        {
            T result;
            if (value is JsonElement element)
                result = JsonSerializer.Deserialize<T>(element.GetRawText(), options)!;
            else
                result = (T)Convert.ChangeType(value, typeof(T));

            cache[key] = result;
            return result;
        }
        catch
        {
            return defaultValue;
        }
    }

    public bool Contains(string key)
    {
        return data.ContainsKey(key);
    }

    public void Remove(string key)
    {
        data.Remove(key);
        cache.Remove(key);
    }
}
