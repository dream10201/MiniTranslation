using SpeechLib;
using System;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MinTranslation
{
    public partial class main : Form
    {
        private const String googleUrl = "https://translate.google.cn/translate_a/single?client=gtx&dt=t&sl=";
        private const int WM_HOTKEY = 0x312; //窗口消息-热键  
        private const int WM_CREATE = 0x1; //窗口消息-创建  
        private const int WM_DESTROY = 0x2; //窗口消息-销毁  
        private const int Space = 0x3572; //热键ID
        private bool isShow = false;
        #region 内存回收
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        public static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);
        /// <summary>
        /// 释放内存
        /// </summary>
        public static void ClearMemory(object source, System.Timers.ElapsedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                main.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
            }
        }
        #endregion
        public main()
        {
            InitializeComponent();
            this.Hide();
            //初始speech
            InitSpeech();
            System.Timers.Timer timer = new System.Timers.Timer(10000);
            timer.Elapsed += new System.Timers.ElapsedEventHandler(ClearMemory);
            timer.AutoReset = true;
            timer.Enabled = true;
        }
        private SpVoice spVoice;
        private void InitSpeech() {
            spVoice = new SpVoice();
            spVoice.Rate = 0; //语速,[-10,10]
            spVoice.Volume = 100; //音量,[0,100]
            //spVoice.Voice = spVoice.GetVoices().Item(0); //语音库
            //voice.Speak(resultText);
        }
        //翻译
        private void Translation(Object obj) {
            String text = obj.ToString();
            bool en = true; 
            StringBuilder url = new StringBuilder(googleUrl);
            int zhNum = 0, enNum = 0;
            for (int i = 0; i < text.Length; i++)
            {
                //过滤数字
                if (!int.TryParse(text[i].ToString(), out int n))
                {
                    if (text[0] > 127)
                    {
                        //汉字
                        zhNum++;
                    }
                    else
                    {
                        //字母
                        enNum++;
                    }
                }
            }
            //判断汉字和字母占比类决定翻译
            if (enNum > zhNum) {
                url.Append("en&tl=zh-CN&q=");
                en = false;
            }
            else
            {
                url.Append("zh-CN&tl=en&q=");
            }
            url.Append(text);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            HttpHelper httpHelper = new HttpHelper();
            HttpItem httpItem = new HttpItem();
            httpItem.URL = url.ToString();
            httpItem.ResultType = ResultType.String;
            httpItem.Method = "get";
            HttpResult httpresult = httpHelper.GetHtml(httpItem);
            String resultHtml = httpresult.Html;
            //正则获取结果集
            Regex regex = new Regex("\\[\\\".*?\\\"");
            MatchCollection mc = regex.Matches(resultHtml);
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < mc.Count; i++)
            {
                result.Append(mc[i]);
            }
            String resultText = result.ToString().Replace("\"", "").Replace("[", "");
            this.BeginInvoke(new Action(()=> {
                this.resultTextBox.Text = resultText;
                //获取自动换行后的行数
                int num = this.resultTextBox.GetLineFromCharIndex(this.resultTextBox.TextLength)+1;
                this.resultTextBox.Size = new Size(this.resultTextBox.Width, num * 20);
            }));
            //朗读英文,停止上一朗读
            spVoice.Speak(en?resultText:text,SpeechVoiceSpeakFlags.SVSFlagsAsync |SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
        }
        private void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 13) {
                ThreadPool.QueueUserWorkItem(new WaitCallback(Translation),this.textBox.Text);
            }
        }
        private void FormStatus(bool status) {
            if (status)
            {
                this.Show();
                this.Activate();
                this.Focus();
                this.textBox.Focus();
                this.textBox.SelectAll();
            }
            else {
                spVoice.Speak("", SpeechVoiceSpeakFlags.SVSFlagsAsync | SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
                this.Hide();
            }
            isShow = status;
        }
        //重写窗体的WndProc函数，在窗口创建的时候注册热键，窗口销毁时销毁热键
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            switch (m.Msg)
            {
                case WM_HOTKEY: //窗口消息-热键ID  
                    switch (m.WParam.ToInt32())
                    {
                        case Space: //热键ID  
                            if (isShow) {
                                FormStatus(false);
                            }
                            else
                            {
                                FormStatus(true);
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case WM_CREATE: //窗口消息-创建  
                    AppHotKey.RegKey(Handle, Space,  AppHotKey.KeyModifiers.Alt, Keys.Q);
                    break;
                case WM_DESTROY: //窗口消息-销毁
                    AppHotKey.UnRegKey(Handle, Space); //销毁热键  
                    break;
                default:
                    break;
            }
        }

        private void main_Shown(object sender, EventArgs e)
        {
            this.Hide();
        }

        //托盘显示选项事件
        private void Show_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormStatus(true);
        }
        //托盘退出事件
        private void Exit_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //退出程序
            AppHotKey.UnRegKey(Handle, Space); //销毁热键
            this.notifyIcon.Dispose();
            this.Dispose();
            Environment.Exit(0);
        }
        //鼠标左键显示窗体
        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) {
                FormStatus(true);
            }
        }
        //窗体关闭事件
        private void main_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            FormStatus(false);
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == System.Convert.ToChar(13))
            {
                e.Handled = true;
            }
        }
    }
}
