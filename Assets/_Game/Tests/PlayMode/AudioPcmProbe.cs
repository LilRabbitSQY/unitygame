using System;
using UnityEngine;

namespace FinalDefense.Tests
{
    // Test-only native mixer callback. Never reads Unity objects from the audio
    // thread and never changes samples; it measures the actual PCM stream.
    public sealed class AudioPcmProbe : MonoBehaviour
    {
        private readonly object gate = new object();
        private double sumSquares;
        private long sampleCount, blocks;
        private int channelCount;
        private void OnAudioFilterRead(float[] data, int channels)
        {
            double sum = 0;
            for (int i=0;i<data.Length;i++) sum += (double)data[i]*data[i];
            lock(gate) { sumSquares+=sum;sampleCount+=data.Length;blocks++;channelCount=channels; }
        }
        public void ResetCapture() { lock(gate) { sumSquares=0;sampleCount=0;blocks=0;channelCount=0; } }
        public Snapshot Read() { lock(gate) return new Snapshot { rms=sampleCount==0?0:Math.Sqrt(sumSquares/sampleCount),samples=sampleCount,blocks=blocks,channels=channelCount }; }
        public struct Snapshot { public double rms;public long samples,blocks;public int channels; }
    }
}
