#if !UNITY_2022_1_OR_NEWER
using System.Collections.Generic;

namespace BugSplatUnity.Runtime.Util.Extensions
{
    public static class DictionaryExtensions
    {
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary == null) return false;
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
        }
    }
}
#endif