using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>Clips the actual glyph triangles, so each piece is half of the same number.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class DiagonalHealthHalf : BaseMeshEffect
    {
        [SerializeField] bool upperHalf;
        readonly List<UIVertex> source = new List<UIVertex>();
        readonly UIVertex[] polygon = new UIVertex[4];

        public bool UpperHalf
        {
            get => upperHalf;
            set { if (upperHalf == value) return; upperHalf = value; graphic.SetVerticesDirty(); }
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            source.Clear();
            mesh.GetUIVertexStream(source);
            var minimum = source[0].position;
            var maximum = minimum;
            foreach (var vertex in source)
            {
                minimum = Vector3.Min(minimum, vertex.position);
                maximum = Vector3.Max(maximum, vertex.position);
            }
            var center = (minimum + maximum) * .5f;
            float slope = (maximum.y - minimum.y) * .65f / Mathf.Max(.001f, maximum.x - minimum.x);
            mesh.Clear();
            for (int triangle = 0; triangle + 2 < source.Count; triangle += 3)
            {
                int count = 0;
                var previous = source[triangle + 2];
                float previousDistance = Distance(previous.position, center, slope);
                for (int corner = 0; corner < 3; corner++)
                {
                    var current = source[triangle + corner];
                    float currentDistance = Distance(current.position, center, slope);
                    if ((previousDistance >= 0) != (currentDistance >= 0))
                    {
                        float fraction = previousDistance / (previousDistance - currentDistance);
                        polygon[count++] = Interpolate(previous, current, fraction);
                    }
                    if (currentDistance >= 0) polygon[count++] = current;
                    previous = current;
                    previousDistance = currentDistance;
                }
                int start = mesh.currentVertCount;
                for (int vertex = 0; vertex < count; vertex++) mesh.AddVert(polygon[vertex]);
                for (int vertex = 1; vertex + 1 < count; vertex++)
                    mesh.AddTriangle(start, start + vertex, start + vertex + 1);
            }
        }

        float Distance(Vector3 point, Vector3 center, float slope)
        {
            float distance = point.y - center.y - (point.x - center.x) * slope;
            return upperHalf ? distance : -distance;
        }

        static UIVertex Interpolate(UIVertex a, UIVertex b, float fraction)
        {
            var result = a;
            result.position = Vector3.LerpUnclamped(a.position, b.position, fraction);
            result.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, fraction);
            result.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, fraction);
            result.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, fraction);
            result.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, fraction);
            result.color = Color32.Lerp(a.color, b.color, fraction);
            return result;
        }
    }
}
