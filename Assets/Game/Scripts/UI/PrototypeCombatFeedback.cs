using System;
using ImmortalLoot.Equipment;
using UnityEngine;

namespace ImmortalLoot.UI
{
    public sealed class PrototypeCombatFeedback : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private AudioSource _source;
        private AudioClip _hit;
        private AudioClip _critical;
        private AudioClip _boss;
        private AudioClip _loot;
        private AudioClip _equip;
        private float _nextHitTime;
        private bool _bossPlayed;

        public int GeneratedClipCount => Count(_hit) + Count(_critical) + Count(_boss) + Count(_loot) + Count(_equip);

        public void Initialize()
        {
            if (_source != null) return;
            _source = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.42f;
            _hit = CreateCue("hit", 180f, 0.055f, 0.2f);
            _critical = CreateCue("critical", 520f, 0.11f, 0.36f, 180f);
            _boss = CreateCue("boss", 92f, 0.32f, 0.48f, 46f);
            _loot = CreateCue("loot", 660f, 0.16f, 0.3f, 990f);
            _equip = CreateCue("equip", 330f, 0.13f, 0.28f, 495f);
        }

        public void PlayHit(bool critical)
        {
            if (_source == null || Time.unscaledTime < _nextHitTime) return;
            _nextHitTime = Time.unscaledTime + (critical ? 0.06f : 0.12f);
            _source.PlayOneShot(critical ? _critical : _hit, critical ? 0.75f : 0.32f);
        }

        public void PlayBossAppearance()
        {
            if (_source == null || _bossPlayed) return;
            _bossPlayed = true;
            _source.PlayOneShot(_boss, 0.9f);
        }

        public void PlayLoot(EquipmentQuality quality)
        {
            if (_source == null) return;
            var volume = Mathf.Lerp(0.45f, 1f, (int)quality / (float)EquipmentQuality.Mythic);
            _source.pitch = 1f + (int)quality * 0.045f;
            _source.PlayOneShot(_loot, volume);
            _source.pitch = 1f;
        }

        public void PlayEquip()
        {
            if (_source != null) _source.PlayOneShot(_equip, 0.75f);
        }

        private static AudioClip CreateCue(string name, float startFrequency, float seconds, float amplitude, float endFrequency = 0f)
        {
            var samples = Math.Max(1, Mathf.CeilToInt(seconds * SampleRate));
            var data = new float[samples];
            var finish = endFrequency <= 0f ? startFrequency : endFrequency;
            var phase = 0d;
            for (var index = 0; index < samples; index++)
            {
                var progress = index / (float)samples;
                var frequency = Mathf.Lerp(startFrequency, finish, progress);
                phase += 2d * Math.PI * frequency / SampleRate;
                var envelope = Mathf.Pow(1f - progress, 2f);
                data[index] = (float)Math.Sin(phase) * amplitude * envelope;
            }
            var clip = AudioClip.Create("Generated_" + name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static int Count(UnityEngine.Object value) => value == null ? 0 : 1;
    }
}
