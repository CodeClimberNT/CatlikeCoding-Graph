using UnityEngine;

[RequireComponent(typeof(Transform))]
public class Graph : MonoBehaviour {
    [SerializeField] private Transform pointPrefab;

    [SerializeField, Range(10, 100)] private byte resolution = 127;

    [SerializeField] private FunctionLibrary.FunctionName function;

    private enum TransitionMode {
        Cycle,
        Random
    }

    [SerializeField] private TransitionMode _transitionMode;

    [SerializeField, Min(0f)] private float functionDuration = 1f, transitionDuration = 1f;

    private Transform[] _points;

    private float _duration;
    private bool _transitioning;
    private FunctionLibrary.FunctionName _transitionFunction;

    private void Awake() {
        float step = 2f / resolution;

        var scale = Vector3.one * step;

        _points = new Transform[resolution * resolution];

        for (int i = 0; i < _points.Length; i++) {
            Transform point = _points[i] = Instantiate(pointPrefab);

            point.localScale = scale;
            point.SetParent(transform, false);
        }
    }

    private void Update() {
        _duration += Time.deltaTime;
        if (_transitioning) {
            if (_duration >= transitionDuration) {
                _duration -= transitionDuration;
                _transitioning = false;
            }
        }
        else if (_duration >= functionDuration) {
            _duration -= functionDuration;
            _transitioning = true;
            _transitionFunction = function;
            PickNextFunction();
        }

        if (_transitioning) {
            UpdateFunctionTransition();
        }
        else {
            UpdateFunction();
        }
    }

    private void PickNextFunction() {
        function = _transitionMode == TransitionMode.Cycle
            ? FunctionLibrary.GetNextFunctionName(function)
            : FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }

    private void UpdateFunction() {
        var f = FunctionLibrary.GetFunction(function);

        float time = Time.time;
        float step = 2f / resolution;

        float v = 0.5f * step - 1f;

        for (int i = 0, x = 0, z = 0; i < _points.Length; i++, x++) {
            if (x == resolution) {
                x = 0;
                z += 1;
                v = (z + 0.5f) * step - 1f;
            }

            float u = (x + 0.5f) * step - 1f;

            _points[i].localPosition = f(u, v, time);
        }
    }

    private void UpdateFunctionTransition() {
        FunctionLibrary.Function from = FunctionLibrary.GetFunction(_transitionFunction),
            to = FunctionLibrary.GetFunction(function);
        float progress = _duration / transitionDuration;
        float time = Time.time;
        float step = 2f / resolution;

        float v = 0.5f * step - 1f;

        for (int i = 0, x = 0, z = 0; i < _points.Length; i++, x++) {
            if (x == resolution) {
                x = 0;
                z += 1;
                v = (z + 0.5f) * step - 1f;
            }

            float u = (x + 0.5f) * step - 1f;

            _points[i].localPosition = FunctionLibrary.Morph(u, v, time, from, to, progress);
        }
    }
}