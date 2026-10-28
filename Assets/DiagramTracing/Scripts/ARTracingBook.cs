using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace RutherfordSim.Bio.Tracing
{
    [Serializable]
    public class BookPageSpread
    {
        public string spreadTitle = "Chapter 1";
        public DiagramDataSO leftDiagram, rightDiagram;
    }

    [SelectionBase]
    [DisallowMultipleComponent]
    public class ARTracingBook : MonoBehaviour
    {
        [SerializeField] List<BookPageSpread> _pages = new List<BookPageSpread>();
        [SerializeField, Min(0)] int _startingPageIndex = 0;
        [SerializeField] ARTracingBoard _leftBoard, _rightBoard;
        [SerializeField] Button _prevPageButton, _nextPageButton;
        [SerializeField] TextMeshProUGUI _pageNumberText, _bookTitleText;
        [SerializeField] Transform _turningPageLeaf;
        [SerializeField, Range(0.2f, 1.2f)] float _turnDuration = 0.45f;
        [SerializeField] AudioSource _audioSource;
        [SerializeField] AudioClip _pageTurnSound;
        [SerializeField, Range(0.5f, 1f)] float _completionThreshold = 0.95f;

        public UnityEvent<int, int> onPageChanged;
        public UnityEvent<int, bool> onSubmitPressed;

        bool _isTurningPage;
        readonly Dictionary<DiagramDataSO, DiagramTracingState> _savedDiagramStates = new Dictionary<DiagramDataSO, DiagramTracingState>();

        public int CurrentPageIndex { get; private set; }
        public int TotalPages => _pages != null ? _pages.Count : 0;
        public BookPageSpread CurrentSpread => (CurrentPageIndex >= 0 && CurrentPageIndex < TotalPages) ? _pages[CurrentPageIndex] : null;

        void Awake()
        {
            if ( _prevPageButton ) _prevPageButton.onClick.AddListener(PreviousPage);
            if ( _nextPageButton ) _nextPageButton.onClick.AddListener(NextPage);

            if ( _leftBoard )
            {
                _leftBoard.onSubmitClicked.AddListener(() => OnBoardSubmitted(false));
                _leftBoard.onStrokeStarted.AddListener(UpdateDiagramLockState);
                _leftBoard.onThresholdReached.AddListener(UpdateDiagramLockState);
                _leftBoard.onSubmitClicked.AddListener(UpdateDiagramLockState);
                _leftBoard.onResetClicked.AddListener(UpdateDiagramLockState);
                _leftBoard.SetBackingPlateVisible(false);
            }

            if ( _rightBoard )
            {
                _rightBoard.onSubmitClicked.AddListener(() => OnBoardSubmitted(true));
                _rightBoard.onStrokeStarted.AddListener(UpdateDiagramLockState);
                _rightBoard.onThresholdReached.AddListener(UpdateDiagramLockState);
                _rightBoard.onSubmitClicked.AddListener(UpdateDiagramLockState);
                _rightBoard.onResetClicked.AddListener(UpdateDiagramLockState);
                _rightBoard.SetBackingPlateVisible(false);
            }
        }

        void Start()
        {
            CurrentPageIndex = Mathf.Clamp(_startingPageIndex, 0, Mathf.Max(0, TotalPages - 1));
            ApplyCurrentSpread(false);
        }

        public void NextPage()
        {
            if ( _isTurningPage || CurrentPageIndex >= TotalPages - 1 ) return;
            SaveCurrentSpreadState();
            CurrentPageIndex++;
            StartCoroutine(TurnPageRoutine(true));
        }

        public void PreviousPage()
        {
            if ( _isTurningPage || CurrentPageIndex <= 0 ) return;
            SaveCurrentSpreadState();
            CurrentPageIndex--;
            StartCoroutine(TurnPageRoutine(false));
        }

        public void GoToPage(int pageIndex)
        {
            if ( _isTurningPage || pageIndex < 0 || pageIndex >= TotalPages || pageIndex == CurrentPageIndex ) return;
            SaveCurrentSpreadState();
            bool forward = pageIndex > CurrentPageIndex;
            CurrentPageIndex = pageIndex;
            StartCoroutine(TurnPageRoutine(forward));
        }

        void SaveCurrentSpreadState()
        {
            if ( _leftBoard && _leftBoard.CurrentDiagramData )
            {
                var state = _leftBoard.CaptureState();
                if ( state != null ) _savedDiagramStates[_leftBoard.CurrentDiagramData] = state;
            }

            if ( _rightBoard && _rightBoard.CurrentDiagramData )
            {
                var state = _rightBoard.CaptureState();
                if ( state != null ) _savedDiagramStates[_rightBoard.CurrentDiagramData] = state;
            }
        }

        IEnumerator TurnPageRoutine(bool forward)
        {
            _isTurningPage = true;
            SetNavigationButtonsInteractable(false);

            if ( _audioSource && _pageTurnSound ) _audioSource.PlayOneShot(_pageTurnSound);

            if ( _turningPageLeaf )
            {
                float startAngle = forward ? 0f : 180f;
                float targetAngle = forward ? 180f : 0f;
                bool dataSwapped = false;

                _turningPageLeaf.localRotation = Quaternion.Euler(0f, 0f, startAngle);
                _turningPageLeaf.gameObject.SetActive(true);

                float elapsed = 0f;
                while ( elapsed < _turnDuration )
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / _turnDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    float angle = Mathf.Lerp(startAngle, targetAngle, smoothT);

                    _turningPageLeaf.localRotation = Quaternion.Euler(0f, 0f, angle);

                    if ( !dataSwapped && t >= 0.5f )
                    {
                        dataSwapped = true;
                        ApplyCurrentSpread(true);
                    }

                    yield return null;
                }

                _turningPageLeaf.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
                _turningPageLeaf.gameObject.SetActive(false);
            }
            else ApplyCurrentSpread(true);

            SetNavigationButtonsInteractable(true);
            _isTurningPage = false;
        }

        void ApplyCurrentSpread(bool isAnimated)
        {
            if ( TotalPages == 0 ) return;
            var spread = CurrentSpread;
            if ( spread == null ) return;

            if ( _leftBoard )
            {
                if ( spread.leftDiagram )
                {
                    _leftBoard.gameObject.SetActive(true);
                    _savedDiagramStates.TryGetValue(spread.leftDiagram, out var savedState);
                    _leftBoard.Initialize(spread.leftDiagram, _completionThreshold, savedState);
                }
                else _leftBoard.gameObject.SetActive(false);
            }

            if ( _rightBoard )
            {
                if ( spread.rightDiagram )
                {
                    _rightBoard.gameObject.SetActive(true);
                    _savedDiagramStates.TryGetValue(spread.rightDiagram, out var savedState);
                    _rightBoard.Initialize(spread.rightDiagram, _completionThreshold, savedState);
                }
                else _rightBoard.gameObject.SetActive(false);
            }

            if ( _pageNumberText )
            {
                int leftPageNum = (CurrentPageIndex * 2) + 1;
                int rightPageNum = leftPageNum + 1;
                _pageNumberText.text = $"Pages {leftPageNum}-{rightPageNum} of {TotalPages * 2}";
            }

            if ( _bookTitleText ) _bookTitleText.text = !string.IsNullOrEmpty(spread.spreadTitle) ? spread.spreadTitle : $"Spread {CurrentPageIndex + 1}";

            UpdateNavigationButtons();
            UpdateDiagramLockState();
            onPageChanged?.Invoke(CurrentPageIndex, TotalPages);
        }

        void UpdateNavigationButtons()
        {
            if ( _prevPageButton ) _prevPageButton.interactable = CurrentPageIndex > 0 && !_isTurningPage;
            if ( _nextPageButton ) _nextPageButton.interactable = CurrentPageIndex < TotalPages - 1 && !_isTurningPage;
        }

        void SetNavigationButtonsInteractable(bool interactable)
        {
            if ( _prevPageButton ) _prevPageButton.interactable = interactable && CurrentPageIndex > 0;
            if ( _nextPageButton ) _nextPageButton.interactable = interactable && CurrentPageIndex < TotalPages - 1;
        }

        void OnBoardSubmitted(bool isRightPage) => onSubmitPressed?.Invoke(CurrentPageIndex, isRightPage);

        public void EnsureWorldCamera()
        {
            if ( _leftBoard ) _leftBoard.EnsureWorldCamera();
            if ( _rightBoard ) _rightBoard.EnsureWorldCamera();
        }

        public void ClearAllSavedProgress()
        {
            _savedDiagramStates.Clear();
            ApplyCurrentSpread(false);
        }

        public DiagramDataSO GetActiveInProgressDiagram(out string title)
        {
            if ( _leftBoard && _leftBoard.gameObject.activeSelf && _leftBoard.IsInProgress )
            {
                title = _leftBoard.CurrentDiagramData ? _leftBoard.CurrentDiagramData.diagramTitle : "Left Diagram";
                return _leftBoard.CurrentDiagramData;
            }

            if ( _rightBoard && _rightBoard.gameObject.activeSelf && _rightBoard.IsInProgress )
            {
                title = _rightBoard.CurrentDiagramData ? _rightBoard.CurrentDiagramData.diagramTitle : "Right Diagram";
                return _rightBoard.CurrentDiagramData;
            }

            foreach ( var kvp in _savedDiagramStates )
            {
                var data = kvp.Key;
                var state = kvp.Value;
                if ( state != null && !state.isCompleted && !state.isSubmitted && (state.savedProgress > 0.0001f || state.hitLength > 0.0001f || state.hasDrawnContent) )
                {
                    if ( (_leftBoard && _leftBoard.CurrentDiagramData == data) || (_rightBoard && _rightBoard.CurrentDiagramData == data) ) continue;
                    title = data ? data.diagramTitle : "Other Diagram";
                    return data;
                }
            }

            title = null;
            return null;
        }

        public void UpdateDiagramLockState()
        {
            var activeDiagram = GetActiveInProgressDiagram(out string activeTitle);
            if ( activeDiagram )
            {
                if ( _leftBoard && _leftBoard.gameObject.activeSelf ) _leftBoard.SetLockedByOther(_leftBoard.CurrentDiagramData != activeDiagram, activeTitle);
                if ( _rightBoard && _rightBoard.gameObject.activeSelf ) _rightBoard.SetLockedByOther(_rightBoard.CurrentDiagramData != activeDiagram, activeTitle);
            }
            else
            {
                if ( _leftBoard && _leftBoard.gameObject.activeSelf ) _leftBoard.SetLockedByOther(false);
                if ( _rightBoard && _rightBoard.gameObject.activeSelf ) _rightBoard.SetLockedByOther(false);
            }
        }
    }
}
