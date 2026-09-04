using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BugSplat = BugSplatUnity.BugSplat;

namespace Crasher
{
	/// <summary>
	/// The sample's main menu: every crash scenario for the current platform, grouped by the
	/// BugSplat mechanism expected to capture it. The UI is built entirely in code — the scene
	/// carries only this component and its two button sprites — so adding a scenario never means
	/// editing scene YAML (where a missing onClick call state has silently broken buttons before).
	/// </summary>
	public class CrashScenarioMenu : MonoBehaviour, ICrashScenarioHost
	{
		// The scene's canvas scales against 1920x1080 matching height. This canvas has to use the
		// same reference or its contents render at a different scale than the rest of the sample.
		static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

		// The band above the panel stays clear so the spinning BugSplat cube remains visible.
		// The cube is the sample's liveness indicator: after a managed scenario, the cube still
		// turning is the proof the player survived.
		const float PanelHeight = 680f;
		const float PanelMarginX = 60f;
		const float PanelMarginBottom = 24f;
		const float RowHeight = 54f;
		const float RowButtonWidth = 430f;

		static readonly Color BugSplatRed = new Color32(244, 102, 137, 255);
		static readonly Color BugSplatGreen = new Color32(74, 235, 195, 255);
		static readonly Color BugSplatBlue = new Color32(58, 163, 255, 255);
		static readonly Color BugSplatPurple = new Color32(186, 132, 224, 255);
		static readonly Color Amber = new Color32(255, 177, 61, 255);
		static readonly Color Grey = new Color32(140, 140, 150, 255);

		// A translucent navy close to the scene's background, so the panel reads as part of the
		// sample rather than a floating grey dialog.
		static readonly Color PanelNavy = new Color(0.055f, 0.09f, 0.16f, 0.93f);
		static readonly Color SubtitleGrey = new Color32(147, 163, 184, 255);
		static readonly Color BodyText = new Color(0.86f, 0.9f, 0.95f, 1f);

		// The sample's buttons label themselves in near-black rather than white.
		static readonly Color Ink = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);

		[Header("Button skin — assign the sample's UI sprites so this menu matches the scene")]
		[SerializeField] Sprite buttonSprite;
		[SerializeField] Sprite buttonPressedSprite;

		/// <summary>
		/// Rows are built in Awake; whether each one can run depends on the BugSplat client and is
		/// applied in Start alongside the status line. Keep what each row needs to restyle itself
		/// so availability can be applied afterwards.
		/// </summary>
		sealed class Row
		{
			public CrashScenario Scenario;
			public Color Accent;
			public Button Button;
			public Image Image;
			public TextMeshProUGUI Label;
			public TextMeshProUGUI Expected;
		}

		readonly List<Row> rows = new List<Row>();

		TextMeshProUGUI statusText;
		BugSplat bugsplat;

		public BugSplat BugSplat => bugsplat;

		void Awake()
		{
			BuildUI();
		}

		void Start()
		{
			bugsplat = BugSplat.IsInitialized ? BugSplat.Instance : null;
			RefreshStatus();
			ApplyAvailability();
		}

		public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

		public void ShowFeedback()
		{
			var popup = FindAnyObjectByType<FeedbackPopup>(FindObjectsInactive.Include);
			if (popup != null)
			{
				popup.Show();
			}
			else
			{
				Debug.LogError("[BugSplat] FeedbackPopup not found in scene.");
			}
		}

		void RunScenario(CrashScenario scenario)
		{
			// The button is already non-interactable in these cases; this is the backstop for a
			// scenario invoked before Start has applied availability.
			var reason = BlockedReason(scenario);
			if (reason != null)
			{
				Debug.LogWarning($"BugSplat sample: '{scenario.Name}' cannot run. {reason}");
				return;
			}

			if (bugsplat == null)
			{
				Debug.LogError("[BugSplat] BugSplat is not initialized, so nothing can be reported. Select or create a BugSplat Options asset in Edit > Project Settings > BugSplat.");
				return;
			}

			Debug.Log($"BugSplat sample: running '{scenario.Name}' — {scenario.Expected}");
			scenario.Run(this);
		}

		static string PlatformName
		{
			get
			{
#if UNITY_STANDALONE_WIN
				return "Windows";
#elif UNITY_STANDALONE_OSX
				return "macOS";
#elif UNITY_STANDALONE_LINUX
				return "Linux";
#elif UNITY_IOS
				return "iOS";
#elif UNITY_ANDROID
				return "Android";
#elif UNITY_WEBGL
				return "WebGL";
#else
				return Application.platform.ToString();
#endif
			}
		}

		void RefreshStatus()
		{
			if (statusText == null) return;

			if (bugsplat == null)
			{
				statusText.text = "BugSplat is not initialized — nothing will be reported. Select an options asset in Edit > Project Settings > BugSplat.";
				statusText.color = BugSplatRed;
				return;
			}

#if UNITY_EDITOR
			statusText.text =
				$"Editor ({PlatformName} build target) — managed and feedback scenarios run here. " +
				"Native, fail-fast, and hang scenarios are disabled: build a player to run them.";
			statusText.color = Amber;
#elif UNITY_STANDALONE_WIN
			if (bugsplat.WindowsWerEnabled)
			{
				statusText.text = "Windows player — WER is ARMED. Fail-fast scenarios will report.";
				statusText.color = BugSplatGreen;
			}
			else
			{
				statusText.text =
					"Windows player — WER is NOT ARMED, so fail-fast scenarios will not report. " +
					"Use BugSplat > Windows > Register WER Handler in the editor.";
				statusText.color = BugSplatRed;
			}
#elif UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_ANDROID
			statusText.text = $"{PlatformName} player — native crash reporting is active.";
			statusText.color = BugSplatGreen;
#else
			statusText.text =
				$"{PlatformName} — native crash reporting is not yet supported on this platform; " +
				"managed scenarios only.";
			statusText.color = Amber;
#endif
		}

		static Color AccentFor(string groupTitle)
		{
			switch (groupTitle)
			{
				case "MANAGED": return BugSplatGreen;
				case "NATIVE": return BugSplatRed;
				case "FAIL-FAST": return BugSplatBlue;
				case "HANG": return Amber;
				case "FEEDBACK": return BugSplatPurple;
				default: return Grey;
			}
		}

		void BuildUI()
		{
			var canvasGo = new GameObject("CrashScenarioCanvas");
			canvasGo.transform.SetParent(transform, false);
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			// Below FeedbackPopup's 100 so its dialog draws on top.
			canvas.sortingOrder = 90;

			var scaler = canvasGo.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = ReferenceResolution;
			scaler.matchWidthOrHeight = 1f;

			canvasGo.AddComponent<GraphicRaycaster>();

			var panel = CreateElement("Panel", canvasGo.transform);
			var panelRect = panel.GetComponent<RectTransform>();
			panelRect.anchorMin = new Vector2(0, 0);
			panelRect.anchorMax = new Vector2(1, 0);
			panelRect.pivot = new Vector2(0.5f, 0);
			panelRect.anchoredPosition = new Vector2(0, PanelMarginBottom);
			panelRect.sizeDelta = new Vector2(-PanelMarginX * 2, PanelHeight);
			panel.AddComponent<Image>().color = PanelNavy;

			var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
			panelLayout.padding = new RectOffset(28, 28, 20, 20);
			panelLayout.spacing = 8;
			panelLayout.childForceExpandWidth = true;
			panelLayout.childForceExpandHeight = false;
			panelLayout.childControlWidth = true;
			panelLayout.childControlHeight = true;

			var title = CreateTextElement("Title", panel.transform, "Crash Scenarios", 30, FontStyles.Bold, Color.white);
			title.AddComponent<LayoutElement>().preferredHeight = 40;

			var status = CreateTextElement("Status", panel.transform, "", 17, FontStyles.Bold, Color.white);
			statusText = status.GetComponent<TextMeshProUGUI>();
			status.AddComponent<LayoutElement>().preferredHeight = 26;

			BuildScenarioList(panel.transform);
		}

		void BuildScenarioList(Transform parent)
		{
			var viewport = CreateElement("Viewport", parent);
			viewport.AddComponent<LayoutElement>().flexibleHeight = 1;
			viewport.AddComponent<RectMask2D>();
			// A raycast target is required or the wheel and drag never reach the ScrollRect.
			viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);

			var content = CreateElement("Content", viewport.transform);
			var contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0, 1);
			contentRect.anchorMax = new Vector2(1, 1);
			contentRect.pivot = new Vector2(0.5f, 1);
			contentRect.sizeDelta = Vector2.zero;

			var layout = content.AddComponent<VerticalLayoutGroup>();
			layout.spacing = 8;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;
			layout.childControlWidth = true;
			layout.childControlHeight = true;

			var fitter = content.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			var scrollRect = viewport.AddComponent<ScrollRect>();
			scrollRect.content = contentRect;
			scrollRect.viewport = viewport.GetComponent<RectTransform>();
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.scrollSensitivity = 30;

			foreach (var group in CrashScenarios.Groups)
			{
				BuildSectionHeader(content.transform, group);
				foreach (var scenario in group.Scenarios)
				{
					BuildScenarioRow(content.transform, group, scenario);
				}
			}
		}

		void BuildSectionHeader(Transform parent, ScenarioGroup group)
		{
			var accent = ColorUtility.ToHtmlStringRGB(AccentFor(group.Title));
			var subtitle = ColorUtility.ToHtmlStringRGB(SubtitleGrey);
			var header = CreateTextElement(
				$"Section_{group.Title}", parent,
				$"<b><color=#{accent}>{group.Title}</color></b>  <color=#{subtitle}>{group.Subtitle}</color>",
				18, FontStyles.Normal, Color.white);
			header.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.BottomLeft;
			// Taller than the text so each section gets breathing room above its name.
			header.AddComponent<LayoutElement>().preferredHeight = 46;
		}

		void BuildScenarioRow(Transform parent, ScenarioGroup group, CrashScenario scenario)
		{
			var row = CreateElement($"Row_{scenario.Name}", parent);
			var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
			rowLayout.spacing = 14;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = true;
			rowLayout.childControlWidth = true;
			rowLayout.childControlHeight = true;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;
			row.AddComponent<LayoutElement>().preferredHeight = RowHeight;

			var accent = AccentFor(group.Title);
			var color = scenario.KnownGap ? Grey : accent;

			var button = CreateButton("RunBtn", row.transform, scenario.Name, color, () => RunScenario(scenario), 18);
			// minWidth, not just preferredWidth: the description text's preferred width competes
			// for space, and losing that negotiation would give every row a different button width.
			// Minimums are allocated first, so this pins the buttons into an even column.
			var buttonElement = button.AddComponent<LayoutElement>();
			buttonElement.minWidth = RowButtonWidth;
			buttonElement.preferredWidth = RowButtonWidth;
			buttonElement.flexibleWidth = 0;

			var expected = CreateTextElement(
				"Expected", row.transform, scenario.Expected, 16, FontStyles.Normal, BodyText);
			expected.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
			// Zero out the text's own preferred width so it takes exactly the space the button
			// leaves, instead of bargaining for its unwrapped line length.
			var expectedElement = expected.AddComponent<LayoutElement>();
			expectedElement.minWidth = 0;
			expectedElement.preferredWidth = 0;
			expectedElement.flexibleWidth = 1;

			rows.Add(new Row
			{
				Scenario = scenario,
				Accent = color,
				Button = button.GetComponent<Button>(),
				Image = button.GetComponent<Image>(),
				Label = button.GetComponentInChildren<TextMeshProUGUI>(),
				Expected = expected.GetComponent<TextMeshProUGUI>()
			});
		}

		/// <summary>
		/// Why a scenario cannot run right now, or null if it can. The reason replaces the row's
		/// description: "this button is grey" is not useful on its own, but "register the WER
		/// handler" is.
		/// </summary>
		string BlockedReason(CrashScenario scenario)
		{
			if (!scenario.RunsInEditor && Application.isEditor)
			{
				return "Built player only — there is no native reporter in the editor.";
			}

			if (scenario.RequiresWer && bugsplat != null && !bugsplat.WindowsWerEnabled)
			{
				return
					"Needs the WER handler registered, or this terminates the player and reports " +
					"nothing. Use BugSplat > Windows > Register WER Handler.";
			}

			return null;
		}

		void ApplyAvailability()
		{
			foreach (var row in rows)
			{
				var reason = BlockedReason(row.Scenario);
				var blocked = reason != null;
				var color = blocked ? Grey : row.Accent;

				row.Button.interactable = !blocked;
				row.Image.color = color;
				row.Label.color = TextOn(color);
				row.Expected.text = reason ?? row.Scenario.Expected;
				row.Expected.color = blocked ? SubtitleGrey : BodyText;
			}
		}

		GameObject CreateElement(string name, Transform parent)
		{
			var go = new GameObject(name);
			go.AddComponent<RectTransform>();
			go.transform.SetParent(parent, false);
			return go;
		}

		GameObject CreateTextElement(string name, Transform parent, string text, float fontSize, FontStyles style, Color color)
		{
			var go = CreateElement(name, parent);
			var tmp = go.AddComponent<TextMeshProUGUI>();
			tmp.text = text;
			tmp.fontSize = fontSize;
			tmp.fontStyle = style;
			tmp.color = color;
			return go;
		}

		/// <summary>
		/// The sample labels its bright brand colours in near-black. The grey used for disabled
		/// rows and the known-gap row is too dark for that to stay legible, so pick per colour
		/// rather than hard-coding either.
		/// </summary>
		static Color TextOn(Color background)
		{
			var luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
			return luminance > 0.5f ? Ink : Color.white;
		}

		GameObject CreateButton(string name, Transform parent, string label, Color color,
			UnityEngine.Events.UnityAction onClick, float fontSize)
		{
			var go = CreateElement(name, parent);
			var image = go.AddComponent<Image>();
			image.color = color;
			image.sprite = buttonSprite;

			var button = go.AddComponent<Button>();
			button.targetGraphic = image;
			button.onClick.AddListener(onClick);

			// Sprite swap is how the sample's own buttons show a press. It needs both sprites, so
			// fall back to the default colour tint when the skin has not been assigned.
			if (buttonSprite != null && buttonPressedSprite != null)
			{
				button.transition = Selectable.Transition.SpriteSwap;
				var spriteState = button.spriteState;
				spriteState.pressedSprite = buttonPressedSprite;
				button.spriteState = spriteState;
			}

			var textGo = CreateTextElement("Text", go.transform, label, fontSize, FontStyles.Bold, TextOn(color));
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.sizeDelta = Vector2.zero;
			var tmp = textGo.GetComponent<TextMeshProUGUI>();
			tmp.alignment = TextAlignmentOptions.Center;
			tmp.margin = new Vector4(8, 0, 8, 0);

			return go;
		}
	}
}
