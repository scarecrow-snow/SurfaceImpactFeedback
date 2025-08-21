using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SCLib_SurfaceImpactFeedback.Effects
{
    [CreateAssetMenu(menuName = "Surface Impact Feedback/Play Audio Effect", fileName = "PlayAudioEffect")]
    public class PlayAudioEffect : ScriptableObject
    {
        public AudioMixerGroup audioMixerGroup;
        public List<AudioClip> AudioClips = new List<AudioClip>();
        [Tooltip("Values are clamped to 0-1")]
        public Vector2 VolumeRange = new Vector2(0, 1);
    }
}