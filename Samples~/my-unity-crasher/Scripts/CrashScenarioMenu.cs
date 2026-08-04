using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BugSplatUnity.Runtime.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BugSplat = BugSplatUnity.BugSplat;

namespace Crasher
{
	/// <summary>
	/// A scrollable menu of crash scenarios grouped by the BugSplat mechanism expected to capture
	/// each one. Built entirely in code — the sample scene only carries this component, so adding
	/// a scenario never means editing scene YAML (where a missing onClick call state has silently
	/// broken buttons before).
	/// </summary>
	public class CrashScenarioMenu : MonoBehaviour, ICrashScenarioHost
	{
		const float PanelWidth = 860f;
		const float PanelHeight = 640f;
		const float RowHeight = 62f;

		static readonly Color BugSplatRed = new Color32(244, 102, 137, 255);
		static readonly Color BugSplatGreen = new Color32(74, 235, 195, 255);
		static readonly Color BugSplatBlue = new Color32(58, 163, 255, 255);
		static readonly Color Grey = new Color32(140, 140, 150, 255);
		static readonly Color DarkGrey = new Color32(90, 90, 98, 255);
		static readonly Color Ink = new Color(0.16f, 0.16f, 0.16f, 1f);

		readonly ConcurrentQueue<Action> mainThreadWork = new ConcurrentQueue<Action>();

		GameObject panel;
		TextMeshProUGUI statusText;
		BugSplat bugsplat;

		public BugSplat BugSplat => bugsplat;

		void Awake()
		{
			// Unobserved Task exceptions surface on the finalizer thread, where Unity's log
			// callback never fires — marshal to the main thread so they can be reported. This is
			// also the documented workaround for background-thread exceptions generally.
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

			BuildUI();
			panel.SetActive(false);
		}

		void OnDestroy()
		{
			TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
		}

		void Start()
		{
			var manager = FindAnyObjectByType<BugSplatManager>();
			bugsplat = manager != null ? manager.BugSplat : null;
			RefreshStatus();
		}

		void Update()
		{
			while (mainThreadWork.TryDequeue(out var work))
			{
				work();
			}
		}

		public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

		public void OnMainThread(Action action) => mainThreadWork.Enqueue(action);

		void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
		{
			args.SetObserved();
			var exception = args.Exception;
			OnMainThread(() => Debug.LogException(exception));
		}

		public void Show()
		{
			RefreshStatus();
			panel.SetActive(true);
		}

		public void Hide() => panel.SetActive(false);

		void RunScenario(CrashScenario scenario)
		{
			if (!scenario.RunsInEditor && Application.isEditor)
			{
				Debug.LogWarning(
					$"BugSplat sample: '{scenario.Name}' only runs in a built Windows player. " +
					"BugSplat.dll is excluded from the editor, so there is no native reporter here, " +
					"and the crash would take the editor down with any unsaved work.");
				return;
			}

			if (bugsplat == null)
			{
				Debug.LogError("[BugSplat] BugSplatManager not found in scene. Cannot run crash scenarios.");
				return;
			}

			Debug.Log($"BugSplat sample: running '{scenario.Name}' — {scenario.Expected}");
			scenario.Run(this);
		}

		void RefreshStatus()
		{
			if (statusText == null) return;

			if (bugsplat == null)
			{
				statusText.text = "BugSplat is not initialized in this scene.";
				statusText.color = BugSplatRed;
				return;
			}

			var backend = Application.isEditor ? "Editor" : "Player";
			if (bugsplat.WindowsWerEnabled)
			{
				statusText.text = $"{backend} — Windows Error Reporting is ARMED. Fail-fast scenarios will report.";
				statusText.color = new Color(0.1f, 0.5f, 0.3f, 1f);
			}
			else
			{
				statusText.text =
					$"{backend} — Windows Error Reporting is NOT ARMED, so fail-fast scenarios will not " +
					"report. Use BugSplat > Windows > Register WER Handler on a built player.";
				statusText.color = BugSplatRed;
			}
		}

		static Color ColorFor(CapturePath path)
		{
			switch (path)
			{
				case CapturePath.NativeHandler: return BugSplatRed;
				case CapturePath.WindowsErrorReporting: return BugSplatBlue;
				case CapturePath.ManagedHandler: return BugSplatGreen;
				case CapturePath.ManualPost: return Grey;
				case CapturePath.HangWatchdog: return Grey;
				default: return DarkGrey;
			}
		}

		static string LabelFor(CapturePath path)
		{
			switch (path)
			{
				case CapturePath.NativeHandler: return "NATIVE";
				case CapturePath.WindowsErrorReporting: return "WER";
				case CapturePath.ManagedHandler: return "MANAGED";
				case CapturePath.ManualPost: return "POST";
				case CapturePath.HangWatchdog: return "HANG";
				default: return "NONE";
			}
		}

		void BuildUI()
		{
			var canvasGo = new GameObject("CrashScenarioCanvas");
			canvasGo.transform.SetParent(transform, false);
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			// Below FeedbackPopup's 100 so its dialog still wins.
			canvas.sortingOrder = 90;
			canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasGo.AddComponent<GraphicRaycaster>();

			BuildLauncherButton(canvasGo.transform);

			panel = CreateElement("ScenarioPanel", canvasGo.transform);
			var panelRect = panel.GetComponent<RectTransform>();
			panelRect.anchorMin = Vector2.zero;
			panelRect.anchorMax = Vector2.one;
			panelRect.sizeDelta = Vector2.zero;
			panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);

			var box = CreateElement("PanelBox", panel.transform);
			var boxRect = box.GetComponent<RectTransform>();
			boxRect.anchorMin = new Vector2(0.5f, 0.5f);
			boxRect.anchorMax = new Vector2(0.5f, 0.5f);
			boxRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
			box.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);

			const float pad = 20f;
			var cursor = -pad;

			var header = CreateTextElement("Header", box.transform, "Crash Scenarios", 22, FontStyles.Bold);
			SetTopAnchored(header, cursor, 30, pad);
			cursor -= 30 + 6;

			var legend = CreateTextElement(
				"Legend", box.transform,
				"NATIVE = BugSplat crash handler   WER = Windows Error Reporting   " +
				"MANAGED = .NET handler   POST = explicit Post   HANG = watchdog   NONE = not captured by the SDK",
				12, FontStyles.Normal);
			legend.GetComponent<TextMeshProUGUI>().color = new Color(0.4f, 0.4f, 0.4f, 1f);
			SetTopAnchored(legend, cursor, 18, pad);
			cursor -= 18 + 4;

			var status = CreateTextElement("Status", box.transform, "", 12, FontStyles.Bold);
			statusText = status.GetComponent<TextMeshProUGUI>();
			SetTopAnchored(status, cursor, 32, pad);
			cursor -= 32 + 8;

			if (Application.isEditor)
			{
				var banner = CreateTextElement(
					"EditorBanner", box.transform,
					"Editor: native and fail-fast scenarios are disabled. BugSplat.dll is excluded from " +
					"the editor, so they would produce no report and would crash the editor. Build a " +
					"Windows player to run them.",
					12, FontStyles.Bold);
				banner.GetComponent<TextMeshProUGUI>().color = BugSplatRed;
				SetTopAnchored(banner, cursor, 34, pad);
				cursor -= 34 + 8;
			}

			BuildScenarioList(box.transform, cursor, pad);

			var closeButton = CreateButton("CloseBtn", box.transform, "Close", DarkGrey, Hide);
			var closeRect = closeButton.GetComponent<RectTransform>();
			closeRect.anchorMin = new Vector2(1, 1);
			closeRect.anchorMax = new Vector2(1, 1);
			closeRect.pivot = new Vector2(1, 1);
			closeRect.anchoredPosition = new Vector2(-pad, -pad);
			closeRect.sizeDelta = new Vector2(90, 30);
		}

		void BuildLauncherButton(Transform parent)
		{
			// Bottom-right is free: Button_Feedback is anchored top-right and the existing crash
			// button grid is vertically centered.
			var launcher = CreateButton("ScenarioMenuBtn", parent, "Crash Scenarios", BugSplatBlue, Show);
			var rect = launcher.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(1, 0);
			rect.anchorMax = new Vector2(1, 0);
			rect.pivot = new Vector2(1, 0);
			rect.anchoredPosition = new Vector2(-40, 40);
			rect.sizeDelta = new Vector2(260, 56);
		}

		void BuildScenarioList(Transform boxTransform, float cursor, float pad)
		{
			var viewport = CreateElement("Viewport", boxTransform);
			var viewportRect = viewport.GetComponent<RectTransform>();
			viewportRect.anchorMin = new Vector2(0, 0);
			viewportRect.anchorMax = new Vector2(1, 1);
			viewportRect.pivot = new Vector2(0.5f, 1);
			viewportRect.offsetMin = new Vector2(pad, pad);
			viewportRect.offsetMax = new Vector2(-pad, cursor);
			viewport.AddComponent<RectMask2D>();
			// A raycast target is required or the wheel and drag never reach the ScrollRect.
			var viewportImage = viewport.AddComponent<Image>();
			viewportImage.color = new Color(1, 1, 1, 0.01f);

			var content = CreateElement("Content", viewport.transform);
			var contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0, 1);
			contentRect.anchorMax = new Vector2(1, 1);
			contentRect.pivot = new Vector2(0.5f, 1);
			contentRect.sizeDelta = new Vector2(0, 0);

			var layout = content.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 6;
			layout.childForceExpandHeight = false;
			layout.childForceExpandWidth = true;
			layout.childControlHeight = true;
			layout.childControlWidth = true;

			var fitter = content.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			var scrollRect = viewport.AddComponent<ScrollRect>();
			scrollRect.content = contentRect;
			scrollRect.viewport = viewportRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Elastic;
			scrollRect.scrollSensitivity = 30;

			foreach (var scenario in CrashScenarios.All)
			{
				BuildScenarioRow(content.transform, scenario);
			}
		}

		void BuildScenarioRow(Transform parent, CrashScenario scenario)
		{
			var row = CreateElement($"Row_{scenario.Name}", parent);
			var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
			rowLayout.spacing = 10;
			rowLayout.childForceExpandHeight = true;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childControlHeight = true;
			rowLayout.childControlWidth = true;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;

			var rowElement = row.AddComponent<LayoutElement>();
			rowElement.preferredHeight = RowHeight;
			rowElement.flexibleWidth = 1;

			var pill = CreateElement("Pill", row.transform);
			pill.AddComponent<Image>().color = ColorFor(scenario.Path);
			var pillElement = pill.AddComponent<LayoutElement>();
			pillElement.preferredWidth = 84;
			pillElement.flexibleWidth = 0;

			var pillText = CreateTextElement("Text", pill.transform, LabelFor(scenario.Path), 12, FontStyles.Bold);
			var pillTextRect = pillText.GetComponent<RectTransform>();
			pillTextRect.anchorMin = Vector2.zero;
			pillTextRect.anchorMax = Vector2.one;
			pillTextRect.sizeDelta = Vector2.zero;
			var pillTmp = pillText.GetComponent<TextMeshProUGUI>();
			pillTmp.color = Color.white;
			pillTmp.alignment = TextAlignmentOptions.Center;

			var disabled = !scenario.RunsInEditor && Application.isEditor;

			var buttonLabel = scenario.Terminates ? $"{scenario.Name}  (terminates)" : scenario.Name;
			var button = CreateButton(
				"RunBtn", row.transform, buttonLabel,
				disabled ? Grey : ColorFor(scenario.Path),
				() => RunScenario(scenario));
			var buttonElement = button.AddComponent<LayoutElement>();
			buttonElement.preferredWidth = 320;
			buttonElement.flexibleWidth = 0;
			button.GetComponent<Button>().interactable = !disabled;
			button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13;

			var expected = CreateTextElement("Expected", row.transform, scenario.Expected, 12, FontStyles.Normal);
			var expectedTmp = expected.GetComponent<TextMeshProUGUI>();
			expectedTmp.color = disabled ? new Color(0.55f, 0.55f, 0.55f, 1f) : Ink;
			expectedTmp.alignment = TextAlignmentOptions.MidlineLeft;
			var expectedElement = expected.AddComponent<LayoutElement>();
			expectedElement.flexibleWidth = 1;
		}

		GameObject CreateElement(string name, Transform parent)
		{
			var go = new GameObject(name);
			go.AddComponent<RectTransform>();
			go.transform.SetParent(parent, false);
			return go;
		}

		GameObject CreateTextElement(string name, Transform parent, string text, float fontSize, FontStyles style)
		{
			var go = CreateElement(name, parent);
			var tmp = go.AddComponent<TextMeshProUGUI>();
			tmp.text = text;
			tmp.fontSize = fontSize;
			tmp.fontStyle = style;
			tmp.color = Ink;
			return go;
		}

		void SetTopAnchored(GameObject go, float y, float height, float horizontalPad)
		{
			var rect = go.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0, 1);
			rect.anchorMax = new Vector2(1, 1);
			rect.pivot = new Vector2(0.5f, 1);
			rect.anchoredPosition = new Vector2(0, y);
			rect.sizeDelta = new Vector2(-horizontalPad * 2, height);
		}

		GameObject CreateButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
		{
			var go = CreateElement(name, parent);
			var image = go.AddComponent<Image>();
			image.color = color;

			var button = go.AddComponent<Button>();
			button.targetGraphic = image;
			button.onClick.AddListener(onClick);

			var textGo = CreateTextElement("Text", go.transform, label, 15, FontStyles.Bold);
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.sizeDelta = Vector2.zero;
			var tmp = textGo.GetComponent<TextMeshProUGUI>();
			tmp.color = Color.white;
			tmp.alignment = TextAlignmentOptions.Center;

			return go;
		}
	}
}
