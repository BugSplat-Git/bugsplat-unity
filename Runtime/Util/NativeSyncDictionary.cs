using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BugSplatUnity.Runtime.Util
{
    /// <summary>
    /// A Dictionary wrapper that mirrors every change to the native crash reporter: <c>onSet</c> runs when a value
    /// is added or changed, and <c>onRemove</c> runs for each key that <see cref="Remove(TKey)"/> or
    /// <see cref="Clear"/> takes out.
    /// </summary>
    /// <remarks>
    /// Thread-safe. Every member takes one lock, and the callbacks run while it is held, so the native reporter
    /// sees changes in the same order the dictionary applied them. A callback may read or modify this dictionary
    /// from its own thread, but must not wait on another thread that does. <see cref="Keys"/>, <see cref="Values"/>
    /// and enumeration return snapshots taken under the lock, so they never throw because another thread (or the
    /// enumerating loop itself) changed the dictionary.
    /// </remarks>
    internal class NativeSyncDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly object _lock = new object();
        private readonly Dictionary<TKey, TValue> _inner = new Dictionary<TKey, TValue>();
        private Action<TKey, TValue> _onSet;
        private Action<TKey> _onRemove;

        public void SetCallback(Action<TKey, TValue> onSet, Action<TKey> onRemove = null)
        {
            lock (_lock)
            {
                _onSet = onSet;
                _onRemove = onRemove;

                foreach (var kvp in _inner)
                {
                    _onSet?.Invoke(kvp.Key, kvp.Value);
                }
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                lock (_lock) return _inner[key];
            }
            set
            {
                lock (_lock)
                {
                    _inner[key] = value;
                    _onSet?.Invoke(key, value);
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (_lock)
            {
                _inner.Add(key, value);
                _onSet?.Invoke(key, value);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public bool TryAdd(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_inner.ContainsKey(key)) return false;
                _inner[key] = value;
                _onSet?.Invoke(key, value);
                return true;
            }
        }

        public bool Remove(TKey key)
        {
            lock (_lock)
            {
                if (!_inner.Remove(key)) return false;
                _onRemove?.Invoke(key);
                return true;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            lock (_lock)
            {
                if (!((ICollection<KeyValuePair<TKey, TValue>>)_inner).Remove(item)) return false;
                _onRemove?.Invoke(item.Key);
                return true;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                var removed = _inner.Keys.ToList();
                _inner.Clear();
                foreach (var key in removed)
                {
                    _onRemove?.Invoke(key);
                }
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                lock (_lock) return _inner.Keys.ToList();
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                lock (_lock) return _inner.Values.ToList();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock) return _inner.Count;
            }
        }

        public bool IsReadOnly => false;

        public bool ContainsKey(TKey key)
        {
            lock (_lock) return _inner.ContainsKey(key);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            lock (_lock) return ((ICollection<KeyValuePair<TKey, TValue>>)_inner).Contains(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_lock) return _inner.TryGetValue(key, out value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (_lock) ((ICollection<KeyValuePair<TKey, TValue>>)_inner).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            lock (_lock) return _inner.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
