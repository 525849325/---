using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ImmortalLoot.Debugging
{
    public sealed class PlaytestTelemetryRecorder : MonoBehaviour
    {
        private const float SampleIntervalSeconds = 300f;
        private float _elapsed;
        private float _nextSample;
        private int _frames;
        private float _frameSeconds;
        private string _path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Debug.isDebugBuild || Application.isBatchMode && DevelopmentPlaytestOptions.Speed <= 1f || FindAnyObjectByType<PlaytestTelemetryRecorder>() != null) return;
            DontDestroyOnLoad(new GameObject("PlaytestTelemetryRecorder").AddComponent<PlaytestTelemetryRecorder>().gameObject);
        }

        private void Awake()
        {
            _path = Path.Combine(Application.persistentDataPath, "immortal-loot-playtest.jsonl");
            _nextSample = SampleIntervalSeconds / DevelopmentPlaytestOptions.Speed;
            Write("session_start");
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            _elapsed += delta; _frameSeconds += delta; _frames++;
            if (_elapsed < _nextSample) return;
            Write("five_minute_sample");
            _frames = 0; _frameSeconds = 0f; _nextSample += SampleIntervalSeconds / DevelopmentPlaytestOptions.Speed;
        }

        private void OnApplicationQuit() => Write("session_end");

        private void Write(string eventName)
        {
            try
            {
                var fps = _frameSeconds > 0f ? _frames / _frameSeconds : 0f;
                var json = JsonUtility.ToJson(new Sample
                {
                    eventName = eventName, utc = DateTime.UtcNow.ToString("O"), elapsedSeconds = _elapsed,
                    simulatedElapsedSeconds = _elapsed * DevelopmentPlaytestOptions.Speed,
                    scene = SceneManager.GetActiveScene().name, averageFps = fps,
                    allocatedMemoryMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f
                });
                File.AppendAllText(_path, json + Environment.NewLine);
            }
            catch (Exception exception) { Debug.LogWarning("Playtest telemetry write failed: " + exception.Message); }
        }

        [Serializable]
        private sealed class Sample
        {
            public string eventName; public string utc; public float elapsedSeconds; public float simulatedElapsedSeconds; public string scene;
            public float averageFps; public float allocatedMemoryMb;
        }
    }
}
