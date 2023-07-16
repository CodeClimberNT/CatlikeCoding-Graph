using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using TMPro;


public class FrameRateCounter : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI display;

    public enum DisplayMode {
        FPS,
        MS
    }

    [SerializeField] private DisplayMode _displayMode = DisplayMode.FPS;

    [SerializeField, Range(0.1f, 2f)] private float sampleDuration = 0.5f;

    private uint _frames;

    private float _duration, _bestDuration = float.MaxValue, _worstDuration = 0f;

    [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
    //strange error with string interpolation, suppressed for now
    private void Update() {
        float frameDuration = Time.unscaledDeltaTime;
        _frames += 1;
        _duration += frameDuration;

        if (frameDuration < _bestDuration) _bestDuration = frameDuration;

        if (frameDuration > _worstDuration) _worstDuration = frameDuration;

        if (_duration >= sampleDuration) {
            switch (_displayMode) {
                case DisplayMode.FPS:
                    display.SetText(
                        $"FPS\n{1f / _bestDuration:F0}\n" +
                        $"{_frames / _duration:F0}\n" +
                        $"{1f / _worstDuration:F0}");

                    break;
                case DisplayMode.MS:
                    display.SetText(
                        $"FPS\n{100f * _bestDuration:F1}\n" +
                        $"{100f * (_duration / _frames):F1}\n" +
                        $"{100f * _worstDuration:F1}");
                    break;
            }

            _frames = 0;
            _duration = 0f;
            _bestDuration = float.MaxValue;
            _worstDuration = 0f;
        }
    }
}