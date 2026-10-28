using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace RutherfordSim.Bio.Tracing
{
    /// <summary>
    /// Represents one two-page spread in the 3D book, holding 2 diagrams (one on each side).
    /// </summary>
    [Serializable]
    public class BookPageSpread
    {
        public string spreadTitle = "Chapter 1";
        [Tooltip("Diagram data for the left page.")]
        public DiagramDataSO leftDiagram;
        [Tooltip("Diagram data for the right page.")]
        public DiagramDataSO rightDiagram;
    }

    /// <summary>
    /// 3D Interactive Tracing Book for AR.
    /// Manages 4 two-page spreads (8 diagrams total) with persistent pooled left and right
    /// ARTracingBoard instances. Changing pages flips the 3D leaf and reuses the pooled
    /// boards by reinitializing them with the new page's diagrams, avoiding extra instantiation.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class ARTracingBook : MonoBehaviour
    {
        [Header("Book Pages Data")]
        [Tooltip("List of 4 two-page spreads in the book (each spread has 2 diagrams: left & right).")]
        [SerializeField] private List<BookPageSpread> _pages = new List<BookPageSpread>();
        [SerializeField, Min(0)] private int _startingPageIndex = 0;

        [Header("Pooled Tracing Boards (Object Pooling)")]
        [Tooltip("The persistent pooled tracing board on the left page.")]
        [SerializeField] private ARTracingBoard _leftBoard;
        [Tooltip("The persistent pooled tracing board on the right page.")]
        [SerializeField] private ARTracingBoard _rightBoard;

        [Header("Navigation UI")]
        [SerializeField] private Button _prevPageButton;
        [SerializeField] private Button _nextPageButton;
        [SerializeField] private TextMeshProUGUI _pageNumberText;
        [SerializeField] private TextMeshProUGUI _bookTitleText;

        [Header("Page Turn Animation (3D Leaf)")]
        [Tooltip("The middle page/leaf transform that pivots around the book spine.")]
        [SerializeField] private Transform _turningPageLeaf;
        [SerializeField, Range(0.2f, 1.2f)] private float _turnDuration = 0.45f;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _pageTurnSound;

        [Header("Completion Settings")]
        [Tooltip("Target percentage required to spawn each diagram's submit button (e.g. 0.95 for 95%).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _completionThreshold = 0.95f;

        [Header("Events")]
        public UnityEvent<int, int> onPageChanged; // (currentPageIndex, totalPages)
        public UnityEvent<int, bool> onSubmitPressed; // (pageIndex, isRightPage)

        private bool _isTurningPage = false;
        private readonly Dictionary<DiagramDataSO, DiagramTracingState> _savedDiagramStates = new Dictionary<DiagramDataSO, DiagramTracingState>();

        public int CurrentPageIndex { get; private set; }
        public int TotalPages => _pages != null ? _pages.Count : 0;
        public BookPageSpread CurrentSpread => (CurrentPageIndex >= 0 && CurrentPageIndex < TotalPages) ? _pages[CurrentPageIndex] : null;

        private void Awake()
        {
            if (_prevPageButton != null)
                _prevPageButton.onClick.AddListener(PreviousPage);

            if (_nextPageButton != null)
                _nextPageButton.onClick.AddListener(NextPage);

            // Hook into pooled board submit buttons and mutual exclusion events
            if (_leftBoard != null)
            {
                _leftBoard.onSubmitClicked.AddListener(() => OnBoardSubmitted(false));
                _leftBoard.onStrokeStarted.AddListener(UpdateDiagramLockState);
                _leftBoard.onThresholdReached.AddListener(UpdateDiagramLockState);
                _leftBoard.onSubmitClicked.AddListener(UpdateDiagramLockState);
                _leftBoard.onResetClicked.AddListener(UpdateDiagramLockState);
            }

            if (_rightBoard != null)
            {
                _rightBoard.onSubmitClicked.AddListener(() => OnBoardSubmitted(true));
                _rightBoard.onStrokeStarted.AddListener(UpdateDiagramLockState);
                _rightBoard.onThresholdReached.AddListener(UpdateDiagramLockState);
                _rightBoard.onSubmitClicked.AddListener(UpdateDiagramLockState);
                _rightBoard.onResetClicked.AddListener(UpdateDiagramLockState);
            }

            // Hide backing plates if embedded inside book geometry
            if (_leftBoard != null)
                _leftBoard.SetBackingPlateVisible(false);

            if (_rightBoard != null)
                _rightBoard.SetBackingPlateVisible(false);
        }

        private void Start()
        {
            CurrentPageIndex = Mathf.Clamp(_startingPageIndex, 0, Mathf.Max(0, TotalPages - 1));
            ApplyCurrentSpread(false);
        }

        /// <summary>
        /// Flips forward to the next page spread, reusing the pooled boards.
        /// </summary>
        public void NextPage()
        {
            if (_isTurningPage || CurrentPageIndex >= TotalPages - 1)
                return;

            SaveCurrentSpreadState();
            CurrentPageIndex++;
            StartCoroutine(TurnPageRoutine(true));
        }

        /// <summary>
        /// Flips backward to the previous page spread, reusing the pooled boards.
        /// </summary>
        public void PreviousPage()
        {
            if (_isTurningPage || CurrentPageIndex <= 0)
                return;

            SaveCurrentSpreadState();
            CurrentPageIndex--;
            StartCoroutine(TurnPageRoutine(false));
        }

        /// <summary>
        /// Jumps directly to a specific page spread.
        /// </summary>
        public void GoToPage(int pageIndex)
        {
            if (_isTurningPage || pageIndex < 0 || pageIndex >= TotalPages || pageIndex == CurrentPageIndex)
                return;

            SaveCurrentSpreadState();
            bool forward = pageIndex > CurrentPageIndex;
            CurrentPageIndex = pageIndex;
            StartCoroutine(TurnPageRoutine(forward));
        }

        /// <summary>
        /// Saves the current tracing strokes and completion/submitted state for both active pages.
        /// </summary>
        private void SaveCurrentSpreadState()
        {
            if (_leftBoard != null && _leftBoard.CurrentDiagramData != null)
            {
                var state = _leftBoard.CaptureState();
                if (state != null)
                    _savedDiagramStates[_leftBoard.CurrentDiagramData] = state;
            }

            if (_rightBoard != null && _rightBoard.CurrentDiagramData != null)
            {
                var state = _rightBoard.CaptureState();
                if (state != null)
                    _savedDiagramStates[_rightBoard.CurrentDiagramData] = state;
            }
        }

        private IEnumerator TurnPageRoutine(bool forward)
        {
            _isTurningPage = true;
            SetNavigationButtonsInteractable(false);

            if (_audioSource != null && _pageTurnSound != null)
                _audioSource.PlayOneShot(_pageTurnSound);

            if (_turningPageLeaf != null)
            {
                // Leaf rotation around spine (Z axis in book space)
                // Forward flip: 0 deg (right side) -> +180 deg (left side)
                // Backward flip: +180 deg (left side) -> 0 deg (right side)
                float startAngle = forward ? 0f : 180f;
                float targetAngle = forward ? 180f : 0f;
                bool dataSwapped = false;

                _turningPageLeaf.localRotation = Quaternion.Euler(0f, 0f, startAngle);
                _turningPageLeaf.gameObject.SetActive(true);

                float elapsed = 0f;
                while (elapsed < _turnDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / _turnDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    float angle = Mathf.Lerp(startAngle, targetAngle, smoothT);

                    _turningPageLeaf.localRotation = Quaternion.Euler(0f, 0f, angle);

                    // Swap the pooled board data halfway through the turn when the page is vertical
                    if (!dataSwapped && t >= 0.5f)
                    {
                        dataSwapped = true;
                        ApplyCurrentSpread(true);
                    }

                    yield return null;
                }

                _turningPageLeaf.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
                _turningPageLeaf.gameObject.SetActive(false);
            }
            else
            {
                // Instant swap if no animated leaf
                ApplyCurrentSpread(true);
            }

            SetNavigationButtonsInteractable(true);
            _isTurningPage = false;
        }

        /// <summary>
        /// Reuses and re-initializes the pooled left and right ARTracingBoard instances
        /// with the current page's diagrams, resetting their canvas, progress, and tools.
        /// </summary>
        private void ApplyCurrentSpread(bool isAnimated)
        {
            if (TotalPages == 0) return;

            var spread = CurrentSpread;
            if (spread == null) return;

            // 1. Reuse Left Board
            if (_leftBoard != null)
            {
                if (spread.leftDiagram != null)
                {
                    _leftBoard.gameObject.SetActive(true);
                    _savedDiagramStates.TryGetValue(spread.leftDiagram, out var savedState);
                    _leftBoard.Initialize(spread.leftDiagram, _completionThreshold, savedState);
                }
                else
                {
                    _leftBoard.gameObject.SetActive(false);
                }
            }

            // 2. Reuse Right Board
            if (_rightBoard != null)
            {
                if (spread.rightDiagram != null)
                {
                    _rightBoard.gameObject.SetActive(true);
                    _savedDiagramStates.TryGetValue(spread.rightDiagram, out var savedState);
                    _rightBoard.Initialize(spread.rightDiagram, _completionThreshold, savedState);
                }
                else
                {
                    _rightBoard.gameObject.SetActive(false);
                }
            }

            // 3. Update Book Navigation UI
            if (_pageNumberText != null)
            {
                int leftPageNum = (CurrentPageIndex * 2) + 1;
                int rightPageNum = leftPageNum + 1;
                int totalIndividualPages = TotalPages * 2;
                _pageNumberText.text = $"Pages {leftPageNum}-{rightPageNum} of {totalIndividualPages}";
            }

            if (_bookTitleText != null)
            {
                _bookTitleText.text = !string.IsNullOrEmpty(spread.spreadTitle) ? spread.spreadTitle : $"Spread {CurrentPageIndex + 1}";
            }

            UpdateNavigationButtons();
            UpdateDiagramLockState();
            onPageChanged?.Invoke(CurrentPageIndex, TotalPages);
        }

        private void UpdateNavigationButtons()
        {
            if (_prevPageButton != null)
                _prevPageButton.interactable = CurrentPageIndex > 0 && !_isTurningPage;

            if (_nextPageButton != null)
                _nextPageButton.interactable = CurrentPageIndex < TotalPages - 1 && !_isTurningPage;
        }

        private void SetNavigationButtonsInteractable(bool interactable)
        {
            if (_prevPageButton != null)
                _prevPageButton.interactable = interactable && CurrentPageIndex > 0;

            if (_nextPageButton != null)
                _nextPageButton.interactable = interactable && CurrentPageIndex < TotalPages - 1;
        }

        private void OnBoardSubmitted(bool isRightPage)
        {
            Debug.Log($"[ARTracingBook] Diagram on {(isRightPage ? "Right" : "Left")} page of spread {CurrentPageIndex + 1} submitted!");
            onSubmitPressed?.Invoke(CurrentPageIndex, isRightPage);
        }

        /// <summary>
        /// Ensure world camera is bound on all pooled boards.
        /// </summary>
        public void EnsureWorldCamera()
        {
            if (_leftBoard != null)
                _leftBoard.EnsureWorldCamera();

            if (_rightBoard != null)
                _rightBoard.EnsureWorldCamera();
        }

        /// <summary>
        /// Clears all saved progress across all diagrams in the book and resets boards.
        /// </summary>
        public void ClearAllSavedProgress()
        {
            _savedDiagramStates.Clear();
            ApplyCurrentSpread(false);
        }

        /// <summary>
        /// Finds the diagram currently being traced across the active boards or saved pages.
        /// Returns null if all diagrams are either unstarted (0%) or completed/submitted.
        /// </summary>
        public DiagramDataSO GetActiveInProgressDiagram(out string title)
        {
            // 1. Check live left board
            if (_leftBoard != null && _leftBoard.gameObject.activeSelf && _leftBoard.IsInProgress)
            {
                title = _leftBoard.CurrentDiagramData != null ? _leftBoard.CurrentDiagramData.diagramTitle : "Left Diagram";
                return _leftBoard.CurrentDiagramData;
            }

            // 2. Check live right board
            if (_rightBoard != null && _rightBoard.gameObject.activeSelf && _rightBoard.IsInProgress)
            {
                title = _rightBoard.CurrentDiagramData != null ? _rightBoard.CurrentDiagramData.diagramTitle : "Right Diagram";
                return _rightBoard.CurrentDiagramData;
            }

            // 3. Check saved states across other pages
            foreach (var kvp in _savedDiagramStates)
            {
                var data = kvp.Key;
                var state = kvp.Value;
                if (state != null && !state.isCompleted && !state.isSubmitted && (state.savedProgress > 0.0001f || state.hitLength > 0.0001f || state.hasDrawnContent))
                {
                    // If this diagram is currently loaded onto one of the live boards, skip here because the live board check already handled it
                    if ((_leftBoard != null && _leftBoard.CurrentDiagramData == data) ||
                        (_rightBoard != null && _rightBoard.CurrentDiagramData == data))
                    {
                        continue;
                    }
                    title = data != null ? data.diagramTitle : "Other Diagram";
                    return data;
                }
            }

            title = null;
            return null;
        }

        /// <summary>
        /// Enforces single active diagram mutual exclusion:
        /// When one diagram is in-progress, all other boards are locked until that diagram is completed or cleared.
        /// </summary>
        public void UpdateDiagramLockState()
        {
            var activeDiagram = GetActiveInProgressDiagram(out string activeTitle);

            if (activeDiagram != null)
            {
                // A diagram is currently in-progress!
                // Lock every board EXCEPT the one drawing that active diagram.
                if (_leftBoard != null && _leftBoard.gameObject.activeSelf)
                {
                    bool isLeftTheActiveOne = _leftBoard.CurrentDiagramData == activeDiagram;
                    _leftBoard.SetLockedByOther(!isLeftTheActiveOne, activeTitle);
                }

                if (_rightBoard != null && _rightBoard.gameObject.activeSelf)
                {
                    bool isRightTheActiveOne = _rightBoard.CurrentDiagramData == activeDiagram;
                    _rightBoard.SetLockedByOther(!isRightTheActiveOne, activeTitle);
                }
            }
            else
            {
                // No diagram is currently in-progress (all are either at 0% or completed/submitted).
                // Both boards are unlocked and ready to start tracing!
                if (_leftBoard != null && _leftBoard.gameObject.activeSelf)
                {
                    _leftBoard.SetLockedByOther(false);
                }

                if (_rightBoard != null && _rightBoard.gameObject.activeSelf)
                {
                    _rightBoard.SetLockedByOther(false);
                }
            }
        }
    }
}
