using System;
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
            StringBuilder text = new StringBuilder();
            text.Append(obj.ToString().ToLower());
            bool en = true; 
            StringBuilder url = new StringBuilder(googleUrl);
            int zhNum = 0, enNum = 0;
            for (int i = 0; i < text.ToString().Length; i++)
            {
                //过滤数字
                if (!int.TryParse(text.ToString()[i].ToString(), out int n))
                {
                    if (text.ToString()[i] > 127)
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
            url.Append(UrlEncode(text.ToString()));
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            HttpHelper httpHelper = new HttpHelper();
            HttpItem httpItem = new HttpItem();
            httpItem.URL = url.ToString();
            httpItem.ResultType = ResultType.String;
            httpItem.Method = "post";
            HttpResult httpresult = httpHelper.GetHtml(httpItem);
            StringBuilder resultHtml = new StringBuilder();
            resultHtml.Append(httpresult.Html);
            //正则获取结果集
            Regex regex = new Regex("\\[\\\".*?\\\"");
            MatchCollection mc = regex.Matches(resultHtml.ToString());
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < mc.Count; i++)
            {
                result.Append(mc[i]);
            }
            StringBuilder resultText = new StringBuilder();
            resultText.Append(result.ToString().Replace("\"", "").Replace("[", ""));
            this.BeginInvoke(new Action(()=> {
                this.resultTextBox.Text = resultText.ToString();
                //获取自动换行后的行数
                int num = this.resultTextBox.GetLineFromCharIndex(this.resultTextBox.TextLength)+1;
                this.resultTextBox.Size = new Size(this.resultTextBox.Width, num * 20);
            }));
            soundText.Clear();
            soundText.Append(en ? resultText.ToString() : text.ToString());
        }
        private void FormStatus(bool status) {
            if (status)
            {
                this.Show();
                this.Activate();
                this.textBox.SelectAll();
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
        }

        private void main_KeyPress(object sender, KeyPressEventArgs e)
        {
            //Esc
            if (e.KeyChar == '\u001b') {
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
        public void Reading() {
            if (this.resultTextBox.Height == 0)
            {
                speech.Voice("You're the best");
            }
            else
            {
                speech.Voice(soundText.ToString());
            }
        }
        private static readonly String[] symbols = { "\r\n", "\r", "\n", "\t","#","<p>", "</p>", "<", ">", "//", "/*", "/**", "*/", "**/", "*" };
        //替换文本框换行符
        private void textBox_TextChanged(object sender, EventArgs e)
        {
            int position = this.textBox.SelectionStart;
            String temp = textBox.Text;
            for (int i = 0; i < symbols.Length; i++) {
                temp = temp.Replace(symbols[i], " ");
            }
            //去除多余空格
            textBox.Text = Regex.Replace(temp, "\\s{2,}", " ");
            this.textBox.SelectionStart = position;
        }

        private void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode.ToString()) {
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