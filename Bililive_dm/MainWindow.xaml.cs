using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Serialization;
using XiguaDanmakuHelper;

namespace Bililive_dm
{
    /// <summary>
    ///     MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int GWL_EXSTYLE = -20;
        private const int _maxCapacity = 100;

        private readonly Queue<MessageModel> _danmakuQueue = new Queue<MessageModel>();

        private readonly ObservableCollection<string> _messageQueue = new ObservableCollection<string>();

        private readonly Thread ProcDanmakuThread;

        private readonly ObservableCollection<SessionItem> SessionItems = new ObservableCollection<SessionItem>();

        private readonly DispatcherTimer timer;
        private Api b;
        private IDanmakuWindow fulloverlay;
        private Thread getDanmakuThread;
        public MainOverlay overlay;
        private readonly Thread releaseThread;

        private StoreModel settings;
        
        private bool ChatOpt;
        private bool GiftOpt;

        public MainWindow()
        {
            InitializeComponent();
            //初始化日志

            try
            {
                LiverName.Text = Properties.Settings.Default.name;
            }
            catch
            {
                LiverName.Text = "";
            }

            ChatOpt = true;
            GiftOpt = false;
            b = new Api();
            overlay_enabled = true;
            OpenOverlay();
            overlay.Show();

            Closed += MainWindow_Closed;

            Api.OnMessage += b_ReceivedDanmaku;
//            b.OnMessage += ProcDanmaku;
            Api.LogMessage += b_LogMessage;
            Api.OnRoomCounting += b_ReceivedRoomCount;


            timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, FuckMicrosoft,
                Dispatcher);
            timer.Start();

            Log.DataContext = _messageQueue;

            releaseThread = new Thread(() =>
            {
                while (true)
                {
                    Utils.ReleaseMemory(true);
                    Thread.Sleep(30 * 1000);
                }
            });
            releaseThread.IsBackground = true;
            getDanmakuThread = new Thread(() =>
            {
                while (true)
                    if (b.isLive)
                    {
                        b.GetDanmaku();
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Thread.Sleep(100000);
                    }
            });
            getDanmakuThread.IsBackground = true;
            //            releaseThread.Start();
            ProcDanmakuThread = new Thread(() =>
            {
                while (true)
                {
                    lock (_danmakuQueue)
                    {
                        var count = 0;
                        if (_danmakuQueue.Any()) count = (int) Math.Ceiling(_danmakuQueue.Count / 30.0);

                        for (var i = 0; i < count; i++)
                            if (_danmakuQueue.Any())
                            {
                                var danmaku = _danmakuQueue.Dequeue();
                                ProcDanmaku(danmaku);
                            }
                    }

                    Thread.Sleep(25);
                }
            })
            {
                IsBackground = true
            };
            ProcDanmakuThread.Start();

            for (var i = 0; i < 100; i++) _messageQueue.Add("");
            logging("可以点击日志复制到剪贴板");

            Loaded += MainWindow_Loaded;
        }

        private void b_LogMessage(string e)
        {
            logging(e);
        }

        [DllImport("user32", EntryPoint = "SetWindowLong")]
        private static extern uint SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);

        [DllImport("user32", EntryPoint = "GetWindowLong")]
        private static extern uint GetWindowLong(IntPtr hwnd, int nIndex);

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var sc = Log.Template.FindName("LogScroll", Log) as ScrollViewer;
            sc?.ScrollToEnd();
            showChat.IsChecked = ChatOpt;
            showPresent.IsChecked = GiftOpt;
            try
            {
                var isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User |
                                                            IsolatedStorageScope.Domain |
                                                            IsolatedStorageScope.Assembly, null, null);
                var settingsreader =
                    new XmlSerializer(typeof(StoreModel));
                var reader = new StreamReader(new IsolatedStorageFileStream(
                    "settings.xml", FileMode.Open, isoStore));
                settings = (StoreModel) settingsreader.Deserialize(reader);
                reader.Close();
            }
            catch (Exception)
            {
                settings = new StoreModel();
            }

            settings.SaveConfig();
            settings.toStatic();
            OptionDialog.LayoutRoot.DataContext = settings;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
        }

        ~MainWindow()
        {
            if (fulloverlay != null)
            {
                fulloverlay.Dispose();
                fulloverlay = null;
            }
        }

        private void FuckMicrosoft(object sender, EventArgs eventArgs)
        {
            if (fulloverlay != null) fulloverlay.ForceTopmost();
            if (overlay != null)
            {
                overlay.Topmost = false;
                overlay.Topmost = true;
            }
        }

        private void OpenOverlay()
        {
            overlay = new MainOverlay();
            overlay.Deactivated += overlay_Deactivated;
            overlay.SourceInitialized += delegate
            {
                var hwnd = new WindowInteropHelper(overlay).Handle;
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            };
            overlay.Background = Brushes.Transparent;
            overlay.ShowInTaskbar = false;
            overlay.Topmost = true;
            overlay.Top = SystemParameters.WorkArea.Top + Store.MainOverlayXoffset;
            overlay.Left = SystemParameters.WorkArea.Right - Store.MainOverlayWidth + Store.MainOverlayYoffset;
            overlay.Height = SystemParameters.WorkArea.Height;
            overlay.Width = Store.MainOverlayWidth;
        }

        private void overlay_Deactivated(object sender, EventArgs e)
        {
            if (sender is MainOverlay) (sender as MainOverlay).Topmost = true;
        }

        private async void connbtn_Click(object sender, RoutedEventArgs e)
        {
            Name = LiverName.Text.Trim();
            b = new Api(Name);

            ConnBtn.IsEnabled = false;
            DisconnBtn.IsEnabled = false;
            var connectresult = false;
            var trytime = 0;
            logging("正在连接");

            connectresult = await b.ConnectAsync();

            if (connectresult)
            {
                logging("連接成功");
                AddDMText("提示", "連接成功", true);
                getDanmakuThread.Start();
            }
            else
            {
                logging("連接失敗");
                AddDMText("提示", "連接失敗", true);
                ConnBtn.IsEnabled = true;
            }

            DisconnBtn.IsEnabled = true;
        }

        public void b_ReceivedRoomCount(long popularity)
        {
//            logging("當前房間人數:" + e.UserCount);
//            AddDMText("當前房間人數", e.UserCount+"", true);
            //AddDMText(e.Danmaku.CommentUser, e.Danmaku.CommentText);
            if (CheckAccess())
            {
                OnlinePopularity.Text = popularity.ToString();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => { OnlinePopularity.Text = popularity.ToString(); }));
            }
        }

        public void b_ReceivedDanmaku(MessageModel e)
        {
            lock (_danmakuQueue)
            {
                _danmakuQueue.Enqueue(e);
            }
        }

        private void ProcDanmaku(MessageModel danmakuModel)
        {
            switch (danmakuModel.MsgType)
            {
                case MessageEnum.Chat:
                    if (ChatOpt)
                    {
                        logging(danmakuModel.ChatModel.ToString());
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AddDMText(danmakuModel.ChatModel.user.ToString(),
                                danmakuModel.ChatModel.content);
                        }));
                    }
                    break;
                case MessageEnum.Gifting:
                    break;
                case MessageEnum.Gift:
                {
                    if (GiftOpt)
                    {
                        logging("收到礼物 : " + danmakuModel.GiftModel.user + " 赠送的 " + danmakuModel.GiftModel.count +
                                " 个 " + danmakuModel.GiftModel.GetName());
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AddDMText("收到礼物",
                                danmakuModel.GiftModel.ToString(), true);
                        }));
                    }
                    break;
                }
                case MessageEnum.Join:
                {
                    if (GiftOpt)
                    {
                        logging("粉丝团新成员 : 欢迎 " + danmakuModel.UserModel + " 加入了粉丝团");
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AddDMText("粉丝团新成员",
                                "欢迎" + danmakuModel.UserModel + "加入了粉丝团", true);
                        }));
                    }
                    break;
                }
            }
        }

        public void logging(string text)
        {
            if (Log.Dispatcher.CheckAccess())
                lock (_messageQueue)
                {
                    if (_messageQueue.Count >= _maxCapacity) _messageQueue.RemoveAt(0);

                    _messageQueue.Add("[" + DateTime.Now.ToString("T") + "]" + text);
                }
            else
                Log.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => logging(text)));
        }

        public void AddDMText(string user, string text, bool warn = false)
        {
            if (!overlay_enabled) return;
            if (Dispatcher.CheckAccess())
            {
                var c = new DanmakuTextControl();

                c.UserName.Text = user;
                if (warn) c.UserName.Foreground = Brushes.Red;
                c.Text.Text = text;
                c.ChangeHeight();
                var sb = (Storyboard) c.Resources["Storyboard1"];
                //Storyboard.SetTarget(sb,c);
                sb.Completed += sb_Completed;
                overlay.LayoutRoot.Children.Add(c);
            }
            else
            {
                Log.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => AddDMText(user, text)));
            }
        }

        private void sb_Completed(object sender, EventArgs e)
        {
            var s = sender as ClockGroup;
            if (s == null) return;
            var c = Storyboard.GetTarget(s.Children[2].Timeline) as DanmakuTextControl;
            if (c != null) overlay.LayoutRoot.Children.Remove(c);
        }

        public void Test_OnClick(object sender, RoutedEventArgs e)
        {
            AddDMText("提示", "這是一個測試😀😭", true);
        }

        private void Disconnbtn_OnClick(object sender, RoutedEventArgs e)
        {
            ConnBtn.IsEnabled = true;
            getDanmakuThread.Abort();
            getDanmakuThread = new Thread(() =>
            {
                while (true)
                    if (b.isLive)
                    {
                        b.GetDanmaku();
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Thread.Sleep(100000);
                    }
            }) {IsBackground = true};
        }

        private void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is TextBlock textBlock)
                {
                    Clipboard.SetText(textBlock.Text);
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        new Action(() => { MessageBox.Show("本行记录已复制到剪贴板"); }));
                }
            }
            catch (Exception)
            {
            }
        }

        #region Runtime settings

        private readonly bool overlay_enabled = true;

        #endregion

        private void ShowChat_OnUnchecked(object sender, RoutedEventArgs e)
        {
            ChatOpt = false;
        }

        private void showPresent_OnUnchecked(object sender, RoutedEventArgs e)
        {
            GiftOpt = false;
        }

        private void showPresent_OnChecked(object sender, RoutedEventArgs e)
        {
            GiftOpt = true;
        }

        private void showChat_OnChecked(object sender, RoutedEventArgs e)
        {
            ChatOpt = true;
        }
    }
}