using System.Data;
using System.Media;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Windows.Controls;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;



namespace Spicky_don_t_neru
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WaveOutEvent _outputDevice1;
        private WaveOutEvent _outputDevice2;
        private WaveOutEvent _outputDevice3;
        private AudioFileReader _audioFile1;
        private AudioFileReader _audioFile2;
        private AudioFileReader _audioFile3;
        private SmbPitchShiftingSampleProvider _pitchShifter;

        private SKBitmap _bitmap;
        private SKPoint[] _vertices;
        private SKPoint[] _texCoords;
        private ushort[] _indices;

        private int _rows = 40;
        private int _cols = 40;
        private float _targetSize = 200f; // 이미지 표시 크기
        private bool _isDragging = false;
        private Point _lastMousePos;
        private float _warpRadius = 80f;
        private float _margin = 50f;
        private double _lastDistance = 0;
        private Point _StartMousePos;

        private SKPoint[] _initialVertices; // 원래 위치 저장용
        private bool _isResetting = false;  // 현재 복원 애니메이션 중인지 확인

        // 매 프레임 생성하지 않고 재사용할 리소스들
        private readonly SKPaint _paint = new SKPaint
        {
            IsAntialias = true
        };

        // 샘플링 설정 (선형 필터링)
        private readonly SKSamplingOptions _sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);


        private readonly MediaPlayer dontneruplayer = new MediaPlayer();
        private readonly MediaPlayer startplayer = new MediaPlayer();
        private readonly MediaPlayer euuplayer = new MediaPlayer();
        private enum DragState { None, Inward, Outward }
        private DragState _currentDragState = DragState.None;
        private bool _isDirectionFixed = false;
        private const double _determinationThreshold = 3.0;

        private readonly Uri SpeakiimgUri = new Uri("pack://application:,,,/Assets/Speaki.png");

        private void InitializeMesh()
        {
            if (_bitmap == null) return;

            // 격자의 점 개수 계산 (40x40 칸이면 점은 41x41개)
            int vCount = (_rows + 1) * (_cols + 1);
            _vertices = new SKPoint[vCount];    // 화면에 그려질 실제 좌표
            _initialVertices = new SKPoint[vCount];
            _texCoords = new SKPoint[vCount];   // 이미지 소스의 좌표 (변하지 않음)

            float targetW = 200;
            float targetH = 200;

            float imgW = _bitmap.Width;
            float imgH = _bitmap.Height;

            for (int r = 0; r <= _rows; r++)
            {
                for (int c = 0; c <= _cols; c++)
                {
                    int i = r * (_cols + 1) + c;

                    // 0~1 사이의 상대적 위치 계산
                    float xRel = (float)c / _cols;
                    float yRel = (float)r / _rows;

                    _vertices[i] = new SKPoint(xRel * targetW, yRel * targetH);
                    _initialVertices[i] = new SKPoint(xRel * targetW, yRel * targetH);
                    _texCoords[i] = new SKPoint(xRel * imgW, yRel * imgH);
                }
            }
            // 삼각형 연결 순서(인덱스) 생성
            _indices = CreateIndices(_rows, _cols);

        }

        private ushort[] CreateIndices(int rows, int cols)
        {
            ushort[] indices = new ushort[rows * cols * 6];
            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 현재 사각형의 왼쪽 상단 점 번호
                    ushort i = (ushort)(r * (cols + 1) + c);

                    // 사각형 하나를 삼각형 2개로 쪼개서 저장
                    indices[index++] = i;
                    indices[index++] = (ushort)(i + 1);
                    indices[index++] = (ushort)(i + cols + 1);

                    indices[index++] = (ushort)(i + 1);
                    indices[index++] = (ushort)(i + cols + 2);
                    indices[index++] = (ushort)(i + cols + 1);
                }
            }
            return indices;
        }

        public MainWindow()
        {
            InitializeComponent();
            InitNAudio();

            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.WindowState = WindowState.Maximized;
            this.Topmost = true;

            var resourceStream = Application.GetResourceStream(SpeakiimgUri);
            if (resourceStream != null)
            {
                using (var stream = resourceStream.Stream)
                {
                    // 2. 스트림을 이용하여 비트맵 디코딩
                    _bitmap = SKBitmap.Decode(stream);
                }
            }


            if (_bitmap != null)
            {
                UpdateShader();
                InitializeMesh();
                SkiaCanvas.InvalidateVisual();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

        }
        private void InitNAudio()
        {
            _audioFile1 = new AudioFileReader("Assets/dontneruspeaki.wav");
            _outputDevice1 = new WaveOutEvent();
            _outputDevice1.Init(_audioFile1);

            _audioFile2 = new AudioFileReader("Assets/euu.wav");
            _outputDevice2 = new WaveOutEvent();
            _outputDevice2.Init(_audioFile2);

            _audioFile3 = new AudioFileReader("Assets/dontpull.wav");
            ISampleProvider monoSource = new StereoToMonoSampleProvider(_audioFile3.ToSampleProvider());
            _pitchShifter = new SmbPitchShiftingSampleProvider(monoSource);
            var finalStereo = new MonoToStereoSampleProvider(_pitchShifter);
            _outputDevice3 = new WaveOutEvent();
            _outputDevice3.Init(finalStereo);
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            //canvas.Clear(SKColors.Yellow); 디버그용
            canvas.Clear(SKColors.Transparent);

            if (_bitmap == null || _vertices == null || _indices == null) return;

            float targetW = 200;
            float targetH = 200;
            float margin = 50;

            float dpiScale = (float)(e.Info.Width / SkiaCanvas.ActualWidth);
            float finalTargetW = targetW * dpiScale;
            float finalTargetH = targetH * dpiScale;
            float finalMargin = margin * dpiScale;

            float drawX = e.Info.Width - finalTargetW - finalMargin;
            float drawY = e.Info.Height - finalTargetH - finalMargin;

            canvas.Save();
            canvas.Translate(drawX, drawY);
            canvas.Scale(dpiScale);

            using (var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, _vertices, _texCoords, null, _indices))
            {
                    canvas.DrawVertices(vertices, SKBlendMode.Modulate, _paint);

            }
            canvas.Restore();

        }
        private void UpdateShader()
        {
            if (_bitmap == null) return;

            // 1. 기존 셰이더 리소스 해제
            _paint.Shader?.Dispose();

            // 2. 새로운 셰이더 생성 
            // ToShader의 인자 순서: TileModeX, TileModeY, SamplingOptions
            _paint.Shader = _bitmap.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                _sampling
            );

            // 3. 페인트 기본 색상을 흰색으로 고정 (검정색 출력 방지)
            _paint.Color = SKColors.White;
        }

        private void SkiaCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 창 크기가 잡히거나 변경될 때마다 메쉬 위치를 우측 하단으로 재계산
            InitializeMesh();
            SkiaCanvas.InvalidateVisual();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _isDirectionFixed = false;
                _StartMousePos = e.GetPosition(SkiaCanvas);
                _lastMousePos = _StartMousePos;
                _lastDistance = 0;
                _currentDragState = DragState.None;

                StopAllDragSounds();
                SkiaCanvas.CaptureMouse();
            }
        }

        private void StopAllDragSounds()
        {
            _outputDevice1?.Stop();
            _outputDevice2?.Stop();
            _outputDevice3?.Stop();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _vertices == null) return;

            Point currentPos = e.GetPosition(SkiaCanvas);

            float offsetX = (float)SkiaCanvas.ActualWidth - _targetSize - _margin;
            float offsetY = (float)SkiaCanvas.ActualHeight - _targetSize - _margin;
            Point imageCenter = new Point(offsetX + (_targetSize / 2), offsetY + (_targetSize / 2));

            double currentDistance = Point.Subtract(currentPos, _lastMousePos).Length;
            double distFromCenter = Point.Subtract(currentPos, imageCenter).Length;
            double distFromStart = Point.Subtract(currentPos, _lastMousePos).Length;

            if (!_isDirectionFixed && currentDistance > _determinationThreshold)
            {
               
                double startDistFromCenter = Point.Subtract(_lastMousePos, imageCenter).Length;
                // 이미지의 중심(또는 클릭 지점)으로부터 멀어지는 방향인지 확인
                if (distFromCenter > startDistFromCenter)
                {
                    _currentDragState = DragState.Outward; // 이미지 밖으로 나가는 판정
                }
                else
                {
                    _currentDragState = DragState.Inward; // 이미지 안으로 들어오는 판정
                }

                _isDirectionFixed = true; // 이제 방향을 고정함 (드래그 끝날 때까지 유지)
                ChangeDragSound(_currentDragState); // 고정된 소리 재생 시작
            }

            if (_isDirectionFixed)
            {
                if (_currentDragState == DragState.Outward)
                {
                    // 바깥으로 더 멀어질수록 피치 상승 (기준점으로부터의 거리 증가분 활용)
                    float pitch = 1.0f + (float)(distFromCenter / 500.0f);
                    if (_pitchShifter != null) _pitchShifter.PitchFactor = Math.Clamp(pitch, 0.7f, 2.0f);

                    if (_outputDevice3.PlaybackState != PlaybackState.Playing) _outputDevice3.Play();
                }
                else
                {
                    if (_outputDevice1.PlaybackState != PlaybackState.Playing) _outputDevice1.Play();
                }
            }

            _lastDistance = currentDistance;

            SKPoint curSK = new SKPoint((float)currentPos.X - offsetX, (float)currentPos.Y - offsetY);
            SKPoint prevSK = new SKPoint((float)_lastMousePos.X - offsetX, (float)_lastMousePos.Y - offsetY);

            SKPoint idelta = new SKPoint(curSK.X - prevSK.X, curSK.Y - prevSK.Y);

            Parallel.For(0, _vertices.Length, i =>
            {
                float dist = SKPoint.Distance(_vertices[i], curSK);
                if (dist < _warpRadius)
                {
                    float weight = (_warpRadius - dist) / _warpRadius;
                    float strength = weight * weight;

                    _vertices[i].X += idelta.X * strength;
                    _vertices[i].Y += idelta.Y * strength;
                }
            });

            _lastMousePos = currentPos;
            SkiaCanvas.InvalidateVisual();
        }

        private void ChangeDragSound(DragState state)
        {
            if (state == DragState.Outward)
            {
                _outputDevice1?.Stop(); // 안쪽 소리 정지

                if (_outputDevice3?.PlaybackState != PlaybackState.Playing)
                {
                    _audioFile3.Position = 0; // 처음부터 재생
                    _outputDevice3?.Play();
                }
            }
            else if (state == DragState.Inward)
            {
                _outputDevice3?.Stop(); // 바깥쪽 소리 정지

                if (_outputDevice1?.PlaybackState != PlaybackState.Playing)
                {
                    _audioFile1.Position = 0;
                    _outputDevice1?.Play();
                }
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = false;
                if (SkiaCanvas.IsMouseCaptured) SkiaCanvas.ReleaseMouseCapture();
                _currentDragState = DragState.None;
                _outputDevice1?.Stop();
                _outputDevice3?.Stop();

                _audioFile2.Position = 0;
                _outputDevice2.Play();

                StartResetAnimation();
            }
        }

        private async void StartResetAnimation()
        {
            if (_isResetting) return; // 이미 재생 중이면 중복 실행 방지
            _isResetting = true;

            float lerpFactor = 0.15f; // 복원 속도 (0.0 ~ 1.0, 높을수록 빠름)
            bool continues = true;

            while (_isResetting && continues)
            {
                continues = false;
                float threshold = 3.5f; // 이 거리 이내로 들어오면 멈춤

                for (int i = 0; i < _vertices.Length; i++)
                {
                    // 현재 위치에서 원래 위치까지의 차이 계산
                    float dx = _initialVertices[i].X - _vertices[i].X;
                    float dy = _initialVertices[i].Y - _vertices[i].Y;

                    if (Math.Abs(dx) > threshold || Math.Abs(dy) > threshold)
                    {
                        // 조금씩 이동 (Lerp)
                        _vertices[i].X += dx * lerpFactor;
                        _vertices[i].Y += dy * lerpFactor;
                        continues = true; // 아직 이동해야 할 점이 남았음
                    }
                    else
                    {
                        // 완전히 도착하면 좌표 고정
                        _vertices[i] = _initialVertices[i];
                    }
                }

                SkiaCanvas.InvalidateVisual(); // 화면 갱신

                // 약 60FPS 정도로 루프 (16ms)
                await Task.Delay(16);

                // 만약 애니메이션 중에 다시 드래그를 시작하면 중단
                if (_isDragging)
                {
                    _isResetting = false;
                    break;
                }
            }

            _isResetting = false;
        }
    }
}