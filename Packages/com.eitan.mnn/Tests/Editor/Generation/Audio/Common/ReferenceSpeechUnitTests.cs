using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class ReferenceSpeechUnitTests
    {
        private string _root;
        [SetUp] public void Setup() { _root = Path.GetFullPath(Path.Combine("TestArtifacts~", "ReferenceSpeechUnit", Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(_root); }
        [TearDown] public void Cleanup() { Directory.Delete(_root, true); }
        private static float[] Tone(int rate, double seconds = 1) => Enumerable.Range(0, (int)(rate * seconds)).Select(i => (float)(.25 * Math.Sin(i * 2 * Math.PI * 440 / rate))).ToArray();
        private string Wave(int bits, int channels, bool floating = false, bool oddChunk = false)
        {
            string path = Path.Combine(_root, "reference.wav");
            using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)(floating ? 3 : 1)); writer.Write((short)channels); writer.Write(8000); writer.Write(8000 * channels * bits / 8); writer.Write((short)(channels * bits / 8)); writer.Write((short)bits);
                if (oddChunk) { writer.Write(Encoding.ASCII.GetBytes("JUNK")); writer.Write(1); writer.Write((short)0); }
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(8000 * channels * bits / 8);
                for (int i = 0; i < 8000; ++i) for (int c = 0; c < channels; ++c)
                {
                    float value = i % 2 == 0 ? -.25f : .25f;
                    if (floating) writer.Write(value);
                    else if (bits == 16) writer.Write((short)(value * 32768));
                    else if (bits == 32) writer.Write((int)(value * 2147483648.0));
                    else { int pcm = (int)(value * 8388608); writer.Write((byte)pcm); writer.Write((byte)(pcm >> 8)); writer.Write((byte)(pcm >> 16)); }
                }
                writer.Seek(4, SeekOrigin.Begin); writer.Write((int)stream.Length - 8);
            }
            return path;
        }
        [TestCase(16, 1), TestCase(16, 2), TestCase(24, 1), TestCase(24, 2), TestCase(32, 1), TestCase(32, 2)]
        public void PcmWaveWidthsAndStereoMixReadCorrectly(int bits, int channels)
        {
            var audio = MNNReferenceAudio.FromWaveFile(Wave(bits, channels, oddChunk: true));
            Assert.AreEqual(8000, audio.SampleRate); Assert.AreEqual(1, audio.Duration);
            Assert.AreEqual(-.25f, audio.Samples[0]); Assert.AreEqual(.25f, audio.Samples[1]);
        }
        [Test] public void FloatWaveRejectsNonFiniteAndOutOfRangeSamples()
        {
            string path = Wave(32, 2, true);
            Assert.AreEqual(.25f, MNNReferenceAudio.FromWaveFile(path).Samples[1]);
            foreach (float bad in new[]{float.NaN, float.PositiveInfinity, 1.01f})
            {
                var bytes = File.ReadAllBytes(path); Array.Copy(BitConverter.GetBytes(bad), 0, bytes, 44, 4); File.WriteAllBytes(path, bytes);
                Assert.Throws<InvalidDataException>(() => MNNReferenceAudio.FromWaveFile(path));
            }
        }
        [TestCase("riff"), TestCase("length"), TestCase("chunk"), TestCase("align"), TestCase("format"), TestCase("trailing"), TestCase("missing"), TestCase("partial")]
        public void MalformedWaveCannotReachInference(string fault)
        {
            string path = Wave(16, 1); var bytes = File.ReadAllBytes(path);
            if (fault == "riff") bytes[0] = 0;
            if (fault == "length") bytes[4] = 0;
            if (fault == "chunk") Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, bytes, 40, 4);
            if (fault == "align") bytes[32] = 3;
            if (fault == "format") bytes[20] = 6;
            if (fault == "missing") bytes[36] = 0;
            if (fault == "partial") { Array.Resize(ref bytes, bytes.Length - 1); Array.Copy(BitConverter.GetBytes(bytes.Length - 8), 0, bytes, 4, 4); }
            if (fault == "trailing") { Array.Resize(ref bytes, bytes.Length + 3); Array.Copy(BitConverter.GetBytes(bytes.Length - 8), 0, bytes, 4, 4); }
            File.WriteAllBytes(path, bytes); Assert.Throws<InvalidDataException>(() => MNNReferenceAudio.FromWaveFile(path));
        }
        [Test] public void ReferenceOwnsCopyAndEnforcesDurationRateAndSignalBounds()
        {
            var samples = Tone(8000, .5); var audio = new MNNReferenceAudio(samples, 8000); samples[1] = 0; Assert.AreNotEqual(0, audio.Samples[1]);
            Assert.DoesNotThrow(() => new MNNReferenceAudio(Tone(8000, 15), 8000));
            Assert.Throws<ArgumentException>(() => new MNNReferenceAudio(Tone(8000, .49), 8000));
            Assert.Throws<ArgumentException>(() => new MNNReferenceAudio(Tone(8000, 15.01), 8000));
            Assert.Throws<ArgumentException>(() => new MNNReferenceAudio(new float[8000], 8000));
            Assert.Throws<ArgumentException>(() => new MNNReferenceAudio(null, 8000));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNReferenceAudio(samples, 7999));
            Assert.Throws<ArgumentException>(() => new MNNReferenceAudio(Tone(8001, .5), 8001), "An odd sample rate must not round the minimum duration down.");
            Assert.DoesNotThrow(() => new MNNReferenceAudio(Tone(8001, .5001), 8001));
            foreach (float value in new[]{float.NaN, float.NegativeInfinity, -1.01f}) { samples[1] = value; Assert.Throws<ArgumentException>(() => new MNNReferenceAudio(samples, 8000)); }
        }
        [TestCase(32), TestCase(512), TestCase(1920)]
        public void FftMatchesIndependentDiscreteTransformAndRoundTrips(int length)
        {
            var source = Enumerable.Range(0, length).Select(i => Math.Sin(i * .37) + .1 * Math.Cos(i * .19)).ToArray();
            var real = (double[])source.Clone(); var imaginary = new double[length]; MNNSpeechDsp.Fft(real, imaginary, false);
            foreach (int k in new[]{0, 1, 7, length / 2, length - 1})
            {
                double re = 0, im = 0;
                for (int n = 0; n < length; ++n) { re += source[n] * Math.Cos(-2 * Math.PI * k * n / length); im += source[n] * Math.Sin(-2 * Math.PI * k * n / length); }
                Assert.AreEqual(re, real[k], 1e-8); Assert.AreEqual(im, imaginary[k], 1e-8);
            }
            MNNSpeechDsp.Fft(real, imaginary, true);
            for (int i = 0; i < length; ++i) { Assert.AreEqual(source[i], real[i], 1e-10); Assert.AreEqual(0, imaginary[i], 1e-10); }
            Assert.Throws<ArgumentException>(() => MNNSpeechDsp.Fft(new double[1920], new double[1], false));
        }
        [Test] public void IstftReconstructsIndependentWindowedSineSpectrum()
        {
            const int frames = 5, bins = 961; var spectrum = new float[frames * 1922];
            // A sine of amplitude .1, DFT bin 60. Analyze with a direct DFT,
            // independently of the production FFT, then reconstruct overlap-add.
            for (int f = 0; f < frames; ++f) for (int k = 0; k < bins; ++k)
            {
                double re = 0, im = 0;
                for (int n = 0; n < 1920; ++n)
                {
                    double sample = .1 * Math.Sin(2 * Math.PI * 60 * (f * 480 + n) / 1920) * (.5 - .5 * Math.Cos(2 * Math.PI * n / 1919));
                    re += sample * Math.Cos(-2 * Math.PI * k * n / 1920); im += sample * Math.Sin(-2 * Math.PI * k * n / 1920);
                }
                spectrum[f * 1922 + k] = (float)Math.Log(Math.Max(1e-30, Math.Sqrt(re * re + im * im)));
                spectrum[f * 1922 + bins + k] = (float)Math.Atan2(im, re);
            }
            var wave = MNNSpeechDsp.FireRedIstft(spectrum, default);
            Assert.AreEqual((frames - 1) * 480 + 480, wave.Length);
            for (int n = 0; n < wave.Length; n += 7) Assert.AreEqual(.1 * Math.Sin(2 * Math.PI * 60 * (n + 720) / 1920), wave[n], 2e-6);
            spectrum[0] = float.NaN; Assert.Throws<InvalidDataException>(() => MNNSpeechDsp.FireRedIstft(spectrum, default));
            Assert.Throws<InvalidDataException>(() => MNNSpeechDsp.FireRedIstft(new float[2], default));
        }
        [TestCase(95999, 22050), TestCase(8001, 24000), TestCase(8000, 16000), TestCase(22050, 16000), TestCase(24000, 22050), TestCase(96000, 24000)]
        public void ResamplePreservesSineFrequencyAmplitudeAndDuration(int source, int target)
        {
            var result = MNNSpeechDsp.Resample(Tone(source), source, target, default);
            Assert.AreEqual(target, result.Length);
            double error = 0;
            for (int i = 100; i < result.Length - 100; ++i) error += Math.Pow(result[i] - .25 * Math.Sin(i * 2 * Math.PI * 440 / target), 2);
            Assert.Less(Math.Sqrt(error / (result.Length - 200)), .001);
        }
        [Test] public void FeatureNormalizationsMasksAndCancellationRespectContracts()
        {
            var wave = Tone(16000); var bank = MNNSpeechDsp.Fbank(wave, false, out int frames, default);
            Assert.AreEqual(98, frames); Assert.AreEqual(frames * 80, bank.Length);
            for (int bin = 0; bin < 80; ++bin) Assert.AreEqual(0, Enumerable.Range(0, frames).Average(f => bank[f * 80 + bin]), 2e-6);
            var stacked = MNNSpeechDsp.Fbank(Tone(16000, 1.01), true, out int pairs, default); Assert.AreEqual(50, pairs); Assert.AreEqual(8000, stacked.Length); Assert.True(stacked.Skip(7920).All(x => x == 0));
            var mel = MNNSpeechDsp.Mel22050(new float[22050], out int melFrames, default); Assert.AreEqual(86, melFrames); Assert.True(mel.All(x => Math.Abs(x - Math.Log(1e-5)) < 1e-5));
            var mask = MNNSpeechDsp.CausalMask(4, 2); Assert.AreEqual(0, mask[3 * 4 + 2]); Assert.AreEqual(-1e9f, mask[3 * 4 + 1]); Assert.AreEqual(-1e9f, mask[1]);
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); Assert.Throws<OperationCanceledException>(() => MNNSpeechDsp.Resample(wave, 16000, 16000, cancel.Token));
                Assert.Throws<OperationCanceledException>(() => MNNSpeechDsp.Fbank(wave, false, out _, cancel.Token));
                Assert.Throws<OperationCanceledException>(() => MNNSpeechDsp.Mel22050(wave, out _, cancel.Token));
                Assert.Throws<OperationCanceledException>(() => MNNSpeechDsp.FireRedIstft(new float[1922], cancel.Token));
            }
        }
        [TestCase(null), TestCase(""), TestCase(" "), TestCase("<|zh|>Hello"), TestCase("a\0b")]
        public void InvalidTextCannotReachTokenizerOrGraphs(string text) { Assert.Throws(Is.InstanceOf<ArgumentException>(), () => MNNSpeechDsp.ValidateText(text)); }
        [Test] public void UnpairedSurrogatesCannotReachTokenizerOrGraphs()
        {
            // Construct at runtime: NUnit/Unity serializes attribute strings as UTF-8.
            foreach (char surrogate in new[]{(char)0xd800, (char)0xdc00})
                Assert.Throws(Is.InstanceOf<ArgumentException>(), () => MNNSpeechDsp.ValidateText(new string(surrogate, 1)));
        }
        [Test] public void InvalidGeneratedWaveAndSamplerOutputsFailClearly()
        {
            Assert.Throws<InvalidDataException>(() => MNNSpeechDsp.Result(new float[24000], 24000, "test"));
            Assert.Throws<InvalidDataException>(() => MNNSpeechDsp.Result(new[]{float.NaN}, 24000, "test"));
            var logits = Enumerable.Repeat(-100f, 8194).ToArray(); logits[4] = 10; logits[5] = 5;
            Assert.AreEqual(5, MNNIndexTts25.Select(logits, new HashSet<int>{4})); logits[8193] = 20;
            Assert.AreEqual(8193, MNNIndexTts25.Select(logits, new HashSet<int>())); logits[3] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNIndexTts25.Select(logits, new HashSet<int>()));
        }
        [TestCase(true), TestCase(false)]
        public void DownloadProfilesPinEveryFileAndKeepExternalTokenizerSource(bool index)
        {
            string repo = index ? MNNReferenceSpeechModel.IndexRepository : MNNReferenceSpeechModel.FireRedRepository;
            var files = MNNModelDownloadService.ReferenceSpeechDownloadPlan(repo, "main"); Assert.AreEqual(index ? 14 : 22, files.Length);
            Assert.AreEqual(files.Length, files.Select(x => x.Path).Distinct().Count());
            foreach (var file in files) { Assert.Greater(file.Size, 0); Assert.That(file.Sha256, Does.Match("^[a-f0-9]{64}$")); Assert.That(file.Revision, Does.Match("^[a-f0-9]{40}$")); Assert.False(Path.IsPathRooted(file.Path)); Assert.False(file.Path.Contains("..")); }
            if (!index) Assert.True(files.Where(x => x.Path.StartsWith("text_tokenizer/")).All(x => x.SourceRepository == "FireRedTeam/FireRedTTS3"));
            Assert.Throws<ArgumentException>(() => MNNModelDownloadService.ReferenceSpeechDownloadPlan(repo, "untested-revision"));
            string signature = index ? "gpt_prefill_fp16.mnn" : "redae_encoder.mnn"; File.WriteAllText(Path.Combine(_root, signature), "incomplete");
            var model = MNNStudioModel.Read(_root); Assert.True(model.RequiresReferenceAudio); Assert.AreEqual(index, model.IsIndexTts); Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task);
            Assert.False(model.HasGenerationPipeline); Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.SpeechSynthesis).CanRun(model));
            Assert.Throws<InvalidDataException>(() => MNNReferenceSpeechModel.Validate(_root, index));
        }
        [TestCase("(2, 3)", false), TestCase("(3, 2)", true), TestCase("(6,)", true)]
        public void ProjectionRequiresExactShapeNotOnlyByteCount(string shape, bool invalid)
        {
            string path = Path.Combine(_root, "proj.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) using (var writer = new BinaryWriter(zip.CreateEntry("weight.npy").Open()))
            {
                writer.Write(new byte[]{147, 78, 85, 77, 80, 89, 1, 0});
                byte[] header = Encoding.ASCII.GetBytes("{'descr': '<f4', 'fortran_order': False, 'shape': " + shape + ", }\n"); writer.Write((ushort)header.Length); writer.Write(header);
                for (int i = 0; i < 6; ++i) writer.Write(i / 10f);
            }
            using (var zip = ZipFile.OpenRead(path))
                if (invalid) Assert.Throws<InvalidDataException>(() => MNNIndexTts25.ReadProjection(zip, "weight.npy", 2, 3));
                else Assert.AreEqual(.5f, MNNIndexTts25.ReadProjection(zip, "weight.npy", 2, 3)[5]);
        }
    }
}
