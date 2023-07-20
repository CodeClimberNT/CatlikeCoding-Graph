using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;
using Random = UnityEngine.Random;


public class Fractal : MonoBehaviour {
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    private struct UpdateFractalLevelJob : IJobFor {
        public float SpinAngleDelta;
        public float Scale;

        [ReadOnly]
        public NativeArray<FractalPart> Parents;

        public NativeArray<FractalPart> Parts;

        [WriteOnly]
        public NativeArray<float3x4> Matrices;

        public void Execute(int i) {
            FractalPart parent = Parents[i / 5];
            FractalPart part = Parts[i];
            part.SpinAngle += SpinAngleDelta;
            part.WorldRotation = mul(parent.WorldRotation,
                mul(part.Rotation, quaternion.Euler(0f, part.SpinAngle, 0f)));
            part.WorldPosition =
                parent.WorldPosition +
                mul(parent.WorldRotation, 1.5f * Scale * part.Direction);
            Parts[i] = part;
            float3x3 r = float3x3(part.WorldRotation) * Scale;
            Matrices[i] = float3x4(r.c0, r.c1, r.c2, part.WorldPosition);
        }
    }

    private struct FractalPart {
        public float3 Direction, WorldPosition;
        public quaternion Rotation, WorldRotation;
        public float SpinAngle;
    }

    private NativeArray<FractalPart>[] _parts;
    private NativeArray<float3x4>[] _matrices;

    [SerializeField, Range(2, 9)]
    private int depth = 4;

    [SerializeField]
    private Mesh mesh;

    [SerializeField]
    private Material material;

    [SerializeField]
    private Gradient gradient;

    private static MaterialPropertyBlock _propertyBlock;

    private static readonly float3[] Directions = new[] {
        up(),
        right(),
        left(),
        forward(),
        back()
    };

    private static readonly quaternion[] Rotations = {
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI),
        quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(-0.5f * PI),
        quaternion.RotateX(0.5f * PI)
    };

    private ComputeBuffer[] _matricesBuffers;
    private static readonly int MatricesId = Shader.PropertyToID("_Matrices");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SequenceNumbersId = Shader.PropertyToID("_SequenceNumbers");

    private Vector4[] sequenceNumbers;

    private void OnEnable() {
        _parts = new NativeArray<FractalPart>[depth];
        _matrices = new NativeArray<float3x4>[depth];
        _matricesBuffers = new ComputeBuffer[depth];

        sequenceNumbers = new Vector4[depth];
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < _parts.Length; i++, length *= 5) {
            _parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            _matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            _matricesBuffers[i] = new ComputeBuffer(length, stride);
            sequenceNumbers[i] = new Vector4(Random.value, Random.value);
        }


        _parts[0][0] = CreatePart(0);
        for (int li = 1; li < _parts.Length; li++) {
            var levelParts = _parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                levelParts[fpi] = CreatePart(fpi % 5);
            }
        }

        _propertyBlock ??= new MaterialPropertyBlock();
    }

    private void OnDisable() {
        for (int i = 0; i < _matricesBuffers.Length; i++) {
            _matricesBuffers[i].Release();
            _parts[i].Dispose();
            _matrices[i].Dispose();
        }

        _parts = null;
        _matrices = null;
        _matricesBuffers = null;
        sequenceNumbers = null;
    }

    private void OnValidate() {
        if (_parts != null && enabled) {
            OnDisable();
            OnEnable();
        }
    }

    private static FractalPart CreatePart(int childIndex) =>
        new() {
            Direction = Directions[childIndex],
            Rotation = Rotations[childIndex]
        };


    private void Update() {
        float spinAngleDelta = 0.125f * PI * Time.deltaTime;
        FractalPart rootPart = _parts[0][0];
        rootPart.SpinAngle += spinAngleDelta;
        rootPart.WorldRotation =
            mul(transform.rotation, mul(rootPart.Rotation, quaternion.RotateY(rootPart.SpinAngle)));
        rootPart.WorldPosition = transform.position;
        _parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;

        float3x3 r = float3x3(rootPart.WorldRotation) * objectScale;
        _matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.WorldPosition);

        float scale = objectScale;
        JobHandle jobHandle = default;
        for (int li = 1; li < _parts.Length; li++) {
            scale *= 0.5f;
            jobHandle = new UpdateFractalLevelJob {
                SpinAngleDelta = spinAngleDelta,
                Scale = scale,
                Parents = _parts[li - 1],
                Parts = _parts[li],
                Matrices = _matrices[li]
            }.ScheduleParallel(_parts[li].Length, 5, jobHandle);
        }

        jobHandle.Complete();

        var bounds = new Bounds(rootPart.WorldPosition, 3f * objectScale * Vector3.one);
        for (int i = 0; i < _matricesBuffers.Length; i++) {
            ComputeBuffer buffer = _matricesBuffers[i];
            buffer.SetData(_matrices[i]);
            _propertyBlock.SetColor(
                BaseColorId,
                gradient.Evaluate(i / (_matricesBuffers.Length - 1f))
            );
            _propertyBlock.SetBuffer(MatricesId, buffer);
            _propertyBlock.SetVector(SequenceNumbersId, sequenceNumbers[i]);
            material.SetBuffer(MatricesId, buffer);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, _propertyBlock);
        }
    }
}