using SpeechLib;
using System;

namespace MiniTranslation
{
    class Speech
    {
        private SpVoice spVoice = null;
        public Speech() {
            try { 
                spVoice = new SpVoice();
                spVoice.Rate = 0; //语速,[-10,10]
                spVoice.Volume = 100; //音量,[0,100]
                //spVoice.Voice = spVoice.GetVoices().Item(0); //语音库
                //voice.Speak(resultText);
            }
            catch (Exception e) {
                spVoice = null;
            }
        }
        public void Voice(String text) {
            if (spVoice != null)
            {
                spVoice.Speak(text, SpeechVoiceSpeakFlags.SVSFlagsAsync | SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
            }
        }

    }
}
