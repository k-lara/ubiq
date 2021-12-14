using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Avatars;

namespace Ubiq.Samples
{
    /// <summary>
    /// Displays an audio volume indicator for just-played audio samples from
    /// the peer associated with this avatar
    /// </summary>
    public class SpeechIndicator : MonoBehaviour
    {
        public enum Mode
        {
            Current,
            History
        }

        public Mode mode;

        public List<Transform> volumeIndicators;
        public Vector3 minIndicatorScale;
        public Vector3 maxIndicatorScale;
        public float sampleSecondsPerIndicator;

        public float minVolume;
        public float maxVolume;

        private Avatars.Avatar avatar;
        private VoipAvatar voipAvatar;
        private AudioSource replayAudioSource;
        private int lastSampleTimeMilliseconds;

        private float currentFrameVolumeSum = 0;
        private int currentFrameSampleCount = 0;
        private float[] volumeFrames;

        // for replay
        private int deltaTimeSamples = 0;
        private int absTimeSamples = 0;
        private int lastTimeSamples = 0;
        private float volume = 0.0f;
        private int samples = 0;

        private void Update()
        {
            if(replayAudioSource)
            {
                (volume, samples) = GetStatsForReplay();
            }
        }
        private (float, int) GetStatsForReplay()
        {
            if (absTimeSamples < 0)
            {
                absTimeSamples = replayAudioSource.timeSamples;
                lastTimeSamples = replayAudioSource.timeSamples;
            }
            else
            {
                var deltaTimeSamples = replayAudioSource.timeSamples - lastTimeSamples;
                if (deltaTimeSamples < 0)
                {
                    deltaTimeSamples += replayAudioSource.clip.samples;
                }
                var volume = 0.0f;
                if (deltaTimeSamples > 0)
                {
                    var floatPcms = new float[deltaTimeSamples];

                    // Gather volume for this set of stats
                    replayAudioSource.clip.GetData(floatPcms, lastTimeSamples);
                    for (int i = 0; i < floatPcms.Length; i++)
                    {
                        volume += Mathf.Abs(floatPcms[i]);
                        floatPcms[i] = 0;
                    }
                }

                // Update time trackers
                absTimeSamples += deltaTimeSamples;
                lastTimeSamples = replayAudioSource.timeSamples;

                // Calculate stats for the advance
                return (volume, deltaTimeSamples);
            }

            return (0, 0);
        }

        public void SetReplayAudioSource(AudioSource audioSource)
        {
            replayAudioSource = audioSource;
        }

        private void Start()
        {
            avatar = GetComponentInParent<Avatars.Avatar>();
            voipAvatar = GetComponentInParent<VoipAvatar>();
        }

        private void LateUpdate()
        {
            if (replayAudioSource) // gives replayed avatars a speech indicator too
            {
                //Debug.Log("replayAudioSource");    
                UpdateSamples();
                UpdateIndicators();
                UpdatePosition();
                //Debug.Log(string.Join(", ", volumeFrames));
                return;
            }

            if (!avatar || avatar.IsLocal || !voipAvatar)
            {
                Hide();
                enabled = false;
                return;
            }

            if (!voipAvatar.peerConnection)
            {
                Hide();
                return;
            }

            UpdateSamples();
            UpdateIndicators();
            UpdatePosition();
        }

        private void UpdateSamples()
        {
            if (volumeFrames == null || volumeFrames.Length != volumeIndicators.Count)
            {
                volumeFrames = new float[volumeIndicators.Count];
            }

            var volumeWindowSampleCount = GetVolumeWindowSampleCount();

            if (replayAudioSource)
            {
                currentFrameVolumeSum += volume;
                currentFrameSampleCount += samples;
                //Debug.Log("replay " + currentFrameSampleCount + " " + currentFrameVolumeSum);
            }
            else
            {
                var stats = voipAvatar.peerConnection.audioSink.lastFrameStats;
                currentFrameVolumeSum += stats.volume;
                currentFrameSampleCount += stats.samples;
                //Debug.Log(currentFrameSampleCount + " " + currentFrameVolumeSum);
            }

            if (currentFrameSampleCount > volumeWindowSampleCount)
            {
                PushVolumeSample(currentFrameVolumeSum / currentFrameSampleCount);
                currentFrameVolumeSum = 0;
                currentFrameSampleCount = 0;
            }
        }

        private int GetVolumeWindowSampleCount()
        {
            int sampleRate;
            if (replayAudioSource)
            {
                sampleRate = replayAudioSource.clip.frequency;
            }
            else
            {
               sampleRate = voipAvatar.peerConnection.audioSink.sampleRate;
            }

            return (int)(sampleSecondsPerIndicator * sampleRate);
        }

        private void PushVolumeSample(float sample)
        {
            for (int i = volumeFrames.Length - 1; i >= 1; i--)
            {
                volumeFrames[i] = volumeFrames[i-1];
            }
            volumeFrames[0] = sample;
        }

        private void UpdateIndicators()
        {
            switch(mode)
            {
                case Mode.Current : UpdateIndicatorsCurrent(); break;
                case Mode.History : UpdateIndicatorsHistory(); break;
            }
        }

        private void UpdateIndicatorsCurrent()
        {
            if (volumeFrames.Length == 0)
            {
                return;
            }

            var currentVolume = volumeFrames[0];
            var range =  maxVolume - minVolume;
            var t = (currentVolume - minVolume) / range;
            var indicatorCount =  Mathf.RoundToInt(t * volumeIndicators.Count);

            for (int i = 0; i < volumeIndicators.Count; i++)
            {
                volumeIndicators[i].gameObject.SetActive(i < indicatorCount);
                var tScale = i/(float)volumeIndicators.Count;
                volumeIndicators[i].localScale = Vector3.Lerp(
                    minIndicatorScale,maxIndicatorScale,tScale);
            }
        }

        private void UpdateIndicatorsHistory()
        {
            for (int i = 0; i < volumeFrames.Length; i++)
            {
                if (volumeFrames[i] > minVolume)
                {
                    volumeIndicators[i].gameObject.SetActive(true);
                    var range =  maxVolume - minVolume;
                    var t = (volumeFrames[i] - minVolume) / range;
                    volumeIndicators[i].localScale = Vector3.Lerp(
                        minIndicatorScale,maxIndicatorScale,t);
                }
                else
                {
                    volumeIndicators[i].gameObject.SetActive(false);
                }
            }
        }

        private void UpdatePosition()
        {
            var cameraTransform = Camera.main.transform;
            var headTransform = transform.parent;
            var indicatorRootTransform = transform;

            // If no indicator is being shown currently, reset position
            var indicatorVisible = false;
            for (int i = 0; i < volumeIndicators.Count; i++)
            {
                if (volumeIndicators[i].gameObject.activeInHierarchy)
                {
                    indicatorVisible = true;
                    break;
                }
            }

            if (!indicatorVisible)
            {
                indicatorRootTransform.forward = headTransform.forward;
            }

            // Rotate s.t. the indicator is always 90 deg from camera
            // Method - always two acceptable orientations, pick the closest
            var headToCamera = cameraTransform.position - headTransform.position;
            var headToCameraDir = headToCamera.normalized;
            var dirA = Vector3.Cross(headToCameraDir,headTransform.up);
            var dirB = Vector3.Cross(headTransform.up,headToCameraDir);

            var simA = Vector3.Dot(dirA,indicatorRootTransform.forward);
            var simB = Vector3.Dot(dirB,indicatorRootTransform.forward);

            var forward = simA > simB ? dirA : dirB;

            // Deal with rare case when avatars share a position
            if (forward.sqrMagnitude <= 0)
            {
                forward = indicatorRootTransform.forward;
            }

            indicatorRootTransform.forward = forward;
        }

        private void Hide()
        {
            for (int i = 0; i < volumeIndicators.Count; i++)
            {
                volumeIndicators[i].gameObject.SetActive(false);
            }
        }
    }
}