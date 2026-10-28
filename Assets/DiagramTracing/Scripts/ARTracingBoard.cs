using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace RutherfordSim.Bio.Tracing
{
    [Serializable]
    public class DiagramTracingState
    {
        public DiagramDataSO diagramData;
        public Color32[] pixels, edgeColors;
        public bool[] hits;
        public float hitLength, savedProgress;
        public bool thresholdFired, isCompleted, isSubmitted, isEraserActive, hasDrawnContent;
    }

    [SelectionBase]
    [DisallowMultipleComponent]
    public class ARTracingBoard : MonoBehaviour
    {
        [SerializeField] DiagramDataSO _diagramData;
        [SerializeField, Range(0.5f, 1f)] float _completionThreshold = 0.95f;
        [SerializeField] DiagramTracingCanvas _tracingCanvas;
        [SerializeField] Canvas _worldCanvas;
        [SerializeField] TextMeshProUGUI _titleText, _subtitleText, _progressPercentageText, _eraserButtonText;
        [SerializeField] Slider _progressSlider;
        [SerializeField] Button _resetButton, _eraserToggleButton, _submitButton;
        [SerializeField] GameObject _submitButtonRoot, _backingPlate;
        [SerializeField] AudioSource _audioSource;
        [SerializeField] AudioClip _completionSound;

        public UnityEvent<float> onProgressChanged;
        public UnityEvent onThresholdReached, onSubmitClicked, onResetClicked, onStrokeStarted;

        bool _isEraserActive, _isCompleted, _isSubmitted, _isLockedByOther;

        public DiagramDataSO CurrentDiagramData => _diagramData;
        public float CompletionThreshold => _completionThreshold;
        public bool IsCompleted => _isCompleted;
        public bool IsSubmitted => _isSubmitted;
        public bool IsLockedByOther => _isLockedByOther;
        public float CurrentProgress => _tracingCanvas ? _tracingCanvas.Progress : 0f;

        public bool IsInProgress
        {
            get
            {
                if ( !_diagramData || _isCompleted || _isSubmitted ) return false;
                return CurrentProgress > 0.0001f || (_tracingCanvas && _tracingCanvas.HasDrawnContent);
            }
        }

        public void SetBackingPlateVisible(bool visible)
        {
            if ( _backingPlate ) _backingPlate.SetActive(visible);
            else
            {
                var plate = transform.Find("BoardBackingPlate");
                if ( plate ) plate.gameObject.SetActive(visible);
            }
        }

        void Awake()
        {
            EnsureWorldCamera();
            if ( _resetButton ) _resetButton.onClick.AddListener(ResetTracing);
            if ( _eraserToggleButton ) _eraserToggleButton.onClick.AddListener(ToggleEraser);
            if ( _submitButton ) _submitButton.onClick.AddListener(OnSubmitButtonPressed);
            if ( _submitButtonRoot ) _submitButtonRoot.SetActive(false);
        }

        void Start()
        {
            EnsureWorldCamera();
            if ( _diagramData ) Initialize(_diagramData, _completionThreshold);
        }

        public void EnsureWorldCamera()
        {
            if ( !_worldCanvas ) _worldCanvas = GetComponentInChildren<Canvas>(true);
            if ( _worldCanvas && _worldCanvas.renderMode == RenderMode.WorldSpace && !_worldCanvas.worldCamera )
                _worldCanvas.worldCamera = Camera.main;
        }

        public void Initialize(DiagramDataSO data, float targetThreshold = 0.95f, DiagramTracingState state = null)
        {
            _diagramData = data;
            _completionThreshold = Mathf.Clamp(targetThreshold, 0.5f, 1f);
            _isCompleted = _isSubmitted = _isEraserActive = false;

            EnsureWorldCamera();
            if ( _submitButtonRoot ) _submitButtonRoot.SetActive(false);

            if ( _submitButton )
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if ( btnText ) btnText.text = "Submit";
            }

            if ( _titleText && data ) _titleText.text = data.diagramTitle;
            if ( _subtitleText && data ) _subtitleText.text = data.diagramSummary;

            UpdateEraserUI();
            UpdateProgressDisplay(0f);

            if ( _tracingCanvas && data )
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

            if ( state != null ) RestoreState(state);
        }

        void HandleProgressChanged(float progress)
        {
            UpdateProgressDisplay(progress);
            onProgressChanged?.Invoke(progress);
            if ( !_isCompleted && progress + 0.0001f >= _completionThreshold ) HandleThresholdReached();
        }

        void HandleThresholdReached()
        {
            if ( _isCompleted ) return;
            _isCompleted = true;

            if ( _submitButtonRoot ) _submitButtonRoot.SetActive(true);
            if ( _subtitleText ) _subtitleText.text = $"Threshold reached ({_completionThreshold * 100f:F0}%)! You can now submit or continue refining.";
            if ( _audioSource && _completionSound ) _audioSource.PlayOneShot(_completionSound);
            onThresholdReached?.Invoke();
        }

        void UpdateProgressDisplay(float progress)
        {
            if ( _progressSlider ) _progressSlider.value = progress;
            if ( _progressPercentageText ) _progressPercentageText.text = $"{progress * 100f:F0}% / {_completionThreshold * 100f:F0}%";
        }

        public void ResetTracing()
        {
            _isCompleted = _isSubmitted = false;
            if ( _submitButtonRoot ) _submitButtonRoot.SetActive(false);

            if ( _submitButton )
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if ( btnText ) btnText.text = "Submit";
            }

            if ( _tracingCanvas )
            {
                _tracingCanvas.ResetToOutline();
                _tracingCanvas.SetInteractive(true);
            }

            if ( _subtitleText && _diagramData ) _subtitleText.text = _diagramData.diagramSummary;
            UpdateProgressDisplay(0f);
            onResetClicked?.Invoke();
        }

        public void SetLockedByOther(bool locked, string activeDiagramTitle = null)
        {
            if ( _isLockedByOther == locked ) return;
            _isLockedByOther = locked;
            if ( _isSubmitted ) return;

            if ( locked )
            {
                if ( _tracingCanvas ) _tracingCanvas.SetInteractive(false);
                if ( _resetButton ) _resetButton.interactable = false;
                if ( _eraserToggleButton ) _eraserToggleButton.interactable = false;
                if ( _subtitleText )
                {
                    string title = !string.IsNullOrEmpty(activeDiagramTitle) ? activeDiagramTitle : "the active diagram";
                    _subtitleText.text = $"Complete or clear {title} before tracing this diagram.";
                }
            }
            else
            {
                if ( _tracingCanvas ) _tracingCanvas.SetInteractive(true);
                if ( _resetButton ) _resetButton.interactable = true;
                if ( _eraserToggleButton ) _eraserToggleButton.interactable = true;
                if ( _subtitleText )
                {
                    if ( _isCompleted ) _subtitleText.text = $"Threshold reached ({_completionThreshold * 100f:F0}%)! You can now submit or continue refining.";
                    else if ( _diagramData ) _subtitleText.text = _diagramData.diagramSummary;
                }
            }
        }

        public DiagramTracingState CaptureState()
        {
            if ( !_diagramData ) return null;

            var state = new DiagramTracingState
            {
                diagramData = _diagramData,
                isCompleted = _isCompleted,
                isSubmitted = _isSubmitted,
                isEraserActive = _isEraserActive,
                savedProgress = CurrentProgress,
                hasDrawnContent = _tracingCanvas && _tracingCanvas.HasDrawnContent
            };

            if ( _tracingCanvas ) _tracingCanvas.CaptureCanvasState(state);
            return state;
        }

        public void RestoreState(DiagramTracingState state)
        {
            if ( state == null || state.diagramData != _diagramData ) return;

            _isCompleted = state.isCompleted;
            _isSubmitted = state.isSubmitted;
            _isEraserActive = state.isEraserActive;
            UpdateEraserUI();

            if ( _tracingCanvas )
            {
                _tracingCanvas.RestoreCanvasState(state);
                _tracingCanvas.SetTool(Color.black, _isEraserActive);
            }

            UpdateProgressDisplay(state.savedProgress);
            if ( _submitButtonRoot ) _submitButtonRoot.SetActive(_isCompleted || _isSubmitted);

            if ( _submitButton )
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if ( btnText ) btnText.text = _isSubmitted ? "Submitted" : "Submit";
            }

            if ( _subtitleText )
            {
                if ( _isSubmitted ) _subtitleText.text = "Submitted! Displaying completed colored diagram.";
                else if ( _isCompleted ) _subtitleText.text = $"Threshold reached ({_completionThreshold * 100f:F0}%)! You can now submit or continue refining.";
                else if ( _diagramData ) _subtitleText.text = _diagramData.diagramSummary;
            }
        }

        public void ToggleEraser()
        {
            _isEraserActive = !_isEraserActive;
            if ( _tracingCanvas ) _tracingCanvas.SetTool(Color.black, _isEraserActive);
            UpdateEraserUI();
        }

        void UpdateEraserUI()
        {
            if ( _eraserButtonText ) _eraserButtonText.text = _isEraserActive ? "Tool: Eraser" : "Tool: Pen";
        }

        void OnSubmitButtonPressed()
        {
            _isSubmitted = true;
            if ( _tracingCanvas ) _tracingCanvas.ShowFullColoredTexture();
            if ( _subtitleText ) _subtitleText.text = "Submitted! Displaying completed colored diagram.";
            if ( _submitButton )
            {
                var btnText = _submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if ( btnText ) btnText.text = "Submitted";
            }
            onSubmitClicked?.Invoke();
        }

        void HandleStrokeStarted() => onStrokeStarted?.Invoke();

        void OnDestroy()
        {
            if ( _tracingCanvas )
            {
                _tracingCanvas.OnProgressChanged -= HandleProgressChanged;
                _tracingCanvas.OnThresholdReached -= HandleThresholdReached;
                _tracingCanvas.OnStrokeStarted -= HandleStrokeStarted;
            }
        }
    }
}
