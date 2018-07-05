using SpeechLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MiniTranslation
{
    class Speech
    {
        private SpVoice spVoice;
        private static bool flag=true;
        public Speech() {
            try { 
                spVoice = new SpVoice();
                spVoice.Rate = 0; //语速,[-10,10]
                spVoice.Volume = 100; //音量,[0,100]
                //spVoice.Voice = spVoice.GetVoices().Item(0); //语音库
                //voice.Speak(resultText);
            }
            catch (Exception e) {
                flag = false;
            }
        }
        public void Voice(String text) {
            if (!flag) return;
            spVoice.Speak(text, SpeechVoiceSpeakFlags.SVSFlagsAsync | SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
        }

    }
}
