using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Engine sound: one looping clip (Assets/Audio/EngineLoop.wav, synthesized by Tools/gen_engine_loop.py).
    /// Pitch and volume follow |speed| / top speed, smoothed; a quiet idle while the driver is in and the car stands;
    /// silent (fades out) when nobody is driving. No allocations per frame.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineAudio : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [SerializeField] CarController car;

        AudioSource source;
        float level;    // smoothed 0..1 speed fraction
        float volume;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool on = car.InputEnabled;
            float target = Mathf.Clamp01(Mathf.Abs(car.Speed) / Mathf.Max(0.1f, car.MaxSpeed));
            level = Mathf.Lerp(level, target, 1f - Mathf.Exp(-config.engineResponse * dt));
            float targetVol = on ? Mathf.Lerp(config.engineIdleVolume, config.engineMaxVolume, level) : 0f;
            volume = Mathf.MoveTowards(volume, targetVol, dt * 2f);
            source.volume = volume;
            source.pitch = Mathf.Lerp(config.engineIdlePitch, config.engineMaxPitch, level);
            if (volume > 0.001f) { if (!source.isPlaying) source.Play(); }
            else if (source.isPlaying) source.Stop();
        }
    }
}
