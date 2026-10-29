namespace PotatoKeysPlus.Audio;

/// <summary>
/// Makes keyboard sounds from scratch (no sound files), as mono float samples.
/// Each pack has a normal key, a "big key" (space / enter / backspace) and a key-up sound.
/// </summary>
public static class SoundSynth
{
    public static readonly string[] Packs = ["Thock", "Clicky", "Typewriter", "Bubble", "Soft"];

    public sealed record Pack(float[][] Normal, float[] Big, float[] Up, float[]? Enter);

    public static Pack Build(string name, int rate)
    {
        float[] Make(double pitch, int seed, bool big = false) => name switch
        {
            "Clicky" => Clicky(rate, pitch, seed, big),
            "Typewriter" => Typewriter(rate, pitch, seed, big),
            "Bubble" => Bubble(rate, pitch, big),
            "Soft" => Soft(rate, pitch, seed, big),
            _ => Thock(rate, pitch, seed, big),
        };

        // A few slightly different pitches so fast typing doesn't sound robotic.
        var normal = new[] { 0.95, 0.985, 1.0, 1.03, 1.06 }.Select((p, i) => Make(p, 11 + i)).ToArray();
        var big = Make(0.78, 99, big: true);
        var up = Scale(Make(1.25, 7), 0.35f);
        float[]? enter = name == "Typewriter" ? Bell(rate) : null;
        return new Pack(normal, big, up, enter);
    }

    // ---- packs ----

    static float[] Thock(int rate, double pitch, int seed, bool big)
    {
        var rnd = new Random(seed);
        int n = Samples(rate, big ? 0.11 : 0.08);
        var o = new float[n];
        double lp = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double body = Math.Sin(2 * Math.PI * 170 * pitch * t) * 0.55 + Math.Sin(2 * Math.PI * 95 * pitch * t) * 0.45;
            body *= Math.Exp(-t / (big ? 0.028 : 0.018));
            lp += 0.25 * (rnd.NextDouble() * 2 - 1 - lp); // low-passed noise = the "knock"
            double knock = lp * Math.Exp(-t / 0.004) * 1.4;
            o[i] = (float)((body + knock) * 0.8);
        }
        return Fade(o, rate);
    }

    static float[] Clicky(int rate, double pitch, int seed, bool big)
    {
        var rnd = new Random(seed);
        int n = Samples(rate, big ? 0.07 : 0.05);
        var o = new float[n];
        double prev = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double noise = rnd.NextDouble() * 2 - 1;
            double hp = noise - prev; // high-passed noise = sharp "tick"
            prev = noise;
            double click1 = hp * Math.Exp(-t / 0.0025);
            double t2 = t - 0.011; // blue switches click twice
            double click2 = t2 > 0 ? hp * 0.55 * Math.Exp(-t2 / 0.002) : 0;
            double ping = Math.Sin(2 * Math.PI * 2600 * pitch * t) * 0.35 * Math.Exp(-t / 0.006);
            double body = big ? Math.Sin(2 * Math.PI * 140 * t) * 0.4 * Math.Exp(-t / 0.02) : 0;
            o[i] = (float)((click1 + click2 + ping + body) * 0.6);
        }
        return Fade(o, rate);
    }

    static float[] Typewriter(int rate, double pitch, int seed, bool big)
    {
        var rnd = new Random(seed);
        int n = Samples(rate, big ? 0.16 : 0.12);
        var o = new float[n];
        double lp = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            lp += 0.6 * (rnd.NextDouble() * 2 - 1 - lp);
            double strike = lp * Math.Exp(-t / 0.0025) * 1.3;
            double ring = (Math.Sin(2 * Math.PI * 1230 * pitch * t) * 0.5
                         + Math.Sin(2 * Math.PI * 2710 * pitch * t) * 0.3
                         + Math.Sin(2 * Math.PI * 4130 * pitch * t) * 0.2) * Math.Exp(-t / 0.03) * 0.35;
            double thud = Math.Sin(2 * Math.PI * (big ? 90 : 130) * t) * Math.Exp(-t / 0.02) * 0.5;
            o[i] = (float)((strike + ring + thud) * 0.7);
        }
        return Fade(o, rate);
    }

    static float[] Bubble(int rate, double pitch, bool big)
    {
        int n = Samples(rate, big ? 0.09 : 0.06);
        var o = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double f = (big ? 280 : 420) * pitch * (1 + 1.3 * (t / (n / (double)rate))); // pitch sweeps up = "pop"
            phase += 2 * Math.PI * f / rate;
            double env = Math.Min(1, t / 0.002) * Math.Exp(-t / (big ? 0.03 : 0.02));
            o[i] = (float)(Math.Sin(phase) * env * 0.7);
        }
        return Fade(o, rate);
    }

    static float[] Soft(int rate, double pitch, int seed, bool big)
    {
        var rnd = new Random(seed);
        int n = Samples(rate, 0.04);
        var o = new float[n];
        double lp = 0;
        double k = 0.12 * pitch * (big ? 0.7 : 1);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            lp += k * (rnd.NextDouble() * 2 - 1 - lp);
            o[i] = (float)(lp * Math.Exp(-t / 0.006) * 2.2);
        }
        return Fade(o, rate);
    }

    static float[] Bell(int rate)
    {
        int n = Samples(rate, 0.6);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            o[i] = (float)((Math.Sin(2 * Math.PI * 1760 * t) * 0.6 + Math.Sin(2 * Math.PI * 3520 * t) * 0.25 + Math.Sin(2 * Math.PI * 5280 * t) * 0.1)
                           * Math.Exp(-t / 0.18) * 0.45);
        }
        return Fade(o, rate);
    }

    // ---- helpers ----

    static int Samples(int rate, double seconds) => (int)(rate * seconds);

    static float[] Scale(float[] s, float gain)
    {
        for (int i = 0; i < s.Length; i++) s[i] *= gain;
        return s;
    }

    /// <summary>Short fade-out so sounds never end with a click.</summary>
    static float[] Fade(float[] s, int rate)
    {
        int fade = Math.Min(s.Length, rate / 200);
        for (int i = 0; i < fade; i++) s[s.Length - 1 - i] *= (float)i / fade;
        return s;
    }
}
