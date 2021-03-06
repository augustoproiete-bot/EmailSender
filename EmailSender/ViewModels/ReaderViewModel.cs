﻿using EmailSender.Interfaces;
using EmailSender.Logger;
using EmailSender.Models;
using EmailSender.Services;
using EmailSender.Settings;
using EmailSender.Settings.Models;
using Stylet;
using StyletIoC;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmailSender.ViewModels
{
    public class ReaderViewModel : PropertyChangedBase
    {
        #region Private fields
        private ILogger _logger;
        private ISender _sender;
        private ReaderSettings _settings;
        private MainAccount _account;
        private INotification _notification;
        private IDialogService _dialog;
        private IReaderMails _reader;
        private BindableCollection<Receiver> _receivers;
        private bool _isAnswer;
        private bool _isAutoStart;
        private int _readInterval;
        private string _textAnswer;
        private string _subjectLetter;
        private string _stopWords;
        private string _reportFolder_1;
        private string _reportFolder_2;
        private string _accountState;
        private string _serverState;
        //times
        private int nextTimeReadInt;
        private string _nextTimeRead;
        private string _lastTimeRead;
        
        //marker of reading process now
        private bool isReadNow;
        //marker of reading service work
        private bool isReadServiceWork;

        private Reporter _reporter;
        private IWindowManager _windMng;
        #endregion

        #region Constructor
        public ReaderViewModel(IContainer ioc)
        {
            _sender = ioc.Get<ISender>();
            _logger = ioc.Get<ILogger>();
            _settings = ioc.Get<AppSettingsModel>().ReaderSettings;
            _account = ioc.Get<AppSettingsModel>().MainAccount;
            _notification = ioc.Get<INotification>();
            _dialog = ioc.Get<IDialogService>();
            _reader = ioc.Get<IReaderMails>();
            _receivers = ioc.Get<BindableCollection<Receiver>>();
            _windMng = ioc.Get<IWindowManager>();

            _reporter = new Reporter(ioc);
            //autostart reader service
            if (IsAutoStart)
            {
                ReadService();
            }
        }
        #endregion

        #region Public fields
        //property of answering function to receivers        
        public bool IsAnswer
        {
            get
            {
                _isAnswer = _settings.IsAnswer;
                return _isAnswer;
            }
            set
            {
                SetAndNotify(ref this._isAnswer, value);
                _settings.IsAnswer = value;
            }
        }

        //property of start reading service
        public bool IsAutoStart
        {
            get
            {
                _isAutoStart = _settings.IsAutoStart;
                return _isAutoStart;
            }
            set
            {
                SetAndNotify(ref this._isAutoStart, value);
                _settings.IsAutoStart = value;
            }
        }

        //property reading interval
        public int ReadInterval
        {
            get
            {
                _readInterval = _settings.ReadInterval;
                return _readInterval;
            }
            set
            {
                SetAndNotify(ref this._readInterval, value);
                _settings.ReadInterval = value;
            }
        }

        
        //property text of answer letter
        public string TextLetter
        {
            get
            {
                _textAnswer = _settings?.AnswerLetter?.Text ?? string.Empty;
                return _textAnswer;
            }
            set
            {
                SetAndNotify(ref this._textAnswer, value);
                _settings.AnswerLetter.Text = value;
            }
        }

        //property subgect of answer letter
        public string SubjectLetter
        {
            get
            {
                _subjectLetter = _settings?.AnswerLetter?.Subject ?? string.Empty;
                return _subjectLetter;
            }
            set
            {
                SetAndNotify(ref this._subjectLetter, value);
                _settings.AnswerLetter.Subject = value;
            }
        }

        //Stop words for filtring email reading
        public string StopWords
        {
            get
            {
                _stopWords = _settings.StopWords;
                return _stopWords;
            }
            set
            {
                SetAndNotify(ref this._stopWords, value);
                _settings.StopWords = value;
            }
        }

        //Report folder 1
        public string ReportFolder_1
        {
            get
            {
                _reportFolder_1 = _settings.ReportFolder_1;
                return _reportFolder_1;
            }
            set
            {
                SetAndNotify(ref this._reportFolder_1, value);
                _settings.ReportFolder_1 = value;
            }
        }

        //report folder 2
        public string ReportFolder_2
        {
            get
            {
                _reportFolder_2 = _settings.ReportFolder_2;
                return _reportFolder_2;
            }
            set
            {
                SetAndNotify(ref this._reportFolder_2, value);
                _settings.ReportFolder_2 = value;
            }
        }
     
        //State account of reading 
        public string AccountState
        {
            get
            {
                _accountState = _account.AccountStatus;
                return _accountState;
            }
            set
            {
                SetAndNotify(ref this._accountState, value);
                _account.AccountStatus = value;
            }
        }
       
        public string ServerState
        {
            get
            {
                _serverState = _account.ServerStatus;
                return _serverState;
            }
            set
            {
                SetAndNotify(ref this._serverState, value);
                _account.ServerStatus = value;
            }
        }

        public string NextTimeRead
        {
            get
            {
                return _nextTimeRead;
            }
            set
            {
                SetAndNotify(ref this._nextTimeRead, value);
            }
        }
       
        public string LastTimeRead
        {
            get
            {
                return _lastTimeRead;
            }
            set
            {
                SetAndNotify(ref this._lastTimeRead, value);
            }
        }
        ///
        #endregion

        #region COMMANDS

        public void SetFolderCommand1()
        {
            ReportFolder_1 = _dialog.OpenFolder();
            NotifyOfPropertyChange(nameof(this.ReportFolder_1));

        }
        public void SetFolderCommand2()
        {
            ReportFolder_2 = _dialog.OpenFolder(); 
            NotifyOfPropertyChange(nameof(this.ReportFolder_2));
        }
        
        public void StartReadServiceCommand()
        {
            isReadServiceWork = true;
            NotifyOfPropertyChange(nameof(this.CanStartReadServiceCommand));
            NotifyOfPropertyChange(nameof(this.CanStopReadServiceCommand));
            ReadService();
        }
        public bool CanStartReadServiceCommand
        {
            get { return !isReadServiceWork; }
        }

        public void StopReadServiceCommand()
        {
            isReadServiceWork = false;
        }
        public bool CanStopReadServiceCommand
        {
            get { return isReadServiceWork; }
        }

        public void ReadMailsCommand()
        {
            if (!(CheckFolder(ReportFolder_1) || CheckFolder(ReportFolder_2)))
            {
                return;
            }

            isReadNow = true;
            _logger.InfoReader("Start Read Command");
            NotifyOfPropertyChange(nameof(this.CanReadMailsCommand));
            CheckFolder(ReportFolder_1);
            CheckFolder(ReportFolder_2);

            Task.Run(() => {
                //ObservableCollection<Answer> answers = _reader.ReadMails(StopWords);
                ObservableCollection<Answer> answers = new ObservableCollection<Answer>();

                _logger.InfoReader($"Finish reading, get answers {answers.Count.ToString()}");

                _reporter.WorkWithResults(answers);

                //set ui elements to next time read
                ChangeUiNextTimeRead();
                _logger.InfoReader("Finished reading");
            });
        }
        public bool CanReadMailsCommand
        {
            get { return !isReadNow; }
        }

        #endregion

        #region Private methods
        private void ReadService()
        {
            isReadServiceWork = true;
            _logger.InfoReader("Start read service");
            NotifyOfPropertyChange(nameof(this.CanStartReadServiceCommand));
            NotifyOfPropertyChange(nameof(this.CanStopReadServiceCommand));
            Task.Run(()=> {
                while (isReadServiceWork)
                {
                    if (nextTimeReadInt < TimeUnixService.Timestamp())
                    {
                        if (!isReadNow)
                        {
                            ReadMailsCommand();
                        }                                            
                    }
                    Thread.Sleep(5000);
                }
                NotifyOfPropertyChange(nameof(this.CanStartReadServiceCommand));
                NotifyOfPropertyChange(nameof(this.CanStopReadServiceCommand));
            });
        }

        private void ChangeUiNextTimeRead()
        {
            Execute.OnUIThread(() => {
                isReadNow = false;
                NotifyOfPropertyChange(nameof(this.CanReadMailsCommand));
                nextTimeReadInt = TimeUnixService.Timestamp() + ReadInterval;
                NextTimeRead = TimeUnixService.TimeStamToStr(nextTimeReadInt);
                LastTimeRead = TimeUnixService.TimeStamToStr(TimeUnixService.Timestamp());
                NotifyOfPropertyChange(nameof(this.NextTimeRead));
                NotifyOfPropertyChange(nameof(this.LastTimeRead));
            });

        }

        
        
        private bool CheckFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                _windMng.ShowMessageBox($"Папки {folder} не существует!");
                return false;
            }
            return true;
        }

        #endregion

    }
}
