using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CommandMessenger;

namespace DataLogging
{
    public partial class ChartForm : Form
    {
        private readonly DataLogging _dataLogging;
        private long _previousChartUpdate;

        private readonly List<double> _times = new List<double>();
        private readonly List<double> _analog1 = new List<double>();
        private readonly List<double> _analog2 = new List<double>();

        public ChartForm()
        {
            InitializeComponent();
            _dataLogging = new DataLogging();
            _dataLogging.Setup(this);
        }

        public void SetupChart()
        {
            var plot = chartControl.Plot;
            plot.Title("Data logging using CmdMessenger");
            plot.XLabel("Time (s)");
            plot.YLabel("Voltage (v)");
            plot.Add.ScatterLine(_times, _analog1, ScottPlot.Color.FromColor(Color.Red)).LegendText = "Analog 1";
            plot.Add.ScatterLine(_times, _analog2, ScottPlot.Color.FromColor(Color.Blue)).LegendText = "Analog 2";
            plot.ShowLegend();
            chartControl.Refresh();
        }

        public void UpdateGraph(double time, double analog1, double analog2)
        {
            _times.Add(time);
            _analog1.Add(analog1);
            _analog2.Add(analog2);

            // Keep rolling buffer bounded
            const int maxPoints = 3000;
            if (_times.Count > maxPoints)
            {
                _times.RemoveAt(0);
                _analog1.RemoveAt(0);
                _analog2.RemoveAt(0);
            }

            if (!TimeUtils.HasExpired(ref _previousChartUpdate, 100)) return;

            const double windowWidth = 30.0;
            chartControl.Plot.Axes.SetLimitsX(time - windowWidth, time);
            chartControl.Plot.Axes.AutoScaleY();
            chartControl.Refresh();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dataLogging.Exit();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
