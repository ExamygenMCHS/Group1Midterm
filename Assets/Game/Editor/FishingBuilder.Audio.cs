using System;
using System.IO;
using UnityEditor;
using UnityEngine;

//the fishing sounds, synthesised: mono 44.1 kHz 16-bit WAVs, gentle (peaks about -7 dBFS) with short fades so nothing clicks
public static partial class FishingBuilder
{
    private const int Rate = 44100;

    private class Synth
    {
        public readonly float[] s;
        private uint rng;
        public Synth(float seconds, uint seed) { s = new float[Mathf.CeilToInt(seconds * Rate)]; rng = seed * 2654435761u + 1u; }
        public int N { get { return s.Length; } }
        public float Noise()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 8388607.5f - 1f;
        }
        public static float T(int i) { return i / (float)Rate; }

        //a sine with a pitch sweep from f0 to f1 (exponential), starting at 'start' seconds, decaying envelope
        public void Tone(float start, float dur, float f0, float f1, float amp, float attack, float decayPower = 2f, float harmonic = 0f)
        {
            int a = Mathf.FloorToInt(start * Rate), n = Mathf.FloorToInt(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n && a + i < N; i++)
            {
                float t = i / (float)n;
                float f = f0 * Mathf.Pow(f1 / f0, t);
                phase += 2.0 * Math.PI * f / Rate;
                float env = Mathf.Min(1f, i / (attack * Rate + 1f)) * Mathf.Pow(1f - t, decayPower);
                s[a + i] += amp * env * ((float)Math.Sin(phase) + harmonic * (float)Math.Sin(phase * 2.0));
            }
        }

        //band-limited noise burst: two one-pole filters around a (sweeping) centre
        public void NoiseBurst(float start, float dur, float fLo0, float fLo1, float amp, float attack, float decayPower = 2f, float hp = 200f)
        {
            int a = Mathf.FloorToInt(start * Rate), n = Mathf.FloorToInt(dur * Rate);
            float lp = 0f, hpState = 0f, prev = 0f;
            float kh = Mathf.Exp(-2f * Mathf.PI * hp / Rate);
            for (int i = 0; i < n && a + i < N; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(fLo0, fLo1, t);
                float k = 1f - Mathf.Exp(-2f * Mathf.PI * f / Rate);
                lp += k * (Noise() - lp);
                hpState = kh * (hpState + lp - prev);
                prev = lp;
                float env = Mathf.Min(1f, i / (attack * Rate + 1f)) * Mathf.Pow(1f - t, decayPower);
                s[a + i] += amp * env * hpState * 3f;
            }
        }

        public void Click(float start, float freq, float amp, float dur = 0.006f)
        {
            Tone(start, dur, freq, freq * 0.8f, amp, 0.0003f, 3f);
            NoiseBurst(start, dur * 0.7f, 6000f, 3000f, amp * 0.6f, 0.0002f, 3f, 1500f);
        }

        //normalise to the peak, then short fades at both ends
        public void Finish(float peak = 0.44f, float fadeIn = 0.003f, float fadeOut = 0.02f, bool loop = false)
        {
            float max = 1e-6f;
            foreach (float v in s)
                max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < N; i++)
                s[i] *= peak / max;
            if (loop)
                return;
            int fi = Mathf.Max(1, Mathf.FloorToInt(fadeIn * Rate)), fo = Mathf.Max(1, Mathf.FloorToInt(fadeOut * Rate));
            for (int i = 0; i < fi && i < N; i++)
                s[i] *= i / (float)fi;
            for (int i = 0; i < fo && i < N; i++)
                s[N - 1 - i] *= i / (float)fo;
        }
    }

    private static void SaveWav(string name, Synth syn)
    {
        string path = AudioDir + "/" + name + ".wav";
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            int bytes = syn.N * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float v in syn.s)
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32767, 32767));
            File.WriteAllBytes(path, ms.ToArray());
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp != null && !imp.forceToMono)
        {
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }
    }

    public static string BuildAudio()
    {
        EnsureFolder(AudioDir);

        //cast: an airy whoosh that swells and passes, with a faint reel whirr underneath
        var cast = new Synth(0.55f, 1);
        cast.NoiseBurst(0f, 0.5f, 600f, 2600f, 1f, 0.18f, 1.6f, 400f);
        for (int i = 0; i < 18; i++)
            cast.Click(0.08f + i * 0.022f, 2400f, 0.08f * (1f - i / 18f), 0.004f);
        cast.Tone(0.05f, 0.45f, 900f, 1300f, 0.05f, 0.05f, 1.5f);
        cast.Finish(0.38f, 0.01f, 0.06f);
        SaveWav("Cast", cast);

        //bobber land: a soft plop
        var plop = new Synth(0.3f, 2);
        plop.Tone(0f, 0.16f, 620f, 170f, 1f, 0.002f, 2.5f);
        plop.NoiseBurst(0.005f, 0.22f, 2500f, 700f, 0.35f, 0.003f, 2.5f, 300f);
        plop.Finish(0.38f);
        SaveWav("Bobber Land", plop);

        //nibble: a tiny water blip
        var nib = new Synth(0.1f, 3);
        nib.Tone(0f, 0.07f, 850f, 1350f, 1f, 0.002f, 2f);
        nib.NoiseBurst(0f, 0.04f, 3000f, 1500f, 0.2f, 0.001f, 2f, 500f);
        nib.Finish(0.26f, 0.002f, 0.015f);
        SaveWav("Nibble", nib);

        //bite: a sharp splash and a deep gulp that says "now!"
        var bite = new Synth(0.45f, 4);
        bite.NoiseBurst(0f, 0.3f, 5000f, 900f, 1f, 0.001f, 2.2f, 250f);
        bite.Tone(0.01f, 0.12f, 420f, 140f, 0.9f, 0.002f, 2f);
        bite.Tone(0.12f, 0.16f, 300f, 110f, 0.7f, 0.004f, 2f);
        bite.Finish(0.48f);
        SaveWav("Bite", bite);

        //hook: a quick taut twang
        var hook = new Synth(0.35f, 5);
        hook.Tone(0f, 0.32f, 190f, 240f, 1f, 0.001f, 2.5f, 0.45f);
        hook.Tone(0f, 0.18f, 570f, 720f, 0.35f, 0.001f, 3f);
        hook.NoiseBurst(0f, 0.02f, 7000f, 4000f, 0.4f, 0.0005f, 2f, 2000f);
        hook.Finish(0.42f);
        SaveWav("Hook", hook);

        //surge: a splashy thrash, several bursts
        var surge = new Synth(0.65f, 6);
        float[] at = { 0f, 0.12f, 0.27f, 0.40f };
        for (int i = 0; i < at.Length; i++)
        {
            surge.NoiseBurst(at[i], 0.22f, 4200f - i * 500f, 900f, 1f - i * 0.15f, 0.004f, 2f, 300f);
            surge.Tone(at[i], 0.08f, 380f, 160f, 0.35f, 0.002f, 2f);
        }
        surge.Finish(0.42f);
        SaveWav("Surge", surge);

        //reel loop: 0.5 s of steady reel clicks over a whirr with a whole number of cycles, so it loops seamlessly
        var reel = new Synth(0.5f, 7);
        for (int i = 0; i < 12; i++)
            reel.Click(i * (0.5f / 12f) + 0.002f, 2600f + (i % 2) * 300f, 0.5f, 0.007f);
        for (int i = 0; i < reel.N; i++)
            reel.s[i] += 0.04f * Mathf.Sin(2f * Mathf.PI * 440f * Synth.T(i)) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 24f * Synth.T(i)));
        reel.Finish(0.30f, 0f, 0f, true);
        SaveWav("Reel Loop", reel);

        //line snap: a crack, a high ping and a whip of air
        var snap = new Synth(0.5f, 8);
        snap.NoiseBurst(0f, 0.03f, 9000f, 6000f, 1f, 0.0003f, 2f, 2500f);
        snap.Tone(0f, 0.25f, 1500f, 1200f, 0.35f, 0.0005f, 3f);
        snap.NoiseBurst(0.03f, 0.4f, 3500f, 600f, 0.5f, 0.02f, 1.8f, 400f);
        snap.Finish(0.42f);
        SaveWav("Line Snap", snap);

        //reel in: a short accelerating zip
        var zip = new Synth(0.35f, 9);
        float tz = 0f;
        for (int i = 0; tz < 0.3f; i++)
        {
            zip.Click(tz, 2200f + i * 60f, 0.45f, 0.005f);
            tz += Mathf.Lerp(0.03f, 0.009f, i / 20f);
        }
        zip.Tone(0f, 0.3f, 500f, 900f, 0.1f, 0.03f, 1.5f);
        zip.Finish(0.34f);
        SaveWav("Reel In", zip);

        //catch: a warm three-note rise (C, E, G)
        var ok = new Synth(0.8f, 10);
        float[] notes = { 523.25f, 659.25f, 783.99f };
        for (int i = 0; i < 3; i++)
            ok.Tone(i * 0.12f, i == 2 ? 0.55f : 0.32f, notes[i], notes[i], 1f, 0.008f, 1.8f, 0.25f);
        ok.Finish(0.40f, 0.004f, 0.05f);
        SaveWav("Catch", ok);

        //trophy: a fuller five-note fanfare (G, C, E, G, C) with a held last note
        var trophy = new Synth(1.4f, 11);
        float[] fan = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < 5; i++)
        {
            float len = i == 4 ? 0.9f : 0.3f;
            trophy.Tone(i * 0.11f, len, fan[i], fan[i], 1f, 0.006f, 1.5f, 0.3f);
            trophy.Tone(i * 0.11f, len, fan[i] * 1.5f, fan[i] * 1.5f, 0.18f, 0.01f, 1.8f);
        }
        trophy.Finish(0.42f, 0.004f, 0.08f);
        SaveWav("Trophy", trophy);

        //bait: a soft click
        var bait = new Synth(0.08f, 12);
        bait.Click(0f, 1900f, 1f, 0.012f);
        bait.Tone(0.004f, 0.04f, 900f, 700f, 0.3f, 0.001f, 2f);
        bait.Finish(0.30f, 0.001f, 0.012f);
        SaveWav("Bait", bait);
        return "audio: 12 clips";
    }
}
