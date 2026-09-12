using BugSplatUnity.Runtime.Native;
using NUnit.Framework;

namespace BugSplatUnity.RuntimeTests.Native
{
	/// <summary>
	/// The parser is what turns Unity's stack trace text into the frames a structured report
	/// carries. Both shapes Unity produces are pinned here, including the file-less frames IL2CPP
	/// release builds print, so a report never silently loses its stack.
	/// </summary>
	public class UnityStackTraceParserTests
	{
		[Test]
		public void Parse_LogCallbackShape_SplitsFunctionFileAndLine()
		{
			var trace =
				"Crasher.CrashScenarios.ThrowUnhandledManagedException () (at Assets/Samples/BugSplat/5.0.0/my-unity-crasher/Scripts/CrashScenarios.cs:210)\n" +
				"Crasher.CrashScenarios.SampleStackFrame2 (Crasher.CrashScenarios+ManagedScenario scenario) (at Assets/Scripts/CrashScenarios.cs:198)\n" +
				"UnityEngine.Debug:LogException(Exception)\n";

			var parsed = UnityStackTraceParser.Parse("Exception: BugSplat sample: unhandled managed exception", trace);

			Assert.AreEqual("Exception", parsed.ExceptionType);
			Assert.AreEqual("BugSplat sample: unhandled managed exception", parsed.Message);
			Assert.AreEqual(3, parsed.Frames.Count);
			Assert.AreEqual("Crasher.CrashScenarios.ThrowUnhandledManagedException ()", parsed.Frames[0].Function);
			Assert.AreEqual("Assets/Samples/BugSplat/5.0.0/my-unity-crasher/Scripts/CrashScenarios.cs", parsed.Frames[0].File);
			Assert.AreEqual(210, parsed.Frames[0].Line);
			Assert.AreEqual("Crasher.CrashScenarios.SampleStackFrame2 (Crasher.CrashScenarios+ManagedScenario scenario)", parsed.Frames[1].Function);
			Assert.AreEqual(198, parsed.Frames[1].Line);
			Assert.AreEqual("UnityEngine.Debug:LogException(Exception)", parsed.Frames[2].Function);
			Assert.AreEqual(string.Empty, parsed.Frames[2].File);
			Assert.AreEqual(0, parsed.Frames[2].Line);
		}

		[Test]
		public void Parse_ExceptionToStringShape_ReadsHeaderAndMonoFrames()
		{
			var trace =
				"System.NullReferenceException: Object reference not set to an instance of an object\n" +
				"  at Crasher.Player.Update () [0x0001a] in C:\\game\\Assets\\Scripts\\Player.cs:42 \n" +
				"  at Crasher.Menu.Run () [0x00000] in <00000000000000000000000000000000>:0 \n";

			var parsed = UnityStackTraceParser.Parse(null, trace);

			Assert.AreEqual("System.NullReferenceException", parsed.ExceptionType);
			Assert.AreEqual("Object reference not set to an instance of an object", parsed.Message);
			Assert.AreEqual(2, parsed.Frames.Count);
			Assert.AreEqual("Crasher.Player.Update ()", parsed.Frames[0].Function);
			Assert.AreEqual("C:\\game\\Assets\\Scripts\\Player.cs", parsed.Frames[0].File);
			Assert.AreEqual(42, parsed.Frames[0].Line);
			// IL2CPP release: the placeholder file is dropped rather than reported as a path.
			Assert.AreEqual("Crasher.Menu.Run ()", parsed.Frames[1].Function);
			Assert.AreEqual(string.Empty, parsed.Frames[1].File);
			Assert.AreEqual(0, parsed.Frames[1].Line);
		}

		[Test]
		public void Parse_HeaderWithoutRecognizableType_BecomesTheMessage()
		{
			var parsed = UnityStackTraceParser.Parse(null, "something odd happened\nCrasher.Foo.Bar () (at Assets/Foo.cs:1)");

			Assert.AreEqual("Exception", parsed.ExceptionType);
			Assert.AreEqual("something odd happened", parsed.Message);
			Assert.AreEqual(1, parsed.Frames.Count);
		}

		[Test]
		public void Parse_RethrowAndInnerExceptionMarkers_AreKeptAsFrames()
		{
			var trace =
				"System.AggregateException: One or more errors occurred.\n" +
				" ---> System.InvalidOperationException: inner\n" +
				"  at A.B () [0x00000] in <f>:0 \n" +
				"   --- End of inner exception stack trace ---\n" +
				"  at C.D () [0x00000] in <f>:0 \n";

			var parsed = UnityStackTraceParser.Parse(null, trace);

			Assert.AreEqual("System.AggregateException", parsed.ExceptionType);
			CollectionAssert.AreEqual(
				new[] { "---> System.InvalidOperationException: inner", "A.B ()", "--- End of inner exception stack trace ---", "C.D ()" },
				System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(parsed.Frames, f => f.Function)));
		}

		[Test]
		public void Parse_EmptyTrace_ProducesNoFrames()
		{
			var parsed = UnityStackTraceParser.Parse("Exception: boom", string.Empty);

			Assert.AreEqual("Exception", parsed.ExceptionType);
			Assert.AreEqual("boom", parsed.Message);
			Assert.IsEmpty(parsed.Frames);
		}
	}
}
