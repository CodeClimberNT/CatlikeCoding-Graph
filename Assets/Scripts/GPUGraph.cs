using UnityEngine;


[RequireComponent(typeof(Transform))]
public class GPUGraph : MonoBehaviour {
    [SerializeField]
    private ComputeShader computeShader;

    private static readonly int PositionsId = Shader.PropertyToID("_Positions"),
        ResolutionId = Shader.PropertyToID("_Resolution"),
        StepId = Shader.PropertyToID("_Step"),
        TimeId = Shader.PropertyToID("_Time"),
        TransitionProgressId = Shader.PropertyToID("_TransitionProgress");

    [SerializeField]
    private Material material;

    [SerializeField]
    private Mesh mesh;

    private const int MaxResolution = 1000;

    [SerializeField, Range(10, MaxResolution)]
    private int resolution = 100;

    [SerializeField]
    private FunctionLibrary.FunctionName function;

    private enum TransitionMode {
        Cycle,
        Random
    }

    [SerializeField]
    private TransitionMode transitionMode;

    [SerializeField, Min(0f)]
    private float functionDuration = 1f, transitionDuration = 1f;


    private float _duration;
    private bool _transitioning;
    private FunctionLibrary.FunctionName _transitionFunction;

    private ComputeBuffer _positionBuffer;

    private void OnEnable() {
        _positionBuffer = new ComputeBuffer(MaxResolution * MaxResolution, 3 * 4);
    }

    private void OnDisable() {
        _positionBuffer.Release();
        _positionBuffer = null;
    }

    private void Update() {
        _duration += Time.deltaTime;

        if (_transitioning && _duration >= transitionDuration) {
            _duration -= transitionDuration;
            _transitioning = false;
        }

        else if (_duration >= functionDuration) {
            _duration -= functionDuration;
            _transitioning = true;
            _transitionFunction = function;
            PickNextFunction();
        }

        UpdateFunctionOnGPU();
    }

    private void UpdateFunctionOnGPU() {
        float step = 2f / resolution;
        computeShader.SetInt(ResolutionId, resolution);
        computeShader.SetFloat(StepId, step);
        computeShader.SetFloat(TimeId, Time.time);

        if (_transitioning) {
            computeShader.SetFloat(
                TransitionProgressId,
                Mathf.SmoothStep(0f, 1f, _duration / transitionDuration)
            );
        }

        var kernelIndex =
            (int)function +
            (int)(_transitioning ? _transitionFunction : function) * FunctionLibrary.FunctionCount;
        computeShader.SetBuffer(kernelIndex, PositionsId, _positionBuffer);

        int groups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(kernelIndex, groups, groups, 1);

        material.SetBuffer(PositionsId, _positionBuffer);
        material.SetFloat(StepId, step);

        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution * resolution);
    }

    private void PickNextFunction() {
        function = transitionMode == TransitionMode.Cycle
            ? FunctionLibrary.GetNextFunctionName(function)
            : FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }
}