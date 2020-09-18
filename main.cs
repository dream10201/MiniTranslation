using MiniTranslation.resource;
using MiniTranslation.util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MiniTranslation
{
    public partial class main : Form
    {
        private bool isShow = false;
        private Speech speech;
        private StringBuilder soundText = new StringBuilder();
        private const string enzh = "en&tl=zh-CN&q=";
        private const string zhen = "zh-CN&tl=en&q=";

        private Regex regex = new Regex("(?<=\\[\\\").+?(?=\\\",\\\")");
        bool en = true;
        int zhNum = 0, enNum = 0;
        public main()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
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
        private void Translation(Object obj)
        {
            en = true;
            StringBuilder url = new StringBuilder(Constant.TranslateURL);
            zhNum = 0;
            enNum = 0;
            string text = obj.ToString();
            for (int i = 0; i < text.ToString().Length; i++)
            {
                char tc = text.ToString()[i];
                //过滤数字
                if (!int.TryParse(tc.ToString(), out int n))
                {
                    if (tc > 127)
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
            //判断汉字和字母占比决定翻译方向
            if (enNum > zhNum)
            {
                url.Append(enzh);
                en = false;
            }
            else
            {
                url.Append(zhen);
            }
            url.Append(UrlEncode(text));
            string httpresult = HttpUtils.Get(url.ToString());
            //正则获取结果集
            Debug.WriteLine(httpresult);
            httpresult = regex.Matches(httpresult).Count>0? regex.Matches(httpresult)[0].Value:"";
            this.BeginInvoke(new Action(() =>
            {
                this.resultTextBox.Text = httpresult.Replace(@"\""", "\"");
                //获取自动换行后的行数
                int num = this.resultTextBox.GetLineFromCharIndex(this.resultTextBox.TextLength) + 1;
                this.resultTextBox.Size = new Size(this.resultTextBox.Width, num * 20);
            }));
            soundText.Clear();
            soundText.Append(en ? httpresult : text);
            Thread.CurrentThread.Abort();
        }
        private void FormStatus(bool status)
        {
            if (status)
            {
                this.Show();
                this.Activate();
                this.textBox.SelectAll();
            }
            else
            {
                speech.Voice("");
                this.Hide();
            }
            isShow = status;
        }
        //重写窗体的WndProc函数，在窗口创建的时候注册热键，窗口销毁时销毁热键
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Constant.WM_QUERYENDSESSION:
                    Exit_ToolStripMenuItem_Click(null, null);
                    m.Result = (IntPtr)0;
                    break;
                case Constant.WM_HOTKEY: //窗口消息-热键ID  
                    switch (m.WParam.ToInt32())
                    {
                        case Constant.HOTKeyID: //热键ID  
                            if (isShow)
                            {
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
                case Constant.WM_CREATE: //窗口消息-创建  
                    if (!AppHotKey.RegKey(Handle, Constant.HOTKeyID, AppHotKey.KeyModifiers.Alt, Keys.Q)) {
                        Application.Exit();
                    }
                    break;
                case Constant.WM_DESTROY: //窗口消息-销毁
                    AppHotKey.UnRegKey(Handle, Constant.HOTKeyID); //销毁热键  
                    break;
                default:
                    break;
            }
            base.WndProc(ref m);
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
            AppHotKey.UnRegKey(Handle, Constant.HOTKeyID); //销毁热键
            this.notifyIcon.Dispose();
            this.Dispose();
            Environment.Exit(0);
        }
        //鼠标左键显示窗体
        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
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
                    ThreadPool.QueueUserWorkItem(new WaitCallback(Translation), this.textBox.Text.ToLower());
                    break;
                //解决MultiLine=True之后，Ctrl+A 无法全选
                case '\x1':
                    ((TextBox)sender).SelectAll();
                    e.Handled = true;
                    break;
            }
        }
        //窗体Activated事件
        private void main_Activated(object sender, EventArgs e)
        {
            this.textBox.Focus();
            this.textBox.SelectionStart = this.textBox.Text.Length;
            this.textBox.ImeMode = ImeMode.Off;
            //ImmSimulateHotKey(this.Handle, IME_CHOTKEY_SHAPE_TOGGLE);  //转换成半角
        }

        private void main_KeyPress(object sender, KeyPressEventArgs e)
        {
            //Esc
            if (e.KeyChar == '\u001b')
            {
                FormStatus(false);
            }
        }
        private void read_MouseHover(object sender, EventArgs e)
        {
            //朗读英文,停止上一朗读
            speech.Voice(soundText.ToString());
        }
        //朗读标签鼠标点击事件
        private void read_MouseClick(object sender, MouseEventArgs e)
        {
            Reading();
        }
        public void Reading()
        {
            if (this.resultTextBox.Height > 0)
            {
                speech.Voice(soundText.ToString());
            }
        }
        //private static readonly String[] symbols = { "\r\n", "\r", "\n", "\t", "#", "<p>", "</p>", "<", ">", "//", "/*", "/**", "*/", "**/", "*", "_" };
        //替换文本框换行符
        private void textBox_TextChanged(object sender, EventArgs e)
        {
            int position = this.textBox.SelectionStart;
            String temp = textBox.Text;
            /*for (int i = 0; i < symbols.Length; i++)
            {
                temp = temp.Replace(symbols[i], " ");
            }*/
            //去除多余空格
            textBox.Text = Regex.Replace(temp, "\\s{2,}", " ");
            this.textBox.SelectionStart = position;
        }

        private void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode.ToString())
            {
                case "Menu":
                    //通知系统，执行完毕，防止Alt键使TextBox丢失焦点导致界面显示后第一个按键无法输入
                    e.Handled = true;
                    break;
                case "Tab":
                    //朗读
                    Reading();
                    e.Handled = true;
                    break;
            }
        }
    }
}