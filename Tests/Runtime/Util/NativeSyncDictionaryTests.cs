using BugSplatUnity.Runtime.Util;
using NUnit.Framework;
using System.Collections.Generic;

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
