namespace DataLogging
{
    partial class ChartForm
    {
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.chartControl = new ScottPlot.WinForms.FormsPlot();
            this.SuspendLayout();
            // chartControl
            this.chartControl.Anchor = System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Bottom
                | System.Windows.Forms.AnchorStyles.Left
                | System.Windows.Forms.AnchorStyles.Right;
            this.chartControl.Location = new System.Drawing.Point(12, 12);
            this.chartControl.Name = "chartControl";
            this.chartControl.Size = new System.Drawing.Size(521, 442);
            this.chartControl.TabIndex = 0;
            // ChartForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(545, 466);
            this.Controls.Add(this.chartControl);
            this.Name = "ChartForm";
            this.Text = "Data Logging and Charting";
            this.ResumeLayout(false);
        }

        #endregion

        public ScottPlot.WinForms.FormsPlot chartControl = null!;
    }
}
