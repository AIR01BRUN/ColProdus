using System;

[System.Serializable]
public class DataJson
{
    public string id;
    public string objectID;
    public string iconID;
    public string name;
    public CustomComponentData[] customComponents;
}


[System.Serializable]
public class KeyValuePairData
{
    public string key;
    public string value; // raw JSON value. Works if JsonUtility maps numbers to string.
    public KeyValuePairData[] values;

    public int GetInt(string key, int defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return Int32.Parse(entry.value);;
        }

        return defaultValue;
    }
     public float GetFloat(string key, float defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return float.Parse(entry.value);;
        }

        return defaultValue;
    }
    public string GetString(string key, string defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return entry.value;
        }
        return defaultValue;
    }
    public bool GetBool(string key, bool defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return bool.Parse(entry.value);
        }
        return defaultValue;
    }


}
[System.Serializable]
public class CustomComponentData
{
    public string name;
    public KeyValuePairData[] values;
    public int GetInt(string key, int defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return Int32.Parse(entry.value);;
        }

        return defaultValue;
    }
    public float GetFloat(string key, float defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return float.Parse(entry.value);;
        }

        return defaultValue;
    }
    public string GetString(string key, string defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return entry.value;
        }
        return defaultValue;
    }
      public bool GetBool(string key, bool defaultValue = default)
    {
        foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return bool.Parse(entry.value);
        }
        return defaultValue;
    }
    public KeyValuePairData[] GetArray(string key)
    {
         foreach (var entry in values)
        {
            if (entry.key != key) continue;
            return entry.values;
        }
        return new KeyValuePairData[0];
    }
}
