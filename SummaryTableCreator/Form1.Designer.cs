namespace SummaryTableCreator
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            rtb_log = new RichTextBox();
            btn_import = new Button();
            nud_report_section = new NumericUpDown();
            btn_extract = new Button();
            btn_generate_table = new Button();
            lbl_filepath = new Label();
            cb_cvss = new CheckBox();
            cb_cvss_score = new CheckBox();
            pictureBox1 = new PictureBox();
            lbl_section_start = new Label();
            toolTip1 = new ToolTip(components);
            cb_recommendations = new CheckBox();
            cb_interim_excel = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)nud_report_section).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // rtb_log
            // 
            rtb_log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtb_log.BackColor = SystemColors.ActiveCaptionText;
            rtb_log.BorderStyle = BorderStyle.None;
            rtb_log.Font = new Font("Consolas", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            rtb_log.ForeColor = Color.SteelBlue;
            rtb_log.Location = new Point(313, 12);
            rtb_log.Name = "rtb_log";
            rtb_log.ReadOnly = true;
            rtb_log.Size = new Size(484, 532);
            rtb_log.TabIndex = 0;
            rtb_log.Text = "[+] Summary of Findings Table Generator v1.3";
            // 
            // btn_import
            // 
            btn_import.Font = new Font("Consolas", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btn_import.Location = new Point(12, 76);
            btn_import.Name = "btn_import";
            btn_import.Size = new Size(282, 33);
            btn_import.TabIndex = 1;
            btn_import.Text = "Import";
            btn_import.UseVisualStyleBackColor = true;
            btn_import.Click += btn_import_Click;
            // 
            // nud_report_section
            // 
            nud_report_section.Font = new Font("Consolas", 12F, FontStyle.Bold);
            nud_report_section.Location = new Point(240, 234);
            nud_report_section.Name = "nud_report_section";
            nud_report_section.Size = new Size(41, 26);
            nud_report_section.TabIndex = 3;
            nud_report_section.TextAlign = HorizontalAlignment.Center;
            nud_report_section.Value = new decimal(new int[] { 6, 0, 0, 0 });
            // 
            // btn_extract
            // 
            btn_extract.Font = new Font("Consolas", 12F, FontStyle.Bold);
            btn_extract.Location = new Point(13, 265);
            btn_extract.Name = "btn_extract";
            btn_extract.Size = new Size(281, 29);
            btn_extract.TabIndex = 4;
            btn_extract.Text = "Extract";
            btn_extract.UseVisualStyleBackColor = true;
            btn_extract.Click += btn_extract_Click;
            // 
            // btn_generate_table
            // 
            btn_generate_table.Font = new Font("Consolas", 12F, FontStyle.Bold);
            btn_generate_table.Location = new Point(13, 435);
            btn_generate_table.Name = "btn_generate_table";
            btn_generate_table.Size = new Size(282, 60);
            btn_generate_table.TabIndex = 5;
            btn_generate_table.Text = "Generate";
            btn_generate_table.UseVisualStyleBackColor = true;
            btn_generate_table.Click += btn_generate_table_Click;
            // 
            // lbl_filepath
            // 
            lbl_filepath.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lbl_filepath.Location = new Point(12, 112);
            lbl_filepath.Name = "lbl_filepath";
            lbl_filepath.Size = new Size(282, 103);
            lbl_filepath.TabIndex = 6;
            // 
            // cb_cvss
            // 
            cb_cvss.AutoSize = true;
            cb_cvss.Font = new Font("Consolas", 12F, FontStyle.Bold);
            cb_cvss.Location = new Point(12, 340);
            cb_cvss.Name = "cb_cvss";
            cb_cvss.Size = new Size(190, 23);
            cb_cvss.TabIndex = 7;
            cb_cvss.Text = "CHECK CVSS Scoring";
            cb_cvss.UseVisualStyleBackColor = true;
            // 
            // cb_cvss_score
            // 
            cb_cvss_score.AutoSize = true;
            cb_cvss_score.Font = new Font("Consolas", 12F, FontStyle.Bold);
            cb_cvss_score.Location = new Point(12, 363);
            cb_cvss_score.Name = "cb_cvss_score";
            cb_cvss_score.Size = new Size(217, 23);
            cb_cvss_score.TabIndex = 8;
            cb_cvss_score.Text = "Show CVSS Score (8.8)";
            cb_cvss_score.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            pictureBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pictureBox1.BackColor = SystemColors.ActiveCaptionText;
            pictureBox1.Location = new Point(300, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(509, 559);
            pictureBox1.TabIndex = 9;
            pictureBox1.TabStop = false;
            // 
            // lbl_section_start
            // 
            lbl_section_start.Font = new Font("Consolas", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lbl_section_start.Location = new Point(13, 236);
            lbl_section_start.Name = "lbl_section_start";
            lbl_section_start.Size = new Size(221, 26);
            lbl_section_start.TabIndex = 10;
            lbl_section_start.Text = "Start From Section:";
            // 
            // cb_recommendations
            // 
            cb_recommendations.AutoSize = true;
            cb_recommendations.Checked = true;
            cb_recommendations.CheckState = CheckState.Checked;
            cb_recommendations.Font = new Font("Consolas", 12F, FontStyle.Bold);
            cb_recommendations.Location = new Point(12, 409);
            cb_recommendations.Name = "cb_recommendations";
            cb_recommendations.Size = new Size(217, 23);
            cb_recommendations.TabIndex = 11;
            cb_recommendations.Text = "Populate Descriptions";
            cb_recommendations.UseVisualStyleBackColor = true;
            // 
            // cb_interim_excel
            // 
            cb_interim_excel.AutoSize = true;
            cb_interim_excel.Font = new Font("Consolas", 12F, FontStyle.Bold);
            cb_interim_excel.Location = new Point(12, 386);
            cb_interim_excel.Name = "cb_interim_excel";
            cb_interim_excel.Size = new Size(145, 23);
            cb_interim_excel.TabIndex = 12;
            cb_interim_excel.Text = "Interim Excel";
            cb_interim_excel.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.GradientActiveCaption;
            ClientSize = new Size(809, 556);
            Controls.Add(cb_interim_excel);
            Controls.Add(cb_recommendations);
            Controls.Add(lbl_section_start);
            Controls.Add(cb_cvss_score);
            Controls.Add(cb_cvss);
            Controls.Add(lbl_filepath);
            Controls.Add(btn_generate_table);
            Controls.Add(btn_extract);
            Controls.Add(nud_report_section);
            Controls.Add(btn_import);
            Controls.Add(rtb_log);
            Controls.Add(pictureBox1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimumSize = new Size(825, 595);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "vegeTables v1.3";
            ((System.ComponentModel.ISupportInitialize)nud_report_section).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RichTextBox rtb_log;
        private Button btn_import;
        private NumericUpDown nud_report_section;
        private Button btn_extract;
        private Button btn_generate_table;
        private Label lbl_filepath;
        private CheckBox cb_cvss;
        private CheckBox cb_cvss_score;
        private PictureBox pictureBox1;
        private Label lbl_section_start;
        private ToolTip toolTip1;
        private CheckBox cb_recommendations;
        private CheckBox cb_interim_excel;
    }
}
