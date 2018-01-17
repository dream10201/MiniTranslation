﻿using System;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MinTranslation
{
    public partial class main : Form
    {
        private const String googleUrl = "http://translate.google.cn/translate_a/single?client=gtx&sl=#current#&tl=#aims#&dt=t&q=";
        private const int WM_HOTKEY = 0x312; //窗口消息-热键  
        private const int WM_CREATE = 0x1; //窗口消息-创建  
        private const int WM_DESTROY = 0x2; //窗口消息-销毁  
        private const int Space = 0x3572; //热键ID
        private bool isShow = false;
        public main()
        {
            InitializeComponent();
            this.Hide();
        }
        //翻译
        private void Translation(Object obj) {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            //翻译
            String text = obj.ToString();
            StringBuilder url = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (!int.TryParse(text[i].ToString(), out int n))
                {
                    if (text[0] > 127)
                    {
                        //汉字开头
                        url.Append(googleUrl.Replace("#current#", "zh-CN").Replace("#aims#", "en"));
                    }
                    else
                    {
                        //字母开头
                        url.Append(googleUrl.Replace("#current#", "en").Replace("#aims#", "zh-CN"));
                    }
                    break;
                }
            }
            url.Append(text);
            HttpHelper httpHelper = new HttpHelper();
            HttpItem httpItem = new HttpItem();
            httpItem.URL = url.ToString();
            httpItem.ResultType = ResultType.String;
            httpItem.Method = "get";
            HttpResult httpresult = httpHelper.GetHtml(httpItem);
            String resultHtml = httpresult.Html;
            Regex regex = new Regex("\\[\\\".*?\\\"");
            MatchCollection mc = regex.Matches(resultHtml);
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < mc.Count; i++)
            {
                result.Append(mc[i]);
            }
            String resultText = result.ToString().Replace("\"", "").Replace("[", "");
            int num = resultText.Length > 24 ? resultText.Length / 24 + 1 : 1;
            this.BeginInvoke(new Action(()=> {
                this.resultTextBox.Text = resultText;
                this.resultTextBox.Size = new Size(this.resultTextBox.Width, num * this.resultTextBox.Font.Height + this.resultTextBox.Font.Height / 8);
            }));
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
                this.textBox.Select();
            }
            else {
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
            System.Environment.Exit(0);
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
    }
}
