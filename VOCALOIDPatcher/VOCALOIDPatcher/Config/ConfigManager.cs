namespace VOCALOIDPatcher.Config;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class ConfigManager
{
    private readonly string filePath;
    private Dictionary<string, object> data = new();

    private readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true
    };

    public ConfigManager(string filePath)
    {
        this.filePath = filePath;
        Load();
    }

    public void Load()
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
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(filePath, json);
    }

    public void Set<T>(string key, T value)
    {
        data[key] = value!;
        Save();
    }

    public T Get<T>(string key, T defaultValue = default!)
    {
        if (!data.TryGetValue(key, out var value))
            return defaultValue;

        try
        {
            if (value is JsonElement element)
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText(), options)!;
            }

            return (T)Convert.ChangeType(value, typeof(T));
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
    }
}