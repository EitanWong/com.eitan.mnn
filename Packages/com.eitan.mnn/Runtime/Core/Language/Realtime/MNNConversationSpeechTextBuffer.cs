using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    // One response owns this bounded text backlog. Audio remains bounded by its
    // existing channel; slow synthesis/playback never blocks the native callback.
    public class MNNConversationSpeechTextBuffer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
        private readonly int _maximumCharacters;
        private int _characters;
        private bool _completed;

        public MNNConversationSpeechTextBuffer(int maximumCharacters)
        {
            if (maximumCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            _maximumCharacters = maximumCharacters;
        }

        public void Append(string delta)
        {
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            lock (_gate)
            {
                if (_completed) throw new InvalidOperationException("Speech text is complete.");
                if (delta.Length > _maximumCharacters - _characters)
                    throw new InvalidOperationException("Speech text exceeded the response character budget.");
                if (delta.Length == 0) return;
                _characters += delta.Length;
                _pending.Enqueue(delta);
                _available.Release();
            }
        }

        public void Complete()
        {
            lock (_gate)
            {
                if (_completed) return;
                _completed = true;
                _available.Release();
            }
        }

        public async Task DrainAsync(Func<string, CancellationToken, Task> consume, CancellationToken token)
        {
            while (true)
            {
                await _available.WaitAsync(token).ConfigureAwait(false);
                string delta;
                lock (_gate)
                {
                    if (_pending.Count == 0 && _completed) return;
                    delta = _pending.Dequeue();
                }
                await consume(delta, token).ConfigureAwait(false);
            }
        }

        // The owner cancels and joins DrainAsync before disposing this buffer.
        public void Dispose() => _available.Dispose();
    }
}
