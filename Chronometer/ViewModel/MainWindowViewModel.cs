﻿using una.regataiade.chronograph;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System.IO;
using System;
using System.Windows.Forms;
using una.regataiade;
using una.regataiade.excel;

namespace Chronometer.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private SerialPortManager _serialPortManager = new SerialPortManager();
        private WorksheetManager _worksheetManager = new WorksheetManager();
        private FileStream _file;
        private StreamWriter _writer;

        public RelayCommand RefreshAvailableCOMPortsCommand { get; private set; }
        public RelayCommand CloseCOMPortsCommand { get; private set; }
        public RelayCommand InitializeTraceFileMenuItemCommand { get; private set; }
        public RelayCommand ClearListMenuItemCommand { get; private set; }
        public RelayCommand QuitMenuItemCommand { get; private set; }
        public RelayCommand AboutMenuItemCommand { get; private set; }
        public RelayCommand OpenChronographLinkCommand { get; set; }

        public RelayCommand OpenExcelCommand { get; private set; }
        public RelayCommand LockSheetPropertiesCommand { get; private set; }
        public RelayCommand UnlockSheetPropertiesCommand { get; private set; }
        public ObservableCollection<RaceTimeViewModel> Departures { get; set; } = new ObservableCollection<RaceTimeViewModel>();
        public ObservableCollection<RaceTimeViewModel> Arrivals { get; set; } = new ObservableCollection<RaceTimeViewModel>();
        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();        

        public MainWindowViewModel()
        {
            Chronographies = new ObservableCollection<AbstractChronograph>
            {
                ChronographFactory.CreateCP502Chronograph(),
                ChronographFactory.CreateCP520Chronograph()
            };

            //if (IsInDesignMode)
            //{
            //    Chronograph = Chronographies[0];
            //    AvailableComPorts = new ObservableCollection<string>
            //    {
            //        "COM1",
            //        "COM2",
            //        "COM3"
            //    };
            //    ComPort = "COM1";

            //    Departures.Add(new RaceTimeViewModel(new RaceTime
            //    {
            //        Order = 1,
            //        Departure = "00.00.00.000"

            //    }));
            //    Departures.Add(new RaceTimeViewModel(new RaceTime
            //    {
            //        Order = 2,
            //        Departure = "00.02.00.000"

            //    }));
            //    Arrivals.Add(new RaceTimeViewModel(new RaceTime
            //    {
            //        Order = 3,
            //        Departure = "00.00.00.000"
            //    }));
            //    Arrivals.Add(new RaceTimeViewModel(new RaceTime
            //    {
            //        Order = 4,
            //        Departure = "00.02.00.000"
            //    }));
            //    Logs.Add("log 1");
            //    Logs.Add("log 2");
            //}

            InitializeTraceFileMenuItemCommand = new RelayCommand(ExecuteInitializeTraceFileMenuItemCommand);
            ClearListMenuItemCommand = new RelayCommand(ExecuteClearListMenuItemCommand);
            QuitMenuItemCommand = new RelayCommand(ExecuteQuitMenuItemCommand);
            RefreshAvailableCOMPortsCommand = new RelayCommand(ExecuteRefreshAvailableCOMPortsCommand);
            OpenChronographLinkCommand = new RelayCommand(ExecuteOpenChronographLinkCommand, CanExecuteOpenChronographLinkCommand);
            CloseCOMPortsCommand = new RelayCommand(ExecuteCloseCOMPortsCommand, CanExecuteCloseCOMPortsCommand);
            
            OpenExcelCommand = new RelayCommand(ExecuteOpenExcelCommand, CanExecuteOpenExcelCommand);
            LockSheetPropertiesCommand = new RelayCommand(ExecuteLockSheetPropertiesCommand, CanExecuteLockSheetPropertiesCommand);
            UnlockSheetPropertiesCommand = new RelayCommand(ExecuteUnlockSheetPropertiesCommand, CanExecuteUnlockSheetPropertiesCommand);
            
            ExecuteRefreshAvailableCOMPortsCommand();
            ExecuteInitializeTraceFileMenuItemCommand();

            _serialPortManager.SerialPort.DataReceived += SerialPort_DataReceived;
        }

        private ObservableCollection<AbstractChronograph> _chronographies;
        public ObservableCollection<AbstractChronograph> Chronographies
        {
            get { return _chronographies; }
            set { Set(ref _chronographies, value); }
        }
        
        private AbstractChronograph _chronograph;
        public AbstractChronograph Chronograph
        {
            get { return _chronograph; }
            set { Set(ref _chronograph, value); }
        }

        private ObservableCollection<string> _availableComPorts = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableComPorts
        {
            get { return _availableComPorts; }
            set { Set(ref _availableComPorts, value); }
        }

        private string _comPort;
        public string ComPort
        {
            get { return _comPort; }
            set { Set(ref _comPort, value); }
        }

        public bool _isSerialPortOpened;
        public bool IsSerialPortOpened
        {
            get { return _isSerialPortOpened; }
            set { Set(ref _isSerialPortOpened, value); }
        }

        private string _workbookPath;
        public string WorkbookPath
        {
            get { return _workbookPath; }
            set
            {
                if (_workbookPath == value) return;
                Set(ref _workbookPath, value);
                Sheet = null;
                Sheets.Clear();
                RaisePropertyChanged(() => IsWorkbookOpened);
                RaisePropertyChanged(() => CanChangeSheet);
            }
        }

        public bool IsWorkbookOpened => !string.IsNullOrEmpty(WorkbookPath);      
        public bool IsSheetSelected => IsWorkbookOpened && Sheet != null;
        public bool CanChangeSheet => IsWorkbookOpened && !IsSheetLocked;

        public bool _isSheetLocked;
        public bool IsSheetLocked
        {
            get { return _isSheetLocked; }
            set
            {
                Set(ref _isSheetLocked, value);
                RaisePropertyChanged(() => CanChangeSheet);
            }
        }

        private ObservableCollection<string> _sheets = new ObservableCollection<string>();
        public ObservableCollection<string> Sheets
        {
            get { return _sheets; }
            set { Set(ref _sheets, value); }
        }

        private string _sheet;
        public string Sheet
        {
            get { return _sheet; }
            set
            {
                if (_sheet == value) return;
                Set(ref _sheet, value);
                _worksheetManager.SelectSheet(value);
                RaisePropertyChanged(() => IsSheetSelected);
            }
        }

        private int _offset = 0;
        public int Offset
        {
            get { return _offset; }
            set
            {
                Set(ref _offset, value);
                _worksheetManager.Offset = value;
            }
        }

        #region File
        private void ExecuteInitializeTraceFileMenuItemCommand()
        {
            _file?.Close();
            _file = File.Open("Trace_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt", FileMode.CreateNew);

            _writer = new StreamWriter(_file)
            {
                AutoFlush = true
            };
            _writer.WriteLine("Fichier d'enregistrement des impulsions du chronomètre TAG HEUER");

            if (_chronograph != null)
            {
                _writer.WriteLine(_chronograph.GetName());
                _writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            }
        }
        
        private void ExecuteClearListMenuItemCommand()
        {
            Departures.Clear();
            Arrivals.Clear();
        }

        private void ExecuteQuitMenuItemCommand()
        {
            System.Windows.Application.Current.Shutdown();
        }
        #endregion
        
        #region Chronometer
        private void ExecuteRefreshAvailableCOMPortsCommand()
        {
            AvailableComPorts.Clear();
            foreach (var item in _serialPortManager.AvailablePorts)
                AvailableComPorts.Add(item);
        }

        private bool CanExecuteOpenChronographLinkCommand()
        {
            return Chronograph != null && !IsSerialPortOpened && !string.IsNullOrWhiteSpace(ComPort);
        }

        private void ExecuteOpenChronographLinkCommand()
        {
            _writer.WriteLine(_chronograph.GetName());
            _writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            
            IsSerialPortOpened = _serialPortManager.Open(Chronograph, ComPort);
        }

        private bool CanExecuteCloseCOMPortsCommand()
        {
            return IsSerialPortOpened;
        }

        private void ExecuteCloseCOMPortsCommand()
        {
            _serialPortManager.Close();
            IsSerialPortOpened = _serialPortManager.IsOpened;
        }
        #endregion

        #region Methods
        private void SerialPort_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs args)
        {
            try
            {
                var res = _chronograph.Interpret(_serialPortManager.SerialPort);
                res.Order += _worksheetManager.Offset;
                var raceTimeViewModel = new RaceTimeViewModel(res)
                {
                    Resend = new GalaSoft.MvvmLight.Command.RelayCommand<RaceTimeViewModel>(x => SendRaceTime(x))
                };
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (res.Departure != null)
                    {
                        Departures.Add(raceTimeViewModel);
                        _writer.WriteLine($"1-{res.Order}-{res.Departure}");
                    }
                    else if (res.Arrival != null)
                    {
                        Arrivals.Add(raceTimeViewModel);
                        _writer.WriteLine($"2-{res.Order}-{res.Arrival}");
                    }
                });

                SendRaceTime(raceTimeViewModel);
            }
            catch (Exception ex)
            {
                // I said no crash !
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Add(ex.Message);
                });
            }
        }

        private void SendRaceTime(RaceTimeViewModel raceTime)
        {
            try
            {
                raceTime.Saved = _worksheetManager.AddRaceTime(raceTime.RaceTime);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Add(ex.Message);
                });
            }
        }
        #endregion

        #region Excel

        private bool CanExecuteOpenExcelCommand()
        {
            return true;
        }

        private void ExecuteOpenExcelCommand()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Excel files (*.xlsx)|*.xlsx;*.xls";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                WorkbookPath = dialog.FileName;
                _worksheetManager.Open(dialog.FileName);
                Sheets = _worksheetManager.Worksheets;
            }
        }

        private bool CanExecuteLockSheetPropertiesCommand()
        {
            return !IsSheetLocked && Sheet != null;
        }

        private void ExecuteLockSheetPropertiesCommand()
        {
            IsSheetLocked = true;
        }

        private bool CanExecuteUnlockSheetPropertiesCommand()
        {
            return IsSheetLocked;
        }

        private void ExecuteUnlockSheetPropertiesCommand()
        {
            IsSheetLocked = false;
        }

        #endregion
    }

    public class MainWindowViewModelDesignData : MainWindowViewModel
    {
        public new ObservableCollection<RaceTimeViewModel> Departures { get; set; } = new ObservableCollection<RaceTimeViewModel>
        {
            new RaceTimeViewModel(new RaceTime
                {
                    Departure = "00:00:00",
                    Order = 1,
                })
            {
                Saved = true,
            },
            new RaceTimeViewModel(new RaceTime
                {
                    Departure = "00:00:00",
                    Order = 2,
                })
            {
                Saved = false,
            },
        };
    }
}
