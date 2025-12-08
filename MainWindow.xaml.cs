using System.Data;
using System.Media;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;


namespace Spicky_don_t_neru
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT(); // 이 변수가 Taskbar를 제외한 작업 영역
            public int dwFlags = 0;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFO lpmi);

        private readonly MediaPlayer dontneruplayer = new MediaPlayer();
        private readonly MediaPlayer startplayer = new MediaPlayer();

        private readonly Uri dontneruUri = new Uri("Assets/dontneruspeaki.wav", UriKind.Relative);
        private readonly Uri startUri = new Uri("Assets/SpeakiStartup.wav", UriKind.Relative);
        private readonly Uri SpeakiimgUri = new Uri("pack://application:,,,/Assets/Speaki.png");
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, EventArgs e)
        {
            PlayStartup();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // 창이 위치한 모니터의 핸들을 가져오기
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            // MONITORINFO 구조체를 초기화하고 작업 영역 정보를 요청
            MONITORINFO mi = new MONITORINFO();
            GetMonitorInfo(hMonitor, mi);

            double newLeft = mi.rcWork.right - this.ActualWidth;

            double newTop = mi.rcWork.bottom - this.ActualHeight;

            // 6. 창 위치 적용
            this.Left = newLeft;
            this.Top = newTop;
        }

        private void PlayStartup()
        {
            startplayer.Open(startUri);
            startplayer.Volume = 0.7;
            startplayer.Play();
        }

        /*
         * 마우스 좌클릭으로 애니메이션을 재생하는 이벤트
         * Xaml의 Image클래스와 바인드
         */
        private void Anim_OnMouseLeftButtonDouble(object sender, MouseButtonEventArgs e)
        {
            Speaki.Source = new BitmapImage(SpeakiimgUri);

            do_not_neru_anim(e.GetPosition(Speaki));

            dontneruplayer.Volume = 0.7;
            dontneruplayer.Open(dontneruUri);
                dontneruplayer.Stop();
                dontneruplayer.Play();

                e.Handled = true;
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void startup_neru_anim()
        {

        }
        private void do_not_neru_anim(Point clickpoint)
        {
            //클릭 지점을 0,1값으로 변형
            double normalizedX = clickpoint.X / Speaki.ActualWidth;
            double normalizedY = clickpoint.Y / Speaki.ActualHeight;
            Speaki.RenderTransformOrigin = new Point(normalizedX, normalizedY);

            double targetScaleY = 0.8;
            double offsetY = (1 - targetScaleY) * Speaki.ActualHeight * normalizedY;

            //애니메이션 효과구현
            DoubleAnimation spkiY = new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(100)) //offset이 발생하여 Y축이 이동함
            {
                AutoReverse = true,
                FillBehavior = FillBehavior.Stop
            };
            DoubleAnimation spkiX = new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(100))
            {
                AutoReverse = true,
                FillBehavior = FillBehavior.Stop
            };

            DoubleAnimation corrY = new DoubleAnimation(-offsetY, TimeSpan.FromMilliseconds(100))
            {
                AutoReverse = true,
                FillBehavior = FillBehavior.Stop
            };
            //Completed 메소드사용하여 좌표값 초기화

            //SquishScaleTransform을 각각 애니메이션 효과에 적용
            SquishScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, spkiY);
            SquishScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, spkiX);

            CorrectionTranslateTransform.BeginAnimation(TranslateTransform.YProperty, corrY);
        }
    }
}