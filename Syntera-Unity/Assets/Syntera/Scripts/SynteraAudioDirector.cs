using UnityEngine;

namespace Syntera
{
    // Original synthesized tonal bed plus optional professionally recorded dialogue.
    // Files are opt-in Resources/Voice/Chapter_00_Line_00.wav, ... (zero-indexed).
    // The dialogue remains playable entirely with subtitles if a recording is absent.
    public sealed class SynteraAudioDirector : MonoBehaviour
    {
        public bool Muted { get; private set; }
        public bool VoicePlaying => voiceSource != null && voiceSource.isPlaying;
        public float DialogueLevel { get; private set; }
        private AudioSource musicSource;
        private AudioSource voiceSource;
        private AudioClip calm, crisis;
        private readonly float[] samples = new float[128];
        private int chapter;

        public void Initialize()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.spatialBlend = 0;
            musicSource.volume = .13f;
            musicSource.playOnAwake = false;
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.spatialBlend = 0;
            voiceSource.volume = .85f;
            voiceSource.playOnAwake = false;

            calm = Compose(false);
            crisis = Compose(true);
            SetChapter(0);
        }

        private static AudioClip Compose(bool tense)
        {
            const int rate = 22050, seconds = 8;
            float[] buffer = new float[rate * seconds];
            float fundamental = tense ? 73.42f : 110f;
            for (int i = 0; i < buffer.Length; ++i)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * (i % (rate * 2)) / (rate * 2));
                float pad = Mathf.Sin(2f * Mathf.PI * fundamental * t) * .24f;
                pad += Mathf.Sin(2f * Mathf.PI * fundamental * 1.5f * t) * .1f;
                pad += Mathf.Sin(2f * Mathf.PI * fundamental * 2f * t) * .06f;
                float pulse = Mathf.Sin(2f * Mathf.PI * (tense ? 3f : 1f) * t) * .03f;
                buffer[i] = (pad + pulse) * (.65f + .35f * envelope);
            }
            var clip = AudioClip.Create(tense ? "Original Syntera crisis tone" : "Original Syntera ambient tone", buffer.Length, 1, rate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        public void SetChapter(int newChapter)
        {
            chapter = newChapter;
            if (musicSource == null) return;
            AudioClip next = chapter == 4 || chapter == 5 || chapter >= 8 ? crisis : calm;
            if (musicSource.clip != next)
            {
                musicSource.Stop();
                musicSource.clip = next;
                if (!Muted) musicSource.Play();
            }
        }

        public bool PlayVoice(int index)
        {
            if (voiceSource == null) return false;
            voiceSource.Stop();
            var file = "Voice/Chapter_" + chapter.ToString("00") + "_Line_" + index.ToString("00");
            var audio = Resources.Load<AudioClip>(file);
            if (audio == null) return false;
            voiceSource.clip = audio;
            if (!Muted) voiceSource.Play();
            return !Muted;
        }

        public void StopVoice()
        {
            if (voiceSource != null) voiceSource.Stop();
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            if (voiceSource != null) voiceSource.mute = Muted;
            if (musicSource != null)
            {
                musicSource.mute = Muted;
                if (!Muted && !musicSource.isPlaying) musicSource.Play();
            }
        }

        private void Update()
        {
            DialogueLevel = 0;
            if (voiceSource == null || !voiceSource.isPlaying || Muted) return;
            voiceSource.GetOutputData(samples, 0);
            float sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += Mathf.Abs(samples[i]);
            DialogueLevel = Mathf.Clamp01(sum / samples.Length * 6f);
        }

        private void OnDestroy()
        {
            if (calm != null) Destroy(calm);
            if (crisis != null) Destroy(crisis);
        }
    }
}
