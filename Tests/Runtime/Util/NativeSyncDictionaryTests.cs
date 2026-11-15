using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BugSplatUnity.RuntimeTests.Util
{
    public class NativeSyncDictionaryTests
    {
        [Test]
        public void Add_WithCallback_ShouldInvokeCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var received = new List<KeyValuePair<string, string>>();
            dict.SetCallback((k, v) => received.Add(new KeyValuePair<string, string>(k, v)));

            dict.Add("key1", "value1");

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("key1", received[0].Key);
            Assert.AreEqual("value1", received[0].Value);
        }

        [Test]
        public void Indexer_WithCallback_ShouldInvokeCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var received = new List<KeyValuePair<string, string>>();
            dict.SetCallback((k, v) => received.Add(new KeyValuePair<string, string>(k, v)));

            dict["key1"] = "value1";

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("key1", received[0].Key);
            Assert.AreEqual("value1", received[0].Value);
        }

        [Test]
        public void Indexer_Update_ShouldInvokeCallbackWithNewValue()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var received = new List<KeyValuePair<string, string>>();
            dict.SetCallback((k, v) => received.Add(new KeyValuePair<string, string>(k, v)));

            dict["key1"] = "value1";
            dict["key1"] = "value2";

            Assert.AreEqual(2, received.Count);
            Assert.AreEqual("value2", received[1].Value);
            Assert.AreEqual("value2", dict["key1"]);
        }

        [Test]
        public void SetCallback_ShouldReplayExistingEntries()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("key1", "value1");
            dict.Add("key2", "value2");

            var received = new List<KeyValuePair<string, string>>();
            dict.SetCallback((k, v) => received.Add(new KeyValuePair<string, string>(k, v)));

            Assert.AreEqual(2, received.Count);
            Assert.IsTrue(received.Exists(kvp => kvp.Key == "key1" && kvp.Value == "value1"));
            Assert.IsTrue(received.Exists(kvp => kvp.Key == "key2" && kvp.Value == "value2"));
        }

        [Test]
        public void Add_WithoutCallback_ShouldNotThrow()
        {
            var dict = new NativeSyncDictionary<string, string>();

            Assert.DoesNotThrow(() => dict.Add("key1", "value1"));
            Assert.AreEqual("value1", dict["key1"]);
        }

        [Test]
        public void TryAdd_NewKey_ShouldAddAndInvokeCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var received = new List<KeyValuePair<string, string>>();
            dict.SetCallback((k, v) => received.Add(new KeyValuePair<string, string>(k, v)));

            var result = dict.TryAdd("key1", "value1");

            Assert.IsTrue(result);
            Assert.AreEqual(1, received.Count);
        }

        [Test]
        public void TryAdd_ExistingKey_ShouldNotAddOrInvokeCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("key1", "value1");
            var received = new List<KeyValuePair<string, string>>();
            dict.SetCallback((k, v) => received.Add(new KeyValuePair<string, string>(k, v)));

            // Clear replayed entries
            received.Clear();

            var result = dict.TryAdd("key1", "value2");

            Assert.IsFalse(result);
            Assert.AreEqual(0, received.Count);
            Assert.AreEqual("value1", dict["key1"]);
        }

        [Test]
        public void Count_ShouldReturnCorrectCount()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("a", "1");
            dict.Add("b", "2");

            Assert.AreEqual(2, dict.Count);
        }

        [Test]
        public void ContainsKey_ShouldReturnCorrectResult()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("key1", "value1");

            Assert.IsTrue(dict.ContainsKey("key1"));
            Assert.IsFalse(dict.ContainsKey("key2"));
        }

        [Test]
        public void Remove_ShouldRemoveEntry()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("key1", "value1");

            var result = dict.Remove("key1");

            Assert.IsTrue(result);
            Assert.AreEqual(0, dict.Count);
        }

        [Test]
        public void Remove_WithCallback_ShouldInvokeRemoveCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var removed = new List<string>();
            dict.SetCallback((k, v) => { }, k => removed.Add(k));
            dict.Add("key1", "value1");
            dict.Add("key2", "value2");

            var result = dict.Remove("key1");

            Assert.IsTrue(result);
            CollectionAssert.AreEqual(new[] { "key1" }, removed);
            Assert.IsFalse(dict.ContainsKey("key1"));
        }

        [Test]
        public void Remove_MissingKey_ShouldNotInvokeRemoveCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var removed = new List<string>();
            dict.SetCallback((k, v) => { }, k => removed.Add(k));

            Assert.IsFalse(dict.Remove("missing"));
            Assert.IsEmpty(removed);
        }

        [Test]
        public void RemoveKeyValuePair_ShouldInvokeRemoveCallbackOnlyWhenPairMatches()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var removed = new List<string>();
            dict.SetCallback((k, v) => { }, k => removed.Add(k));
            dict.Add("key1", "value1");

            Assert.IsFalse(dict.Remove(new KeyValuePair<string, string>("key1", "other")));
            Assert.IsEmpty(removed);

            Assert.IsTrue(dict.Remove(new KeyValuePair<string, string>("key1", "value1")));
            CollectionAssert.AreEqual(new[] { "key1" }, removed);
        }

        [Test]
        public void Clear_ShouldInvokeRemoveCallbackForEveryKey()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var removed = new List<string>();
            dict.SetCallback((k, v) => { }, k => removed.Add(k));
            dict.Add("a", "1");
            dict.Add("b", "2");
            dict.Add("c", "3");

            dict.Clear();

            Assert.AreEqual(0, dict.Count);
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, removed);
        }

        [Test]
        public void Clear_Empty_ShouldNotInvokeRemoveCallback()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var removed = new List<string>();
            dict.SetCallback((k, v) => { }, k => removed.Add(k));

            dict.Clear();

            Assert.IsEmpty(removed);
        }

        [Test]
        public void RemoveAndClear_WithoutRemoveCallback_ShouldNotThrow()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.SetCallback((k, v) => { });
            dict.Add("a", "1");
            dict.Add("b", "2");

            Assert.DoesNotThrow(() => dict.Remove("a"));
            Assert.DoesNotThrow(() => dict.Clear());
            Assert.AreEqual(0, dict.Count);
        }

        [Test]
        public void RemovingWhileEnumerating_ShouldNotThrow()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("a", "1");
            dict.Add("b", "2");

            Assert.DoesNotThrow(() =>
            {
                foreach (var kvp in dict)
                {
                    dict.Remove(kvp.Key);
                }
            });
            Assert.AreEqual(0, dict.Count);
        }

        [Test]
        public void ConcurrentAccess_ShouldKeepNativeMirrorInSync()
        {
            var dict = new NativeSyncDictionary<string, string>();
            var mirror = new Dictionary<string, string>();
            dict.SetCallback((k, v) => mirror[k] = v, k => mirror.Remove(k));

            const int threadCount = 8;
            const int iterations = 2000;
            var errors = new ConcurrentQueue<Exception>();
            var threads = new List<Thread>();

            for (var t = 0; t < threadCount; t++)
            {
                var id = t;
                threads.Add(new Thread(() =>
                {
                    try
                    {
                        for (var i = 0; i < iterations; i++)
                        {
                            var key = "key" + (i % 16);
                            switch ((i + id) % 5)
                            {
                                case 0: dict[key] = id + ":" + i; break;
                                case 1: dict.TryAdd(key, id + ":" + i); break;
                                case 2: dict.Remove(key); break;
                                case 3: foreach (var kvp in dict) { } break;
                                case 4: if (i % 100 == 4) dict.Clear(); else dict.TryGetValue(key, out _); break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                }));
            }

            threads.ForEach(thread => thread.Start());
            foreach (var thread in threads)
            {
                Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "a worker thread did not finish");
            }

            Assert.IsEmpty(errors, string.Join("\n", errors));
            CollectionAssert.AreEquivalent(mirror, dict.ToList());
        }

        [Test]
        public void Enumeration_ShouldIterateAllEntries()
        {
            var dict = new NativeSyncDictionary<string, string>();
            dict.Add("a", "1");
            dict.Add("b", "2");

            var keys = new List<string>();
            foreach (var kvp in dict)
            {
                keys.Add(kvp.Key);
            }

            Assert.AreEqual(2, keys.Count);
            Assert.Contains("a", keys);
            Assert.Contains("b", keys);
        }
    }
}
