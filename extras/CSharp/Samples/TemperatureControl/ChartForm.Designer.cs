namespace DataLogging
{
    partial class ChartForm
    {
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.temperaturePlot            = new ScottPlot.WinForms.FormsPlot();
            this.heaterPlot                 = new ScottPlot.WinForms.FormsPlot();
            this.GoalTemperatureValue       = new System.Windows.Forms.Label();
            this.GoalTemperatureLabel       = new System.Windows.Forms.Label();
            this.GoalTemperatureTrackBar    = new System.Windows.Forms.TrackBar();
            this.buttonStopAcquisition      = new System.Windows.Forms.Button();
            this.buttonStartAcquisition     = new System.Windows.Forms.Button();
            this.statusStrip1               = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelProgress = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel1      = new System.Windows.Forms.ToolStripStatusLabel();
            this.loggingView1               = new Tools.LoggingView();
            ((System.ComponentModel.ISupportInitialize)(this.GoalTemperatureTrackBar)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // temperaturePlot (top chart)
            this.temperaturePlot.Anchor = System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.temperaturePlot.Location = new System.Drawing.Point(12, 12);
            this.temperaturePlot.Name = "temperaturePlot";
            this.temperaturePlot.Size = new System.Drawing.Size(905, 350);
            this.temperaturePlot.TabIndex = 0;
            // heaterPlot (bottom chart)
            this.heaterPlot.Anchor = System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.heaterPlot.Location = new System.Drawing.Point(12, 368);
            this.heaterPlot.Name = "heaterPlot";
            this.heaterPlot.Size = new System.Drawing.Size(905, 270);
            this.heaterPlot.TabIndex = 1;
            // GoalTemperatureValue
            this.GoalTemperatureValue.AutoSize = true;
            this.GoalTemperatureValue.Location = new System.Drawing.Point(897, 651);
            this.GoalTemperatureValue.Name = "GoalTemperatureValue";
            this.GoalTemperatureValue.Size = new System.Drawing.Size(19, 15);
            this.GoalTemperatureValue.TabIndex = 6;
            this.GoalTemperatureValue.Text = "20";
            // GoalTemperatureLabel
            this.GoalTemperatureLabel.AutoSize = true;
            this.GoalTemperatureLabel.Location = new System.Drawing.Point(23, 652);
            this.GoalTemperatureLabel.Name = "GoalTemperatureLabel";
            this.GoalTemperatureLabel.Size = new System.Drawing.Size(103, 15);
            this.GoalTemperatureLabel.TabIndex = 5;
            this.GoalTemperatureLabel.Text = "Goal temperature";
            // GoalTemperatureTrackBar
            this.GoalTemperatureTrackBar.Location = new System.Drawing.Point(117, 649);
            this.GoalTemperatureTrackBar.Maximum = 1000;
            this.GoalTemperatureTrackBar.Name = "GoalTemperatureTrackBar";
            this.GoalTemperatureTrackBar.Size = new System.Drawing.Size(779, 45);
            this.GoalTemperatureTrackBar.TabIndex = 2;
            this.GoalTemperatureTrackBar.TickFrequency = 10;
            this.GoalTemperatureTrackBar.Value = 200;
            this.GoalTemperatureTrackBar.Scroll += new System.EventHandler(this.GoalTemperatureTrackBarScroll);
            // buttonStopAcquisition
            this.buttonStopAcquisition.Location = new System.Drawing.Point(117, 677);
            this.buttonStopAcquisition.Name = "buttonStopAcquisition";
            this.buttonStopAcquisition.Size = new System.Drawing.Size(98, 35);
            this.buttonStopAcquisition.TabIndex = 7;
            this.buttonStopAcquisition.Text = "Stop acquisition";
            this.buttonStopAcquisition.Click += new System.EventHandler(this.ButtonStopAcquisitionClick);
            // buttonStartAcquisition
            this.buttonStartAcquisition.Location = new System.Drawing.Point(13, 677);
            this.buttonStartAcquisition.Name = "buttonStartAcquisition";
            this.buttonStartAcquisition.Size = new System.Drawing.Size(98, 35);
            this.buttonStartAcquisition.TabIndex = 8;
            this.buttonStartAcquisition.Text = "Start acquisition";
            this.buttonStartAcquisition.Click += new System.EventHandler(this.ButtonStartAcquisitionClick);
            // statusStrip1
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.toolStripStatusLabelProgress, this.toolStripStatusLabel1 });
            this.statusStrip1.Location = new System.Drawing.Point(0, 810);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(929, 22);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 9;
            this.toolStripStatusLabelProgress.Name = "toolStripStatusLabelProgress";
            this.toolStripStatusLabelProgress.Size = new System.Drawing.Size(0, 17);
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(19, 17);
            this.toolStripStatusLabel1.Text = "    ";
            // loggingView1
            this.loggingView1.Anchor = System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Bottom
                | System.Windows.Forms.AnchorStyles.Left
                | System.Windows.Forms.AnchorStyles.Right;
            this.loggingView1.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.loggingView1.FollowLastItem = true;
            this.loggingView1.FormattingEnabled = true;
            this.loggingView1.Items.AddRange(new object[] { "Logging" });
            this.loggingView1.Location = new System.Drawing.Point(12, 722);
            this.loggingView1.MaxEntriesInListBox = 3000;
            this.loggingView1.Name = "loggingView1";
            this.loggingView1.Size = new System.Drawing.Size(905, 82);
            this.loggingView1.TabIndex = 11;
            this.loggingView1.SelectedIndexChanged += new System.EventHandler(this.loggingView1_SelectedIndexChanged);
            // ChartForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(929, 832);
            this.Controls.Add(this.loggingView1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.buttonStartAcquisition);
            this.Controls.Add(this.buttonStopAcquisition);
            this.Controls.Add(this.GoalTemperatureValue);
            this.Controls.Add(this.GoalTemperatureLabel);
            this.Controls.Add(this.GoalTemperatureTrackBar);
            this.Controls.Add(this.heaterPlot);
            this.Controls.Add(this.temperaturePlot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ChartForm";
            this.Text = "Temperature Controller";
            this.Shown += new System.EventHandler(this.ChartFormShown);
            ((System.ComponentModel.ISupportInitialize)(this.GoalTemperatureTrackBar)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        public ScottPlot.WinForms.FormsPlot temperaturePlot = null!;
        public ScottPlot.WinForms.FormsPlot heaterPlot = null!;
        private System.Windows.Forms.Label GoalTemperatureValue = null!;
        private System.Windows.Forms.Label GoalTemperatureLabel = null!;
        private System.Windows.Forms.TrackBar GoalTemperatureTrackBar = null!;
        private System.Windows.Forms.Button buttonStopAcquisition = null!;
        private System.Windows.Forms.Button buttonStartAcquisition = null!;
        private System.Windows.Forms.StatusStrip statusStrip1 = null!;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelProgress = null!;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1 = null!;
        private Tools.LoggingView loggingView1 = null!;
    }
}
