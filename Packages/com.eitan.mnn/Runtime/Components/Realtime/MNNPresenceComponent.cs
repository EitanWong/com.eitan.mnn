using System;
using UnityEngine;
using UnityEngine.Events;

namespace MNN.Unity
{
    /// <summary>Opt-in main-thread adapter. Keep this component outside the UI it fades.
    /// Feed timestamped results from a camera/tracker; never run model inference in Update or an audio callback.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Presence Interaction")]
    public sealed class MNNPresenceComponent : MonoBehaviour
    {
        [Serializable] public sealed class AdmissionEvent : UnityEvent<bool> { }
        [SerializeField] private CanvasGroup _content;
        [SerializeField] private CanvasGroup _blackScreen;
        [SerializeField] private MNNPresenceSleepDisplay _sleepDisplay;
        [SerializeField, Min(.01f)] private float _fadeInSeconds = .25f;
        [SerializeField, Min(.01f)] private float _fadeOutSeconds = .8f;
        [SerializeField] private AdmissionEvent _onAdmissionChanged = new AdmissionEvent();
        [SerializeField] private UnityEvent _onSleep = new UnityEvent();
        [SerializeField] private UnityEvent _onWake = new UnityEvent();
        private MNNPresenceController _controller;
        private MNNPresenceOptions _options;
        private string[] _allowedIdentities;
        private bool _busy;
        private MNNPresenceState _previousState = MNNPresenceState.Sleeping;
        private bool _previousAdmission;
        private float _contentAlpha, _blackAlpha;
        private bool _contentInteraction, _contentRaycasts, _blackInteraction, _blackRaycasts, _saved;
        private Transform _visualRoot;
        private Vector3 _visualScale;
        private float _visualAmount = 1;

        public MNNPresenceSnapshot Current => Controller.Current;
        public event Action<MNNPresenceSnapshot> Changed;
        private MNNPresenceController Controller => _controller ?? (_controller = new MNNPresenceController(_options, _allowedIdentities));

        /// <summary>Configure on the Unity main thread. An empty allow-list admits nobody; null accepts any qualifying face.</summary>
        public void Configure(MNNPresenceOptions options, string[] allowedIdentities = null)
        {
            _options = options;
            _allowedIdentities = allowedIdentities == null ? null : (string[])allowedIdentities.Clone();
            _controller = new MNNPresenceController(_options, _allowedIdentities);
            _busy = false;
            Publish();
        }

        public void SetInteractionBusy(bool busy) => _busy = busy;

        public void ConfigurePresentation(CanvasGroup content, CanvasGroup blackScreen, MNNPresenceSleepDisplay sleepDisplay)
        {
            _content = content; _blackScreen = blackScreen; _sleepDisplay = sleepDisplay;
        }

        public void ConfigureVisualRoot(Transform visualRoot)
        {
            if (_visualRoot != null) _visualRoot.localScale = _visualScale;
            _visualRoot = visualRoot;
            if (_visualRoot != null) _visualScale = _visualRoot.localScale;
            _visualAmount = 1;
        }

        public void Report(MNNPresenceObservation observation)
        {
            Controller.Observe(observation, Time.realtimeSinceStartupAsDouble, _busy);
            Publish();
        }

        private void OnEnable()
        {
            if (_content != null)
            { _contentAlpha = _content.alpha; _contentInteraction = _content.interactable; _contentRaycasts = _content.blocksRaycasts; }
            if (_blackScreen != null)
            { _blackAlpha = _blackScreen.alpha; _blackInteraction = _blackScreen.interactable; _blackRaycasts = _blackScreen.blocksRaycasts; }
            _saved = true;
            _controller = null;
            Publish();
            if (_content != null) { _content.alpha = 0; _content.interactable = false; _content.blocksRaycasts = false; }
        }

        private void Update()
        {
            Controller.Tick(Time.realtimeSinceStartupAsDouble, _busy);
            Publish();
            float target = Current.PresentationTarget;
            if (Current.State == MNNPresenceState.Sleeping && _sleepDisplay == MNNPresenceSleepDisplay.Dimmed) target = .1f;
            Fade(_content, target, Current.CanStartInteraction);
            Fade(_blackScreen, Current.State == MNNPresenceState.Sleeping && _sleepDisplay == MNNPresenceSleepDisplay.BlackScreen ? 1 : 0, false);
            if (_visualRoot != null)
            {
                float duration = target > _visualAmount ? _fadeInSeconds : _fadeOutSeconds;
                _visualAmount = Mathf.MoveTowards(_visualAmount, target, Time.unscaledDeltaTime / Mathf.Max(.01f, duration));
                _visualRoot.localScale = _visualScale * _visualAmount;
            }
        }

        private void Fade(CanvasGroup group, float target, bool interactive)
        {
            if (group == null) return;
            float duration = target > group.alpha ? _fadeInSeconds : _fadeOutSeconds;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(.01f, duration));
            group.interactable = interactive && group.alpha >= .95f;
            group.blocksRaycasts = group.interactable;
        }

        private void Publish()
        {
            var value = Current;
            bool stateChanged = value.State != _previousState;
            bool admissionChanged = value.CanStartInteraction != _previousAdmission;
            bool wasConfirmed = _previousState == MNNPresenceState.Present || _previousState == MNNPresenceState.Grace;
            _previousState = value.State;
            _previousAdmission = value.CanStartInteraction;
            if (admissionChanged) _onAdmissionChanged.Invoke(value.CanStartInteraction);
            if (stateChanged && value.State == MNNPresenceState.Sleeping) _onSleep.Invoke();
            if (stateChanged && value.State == MNNPresenceState.Present && !wasConfirmed) _onWake.Invoke();
            if (stateChanged || admissionChanged) Changed?.Invoke(value);
        }

        private void OnDisable()
        {
            _controller = null;
            _busy = false;
            Publish();
            if (!_saved) return;
            if (_content != null)
            { _content.alpha = _contentAlpha; _content.interactable = _contentInteraction; _content.blocksRaycasts = _contentRaycasts; }
            if (_blackScreen != null)
            { _blackScreen.alpha = _blackAlpha; _blackScreen.interactable = _blackInteraction; _blackScreen.blocksRaycasts = _blackRaycasts; }
            _saved = false;
            if (_visualRoot != null) _visualRoot.localScale = _visualScale;
        }
    }
}
