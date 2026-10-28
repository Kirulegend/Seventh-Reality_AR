using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace RutherfordSim.Bio.Tracing
{
    /// <summary>
    /// Holds the persistent runtime tracing state of a single diagram,
    /// enabling midway progress and completion/submitted status to be saved and restored across page turns.
    /// </summary>
    [Serializable]
    public class DiagramTracingState
    {
        public DiagramDataSO diagramData;
        public Color32[] pixels;
        public bool[] hits;
        public Color32[] edgeColors;
        public float hitLength;
        public bool thresholdFired;
        public bool isCompleted;
        public bool isSubmitted;
        public float savedProgress;
        public bool isEraserActive;
        public bool hasDrawnContent;
    }

    /// <summary>
    /// Modular Diagram Tracing Surface.
    /// Can be spawned onto an AR plane, mounted to a 3D book page, or placed on any surface in World Space.
    /// Manages the tracing canvas, progress display, pen/eraser tools, and reveals the Submit button at 95% completion.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class ARTracingBoard : MonoBehaviour
    {
        [Header("Diagram Data")]
        [Tooltip("The diagram data to trace. Can also be set dynamically via Initialize().")]
        [SerializeField] private DiagramDataSO _diagramData;

        [Header("Completion Settings")]
        [Tooltip("Threshold percentage required to spawn the submit button (e.g. 0.95 for 95%).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _completionThreshold = 0.95f;

        [Header("Core Tracing Canvas")]
        [SerializeField] private DiagramTracingCanvas _tracingCanvas;
        [SerializeField] private Canvas _worldCanvas;

        [Header("UI Controls")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _progressPercentageText;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _eraserToggleButton;
        [SerializeField] private TextMeshProUGUI _eraserButtonText;

        [Header("Submit Button")]
        [Tooltip("The Submit button GameObject/Root that is spawned/activated once 95% threshold is reached.")]
        [SerializeField] private GameObject _submitButtonRoot;
        [SerializeField] private Button _submitButton;

        [Header("Appearance")]
        [Tooltip("Optional 3D backing plate (hidden when embedded inside a 3D book).")]
        [SerializeField] private GameObject _backingPlate;

        [Header("Feedback Audio (Optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _completionSound;

        [Header("Events")]
        public UnityEvent<float> onProgressChanged;
        public UnityEvent onThresholdReached;
        public UnityEvent onSubmitClicked;
        public UnityEvent onResetClicked;
        public UnityEvent onStrokeStarted;

        private bool _isEraserActive = false;
        private bool _isCompleted = false;
        private bool _isSubmitted = false;
        private bool _isLockedByOther = false;

        public DiagramDataSO CurrentDiagramData => _diagramData;
        public float CompletionThreshold => _completionThreshold;
        public bool IsCompleted => _isCompleted;
        public bool IsSubmitted => _isSubmitted;
        public bool IsLockedByOther => _isLockedByOther;
        public float CurrentProgress => _tracingCanvas != null ? _tracingCanvas.Progress : 0f;

        public bool IsInProgress
        {
            get
            {
                if (_diagramData == null || _isCompleted || _isSubmitted) return false;
                return CurrentProgress > 0.0001f || (_tracingCanvas != null && _tracingCanvas.HasDrawnContent);
            }
        }

        /// <summary>
        /// Show or hide the physical backing plate (useful when embedded in a 3D book page).
        /// </summary>
        public void SetBackingPlateVisible(bool visible)
        {
            if (_backingPlate != null)
                _backingPlate.SetActive(visible);
            else
            {
                var plate = transform.Find("BoardBackingPlate");
                if (plate != null)
                    plate.gameObject.SetActive(visible);
            }
        }

        private void Awake()
        {
            EnsureWorldCamera();

            if (_resetButton != null)
                _resetButton.onClick.AddListener(ResetTracing);

            if (_eraserToggleButton != null)
                _eraserToggleButton.onClick.AddListener(ToggleEraser);

            if (_submitButton != null)
                _submitButton.onClick.AddListener(OnSubmitButtonPressed);

            // Initially hide the submit button until 95% is reached
            if (_submitButtonRoot != null)
                _submitButtonRoot.SetActive(false);
        }

        private void Start()
        {
            EnsureWorldCamera();

            if (_diagramData != null)
            {
                Initialize(_diagramData, _completionThreshold);
            }
        }

        /// <summary>
        /// Ensure the World Space Canvas has its event camera bound to the AR / Main Camera for touch raycasting.
        /// </summary>
        public void EnsureWorldCamera()
        {
            if (_worldCanvas == null)
                _worldCanvas = GetComponentInChildren<Canvas>(true);

            if (_worldCanvas != null && _worldCanvas.renderMode == RenderMode.WorldSpace)
            {
                if (_worldCanvas.worldCamera == null)
                    _worldCanvas.worldCamera = Camera.main;
            }
        }

        /// <summary>
        /// Modular entry point: Initialize this board with any diagram data and custom completion threshold.
        /// Perfect for loading different diagrams dynamically or mounting into a 3D book.
        /// </summary>
        public void Initialize(DiagramDataSO data, float targetThreshold = 0.95f, DiagramTracingState state = null)
        {
            _diagramData = data;
            _completionThreshold = Mathf.Clamp(targetThreshold, 0.5f, 1f);
            _isCompleted = false;
            _isSubmitted = false;
            _isEraserActive = false;

            EnsureWorldCamera();

            if (_submitButtonRoot != null)
                _submitButtonRoot.SetActive(false);

            if (_submitButton != null)
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Submit";
            }

            if (_titleText != null && data != null)
                _titleText.text = data.diagramTitle;

            if (_subtitleText != null && data != null)
                _subtitleText.text = data.diagramSummary;

            UpdateEraserUI();
            UpdateProgressDisplay(0f);

            if (_tracingCanvas != null && data != null)
            {
                _tracingCanvas.OnProgressChanged -= HandleProgressChanged;
                _tracingCanvas.OnThresholdReached -= HandleThresholdReached;
                _tracingCanvas.OnStrokeStarted -= HandleStrokeStarted;

                _tracingCanvas.OnProgressChanged += HandleProgressChanged;
                _tracingCanvas.OnThresholdReached += HandleThresholdReached;
                _tracingCanvas.OnStrokeStarted += HandleStrokeStarted;

                _tracingCanvas.SetTargetThreshold(_completionThreshold);
                _tracingCanvas.Initialize(data);
                _tracingCanvas.SetTool(Color.black, false);
                _tracingCanvas.SetInteractive(true);
            }

            if (state != null)
            {
                RestoreState(state);
            }
        }

        private void HandleProgressChanged(float progress)
        {
            UpdateProgressDisplay(progress);
            onProgressChanged?.Invoke(progress);

            // Redundant safety check in case progress hits threshold
            if (!_isCompleted && progress + 0.0001f >= _completionThreshold)
            {
                HandleThresholdReached();
            }
        }

        private void HandleThresholdReached()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            Debug.Log($"[ARTracingBoard] Tracing threshold {_completionThreshold * 100f:F0}% reached! Spawning Submit Button.");

            // Spawn / Activate the Submit Button
            if (_submitButtonRoot != null)
            {
                _submitButtonRoot.SetActive(true);
            }

            if (_subtitleText != null)
            {
                _subtitleText.text = $"Threshold reached ({_completionThreshold * 100f:F0}%)! You can now submit or continue refining.";
            }

            if (_audioSource != null && _completionSound != null)
            {
                _audioSource.PlayOneShot(_completionSound);
            }

            onThresholdReached?.Invoke();
        }

        private void UpdateProgressDisplay(float progress)
        {
            if (_progressSlider != null)
                _progressSlider.value = progress;

            if (_progressPercentageText != null)
                _progressPercentageText.text = $"{progress * 100f:F0}% / {_completionThreshold * 100f:F0}%";
        }

        /// <summary>
        /// Clears the tracing canvas and resets progress back to 0%.
        /// </summary>
        public void ResetTracing()
        {
            _isCompleted = false;
            _isSubmitted = false;

            if (_submitButtonRoot != null)
                _submitButtonRoot.SetActive(false);

            if (_submitButton != null)
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Submit";
            }

            if (_tracingCanvas != null)
            {
                _tracingCanvas.ResetToOutline();
                _tracingCanvas.SetInteractive(true);
            }

            if (_subtitleText != null && _diagramData != null)
                _subtitleText.text = _diagramData.diagramSummary;

            UpdateProgressDisplay(0f);
            onResetClicked?.Invoke();
        }

        /// <summary>
        /// Locks or unlocks drawing on this board when another diagram is currently being traced.
        /// </summary>
        public void SetLockedByOther(bool locked, string activeDiagramTitle = null)
        {
            if (_isLockedByOther == locked) return;
            _isLockedByOther = locked;

            if (_isSubmitted)
            {
                // Submitted diagram is already finalized with colored artwork
                return;
            }

            if (locked)
            {
                if (_tracingCanvas != null)
                    _tracingCanvas.SetInteractive(false);

                if (_resetButton != null)
                    _resetButton.interactable = false;

                if (_eraserToggleButton != null)
                    _eraserToggleButton.interactable = false;

                if (_subtitleText != null)
                {
                    string title = !string.IsNullOrEmpty(activeDiagramTitle) ? activeDiagramTitle : "the active diagram";
                    _subtitleText.text = $"Complete or clear {title} before tracing this diagram.";
                }
            }
            else
            {
                if (_tracingCanvas != null)
                    _tracingCanvas.SetInteractive(true);

                if (_resetButton != null)
                    _resetButton.interactable = true;

                if (_eraserToggleButton != null)
                    _eraserToggleButton.interactable = true;

                if (_subtitleText != null)
                {
                    if (_isCompleted)
                    {
                        _subtitleText.text = $"Threshold reached ({_completionThreshold * 100f:F0}%)! You can now submit or continue refining.";
                    }
                    else if (_diagramData != null)
                    {
                        _subtitleText.text = _diagramData.diagramSummary;
                    }
                }
            }
        }

        /// <summary>
        /// Captures the runtime tracing state (pixels, hit edges, submission) for saving across page turns.
        /// </summary>
        public DiagramTracingState CaptureState()
        {
            if (_diagramData == null) return null;

            var state = new DiagramTracingState
            {
                diagramData = _diagramData,
                isCompleted = _isCompleted,
                isSubmitted = _isSubmitted,
                isEraserActive = _isEraserActive,
                savedProgress = CurrentProgress,
                hasDrawnContent = _tracingCanvas != null && _tracingCanvas.HasDrawnContent
            };

            if (_tracingCanvas != null)
            {
                _tracingCanvas.CaptureCanvasState(state);
            }

            return state;
        }

        /// <summary>
        /// Restores a previously captured tracing state onto this board.
        /// </summary>
        public void RestoreState(DiagramTracingState state)
        {
            if (state == null || state.diagramData != _diagramData) return;

            _isCompleted = state.isCompleted;
            _isSubmitted = state.isSubmitted;
            _isEraserActive = state.isEraserActive;
            UpdateEraserUI();

            if (_tracingCanvas != null)
            {
                _tracingCanvas.RestoreCanvasState(state);
                _tracingCanvas.SetTool(Color.black, _isEraserActive);
            }

            UpdateProgressDisplay(state.savedProgress);

            if (_submitButtonRoot != null)
            {
                _submitButtonRoot.SetActive(_isCompleted || _isSubmitted);
            }

            if (_submitButton != null)
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = _isSubmitted ? "Submitted" : "Submit";
                }
            }

            if (_subtitleText != null)
            {
                if (_isSubmitted)
                {
                    _subtitleText.text = "Submitted! Displaying completed colored diagram.";
                }
                else if (_isCompleted)
                {
                    _subtitleText.text = $"Threshold reached ({_completionThreshold * 100f:F0}%)! You can now submit or continue refining.";
                }
                else if (_diagramData != null)
                {
                    _subtitleText.text = _diagramData.diagramSummary;
                }
            }
        }

        /// <summary>
        /// Toggles between the drawing pen and eraser.
        /// </summary>
        public void ToggleEraser()
        {
            _isEraserActive = !_isEraserActive;
            if (_tracingCanvas != null)
            {
                _tracingCanvas.SetTool(Color.black, _isEraserActive);
            }
            UpdateEraserUI();
        }

        private void UpdateEraserUI()
        {
            if (_eraserButtonText != null)
                _eraserButtonText.text = _isEraserActive ? "Tool: Eraser" : "Tool: Pen";
        }

        private void OnSubmitButtonPressed()
        {
            Debug.Log("[ARTracingBoard] Submit Button Clicked! Revealing full colored diagram.");
            _isSubmitted = true;

            if (_tracingCanvas != null)
            {
                _tracingCanvas.ShowFullColoredTexture();
            }

            if (_subtitleText != null)
            {
                _subtitleText.text = "Submitted! Displaying completed colored diagram.";
            }

            if (_submitButton != null)
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Submitted";
            }

            onSubmitClicked?.Invoke();
        }

        private void HandleStrokeStarted()
        {
            onStrokeStarted?.Invoke();
        }

        private void OnDestroy()
        {
            if (_tracingCanvas != null)
            {
                _tracingCanvas.OnProgressChanged -= HandleProgressChanged;
                _tracingCanvas.OnThresholdReached -= HandleThresholdReached;
                _tracingCanvas.OnStrokeStarted -= HandleStrokeStarted;
            }
        }
    }
}
