using System;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AcousticSpeakerSelectionTests
    {
        [Test]
        public void SpeakerIdentitySurvivesOutputSlotChangesAndCanBeReset()
        {
            var factory = new Factory();
            using (var selection = new MNNAcousticSpeakerSelection(Settings(), factory))
            {
                selection.Load("unused", CancellationToken.None);
                Assert.AreSame(factory.First, selection.PrepareVoiceAsync(factory.Mix(1, .75f), CancellationToken.None).GetAwaiter().GetResult());
                factory.Reverse = true;
                Assert.AreSame(factory.First, selection.PrepareVoiceAsync(factory.Mix(.98f, 1.02f), CancellationToken.None).GetAwaiter().GetResult());
                selection.ResetIdentity();
                Assert.IsNull(selection.PrepareVoiceAsync(factory.Mix(.98f, 1.02f), CancellationToken.None).GetAwaiter().GetResult(),
                    "Ambiguous evidence after identity reset must not pick a slot arbitrarily.");
            }
        }

        [Test]
        public void InvalidCandidateShapeIsReportedAndFailedReleaseRetainsOnlyItsOwner()
        {
            var factory = new Factory { Malformed = true, FailSeparationDispose = true };
            var selection = new MNNAcousticSpeakerSelection(Settings(), factory);
            selection.Load("unused", CancellationToken.None);
            Assert.Throws<InvalidOperationException>(() => selection.PrepareVoiceAsync(factory.Mix(1, .75f), CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<AggregateException>(() => selection.Dispose());
            Assert.AreEqual(1, factory.SpeakerDisposals);
            factory.FailSeparationDispose = false; selection.Dispose();
            Assert.AreEqual(2, factory.SeparationDisposals);
            Assert.AreEqual(1, factory.SpeakerDisposals);
        }

        private static MNNAcousticSettings Settings() => new MNNAcousticSettings { enabled = true, profile = MNNAcousticProfile.AutomaticSpeaker };
        private sealed class Factory : IMNNAcousticModelFactory, IMNNAcousticSeparator, IMNNAcousticSpeakerEncoder
        {
            internal readonly float[] First = new float[8000], Second = new float[8000];
            internal bool Reverse, Malformed, FailSeparationDispose;
            internal int SeparationDisposals, SpeakerDisposals;
            internal Factory()
            {
                for (int i = 0; i < First.Length; ++i)
                { First[i] = (i % 2 == 0 ? .4f : -.4f); Second[i] = (i % 4 < 2 ? .4f : -.4f); }
            }
            internal float[] Mix(float first, float second)
            { var pcm = new float[First.Length]; for (int i = 0; i < pcm.Length; ++i) pcm[i] = First[i] * first + Second[i] * second; return pcm; }
            public IMNNAcousticEnhancer CreateEnhancer(string path) => throw new NotSupportedException();
            public IMNNAcousticKeywordModel CreateKeyword(string path) => throw new NotSupportedException();
            public IMNNAcousticSeparator CreateSeparator(string path) => new Separator(this);
            public IMNNAcousticSpeakerEncoder CreateSpeaker(string path) => new Speaker(this);
            public float[][] Separate(float[] samples, CancellationToken token) => Malformed ? new[] { new float[1], Second } : Reverse ? new[] { Second, First } : new[] { First, Second };
            public float[] Encode(float[] samples, CancellationToken token) => samples[0] == samples[1] ? new[] { 0f, 1f } : new[] { 1f, 0f };
            public void Dispose() { }
            private sealed class Separator : IMNNAcousticSeparator
            {
                private readonly Factory _owner; internal Separator(Factory owner) => _owner = owner;
                public float[][] Separate(float[] samples, CancellationToken token) => _owner.Separate(samples, token);
                public void Dispose() { ++_owner.SeparationDisposals; if (_owner.FailSeparationDispose) throw new InvalidOperationException("release failure"); }
            }
            private sealed class Speaker : IMNNAcousticSpeakerEncoder
            {
                private readonly Factory _owner; internal Speaker(Factory owner) => _owner = owner;
                public float[] Encode(float[] samples, CancellationToken token) => _owner.Encode(samples, token);
                public void Dispose() => ++_owner.SpeakerDisposals;
            }
        }
    }
}
