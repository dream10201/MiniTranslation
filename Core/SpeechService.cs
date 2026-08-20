using System.Speech.Synthesis;

namespace MiniTranslation.Core
{
    /// <summary>基于 Windows 语音合成的朗读服务。</summary>
    public sealed class SpeechService : IDisposable
    {
        private readonly SpeechSynthesizer? _synthesizer;

        public SpeechService()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer { Rate = 0, Volume = 100 };
            }
            catch
            {
                // 无可用语音引擎时禁用朗读
                _synthesizer = null;
            }
        }

        /// <summary>异步朗读，打断上一次未完成的朗读。</summary>
        public void Speak(string text)
        {
            if (_synthesizer == null) return;
            _synthesizer.SpeakAsyncCancelAll();
            if (!string.IsNullOrWhiteSpace(text))
            {
                _synthesizer.SpeakAsync(text);
            }
        }

        public void Stop() => _synthesizer?.SpeakAsyncCancelAll();

        public void Dispose() => _synthesizer?.Dispose();
    }
}
