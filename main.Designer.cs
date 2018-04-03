namespace MinTranslation
{
    partial class main
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(main));
            this.textBox = new System.Windows.Forms.TextBox();
            this.resultTextBox = new System.Windows.Forms.TextBox();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip_notifyIcon = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.Show_ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Exit_ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.read = new System.Windows.Forms.Label();
            this.contextMenuStrip_notifyIcon.SuspendLayout();
            this.SuspendLayout();
            // 
            // textBox
            // 
            this.textBox.AcceptsReturn = true;
            this.textBox.AcceptsTab = true;
            this.textBox.CausesValidation = false;
            this.textBox.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox.ImeMode = System.Windows.Forms.ImeMode.HangulFull;
            this.textBox.Location = new System.Drawing.Point(13, 12);
            this.textBox.Margin = new System.Windows.Forms.Padding(0, 0, 13, 0);
            this.textBox.MaxLength = 999999;
            this.textBox.Name = "textBox";
            this.textBox.Size = new System.Drawing.Size(292, 23);
            this.textBox.TabIndex = 0;
            this.textBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBox_KeyDown);
            this.textBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBox_KeyPress);
            this.textBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.textBox_KeyUp);
            // 
            // resultTextBox
            // 
            this.resultTextBox.AcceptsReturn = true;
            this.resultTextBox.AcceptsTab = true;
            this.resultTextBox.BackColor = System.Drawing.SystemColors.Control;
            this.resultTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.resultTextBox.CausesValidation = false;
            this.resultTextBox.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.resultTextBox.Location = new System.Drawing.Point(13, 42);
            this.resultTextBox.Margin = new System.Windows.Forms.Padding(0, 10, 0, 1);
            this.resultTextBox.MaximumSize = new System.Drawing.Size(314, 0);
            this.resultTextBox.MaxLength = 999999;
            this.resultTextBox.Multiline = true;
            this.resultTextBox.Name = "resultTextBox";
            this.resultTextBox.ReadOnly = true;
            this.resultTextBox.Size = new System.Drawing.Size(314, 0);
            this.resultTextBox.TabIndex = 1;
            this.resultTextBox.TabStop = false;
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.contextMenuStrip_notifyIcon;
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "MinTranslation";
            this.notifyIcon.Visible = true;
            this.notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon_MouseClick);
            // 
            // contextMenuStrip_notifyIcon
            // 
            this.contextMenuStrip_notifyIcon.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Show_ToolStripMenuItem,
            this.Exit_ToolStripMenuItem});
            this.contextMenuStrip_notifyIcon.Name = "contextMenuStrip_notifyIcon";
            this.contextMenuStrip_notifyIcon.Size = new System.Drawing.Size(101, 48);
            // 
            // Show_ToolStripMenuItem
            // 
            this.Show_ToolStripMenuItem.Name = "Show_ToolStripMenuItem";
            this.Show_ToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.Show_ToolStripMenuItem.Text = "显示";
            this.Show_ToolStripMenuItem.Click += new System.EventHandler(this.Show_ToolStripMenuItem_Click);
            // 
            // Exit_ToolStripMenuItem
            // 
            this.Exit_ToolStripMenuItem.Name = "Exit_ToolStripMenuItem";
            this.Exit_ToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.Exit_ToolStripMenuItem.Text = "退出";
            this.Exit_ToolStripMenuItem.Click += new System.EventHandler(this.Exit_ToolStripMenuItem_Click);
            // 
            // read
            // 
            this.read.AutoSize = true;
            this.read.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.read.Location = new System.Drawing.Point(309, 13);
            this.read.Name = "read";
            this.read.Size = new System.Drawing.Size(20, 19);
            this.read.TabIndex = 2;
            this.read.Text = "R";
            this.read.MouseHover += new System.EventHandler(this.read_MouseHover);
            // 
            // main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.CausesValidation = false;
            this.ClientSize = new System.Drawing.Size(339, 47);
            this.Controls.Add(this.read);
            this.Controls.Add(this.resultTextBox);
            this.Controls.Add(this.textBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.ImeMode = System.Windows.Forms.ImeMode.Off;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "main";
            this.Opacity = 0.9D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "MinTranslation";
            this.TopMost = true;
            this.Activated += new System.EventHandler(this.main_Activated);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.main_FormClosing);
            this.Shown += new System.EventHandler(this.Main_Shown);
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.main_KeyPress);
            this.contextMenuStrip_notifyIcon.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBox;
        private System.Windows.Forms.TextBox resultTextBox;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip_notifyIcon;
        private System.Windows.Forms.ToolStripMenuItem Show_ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem Exit_ToolStripMenuItem;
        private System.Windows.Forms.Label read;
    }
}

