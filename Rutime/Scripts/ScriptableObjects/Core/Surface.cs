using System;
using System.Collections.Generic;
using UnityEngine;
using SCLib_SurfaceImpactFeedback.Effects;

namespace SCLib_SurfaceImpactFeedback
{
    [CreateAssetMenu(menuName = "Surface Impact Feedback/Surface", fileName = "Surface")]
    public class Surface : ScriptableObject
    {
        [Serializable]
        public class SurfaceImpactTypeEffect
        {
            public ImpactType ImpactType;
            public SurfaceEffect SurfaceEffect;
        }
        public List<SurfaceImpactTypeEffect> ImpactTypeEffects = new List<SurfaceImpactTypeEffect>();
    }
}