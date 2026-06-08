using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CommandMessenger;

namespace DataLogging
{
    public partial class ChartForm : Form
    {
        private readonly TemperatureControl _temperatureControl;
        private long _previousChartUpdate;
        private bool _connected;
        private double _goalTemperature;

        private readonly List<double> _times      = new List<double>();
        private readonly List<double> _currTemp   = new List<double>();
        private readonly List<double> _goalTemp   = new List<double>();
        private readonly List<double> _heaterTimes = new List<double>();
        private readonly List<double> _heaterVal  = new List<double>();
        private readonly List<double> _heaterPwm  = new List<double>();

        private const int MaxPoints = 3000;

        public ChartForm()
        {
            InitializeComponent();
            _temperatureControl = new TemperatureControl();
        }

        private void ChartFormShown(object sender, EventArgs e)
        {
            _temperatureControl.Setup(this);
        }

        public void SetupChart()
        {
            var tp = temperaturePlot.Plot;
            tp.Title("Temperature controller");
            tp.XLabel("Time (s)");
            tp.YLabel("Temperature (C)");
            tp.Add.ScatterLine(_times, _currTemp, ScottPlot.Color.FromColor(Color.Red)).LegendText = "Current temperature";
            tp.Add.ScatterLine(_times, _goalTemp, ScottPlot.Color.FromColor(Color.Blue)).LegendText = "Goal temperature";
            tp.ShowLegend();

            var hp = heaterPlot.Plot;
            hp.XLabel("Time (s)");
            hp.YLabel("Heater");
            hp.Add.ScatterLine(_heaterTimes, _heaterVal, ScottPlot.Color.FromColor(Color.YellowGreen));
            hp.Add.ScatterLine(_heaterTimes, _heaterPwm, ScottPlot.Color.FromColor(Color.Blue));

            temperaturePlot.Refresh();
            heaterPlot.Refresh();
        }

        public void UpdateGraph(double time, double currTemp, double goalTemp, double heaterValue, bool heaterPwmValue)
        {
            _times.Add(time);    _currTemp.Add(currTemp);    _goalTemp.Add(goalTemp);
            _heaterTimes.Add(time); _heaterVal.Add(heaterValue); _heaterPwm.Add(heaterPwmValue ? 1.05 : 0.05);

            TrimList(_times, _currTemp, _goalTemp);
            TrimList(_heaterTimes, _heaterVal, _heaterPwm);

            if (!TimeUtils.HasExpired(ref _previousChartUpdate, 10)) return;
            SetChartScale(time);
        }

        private static void TrimList(List<double> key, List<double> a, List<double> b)
        {
            while (key.Count > MaxPoints) { key.RemoveAt(0); a.RemoveAt(0); b.RemoveAt(0); }
        }

        private void SetChartScale(double time)
        {
            const double windowWidth = 30.0;
            double xMin = time < windowWidth ? 0 : time - windowWidth;
            double xMax = time < windowWidth ? windowWidth : time;

            temperaturePlot.Plot.Axes.SetLimitsX(xMin, xMax);
            temperaturePlot.Plot.Axes.AutoScaleY();
            heaterPlot.Plot.Axes.SetLimitsX(xMin, xMax);
            heaterPlot.Plot.Axes.AutoScaleY();

            temperaturePlot.Refresh();
            heaterPlot.Refresh();
        }

        public void SetConnected()    { _connected = true;  UpdateUi(); }
        public void SetDisConnected() { _connected = false; UpdateUi(); }

        private void UpdateUi()
        {
            buttonStartAcquisition.Enabled  = _connected;
            buttonStopAcquisition.Enabled   = _connected;
            temperaturePlot.Enabled         = _connected;
            heaterPlot.Enabled              = _connected;
            GoalTemperatureTrackBar.Enabled = _connected;
            GoalTemperatureValue.Enabled    = _connected;
        }

        public void GoalTemperatureTrackBarScroll(object sender, EventArgs e)
        {
            _goalTemperature = GoalTemperatureTrackBar.Value / 10.0;
            GoalTemperatureValue.Text = _goalTemperature.ToString(CultureInfo.InvariantCulture);
            _temperatureControl.GoalTemperature = _goalTemperature;
        }

        private void ButtonStopAcquisitionClick(object sender, EventArgs e)  => _temperatureControl.StopAcquisition();
        private void ButtonStartAcquisitionClick(object sender, EventArgs e) => _temperatureControl.StartAcquisition();

        public void SetStatus(string description) { toolStripStatusLabel1.Text = description; }
        public void LogMessage(string message)    { loggingView1.AddEntry(message); }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e) { }
        private void loggingView1_SelectedIndexChanged(object sender, EventArgs e) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _temperatureControl.Exit();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
