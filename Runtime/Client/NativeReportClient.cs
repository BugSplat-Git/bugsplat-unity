using BugSplatUnity.Runtime.Native;
using BugSplatUnity.Runtime.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BugSplatUnity.Runtime.Client
{
	/// <summary>
	/// Routes managed exception and feedback posts through bugsplat-native once it is running, so a
	/// player has exactly one upload path: the same store, presigned-URL uploader and crash ids the
	/// native crash reports use. Managed exceptions become BugSplat structured reports (crash type
	/// 21) with Unity's stack trace split into frames.
	///
	/// The interfaces are the ones the reporter already talks to, and the response is the same
	/// HttpResponseMessage shape the managed uploader returns, so DotNetStandardExceptionReporter
	/// and every caller of the callbacks are unchanged. Minidump files posted by hand still go
	/// through the managed uploader, which the native SDK has no equivalent of.
	/// </summary>
	internal sealed class NativeReportClient : IDotNetStandardExceptionClient, IDotNetStandardFeedbackClient, INativeCrashReportClient
	{
		private readonly IClientSettingsRepository clientSettings;
		private readonly INativeCrashReportClient minidumpClient;
		private readonly ManagedReportFormat format;

		public NativeReportClient(IClientSettingsRepository clientSettings, INativeCrashReportClient minidumpClient, ManagedReportFormat format)
		{
			this.clientSettings = clientSettings;
			this.minidumpClient = minidumpClient;
			this.format = format;
		}

		public Task<HttpResponseMessage> Post(string stackTrace, IReportPostOptions options = null)
		{
			var parsed = UnityStackTraceParser.Parse(null, stackTrace);
			return PostParsed(parsed, options);
		}

		public Task<HttpResponseMessage> Post(Exception ex, IReportPostOptions options = null)
		{
			var parsed = UnityStackTraceParser.Parse(null, ex?.ToString() ?? string.Empty);
			if (ex != null)
			{
				parsed.ExceptionType = ex.GetType().FullName ?? parsed.ExceptionType;
				parsed.Message = ex.Message ?? parsed.Message;
			}
			return PostParsed(parsed, options);
		}

		public Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, IReportPostOptions options = null)
		{
			return minidumpClient.Post(minidumpFileInfo, options);
		}

		public Task<HttpResponseMessage> PostFeedback(string title, string description, IReportPostOptions options = null)
		{
			// Attachments and per-post metadata are resolved on the caller's thread (they read
			// Unity-owned state); the blocking upload runs on the pool.
			var attachments = CollectAttachments(options, out var tempFiles);
			var overrides = Overrides.From(options);
			return Task.Run(() =>
			{
				try
				{
					overrides.Apply();
					var result = NativeCrashReporter.PostFeedback(title, description, attachments);
					return ToResponse(result);
				}
				finally
				{
					overrides.Restore(clientSettings);
					DeleteTempFiles(tempFiles);
				}
			});
		}

		private Task<HttpResponseMessage> PostParsed(ParsedStackTrace parsed, IReportPostOptions options)
		{
			var attachments = CollectAttachments(options, out var tempFiles);
			var overrides = Overrides.From(options);
			var reportFormat = format;
			return Task.Run(() =>
			{
				try
				{
					overrides.Apply();
					var result = NativeCrashReporter.PostStructuredReport(reportFormat, parsed.ExceptionType, parsed.Message, parsed.Frames, attachments);
					return ToResponse(result);
				}
				finally
				{
					overrides.Restore(clientSettings);
					DeleteTempFiles(tempFiles);
				}
			});
		}

		/// <summary>
		/// The instance-level attachments plus this post's extras. In-memory form data (the
		/// screenshot) is spilled to a temp file, because a native report is a folder of files.
		/// </summary>
		private List<string> CollectAttachments(IReportPostOptions options, out List<string> tempFiles)
		{
			var paths = new List<string>();
			tempFiles = new List<string>();

			void Add(FileInfo file)
			{
				if (file == null) return;
				try
				{
					if (file.Exists && !paths.Contains(file.FullName)) paths.Add(file.FullName);
				}
				catch (Exception) { }
			}

			foreach (var file in clientSettings.Attachments) Add(file);
			if (options == null) return paths;

			foreach (var file in options.AdditionalAttachments) Add(file);

			foreach (var param in options.AdditionalFormDataParams)
			{
				if (param?.Content == null) continue;
				try
				{
					var dir = Path.Combine(Path.GetTempPath(), "bugsplat-unity", Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(dir);
					var name = string.IsNullOrEmpty(param.FileName) ? (param.Name ?? "attachment") + ".bin" : param.FileName;
					var path = Path.Combine(dir, name);
					File.WriteAllBytes(path, param.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
					paths.Add(path);
					tempFiles.Add(path);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"BugSplat warning: could not attach {param.Name}: {ex.Message}");
				}
			}

			return paths;
		}

		private static void DeleteTempFiles(List<string> tempFiles)
		{
			foreach (var path in tempFiles)
			{
				try
				{
					File.Delete(path);
					var dir = Path.GetDirectoryName(path);
					if (dir != null && Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
				}
				catch (Exception) { }
			}
		}

		/// <summary>
		/// The native SDK holds one set of first-class properties per process; a per-post override
		/// is applied for the duration of the post and the instance values are put back afterwards,
		/// so a description given to one Post never rides along on a later native crash.
		/// </summary>
		private struct Overrides
		{
			public string User, Email, Key, Notes, Description;
			public Dictionary<string, string> Attributes;

			public static Overrides From(IReportPostOptions options)
			{
				if (options == null) return default;
				return new Overrides
				{
					User = options.User, Email = options.Email, Key = options.Key, Notes = options.Notes, Description = options.Description,
					Attributes = options.AdditionalAttributes != null && options.AdditionalAttributes.Count > 0
						? new Dictionary<string, string>(options.AdditionalAttributes)
						: null,
				};
			}

			public void Apply()
			{
				if (User != null) NativeCrashReporter.SetUser(User);
				if (Email != null) NativeCrashReporter.SetEmail(Email);
				if (Key != null) NativeCrashReporter.SetKey(Key);
				if (Notes != null) NativeCrashReporter.SetNotes(Notes);
				if (Description != null) NativeCrashReporter.SetDescription(Description);
				if (Attributes != null)
				{
					foreach (var kv in Attributes) NativeCrashReporter.SetAttribute(kv.Key, kv.Value);
				}
			}

			public void Restore(IClientSettingsRepository settings)
			{
				if (User != null) NativeCrashReporter.SetUser(settings.User);
				if (Email != null) NativeCrashReporter.SetEmail(settings.Email);
				if (Key != null) NativeCrashReporter.SetKey(settings.Key);
				if (Notes != null) NativeCrashReporter.SetNotes(settings.Notes);
				if (Description != null) NativeCrashReporter.SetDescription(settings.Description);
				if (Attributes != null)
				{
					foreach (var kv in Attributes)
					{
						NativeCrashReporter.SetAttribute(kv.Key, settings.Attributes.TryGetValue(kv.Key, out var kept) ? kept : null);
					}
				}
			}
		}

		/// <summary>
		/// The response the managed uploader would have produced, so callers parsing
		/// BugSplatResponse from the body keep working: crashId and infoUrl come from the commit.
		/// </summary>
		private static HttpResponseMessage ToResponse(NativeUploadResult result)
		{
			HttpStatusCode status;
			string body;

			if (result.Success && result.Pending)
			{
				status = HttpStatusCode.Accepted;
				body = "{\"status\":\"pending\",\"crashId\":0,\"infoUrl\":\"\"}";
			}
			else if (result.Success)
			{
				status = HttpStatusCode.OK;
				body = "{\"status\":\"success\",\"crashId\":" + result.CrashId + ",\"stackKeyId\":" + result.StackKeyId +
					",\"infoUrl\":\"" + Escape(result.InfoUrl) + "\"}";
			}
			else
			{
				status = result.HttpStatus > 0 ? (HttpStatusCode)result.HttpStatus
					: result.Result == BugSplatNative.Result.Rejected ? HttpStatusCode.RequestEntityTooLarge
					: result.Result == BugSplatNative.Result.Cancelled ? HttpStatusCode.NoContent
					: HttpStatusCode.ServiceUnavailable;
				body = "{\"status\":\"failure\",\"crashId\":0,\"infoUrl\":\"\",\"error\":\"" + result.Result + "\"}";
			}

			return new HttpResponseMessage(status)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			};
		}

		private static string Escape(string s)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			var sb = new StringBuilder(s.Length + 8);
			foreach (var c in s)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
						break;
				}
			}
			return sb.ToString();
		}
	}
}
