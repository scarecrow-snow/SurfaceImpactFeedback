using UnityEngine;
namespace SCLib_SurfaceImpactFeedback
{
    public interface ISurfaceImpactFeedback
    {
        void HandleImpact(GameObject HitObject, in Vector3 HitPoint, in Vector3 HitNormal, ImpactType Impact, int TriangleIndex = 0);
    }
}