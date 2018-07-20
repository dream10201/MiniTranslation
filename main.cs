﻿using System;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MiniTranslation
{
    public partial class main : Form
    {
        private const String googleUrl = "https://translate.google.cn/translate_a/single?client=gtx&dt=t&sl=";
        private const int WM_HOTKEY = 0x312; //窗口消息-热键  
        private const int WM_CREATE = 0x1; //窗口消息-创建  
        private const int WM_DESTROY = 0x2; //窗口消息-销毁  
        private const int Space = 0x3572; //热键ID
        private bool isShow = false;
        private Speech speech;
        private StringBuilder soundText=new StringBuilder();
        public main()
        {
            InitializeComponent();
            speech = new Speech();
        }
        //URL编码
        public static string UrlEncode(string str)
        {
            StringBuilder sb = new StringBuilder();
            byte[] byStr = Encoding.UTF8.GetBytes(str); //默认是System.Text.Encoding.Default.GetBytes(str)
            for (int i = 0; i < byStr.Length; i++)
            {
                sb.Append(@"%" + Convert.ToString(byStr[i], 16));
            }

            return (sb.ToString());
        }
        //翻译
        private void Translation(Object obj) {
            String text = obj.ToString().ToLower();
            bool en = true; 
            StringBuilder url = new StringBuilder(googleUrl);
            int zhNum = 0, enNum = 0;
            for (int i = 0; i < text.Length; i++)
            {
                //过滤数字
                if (!int.TryParse(text[i].ToString(), out int n))
                {
                    if (text[i] > 127)
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
            url.Append(UrlEncode(text));
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            HttpHelper httpHelper = new HttpHelper();
            HttpItem httpItem = new HttpItem();
            httpItem.URL = url.ToString();
            httpItem.ResultType = ResultType.String;
            httpItem.Method = "post";
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
            soundText.Clear();
            soundText.Append(en ? resultText : text);
        }
        private void FormStatus(bool status) {
            if (status)
            {
                this.Show();
                this.Activate();
            }
            else {
                speech.Voice("");
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

        private void Main_Shown(object sender, EventArgs e)
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
            switch (e.KeyChar)
            {
                //粘贴键
                //case '\u0016':
                    //e.Handled = true;   //屏蔽粘贴
                //    replaceClipboard();
                //    break;
                //回车键
                case '\r':
                    e.Handled = true;
                    ThreadPool.QueueUserWorkItem(new WaitCallback(Translation), this.textBox.Text);
                    break;
            }
        }
        //窗体Activated事件
        private void main_Activated(object sender, EventArgs e)
        {
            this.textBox.Focus();
        }

        private void main_KeyPress(object sender, KeyPressEventArgs e)
        {
            //Esc
            if (e.KeyChar == '\u001b') {
                FormStatus(false);
            }
        }
        //替换剪切板文本内容
        public void replaceClipboard()
        {
            Console.WriteLine(textBox.Text);
            IDataObject iData = Clipboard.GetDataObject();
            if (iData.GetDataPresent(DataFormats.Text))
            {
                //if (iData.GetData(DataFormats.Text).ToString().IndexOf("\n") != -1) {
                //    Clipboard.SetDataObject(iData.GetData(DataFormats.Text).ToString().Replace("\n", " "), true);
                //}
                //Clipboard.SetDataObject(iData.GetData(DataFormats.Text).ToString().Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Replace("\t", " "), true);
                //设置文本框文本
                //textBox.Text = iData.GetData(DataFormats.Text).ToString().Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                //开始翻译
                //ThreadPool.QueueUserWorkItem(new WaitCallback(Translation), this.textBox.Text);
            }
        }
        private void read_MouseHover(object sender, EventArgs e)
        {
            //朗读英文,停止上一朗读
            speech.Voice(soundText.ToString());
        }
        //朗读标签鼠标点击时间
        private void read_MouseClick(object sender, MouseEventArgs e)
        {
            if (this.resultTextBox.Height == 0)
            {
                speech.Voice("You're the best");
            }
            else
            {
                speech.Voice(soundText.ToString());
            }
        }
        //替换文本框换行符
        private void textBox_TextChanged(object sender, EventArgs e)
        {
            textBox.Text = textBox.Text.Replace("\r\n", " ");
        }
    }
}