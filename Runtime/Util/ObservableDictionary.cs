using System;
using System.Collections;
using System.Collections.Generic;

namespace BugSplatUnity.Runtime.Util
{
    internal class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> inner = new Dictionary<TKey, TValue>();

        public event Action Changed;

        public TValue this[TKey key]
        {
            get => inner[key];
            set
            {
                inner[key] = value;
                Changed?.Invoke();
            }
        }

        public void Add(TKey key, TValue value)
        {
            inner.Add(key, value);
            Changed?.Invoke();
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public bool Remove(TKey key)
        {
            var removed = inner.Remove(key);
            if (removed)
            {
                Changed?.Invoke();
            }
            return removed;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var removed = ((ICollection<KeyValuePair<TKey, TValue>>)inner).Remove(item);
            if (removed)
            {
                Changed?.Invoke();
            }
            return removed;
        }

        public void Clear()
        {
            if (inner.Count == 0)
            {
                return;
            }
            inner.Clear();
            Changed?.Invoke();
        }

        public ICollection<TKey> Keys => inner.Keys;
        public ICollection<TValue> Values => inner.Values;
        public int Count => inner.Count;
        public bool IsReadOnly => false;
        public bool ContainsKey(TKey key) => inner.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)inner).Contains(item);
        public bool TryGetValue(TKey key, out TValue value) => inner.TryGetValue(key, out value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => inner.GetEnumerator();
    }
}
