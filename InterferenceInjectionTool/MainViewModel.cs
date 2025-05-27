using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace InterferenceInjectionTool
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _currentTimeStep = 0;
        private List<List<double>> _timeStepsPsdValues = new List<List<double>>();
        private List<double> _frequencies = new List<double>();

        private int _vectorLength = 100;
        public int VectorLength
        {
            get => _vectorLength;
            set { _vectorLength = value; OnPropertyChanged(nameof(VectorLength)); }
        }

        private int _offset = 0;
        public int Offset
        {
            get => _offset;
            set { _offset = value; OnPropertyChanged(nameof(Offset)); }
        }

        public PlotModel CleanSignalModel { get; private set; }
        public PlotModel InterferenceSignalModel { get; private set; }
        public PlotModel PreviewSignalModel { get; private set; }

        public ICommand ImportCSVCommand { get; set; }
        public ICommand AddInterferenceCommand { get; set; }
        public ICommand PreviousTimeStepCommand { get; set; }
        public ICommand NextTimeStepCommand { get; set; }

        public MainViewModel()
        {
            ImportCSVCommand = new RelayCommand(ImportCSV);
            AddInterferenceCommand = new RelayCommand(AddInterference);
            PreviousTimeStepCommand = new RelayCommand(ShowPreviousTimeStep);
            NextTimeStepCommand = new RelayCommand(ShowNextTimeStep);

            CleanSignalModel = new PlotModel { Title = "Clean Signal" };
            InterferenceSignalModel = new PlotModel { Title = "Interference" };
            PreviewSignalModel = new PlotModel { Title = "Preview" };

            SetupGraph(CleanSignalModel);
            SetupGraph(InterferenceSignalModel);
            SetupGraph(PreviewSignalModel);
        }

        private void SetupGraph(PlotModel model)
        {
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Frequency (Hz)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "PSD (dBm)" });
        }

        private void ImportCSV()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;

                try
                {
                    var lines = File.ReadAllLines(filePath);
                    _timeStepsPsdValues.Clear();
                    _frequencies.Clear();

                    if (lines.Length < 2)
                    {
                        System.Windows.MessageBox.Show("Invalid CSV format!");
                        return;
                    }

                    var headers = lines[0].Split('\t');
                    int startFreqIndex = Array.IndexOf(headers, "SEG_START_FREQ");
                    int stopFreqIndex = Array.IndexOf(headers, "SEG_STOP_FREQ");
                    int psdIndex = Array.IndexOf(headers, "PSD_MEAS");

                    if (startFreqIndex == -1 || stopFreqIndex == -1 || psdIndex == -1)
                    {
                        System.Windows.MessageBox.Show("CSV file format incorrect: Missing required columns.");
                        return;
                    }

                    var firstDataRow = lines[1].Split('\t');
                    double startFreq = double.Parse(firstDataRow[startFreqIndex]);
                    double stopFreq = double.Parse(firstDataRow[stopFreqIndex]);
                    int numPoints = int.Parse(firstDataRow[3]);

                    double stepSize = (stopFreq - startFreq) / (numPoints - 1);
                    for (int i = 0; i < numPoints; i++)
                    {
                        _frequencies.Add(startFreq + (i * stepSize));
                    }

                    foreach (var line in lines.Skip(1))
                    {
                        var columns = line.Split('\t');
                        if (columns.Length <= psdIndex) continue;

                        string psdString = columns[psdIndex].Trim('[', ']');
                        var psdValues = psdString.Split(',')
                                                 .Select(value => double.Parse(value.Trim()))
                                                 .ToList();

                        _timeStepsPsdValues.Add(psdValues);
                    }

                    _currentTimeStep = 0;
                    UpdateGraph();
                    System.Windows.MessageBox.Show("CSV Imported Successfully!");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error importing CSV: {ex.Message}");
                }
            }
        }

        private void AddInterference()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadInterferenceData(openFileDialog.FileName);
            }
        }

        private void LoadInterferenceData(string filePath)
        {
            try
            {
                var allLines = File.ReadAllLines(filePath).Skip(1).ToList();
                var psdValues = allLines.Select(line => double.Parse(line.Trim())).ToList();

                if (Offset >= psdValues.Count)
                {
                    System.Windows.MessageBox.Show("Offset is larger than data length.");
                    return;
                }

                int count = Math.Min(VectorLength, psdValues.Count - Offset);
                var selectedValues = psdValues.Skip(Offset).Take(count).ToList();

                int cleanLength = _frequencies.Count;

                if (selectedValues.Count < cleanLength)
                {
                    int padCount = cleanLength - selectedValues.Count;
                    selectedValues.AddRange(Enumerable.Repeat(selectedValues.LastOrDefault(), padCount));
                }
                else if (selectedValues.Count > cleanLength)
                {
                    selectedValues = selectedValues.Take(cleanLength).ToList();
                }

       
                System.Diagnostics.Debug.WriteLine($"Clean signal: {cleanLength} pts, Interference: {selectedValues.Count} pts");

                InterferenceSignalModel.Series.Clear();
                var series = new LineSeries { Title = "Interference Signal" };

                for (int i = 0; i < selectedValues.Count; i++)
                {
                    series.Points.Add(new DataPoint(_frequencies[i], selectedValues[i]));
                }

                InterferenceSignalModel.Series.Add(series);
                InterferenceSignalModel.InvalidatePlot(true);
                OnPropertyChanged(nameof(InterferenceSignalModel));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load interference data: {ex.Message}");
            }
        }

        private void UpdateGraph()
        {
            if (_timeStepsPsdValues.Count == 0 || _currentTimeStep >= _timeStepsPsdValues.Count)
                return;

            var currentPsdValues = _timeStepsPsdValues[_currentTimeStep];

            CleanSignalModel.Series.Clear();
            var series = new LineSeries { Title = $"Time Step {_currentTimeStep + 1}" };

            for (int i = 0; i < _frequencies.Count; i++)
            {
                series.Points.Add(new DataPoint(_frequencies[i], currentPsdValues[i]));
            }

            CleanSignalModel.Series.Add(series);
            CleanSignalModel.InvalidatePlot(true);
            OnPropertyChanged(nameof(CleanSignalModel));
        }

        private void ShowPreviousTimeStep()
        {
            if (_currentTimeStep > 0)
            {
                _currentTimeStep--;
                UpdateGraph();
            }
        }

        private void ShowNextTimeStep()
        {
            if (_currentTimeStep < _timeStepsPsdValues.Count - 1)
            {
                _currentTimeStep++;
                UpdateGraph();
            }
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
