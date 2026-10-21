namespace AOITest
{
    partial class Form1
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
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.btnCreateConfig = new System.Windows.Forms.Button();
            this.OpenCurrentFile = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.MesOutPut = new System.Windows.Forms.Button();
            this.LoginCheck = new System.Windows.Forms.Button();
            this.btnBarCodeCheck = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.strBarCode = new System.Windows.Forms.TextBox();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.tabControl2 = new System.Windows.Forms.TabControl();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.btnGetBadMark = new System.Windows.Forms.Button();
            this.btnSendDataForDefaultMES = new System.Windows.Forms.Button();
            this.btnGetBarCodeResult = new System.Windows.Forms.Button();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.btnMessageHandle = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.tabControl2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.SuspendLayout();
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(52, 53);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(141, 20);
            this.comboBox1.TabIndex = 0;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // btnCreateConfig
            // 
            this.btnCreateConfig.Location = new System.Drawing.Point(235, 26);
            this.btnCreateConfig.Name = "btnCreateConfig";
            this.btnCreateConfig.Size = new System.Drawing.Size(132, 38);
            this.btnCreateConfig.TabIndex = 1;
            this.btnCreateConfig.Text = "根据XML生成注入配置";
            this.btnCreateConfig.UseVisualStyleBackColor = true;
            this.btnCreateConfig.Click += new System.EventHandler(this.btnCreateConfig_Click);
            // 
            // OpenCurrentFile
            // 
            this.OpenCurrentFile.Location = new System.Drawing.Point(235, 70);
            this.OpenCurrentFile.Name = "OpenCurrentFile";
            this.OpenCurrentFile.Size = new System.Drawing.Size(132, 29);
            this.OpenCurrentFile.TabIndex = 2;
            this.OpenCurrentFile.Text = "打开当前文件夹";
            this.OpenCurrentFile.UseVisualStyleBackColor = true;
            this.OpenCurrentFile.Click += new System.EventHandler(this.OpenCurrentFile_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.comboBox2);
            this.groupBox1.Controls.Add(this.comboBox1);
            this.groupBox1.Controls.Add(this.OpenCurrentFile);
            this.groupBox1.Controls.Add(this.btnCreateConfig);
            this.groupBox1.Location = new System.Drawing.Point(12, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(398, 181);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "EY";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(23, 58);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(23, 12);
            this.label3.TabIndex = 5;
            this.label3.Text = "DLL";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(-1, 32);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 12);
            this.label2.TabIndex = 4;
            this.label2.Text = "SPI/AOI";
            // 
            // comboBox2
            // 
            this.comboBox2.AutoCompleteCustomSource.AddRange(new string[] {
            "SPI",
            "AOI"});
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Location = new System.Drawing.Point(52, 26);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(141, 20);
            this.comboBox2.TabIndex = 3;
            this.comboBox2.SelectedIndexChanged += new System.EventHandler(this.comboBox2_SelectedIndexChanged);
            // 
            // MesOutPut
            // 
            this.MesOutPut.Location = new System.Drawing.Point(21, 105);
            this.MesOutPut.Name = "MesOutPut";
            this.MesOutPut.Size = new System.Drawing.Size(151, 39);
            this.MesOutPut.TabIndex = 5;
            this.MesOutPut.Text = "MesOutPut-数据导出";
            this.MesOutPut.UseVisualStyleBackColor = true;
            this.MesOutPut.Click += new System.EventHandler(this.MesOutPut_Click);
            // 
            // LoginCheck
            // 
            this.LoginCheck.Location = new System.Drawing.Point(21, 15);
            this.LoginCheck.Name = "LoginCheck";
            this.LoginCheck.Size = new System.Drawing.Size(151, 39);
            this.LoginCheck.TabIndex = 4;
            this.LoginCheck.Text = "LoginCheck-登录验证";
            this.LoginCheck.UseVisualStyleBackColor = true;
            this.LoginCheck.Click += new System.EventHandler(this.LoginCheck_Click);
            // 
            // btnBarCodeCheck
            // 
            this.btnBarCodeCheck.Location = new System.Drawing.Point(21, 60);
            this.btnBarCodeCheck.Name = "btnBarCodeCheck";
            this.btnBarCodeCheck.Size = new System.Drawing.Size(151, 39);
            this.btnBarCodeCheck.TabIndex = 3;
            this.btnBarCodeCheck.Text = "BarCodeCheck-条码验证 ";
            this.btnBarCodeCheck.UseVisualStyleBackColor = true;
            this.btnBarCodeCheck.Click += new System.EventHandler(this.btnBarCodeCheck_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.textBox1);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.strBarCode);
            this.groupBox3.Location = new System.Drawing.Point(416, 2);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(551, 182);
            this.groupBox3.TabIndex = 5;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "测试参数";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(79, 53);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(116, 21);
            this.textBox1.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 29);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "BarCode";
            // 
            // strBarCode
            // 
            this.strBarCode.Location = new System.Drawing.Point(79, 26);
            this.strBarCode.Name = "strBarCode";
            this.strBarCode.Size = new System.Drawing.Size(116, 21);
            this.strBarCode.TabIndex = 0;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Location = new System.Drawing.Point(3, 3);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(555, 281);
            this.richTextBox1.TabIndex = 6;
            this.richTextBox1.Text = "";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(416, 190);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(569, 313);
            this.tabControl1.TabIndex = 8;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.richTextBox1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(561, 287);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "日志";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.dataGridView1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(561, 287);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "配置文件";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(3, 3);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(555, 281);
            this.dataGridView1.TabIndex = 0;
            // 
            // tabControl2
            // 
            this.tabControl2.Controls.Add(this.tabPage3);
            this.tabControl2.Controls.Add(this.tabPage4);
            this.tabControl2.Location = new System.Drawing.Point(12, 190);
            this.tabControl2.Name = "tabControl2";
            this.tabControl2.SelectedIndex = 0;
            this.tabControl2.Size = new System.Drawing.Size(398, 306);
            this.tabControl2.TabIndex = 9;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.btnGetBadMark);
            this.tabPage3.Controls.Add(this.btnSendDataForDefaultMES);
            this.tabPage3.Controls.Add(this.btnGetBarCodeResult);
            this.tabPage3.Controls.Add(this.MesOutPut);
            this.tabPage3.Controls.Add(this.LoginCheck);
            this.tabPage3.Controls.Add(this.btnBarCodeCheck);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(390, 280);
            this.tabPage3.TabIndex = 0;
            this.tabPage3.Text = "AOI";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // btnGetBadMark
            // 
            this.btnGetBadMark.Location = new System.Drawing.Point(21, 150);
            this.btnGetBadMark.Name = "btnGetBadMark";
            this.btnGetBadMark.Size = new System.Drawing.Size(151, 39);
            this.btnGetBadMark.TabIndex = 8;
            this.btnGetBadMark.Text = "GetBadMark-获取坏板";
            this.btnGetBadMark.UseVisualStyleBackColor = true;
            this.btnGetBadMark.Click += new System.EventHandler(this.btnGetBadMark_Click);
            // 
            // btnSendDataForDefaultMES
            // 
            this.btnSendDataForDefaultMES.Location = new System.Drawing.Point(178, 60);
            this.btnSendDataForDefaultMES.Name = "btnSendDataForDefaultMES";
            this.btnSendDataForDefaultMES.Size = new System.Drawing.Size(151, 39);
            this.btnSendDataForDefaultMES.TabIndex = 7;
            this.btnSendDataForDefaultMES.Text = "SendDataForDefaultMES";
            this.btnSendDataForDefaultMES.UseVisualStyleBackColor = true;
            this.btnSendDataForDefaultMES.Click += new System.EventHandler(this.btnSendDataForDefaultMES_Click);
            // 
            // btnGetBarCodeResult
            // 
            this.btnGetBarCodeResult.Location = new System.Drawing.Point(178, 15);
            this.btnGetBarCodeResult.Name = "btnGetBarCodeResult";
            this.btnGetBarCodeResult.Size = new System.Drawing.Size(151, 39);
            this.btnGetBarCodeResult.TabIndex = 6;
            this.btnGetBarCodeResult.Text = "GetBarCodeResult";
            this.btnGetBarCodeResult.UseVisualStyleBackColor = true;
            this.btnGetBarCodeResult.Click += new System.EventHandler(this.btnGetBarCodeResult_Click);
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.btnMessageHandle);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(390, 280);
            this.tabPage4.TabIndex = 1;
            this.tabPage4.Text = "SPI";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // btnMessageHandle
            // 
            this.btnMessageHandle.Location = new System.Drawing.Point(6, 6);
            this.btnMessageHandle.Name = "btnMessageHandle";
            this.btnMessageHandle.Size = new System.Drawing.Size(151, 39);
            this.btnMessageHandle.TabIndex = 5;
            this.btnMessageHandle.Text = "MessageHandle";
            this.btnMessageHandle.UseVisualStyleBackColor = true;
            this.btnMessageHandle.Click += new System.EventHandler(this.btnMessageHandle_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(235, 105);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(132, 29);
            this.button1.TabIndex = 6;
            this.button1.Text = "清除日志";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1014, 515);
            this.Controls.Add(this.tabControl2);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.tabControl2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Button btnCreateConfig;
        private System.Windows.Forms.Button OpenCurrentFile;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnBarCodeCheck;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox strBarCode;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button LoginCheck;
        private System.Windows.Forms.Button MesOutPut;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TabControl tabControl2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button btnMessageHandle;
        private System.Windows.Forms.Button btnGetBarCodeResult;
        private System.Windows.Forms.Button btnSendDataForDefaultMES;
        private System.Windows.Forms.Button btnGetBadMark;
        private System.Windows.Forms.Button button1;
    }
}
