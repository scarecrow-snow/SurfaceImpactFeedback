
using UnityEngine;
using UnityEngine.Audio;

namespace SCLib_SurfaceImpactFeedback
{
    public interface IImpactSound
    {
        public void Play(AudioClip clip, AudioMixerGroup mixerGroup, Vector3 point);
    }
}