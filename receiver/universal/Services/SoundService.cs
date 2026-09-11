using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BathroomSync.Universal.Services;

public static class SoundService {
  [DllImport("winmm.dll", EntryPoint = "PlaySound", SetLastError = true)]
  private static extern bool PlaySoundWindows(byte[] ptrToSound, IntPtr hmod, uint fdwSound);

  const uint SND_ASYNC = 0x0001;
  const uint SND_MEMORY = 0x0004;

  public static void Play(string? soundName, double volume = 0.8) {
    if (string.IsNullOrWhiteSpace(soundName) ||
        soundName.Equals("No sound", StringComparison.OrdinalIgnoreCase)) {
      return;
    }

    Task.Run(() => {
      try {
        var wavBytes = GenerateWav(soundName, volume);
        if (wavBytes.Length == 0) return;

        if (OperatingSystem.IsMacOS()) {
          PlayMac(wavBytes);
        } else if (OperatingSystem.IsWindows()) {
          PlayWindows(wavBytes);
        } else if (OperatingSystem.IsLinux()) {
          PlayLinux(wavBytes);
        }
      } catch (Exception ex) {
        Console.WriteLine($"[SoundService] Failed to play sound '{soundName}': {ex.Message}");
      }
    });
  }

  static void PlayMac(byte[] wavBytes) {
    var tempPath = Path.Combine(Path.GetTempPath(), $"hallzee_sound_{Guid.NewGuid():N}.wav");
    File.WriteAllBytes(tempPath, wavBytes);
    try {
      using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
        FileName = "/usr/bin/afplay",
        Arguments = $"\"{tempPath}\"",
        UseShellExecute = false,
        CreateNoWindow = true
      });
      proc?.WaitForExit(3000);
    } finally {
      try {
        if (File.Exists(tempPath)) File.Delete(tempPath);
      } catch { }
    }
  }

  static void PlayWindows(byte[] wavBytes) {
    try {
      PlaySoundWindows(wavBytes, IntPtr.Zero, SND_MEMORY | SND_ASYNC);
    } catch {
      var tempPath = Path.Combine(Path.GetTempPath(), $"hallzee_sound_{Guid.NewGuid():N}.wav");
      File.WriteAllBytes(tempPath, wavBytes);
      try {
        using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
          FileName = "powershell.exe",
          Arguments = $"-NoProfile -Command \"(New-Object Media.SoundPlayer '{tempPath.Replace("'", "''")}').PlaySync()\"",
          UseShellExecute = false,
          CreateNoWindow = true
        });
        proc?.WaitForExit(3000);
      } finally {
        try {
          if (File.Exists(tempPath)) File.Delete(tempPath);
        } catch { }
      }
    }
  }

  static void PlayLinux(byte[] wavBytes) {
    var tempPath = Path.Combine(Path.GetTempPath(), $"hallzee_sound_{Guid.NewGuid():N}.wav");
    File.WriteAllBytes(tempPath, wavBytes);
    try {
      using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
        FileName = "aplay",
        Arguments = $"\"{tempPath}\"",
        UseShellExecute = false,
        CreateNoWindow = true
      });
      proc?.WaitForExit(3000);
    } finally {
      try {
        if (File.Exists(tempPath)) File.Delete(tempPath);
      } catch { }
    }
  }

  public static byte[] GenerateWav(string soundName, double volume = 0.8) {
    const int sampleRate = 44100;
    var vol = Math.Clamp(volume, 0.0, 1.0);
    if (vol <= 0.001) return Array.Empty<byte>();

    short[] samples;

    switch (soundName.Trim().ToLowerInvariant()) {
      case "bell":
        samples = GenerateBell(sampleRate, vol);
        break;
      case "soft alert":
        samples = GenerateSoftAlert(sampleRate, vol);
        break;
      case "marimba":
        samples = GenerateMarimba(sampleRate, vol);
        break;
      case "subtle ping":
      case "ping":
        samples = GenerateSubtlePing(sampleRate, vol);
        break;
      case "digital watch":
      case "watch":
      case "beep":
        samples = GenerateDigitalWatch(sampleRate, vol);
        break;
      case "gentle knock":
      case "knock":
        samples = GenerateGentleKnock(sampleRate, vol);
        break;
      case "harp ascend":
      case "harp":
        samples = GenerateHarpAscend(sampleRate, vol);
        break;
      case "chime":
      default:
        samples = GenerateChime(sampleRate, vol);
        break;
    }

    return BuildWavFile(samples, sampleRate);
  }

  static short[] GenerateChime(int sampleRate, double vol) {
    // Two bright pleasant chime tones: 784 Hz (G5) for 0.35s and 1046.5 Hz (C6) for 0.45s
    var duration = 0.8;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];
    var split = (int)(sampleRate * 0.3);

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      double sample = 0;

      if (i < split + (int)(sampleRate * 0.35)) {
        // First chime note (G5)
        var t1 = t;
        var env1 = Math.Exp(-7.0 * t1);
        sample += 0.6 * Math.Sin(2.0 * Math.PI * 783.99 * t1) * env1;
        sample += 0.2 * Math.Sin(2.0 * Math.PI * 1567.98 * t1) * env1;
      }

      if (i >= split) {
        // Second chime note (C6)
        var t2 = (double)(i - split) / sampleRate;
        var env2 = Math.Exp(-5.5 * t2);
        sample += 0.7 * Math.Sin(2.0 * Math.PI * 1046.50 * t2) * env2;
        sample += 0.25 * Math.Sin(2.0 * Math.PI * 2093.00 * t2) * env2;
      }

      samples[i] = (short)Math.Clamp((int)(sample * 24000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateBell(int sampleRate, double vol) {
    // Classic desk/classroom bell: 880 Hz (A5) fundamental + 1760 Hz + 2640 Hz harmonics with bell decay
    var duration = 1.1;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      // Fast strike attack, slow resonant decay
      var env = Math.Exp(-4.0 * t);
      var strike = Math.Min(1.0, t * 200.0);
      var sample = (0.65 * Math.Sin(2.0 * Math.PI * 880.0 * t)
                  + 0.25 * Math.Sin(2.0 * Math.PI * 1760.0 * t)
                  + 0.10 * Math.Sin(2.0 * Math.PI * 2640.0 * t)) * env * strike;

      samples[i] = (short)Math.Clamp((int)(sample * 26000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateSoftAlert(int sampleRate, double vol) {
    // Gentle warm two-note rising chord (523 Hz -> 659 Hz)
    var duration = 0.7;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];
    var split = (int)(sampleRate * 0.22);

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      double sample = 0;

      if (i < split + (int)(sampleRate * 0.25)) {
        var t1 = t;
        var env1 = Math.Sin(Math.PI * Math.Clamp(t1 / 0.35, 0.0, 1.0));
        sample += 0.5 * Math.Sin(2.0 * Math.PI * 523.25 * t1) * env1;
      }

      if (i >= split) {
        var t2 = (double)(i - split) / sampleRate;
        var env2 = Math.Sin(Math.PI * Math.Clamp(t2 / 0.45, 0.0, 1.0));
        sample += 0.6 * Math.Sin(2.0 * Math.PI * 659.25 * t2) * env2;
      }

      samples[i] = (short)Math.Clamp((int)(sample * 22000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateMarimba(int sampleRate, double vol) {
    // Warm resonant marimba two-note tap (C5 523.25 Hz -> E5 659.25 Hz)
    var duration = 0.65;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];
    var split = (int)(sampleRate * 0.16);

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      double sample = 0;

      if (i < split + (int)(sampleRate * 0.32)) {
        var t1 = t;
        var env1 = Math.Exp(-12.0 * t1);
        sample += (0.75 * Math.Sin(2.0 * Math.PI * 523.25 * t1)
                 + 0.25 * Math.Sin(2.0 * Math.PI * 2093.00 * t1) * Math.Exp(-20.0 * t1)) * env1;
      }

      if (i >= split) {
        var t2 = (double)(i - split) / sampleRate;
        var env2 = Math.Exp(-10.0 * t2);
        sample += (0.80 * Math.Sin(2.0 * Math.PI * 659.25 * t2)
                 + 0.20 * Math.Sin(2.0 * Math.PI * 2637.00 * t2) * Math.Exp(-20.0 * t2)) * env2;
      }

      samples[i] = (short)Math.Clamp((int)(sample * 25000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateSubtlePing(int sampleRate, double vol) {
    // Crystal clear gentle ping (E6 1318.5 Hz) with smooth exponential taper
    var duration = 0.75;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      var attack = Math.Min(1.0, t * 500.0);
      var env = Math.Exp(-6.0 * t) * attack;
      var sample = (0.85 * Math.Sin(2.0 * Math.PI * 1318.51 * t)
                  + 0.15 * Math.Sin(2.0 * Math.PI * 2637.02 * t)) * env;

      samples[i] = (short)Math.Clamp((int)(sample * 22000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateDigitalWatch(int sampleRate, double vol) {
    // Crisp double electronic beep (two 70ms pulses at 2093 Hz)
    var duration = 0.45;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];
    var pulse1End = (int)(sampleRate * 0.08);
    var gapEnd = (int)(sampleRate * 0.16);
    var pulse2End = (int)(sampleRate * 0.24);

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      double sample = 0;

      if (i < pulse1End) {
        var tp = t;
        sample = 0.75 * Math.Sin(2.0 * Math.PI * 2093.0 * tp)
               + 0.25 * Math.Sin(2.0 * Math.PI * 4186.0 * tp);
      } else if (i >= gapEnd && i < pulse2End) {
        var tp = (double)(i - gapEnd) / sampleRate;
        sample = 0.75 * Math.Sin(2.0 * Math.PI * 2093.0 * tp)
               + 0.25 * Math.Sin(2.0 * Math.PI * 4186.0 * tp);
      }

      samples[i] = (short)Math.Clamp((int)(sample * 20000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateGentleKnock(int sampleRate, double vol) {
    // Warm wooden acoustic knock (two rapid taps at 600 Hz and 750 Hz)
    var duration = 0.42;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];
    var split = (int)(sampleRate * 0.13);

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      double sample = 0;

      if (i < split + (int)(sampleRate * 0.18)) {
        var t1 = t;
        var env1 = Math.Exp(-32.0 * t1);
        sample += (0.7 * Math.Sin(2.0 * Math.PI * 587.33 * t1)
                 + 0.3 * Math.Sin(2.0 * Math.PI * 1174.66 * t1)) * env1;
      }

      if (i >= split) {
        var t2 = (double)(i - split) / sampleRate;
        var env2 = Math.Exp(-32.0 * t2);
        sample += (0.75 * Math.Sin(2.0 * Math.PI * 739.99 * t2)
                 + 0.25 * Math.Sin(2.0 * Math.PI * 1479.98 * t2)) * env2;
      }

      samples[i] = (short)Math.Clamp((int)(sample * 26000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static short[] GenerateHarpAscend(int sampleRate, double vol) {
    // Smooth 3-note ascending triad arpeggio (C5 523 Hz -> E5 659 Hz -> G5 784 Hz)
    var duration = 0.85;
    var totalSamples = (int)(sampleRate * duration);
    var samples = new short[totalSamples];
    var step1 = (int)(sampleRate * 0.14);
    var step2 = (int)(sampleRate * 0.28);

    for (var i = 0; i < totalSamples; i++) {
      double t = (double)i / sampleRate;
      double sample = 0;

      // Note 1: C5
      var env1 = Math.Exp(-5.0 * t);
      sample += 0.5 * Math.Sin(2.0 * Math.PI * 523.25 * t) * env1;

      // Note 2: E5
      if (i >= step1) {
        var t2 = (double)(i - step1) / sampleRate;
        var env2 = Math.Exp(-5.0 * t2);
        sample += 0.55 * Math.Sin(2.0 * Math.PI * 659.25 * t2) * env2;
      }

      // Note 3: G5
      if (i >= step2) {
        var t3 = (double)(i - step2) / sampleRate;
        var env3 = Math.Exp(-4.5 * t3);
        sample += 0.65 * Math.Sin(2.0 * Math.PI * 783.99 * t3) * env3;
      }

      samples[i] = (short)Math.Clamp((int)(sample * 23000 * vol), short.MinValue, short.MaxValue);
    }
    return samples;
  }

  static byte[] BuildWavFile(short[] samples, int sampleRate) {
    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);

    int subChunk2Size = samples.Length * sizeof(short);
    int chunkSize = 36 + subChunk2Size;

    // RIFF header
    writer.Write(new[] { 'R', 'I', 'F', 'F' });
    writer.Write(chunkSize);
    writer.Write(new[] { 'W', 'A', 'V', 'E' });

    // fmt subchunk
    writer.Write(new[] { 'f', 'm', 't', ' ' });
    writer.Write(16); // subchunk1 size (16 for PCM)
    writer.Write((short)1); // audio format (1 for PCM)
    writer.Write((short)1); // num channels (1 for mono)
    writer.Write(sampleRate);
    writer.Write(sampleRate * sizeof(short)); // byte rate
    writer.Write((short)sizeof(short)); // block align
    writer.Write((short)16); // bits per sample

    // data subchunk
    writer.Write(new[] { 'd', 'a', 't', 'a' });
    writer.Write(subChunk2Size);
    foreach (var sample in samples) {
      writer.Write(sample);
    }

    return ms.ToArray();
  }
}
