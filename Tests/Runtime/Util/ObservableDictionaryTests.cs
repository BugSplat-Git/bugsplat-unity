using System.Collections.Generic;
using BugSplatUnity.Runtime.Util;
using NUnit.Framework;

namespace BugSplatUnity.RuntimeTests.Util
{
    public class ObservableDictionaryTests
    {
        ObservableDictionary<string, string> sut;
        int changes;

        [SetUp]
        public void SetUp()
        {
            sut = new ObservableDictionary<string, string>();
            changes = 0;
            sut.Changed += () => changes++;
        }

        [Test]
        public void EveryMutation_ShouldRaiseChanged()
        {
            sut["a"] = "1";
            sut.Add("b", "2");
            sut.Add(new KeyValuePair<string, string>("c", "3"));
            sut.Remove("a");
            sut.Remove(new KeyValuePair<string, string>("b", "2"));
            sut.Clear();

            Assert.AreEqual(6, changes);
            Assert.AreEqual(0, sut.Count);
        }

        [Test]
        public void NoOpRemovalsAndClears_ShouldNotRaiseChanged()
        {
            sut.Remove("missing");
            sut.Remove(new KeyValuePair<string, string>("missing", "x"));
            sut.Clear();

            Assert.AreEqual(0, changes);
        }
    }
}
