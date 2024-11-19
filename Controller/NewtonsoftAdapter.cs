using Newtonsoft.Json;

public static class NewtonsoftAdapter
{
    public static string SerializeObject(object obj)
    {
        return JsonConvert.SerializeObject(obj, Formatting.Indented);
    }

    public static T DeserializeObject<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }
}
