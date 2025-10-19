using UnityEngine;
namespace SCLib_SurfaceImpactFeedback
{
    public interface ISurfaceImpactFeedback
    {
        void HandleImpact(Collider HitCollider, in Vector3 HitPoint, in Vector3 HitNormal, ImpactType Impact, int TriangleIndex = 0);
    }
}