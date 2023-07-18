using System;
using UnityEngine;

public class Fractal : MonoBehaviour {
    private struct FractalPart {
        public Vector3 Direction, WorldPosition;
        public Quaternion Rotation, WorldRotation;
        public float SpinAngle;
    }

    private FractalPart[][] _parts;
    private Matrix4x4[][] _matrices;

    [SerializeField, Range(1, 8)]
    private int depth = 4;

    [SerializeField]
    private Mesh mesh;

    [SerializeField]
    private Material material;

    static MaterialPropertyBlock propertyBlock;

    private static Vector3[] _directions = {
        Vector3.up,
        Vector3.right,
        Vector3.left,
        Vector3.forward,
        Vector3.back
    };

    private static Quaternion[] _rotations = {
        Quaternion.identity,
        Quaternion.Euler(0f, 0f, -90f),
        Quaternion.Euler(0f, 0f, 90f),
        Quaternion.Euler(90f, 0f, 0f),
        Quaternion.Euler(-90f, 0f, 0f)
    };

    private ComputeBuffer[] _matricesBuffers;
    private static readonly int matricesId = Shader.PropertyToID("_Matrices");

    private void OnEnable() {
        _parts = new FractalPart[depth][];
        _matrices = new Matrix4x4[depth][];
        _matricesBuffers = new ComputeBuffer[depth];
        int stride = 16 * 4;
        for (int i = 0, length = 1; i < _parts.Length; i++, length *= 5) {
            _parts[i] = new FractalPart[length];
            _matrices[i] = new Matrix4x4[length];
            _matricesBuffers[i] = new ComputeBuffer(length, stride);
        }


        _parts[0][0] = CreatePart(0);
        for (int li = 1; li < _parts.Length; li++) {
            FractalPart[] levelParts = _parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                levelParts[fpi] = CreatePart(fpi % 5);
            }
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void OnDisable() {
        for (int i = 0; i < _matricesBuffers.Length; i++) {
            _matricesBuffers[i].Release();
        }
        _parts = null;
        _matrices = null;
        _matricesBuffers = null;
    }

    private void OnValidate() {
        if (_parts != null && enabled) {
            OnDisable();
            OnEnable();
        }
    }

    private static FractalPart CreatePart(int childIndex) =>
        new() {
            Direction = _directions[childIndex],
            Rotation = _rotations[childIndex]
        };


    private void Update() {
        float spinAngleDelta = 22.5f * Time.deltaTime;
        FractalPart rootPart = _parts[0][0];
        rootPart.SpinAngle += spinAngleDelta;
        rootPart.WorldRotation = transform.rotation * (rootPart.Rotation * Quaternion.Euler(0f, rootPart.SpinAngle, 0f));
        rootPart.WorldPosition = transform.position;
        _parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;
        _matrices[0][0] = Matrix4x4.TRS(rootPart.WorldPosition, rootPart.WorldRotation, objectScale * Vector3.one);

        float scale = objectScale;
        for (int li = 1; li < _parts.Length; li++) {
            scale *= 0.5f;
            FractalPart[] parentParts = _parts[li - 1];
            FractalPart[] levelParts = _parts[li];
            Matrix4x4[] levelMatrices = _matrices[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                FractalPart parent = parentParts[fpi / 5];
                FractalPart part = levelParts[fpi];
                part.SpinAngle += spinAngleDelta;
                part.WorldRotation = parent.WorldRotation *
                                     (rootPart.Rotation * Quaternion.Euler(0f, rootPart.SpinAngle, 0f));
                part.WorldPosition =
                    parent.WorldPosition +
                    parent.WorldRotation * (1.5f * scale * part.Direction);
                levelParts[fpi] = part;
                levelMatrices[fpi] = Matrix4x4.TRS(part.WorldPosition, part.WorldRotation, scale * Vector3.one);
            }
        }

        var bounds = new Bounds(rootPart.WorldPosition, 3f * objectScale * Vector3.one);
        for (int i = 0; i < _matricesBuffers.Length; i++) {
            ComputeBuffer buffer = _matricesBuffers[i];
            buffer.SetData(_matrices[i]);
            propertyBlock.SetBuffer(matricesId, buffer);
            material.SetBuffer(matricesId, buffer);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
        }

    }
}