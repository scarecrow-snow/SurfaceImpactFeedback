using System.Collections.Generic;
using UnityEngine;

namespace SCLib_SurfaceImpactFeedback.Effects
{
    [CreateAssetMenu(menuName = "Surface Impact Feedback/Surface Effect", fileName = "SurfaceEffect")]
    public class SurfaceEffect : ScriptableObject
    {
        [SerializeField]
        private List<SpawnObjectEffect> spawnObjectEffects = new List<SpawnObjectEffect>();
        
        [SerializeField]
        private List<PlayAudioEffect> playAudioEffects = new List<PlayAudioEffect>();

        public IReadOnlyList<SpawnObjectEffect> SpawnObjectEffects => spawnObjectEffects;
        public IReadOnlyList<PlayAudioEffect> PlayAudioEffects => playAudioEffects;
    }
}