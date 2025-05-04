using Newtonsoft.Json.Linq;

namespace GeofenceWorker.Helper;

public static class FindMaxProperty
{
    public static async Task<T> FindMaxPropertyValueWithExceptionAsync<T>(List<JToken> data, string propertyName) where T : struct, IComparable<T>
    {
        T maxVal = default(T);
        bool foundValidValue = false;

        foreach (var token in data.OfType<JObject>().Where(obj => obj.ContainsKey(propertyName)).Select(obj => obj[propertyName]))
        {
            if (token != null)
            {
                try
                {
                    T value = await Task.Run(() => token.ToObject<T>());
                    if (!foundValidValue || value.CompareTo(maxVal) > 0)
                    {
                        maxVal = value;
                        foundValidValue = true;
                    }
                }
                catch (InvalidCastException)
                {
                    throw new InvalidCastException($"Properti '{propertyName}' memiliki nilai non-numerik: {token} dan tidak dapat dikonversi ke '{typeof(T).Name}'.");
                }
            }
        }

        if (!foundValidValue)
        {
            throw new InvalidOperationException($"Tidak ditemukan nilai valid untuk properti '{propertyName}'.");
        }

        return maxVal;
    }
}