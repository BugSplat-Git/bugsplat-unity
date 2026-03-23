using System.IO;
using System.Net.Http;
using BugSplatUnity;
using BugSplatUnity.Runtime.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crasher
{
    public class FeedbackPopup : MonoBehaviour
    {
        private GameObject popupPanel;
        private TMP_InputField titleInput;
        private TMP_InputField descriptionInput;
        private Toggle includeLogsToggle;

        void Awake()
        {
            BuildUI();
            popupPanel.SetActive(false);
        }

        public void Show()
        {
            titleInput.text = "";
            descriptionInput.text = "";
            includeLogsToggle.isOn = true;
            popupPanel.SetActive(true);
        }

        private void OnSubmit()
        {
            var manager = FindFirstObjectByType<BugSplatManager>();
            if (manager == null || manager.BugSplat == null)
            {
                Debug.LogError("[BugSplat] BugSplatManager not found in scene. Feedback cannot be submitted.");
                return;
            }

            var bugsplat = manager.BugSplat;
            var title = string.IsNullOrEmpty(titleInput.text) ? "User Feedback" : titleInput.text;
            var description = descriptionInput.text;

            if (includeLogsToggle.isOn)
            {
                var logPath = Application.consoleLogPath;
                if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                {
                    var options = new ReportPostOptions();
                    options.AdditionalAttachments.Add(new FileInfo(logPath));
                    StartCoroutine(bugsplat.PostFeedback(title, description, options, FeedbackCallback));
                    popupPanel.SetActive(false);
                    return;
                }
            }

            StartCoroutine(bugsplat.PostFeedback(title, description, callback: FeedbackCallback));
            popupPanel.SetActive(false);
        }

        private void OnCancel()
        {
            popupPanel.SetActive(false);
        }

        private void FeedbackCallback(HttpResponseMessage response)
        {
            if (response == null)
            {
                Debug.LogError("[BugSplat] Feedback submission failed");
                return;
            }

            if (response.IsSuccessStatusCode)
            {
                Debug.Log("[BugSplat] Feedback submitted successfully");
            }
            else
            {
                Debug.LogError($"[BugSplat] Feedback submission failed: {response.StatusCode}");
            }
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("FeedbackCanvas");
            canvasGo.transform.SetParent(this.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dimmed background overlay
            popupPanel = new GameObject("PopupPanel");
            popupPanel.transform.SetParent(canvasGo.transform, false);
            var panelRect = popupPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            var panelImage = popupPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.5f);

            // Popup box
            var pad = 20f;
            var box = CreateElement("PopupBox", popupPanel.transform);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(420, 370);
            var boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            // All elements anchored to top-left/top-right and positioned downward
            var cursor = -pad;

            // Header
            var header = CreateTextElement("Header", box.transform, "Leave Feedback", 22, FontStyles.Bold);
            SetTopAnchored(header, cursor, 32, pad);
            cursor -= 32 + 16;

            // Title label + input
            var titleLabel = CreateTextElement("TitleLabel", box.transform, "Title", 14, FontStyles.Bold);
            titleLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.16f, 0.16f, 0.16f, 1f);
            SetTopAnchored(titleLabel, cursor, 20, pad);
            cursor -= 20 + 4;

            titleInput = CreateInputField("TitleInput", box.transform, "Enter a title...", 36);
            SetTopAnchored(titleInput.gameObject, cursor, 36, pad);
            cursor -= 36 + 14;

            // Description label + input
            var descLabel = CreateTextElement("DescLabel", box.transform, "Description", 14, FontStyles.Bold);
            descLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.16f, 0.16f, 0.16f, 1f);
            SetTopAnchored(descLabel, cursor, 20, pad);
            cursor -= 20 + 4;

            descriptionInput = CreateInputField("DescInput", box.transform, "Describe your feedback...", 80);
            SetTopAnchored(descriptionInput.gameObject, cursor, 80, pad);
            descriptionInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            cursor -= 80 + 12;

            // Include logs toggle
            var toggleGo = CreateElement("LogsToggle", box.transform);
            SetTopAnchored(toggleGo, cursor, 24, pad);

            includeLogsToggle = toggleGo.AddComponent<Toggle>();

            var blue = new Color(0.16f, 0.64f, 0.93f, 1f);

            // Outer border (always visible)
            var toggleBg = CreateElement("Background", toggleGo.transform);
            var toggleBgRect = toggleBg.GetComponent<RectTransform>();
            toggleBgRect.anchorMin = new Vector2(0, 0.5f);
            toggleBgRect.anchorMax = new Vector2(0, 0.5f);
            toggleBgRect.anchoredPosition = new Vector2(10, 0);
            toggleBgRect.sizeDelta = new Vector2(20, 20);
            var toggleBgImage = toggleBg.AddComponent<Image>();
            toggleBgImage.color = blue;

            // Inner cutout (makes it look hollow when checkmark is hidden)
            var inner = CreateElement("Inner", toggleBg.transform);
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.sizeDelta = new Vector2(-4, -4);
            var innerImage = inner.AddComponent<Image>();
            innerImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            // Fill (shown when checked, covers the inner cutout)
            var checkmark = CreateElement("Checkmark", toggleBg.transform);
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = Vector2.zero;
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = blue;

            includeLogsToggle.graphic = checkImage;
            includeLogsToggle.targetGraphic = toggleBgImage;
            includeLogsToggle.isOn = true;

            var toggleLabel = CreateTextElement("Label", toggleGo.transform, "Include logs", 14, FontStyles.Normal);
            var toggleLabelTmp = toggleLabel.GetComponent<TextMeshProUGUI>();
            toggleLabelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var toggleLabelRect = toggleLabel.GetComponent<RectTransform>();
            toggleLabelRect.anchorMin = new Vector2(0, 0);
            toggleLabelRect.anchorMax = new Vector2(1, 1);
            toggleLabelRect.offsetMin = new Vector2(34, 0);
            toggleLabelRect.offsetMax = Vector2.zero;

            // Buttons row anchored to bottom
            var buttonsRow = CreateElement("Buttons", box.transform);
            var buttonsRect = buttonsRow.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0, 0);
            buttonsRect.anchorMax = new Vector2(1, 0);
            buttonsRect.pivot = new Vector2(0.5f, 0);
            buttonsRect.anchoredPosition = new Vector2(0, pad);
            buttonsRect.sizeDelta = new Vector2(-pad * 2, 44);

            var hlg = buttonsRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            CreateButton("CancelBtn", buttonsRow.transform, "Cancel", new Color(0.7f, 0.7f, 0.7f), OnCancel);
            CreateButton("SubmitBtn", buttonsRow.transform, "Submit", new Color(0.16f, 0.64f, 0.93f), OnSubmit);
        }

        private GameObject CreateElement(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }

        private GameObject CreateTextElement(string name, Transform parent, string text, float fontSize, FontStyles style)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = new Color(0.16f, 0.16f, 0.16f, 1f);
            return go;
        }

        private void SetTopAnchored(GameObject go, float y, float height, float horizontalPad)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(-horizontalPad * 2, height);
        }

        private TMP_InputField CreateInputField(string name, Transform parent, string placeholder, float height)
        {
            var go = CreateElement(name, parent);

            var bgImage = go.AddComponent<Image>();
            bgImage.color = Color.white;

            var textArea = CreateElement("Text Area", go.transform);
            var textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8, 4);
            textAreaRect.offsetMax = new Vector2(-8, -4);
            textArea.AddComponent<RectMask2D>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var textComponent = textGo.AddComponent<TextMeshProUGUI>();
            textComponent.fontSize = 14;
            textComponent.color = new Color(0.16f, 0.16f, 0.16f, 1f);

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            var phRect = placeholderGo.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;
            var phText = placeholderGo.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 14;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.placeholder = phText;
            inputField.fontAsset = textComponent.font;

            return inputField;
        }

        private void CreateButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateElement(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textGo = CreateTextElement("Text", go.transform, label, 16, FontStyles.Bold);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }
    }
}
