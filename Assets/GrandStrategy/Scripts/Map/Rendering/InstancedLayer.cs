using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Dibuja muchas copias de pocas mallas con GPU instancing (Graphics.RenderMeshInstanced), agrupadas por
    /// zonas con su caja envolvente: cada fotograma solo se envían las zonas visibles, y cada una en las tres
    /// posiciones del mundo (oeste, centro, este) para el mapa cíclico.
    /// </summary>
    public sealed class InstancedLayer
    {
        private const int MaxPerBatch = 1023;

        private sealed class Group
        {
            public Mesh Mesh;
            /// <summary>Malla simplificada para cuando la zona está lejos de la cámara (LOD); null = siempre Mesh.</summary>
            public Mesh FarMesh;
            public Bounds Bounds;
            public readonly List<Matrix4x4[]> Batches = new List<Matrix4x4[]>();
        }

        private readonly List<Group> _groups = new List<Group>();
        private readonly MaterialPropertyBlock[] _blocks = { new MaterialPropertyBlock(), new MaterialPropertyBlock(), new MaterialPropertyBlock() };

        public int InstanceCount { get; private set; }

        public void Add(Mesh mesh, List<Matrix4x4> matrices, Mesh farMesh = null)
        {
            if (mesh == null || matrices == null || matrices.Count == 0) return;
            var g = new Group { Mesh = mesh, FarMesh = farMesh };
            var meshBounds = mesh.bounds;
            bool first = true;
            for (int start = 0; start < matrices.Count; start += MaxPerBatch)
            {
                int n = Mathf.Min(MaxPerBatch, matrices.Count - start);
                var batch = new Matrix4x4[n];
                for (int i = 0; i < n; i++)
                {
                    var m = matrices[start + i];
                    batch[i] = m;
                    var pos = m.GetColumn(3);
                    float scale = m.lossyScale.x;
                    var b = new Bounds(new Vector3(pos.x, pos.y, pos.z) + meshBounds.center * scale, meshBounds.size * scale);
                    if (first) { g.Bounds = b; first = false; }
                    else g.Bounds.Encapsulate(b);
                }
                g.Batches.Add(batch);
            }
            // El shader eleva cada instancia hasta la altura del terreno.
            g.Bounds.Expand(new Vector3(0, 4f, 0));
            g.Bounds.center += Vector3.up * 2f;
            _groups.Add(g);
            InstanceCount += matrices.Count;
        }

        public void Render(Material material, Camera camera, float worldWidth, Plane[] frustum, bool wrap = true,
            Material farMaterial = null, float lodDistance = 0f)
        {
            if (material == null || camera == null || _groups.Count == 0) return;
            var camPos = camera.transform.position;
            float lod2 = lodDistance * lodDistance;
            float[] offsets = wrap ? new[] { 0f, -worldWidth, worldWidth } : new[] { 0f };
            for (int o = 0; o < offsets.Length; o++)
            {
                _blocks[o].SetFloat("_CopyOffsetX", offsets[o]);
                var rp = MakeParams(material, o, camera);
                var rpFar = farMaterial != null ? MakeParams(farMaterial, o, camera) : rp;
                var shift = new Vector3(offsets[o], 0, 0);
                foreach (var g in _groups)
                {
                    var b = new Bounds(g.Bounds.center + shift, g.Bounds.size);
                    if (!GeometryUtility.TestPlanesAABB(frustum, b)) continue;
                    bool far = g.FarMesh != null && b.SqrDistance(camPos) > lod2;
                    var p = far ? rpFar : rp;
                    p.worldBounds = b;
                    var mesh = far ? g.FarMesh : g.Mesh;
                    foreach (var batch in g.Batches)
                        Graphics.RenderMeshInstanced(p, mesh, 0, batch, batch.Length);
                }
            }
        }

        private RenderParams MakeParams(Material material, int copy, Camera camera) => new RenderParams(material)
        {
            matProps = _blocks[copy],
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            camera = camera,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
        };
    }
}
