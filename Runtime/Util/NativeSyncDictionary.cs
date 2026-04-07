using System;
using System.Collections;
using System.Collections.Generic;

namespace BugSplatUnity.Runtime.Util
{
    /// <summary>
    /// A Dictionary wrapper that invokes a callback whenever a value is added or changed.
    /// Used to automatically sync attributes to the native crash reporter.
    /// </summary>
    internal class NativeSyncDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _inner = new Dictionary<TKey, TValue>();
        private Action<TKey, TValue> _onSet;

        public void SetCallback(Action<TKey, TValue> onSet)
        {
            _onSet = onSet;

            // Sync any existing entries
            foreach (var kvp in _inner)
            {
                _onSet?.Invoke(kvp.Key, kvp.Value);
            }
        }

        public TValue this[TKey key]
        {
            get => _inner[key];
            set
            {
                _inner[key] = value;
                _onSet?.Invoke(key, value);
            }
        }

        public void Add(TKey key, TValue value)
        {
            _inner.Add(key, value);
            _onSet?.Invoke(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_inner).Add(item);
            _onSet?.Invoke(item.Key, item.Value);
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (_inner.ContainsKey(key)) return false;
            _inner[key] = value;
            _onSet?.Invoke(key, value);
            return true;
        }

        // Pass-through members
        public ICollection<TKey> Keys => _inner.Keys;
        public ICollection<TValue> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;
        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)_inner).Contains(item);
        public bool Remove(TKey key) => _inner.Remove(key);
        public bool Remove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)_inner).Remove(item);
        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);
        public void Clear() => _inner.Clear();
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)_inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }
}
