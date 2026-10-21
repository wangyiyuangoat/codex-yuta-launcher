// Codex cinematic V2: continuous reveal with a gentle layered finale.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace YutaCodexV2
{
    internal static class Program
    {
        internal const double Duration = 9.2;
        internal const double FadeStart = 8.35;
        internal static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory;

        [STAThread]
        private static int Main(string[] args)
        {
            Native.EnablePerMonitorDpi();
            Mutex gate = null;
            bool ownsGate = false;
            try
            {
                Options options = Options.Read(args);
                if (options.WindowInfo != null && !options.Preview)
                {
                    string directory = Path.GetDirectoryName(options.WindowInfo);
                    if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(options.WindowInfo, Native.FindTarget(IntPtr.Zero).ToJson(), new UTF8Encoding(false));
                    return 0;
                }
                if (options.Capture != null || options.Render != null) { Export(options); return 0; }
                gate = new Mutex(false, "Local\\YutaCodexCinematicLauncherV2");
                try { ownsGate = gate.WaitOne(0, false); }
                catch (AbandonedMutexException) { ownsGate = true; }
                if (!ownsGate) return 0;
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    Log(e.Exception); e.Handled = true; app.Shutdown(1);
                };
                return app.Run(new LauncherWindow(options));
            }
            catch (Exception e)
            {
                Log(e);
                MessageBox.Show("The launcher could not start.\n\n" + e.Message + "\n\nDetails: " + Path.Combine(BasePath, "launcher.log"),
                    "Codex Cinematic Launcher V2", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 1;
            }
            finally { if (gate != null) { if (ownsGate) gate.ReleaseMutex(); gate.Dispose(); } }
        }

        private static void Export(Options options)
        {
            var scene = new Cinematic();
            if (options.Capture != null)
            {
                Directory.CreateDirectory(options.Capture);
                double[] times = { 0.4, 1.2, 2.6, 3.5, 4.5, 5.5, 6.5, 7.5, 8.2, 8.7 };
                foreach (double t in times) SaveFrame(scene, t, options, Path.Combine(options.Capture,
                    "still-" + t.ToString("0.0", CultureInfo.InvariantCulture) + ".png"));
            }
            if (options.Render != null)
            {
                Directory.CreateDirectory(options.Render);
                int total = (int)Math.Round(Duration * options.Fps);
                int first = Math.Max(0, Math.Min(total, options.StartFrame));
                int last = Math.Max(0, Math.Min(total, options.EndFrame.HasValue ? options.EndFrame.Value : total));
                if (last < first) throw new ArgumentException("--end-frame must be at least --start-frame after clamping to the export duration.");
                for (int i = first; i < last; ++i) SaveFrame(scene, i / (double)options.Fps, options,
                    Path.Combine(options.Render, "frame" + i.ToString("0000") + ".png"));
            }
        }

        private static void SaveFrame(Cinematic scene, double t, Options options, string path)
        {
            var drawing = new DrawingVisual();
            double virtualHeight = 1120.0 * options.Height / options.Width;
            using (DrawingContext dc = drawing.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(options.Width / 1120.0, options.Width / 1120.0));
                scene.Draw(dc, t, 1120, virtualHeight, true);
                dc.Pop();
            }
            var bitmap = new RenderTargetBitmap(options.Width, options.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawing);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write)) encoder.Save(file);
        }

        internal static void Log(Exception error)
        {
            try { File.AppendAllText(Path.Combine(BasePath, "launcher.log"), DateTime.Now.ToString("s") + " " + error + Environment.NewLine); }
            catch { }
        }
    }

    internal sealed class Options
    {
        public bool Preview;
        public string Capture, Render, WindowInfo, Target;
        public string AppId = "OpenAI.Codex_2p2nqsd0c76g0!App";
        public int Fps = 30, Width = 1120, Height = 630, StartFrame;
        public int? EndFrame;
        private static int Number(string value, string name, int min, int max)
        {
            int result;
            if (!Int32.TryParse(value, out result) || result < min || result > max)
                throw new ArgumentException(name + " must be an integer between " + min + " and " + max + ".");
            return result;
        }
        public static Options Read(string[] args)
        {
            var result = new Options();
            string appIdFile = Path.Combine(Program.BasePath, "app-id.txt");
            if (File.Exists(appIdFile))
            {
                string configuredAppId = File.ReadAllText(appIdFile).Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(configuredAppId, @"\A[A-Za-z0-9_.!\-]+\z"))
                    throw new ArgumentException("app-id.txt must contain a Windows desktop application AUMID.");
                result.AppId = configuredAppId;
            }
            foreach (string arg in args)
            {
                if (arg == "--preview") result.Preview = true;
                else if (arg == "--window-info") result.WindowInfo = Path.Combine(Program.BasePath, "window-info.json");
                else if (arg.StartsWith("--window-info=", StringComparison.Ordinal)) result.WindowInfo = Path.GetFullPath(arg.Substring(14).Trim('"'));
                else if (arg.StartsWith("--capture=", StringComparison.Ordinal)) result.Capture = Path.GetFullPath(arg.Substring(10).Trim('"'));
                else if (arg.StartsWith("--render=", StringComparison.Ordinal)) result.Render = Path.GetFullPath(arg.Substring(9).Trim('"'));
                else if (arg.StartsWith("--fps=", StringComparison.Ordinal)) result.Fps = Number(arg.Substring(6), "--fps", 1, 60);
                else if (arg.StartsWith("--width=", StringComparison.Ordinal)) result.Width = Number(arg.Substring(8), "--width", 64, 8192);
                else if (arg.StartsWith("--height=", StringComparison.Ordinal)) result.Height = Number(arg.Substring(9), "--height", 64, 8192);
                else if (arg.StartsWith("--start-frame=", StringComparison.Ordinal)) result.StartFrame = Number(arg.Substring(14), "--start-frame", 0, Int32.MaxValue);
                else if (arg.StartsWith("--end-frame=", StringComparison.Ordinal)) result.EndFrame = Number(arg.Substring(12), "--end-frame", 0, Int32.MaxValue);
                else if (arg.StartsWith("--app-id=", StringComparison.Ordinal)) result.AppId = arg.Substring(9).Trim('"');
                else if (arg.StartsWith("--target=", StringComparison.Ordinal)) result.Target = Path.GetFullPath(arg.Substring(9).Trim('"'));
                else throw new ArgumentException("Unknown option: " + arg);
            }
            return result;
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Rect
        {
            public int Left, Top, Right, Bottom;
            public int Width { get { return Right-Left; } }
            public int Height { get { return Bottom-Top; } }
        }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor, Work;
            public uint Flags;
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct WindowPlacement
        {
            public int Size, Flags, ShowCommand;
            public NativePoint Minimum, Maximum;
            public Rect Normal;
        }
        internal sealed class TargetInfo
        {
            public IntPtr Handle;
            public Rect Bounds;
            public uint Dpi = 96;
            public int Pid;
            public string Title = "", ProcessName = "";
            public bool Minimized;
            public bool Found { get { return Handle != IntPtr.Zero; } }
            public string ToJson() { return ToJson(IntPtr.Zero); }
            public string ToJson(IntPtr overlay)
            {
                Rect actual=new Rect();
                bool measured=overlay!=IntPtr.Zero && BoundsOf(overlay,out actual);
                if (!measured) actual=new Rect();
                uint overlayDpi=0; if (measured) { try { overlayDpi=GetDpiForWindow(overlay); } catch { } }
                return "{\"found\":" + (Found ? "true" : "false") + ",\"hwnd\":" + Handle.ToInt64() + ",\"pid\":" + Pid +
                    ",\"process\":\"" + Escape(ProcessName) + "\",\"title\":\"" + Escape(Title) + "\",\"dpi\":" + Dpi +
                    ",\"minimized\":" + (Minimized?"true":"false") + ",\"left\":" + Bounds.Left + ",\"top\":" + Bounds.Top + ",\"width\":" + Bounds.Width + ",\"height\":" + Bounds.Height +
                    ",\"overlay\":" + (measured ? "{\"left\":"+actual.Left+",\"top\":"+actual.Top+",\"width\":"+actual.Width+",\"height\":"+actual.Height+",\"dpi\":"+overlayDpi+"}" : "null") +
                    ",\"matches\":" + (measured ? (Same(Bounds,actual)?"true":"false") : "null") + "}";
            }
        }
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder title, int max);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect bounds);
        [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacement placement);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] private static extern int DwmBounds(IntPtr window, int attribute, out Rect value, int size);
        [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] private static extern int DwmInt(IntPtr window, int attribute, out int value, int size);
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("shcore.dll")] private static extern int SetProcessDpiAwareness(int awareness);
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", SetLastError=true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);

        internal static void EnablePerMonitorDpi()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }
        internal static void Fit(IntPtr window, Rect bounds)
        {
            if (!SetWindowPos(window, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0010 | 0x0200 | 0x0040))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not match the Codex window bounds.");
        }
        internal static bool BoundsOf(IntPtr window,out Rect bounds)
        {
            return DwmBounds(window,9,out bounds,Marshal.SizeOf(typeof(Rect)))==0 || GetWindowRect(window,out bounds);
        }
        internal static TargetInfo FindTarget(IntPtr preferred)
        {
            TargetInfo cached;
            if (preferred != IntPtr.Zero && ReadTarget(preferred, out cached)) return cached;
            IntPtr foreground = GetForegroundWindow();
            TargetInfo best = null;
            long bestScore = -1;
            EnumWindows(delegate(IntPtr window, IntPtr data)
            {
                TargetInfo current;
                if (!ReadTarget(window, out current)) return true;
                long score = (long)current.Bounds.Width * current.Bounds.Height;
                if (window == foreground) score += 1000000000;
                if (score > bestScore) { best = current; bestScore = score; }
                return true;
            }, IntPtr.Zero);
            if (best != null) return best;
            var fallback = new TargetInfo();
            var info = new MonitorInfo(); info.Size = Marshal.SizeOf(typeof(MonitorInfo));
            if (GetMonitorInfo(MonitorFromWindow(foreground, 2), ref info)) fallback.Bounds = info.Work;
            else fallback.Bounds = new Rect { Left=0, Top=0, Right=1920, Bottom=1080 };
            return fallback;
        }
        private static bool ReadTarget(IntPtr window, out TargetInfo result)
        {
            result = null;
            if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return false;
            try
            {
                int cloaked;
                if (DwmInt(window, 14, out cloaked, 4) == 0 && cloaked != 0) return false;
                uint pid; GetWindowThreadProcessId(window, out pid);
                if (pid == Process.GetCurrentProcess().Id) return false;
                string processName;
                using (Process process = Process.GetProcessById((int)pid)) processName = process.ProcessName;
                if (!String.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(processName, "Codex", StringComparison.OrdinalIgnoreCase)) return false;
                bool minimized=IsIconic(window);
                Rect rect;
                if (minimized)
                {
                    var placement=new WindowPlacement(); placement.Size=Marshal.SizeOf(typeof(WindowPlacement));
                    if (!GetWindowPlacement(window,ref placement)) return false;
                    rect=placement.Normal;
                    // Normal placement uses workspace coordinates for ordinary app windows.
                    var monitor=new MonitorInfo(); monitor.Size=Marshal.SizeOf(typeof(MonitorInfo));
                    if (GetMonitorInfo(MonitorFromWindow(window,2),ref monitor))
                    {
                        if ((placement.Flags & 2) != 0) rect=monitor.Work;
                        else
                        {
                            int dx=monitor.Work.Left-monitor.Monitor.Left, dy=monitor.Work.Top-monitor.Monitor.Top;
                            rect.Left+=dx; rect.Right+=dx; rect.Top+=dy; rect.Bottom+=dy;
                        }
                    }
                }
                else if (!BoundsOf(window,out rect)) return false;
                if (rect.Width < 400 || rect.Height < 250) return false;
                var title = new StringBuilder(512); GetWindowText(window, title, title.Capacity);
                uint dpi = 96; try { dpi = GetDpiForWindow(window); } catch { }
                result = new TargetInfo { Handle=window, Bounds=rect, Pid=(int)pid, ProcessName=processName, Title=title.ToString(), Dpi=dpi == 0 ? 96 : dpi, Minimized=minimized };
                return true;
            }
            catch { return false; }
        }
        internal static bool Same(Rect a, Rect b) { return a.Left==b.Left && a.Top==b.Top && a.Right==b.Right && a.Bottom==b.Bottom; }
        private static string Escape(string value) { return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
    }

    internal sealed class LauncherWindow : Window
    {
        private readonly Options options;
        private readonly Cinematic scene;
        private readonly Stopwatch watch = new Stopwatch();
        private readonly DispatcherTimer failsafe = new DispatcherTimer();
        private Native.TargetInfo target;
        private Native.Rect previousBounds;
        private IntPtr handle;
        private bool started, closing, skipArmed, hasBounds;
        private double lastFit = -1;
        private string launchFailure;

        public LauncherWindow(Options options)
        {
            this.options = options;
            Title = "Codex Cinematic Launcher V2";
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false; Topmost = true;
            AllowsTransparency = true; Background = Brushes.Transparent;
            SnapsToDevicePixels = true; UseLayoutRounding = true;
            target = Native.FindTarget(IntPtr.Zero);
            Left = target.Bounds.Left * 96.0 / target.Dpi; Top = target.Bounds.Top * 96.0 / target.Dpi;
            Width = target.Bounds.Width * 96.0 / target.Dpi; Height = target.Bounds.Height * 96.0 / target.Dpi;
            scene = new Cinematic(); Content = scene;
            SourceInitialized += delegate { handle = new WindowInteropHelper(this).Handle; FitToTarget(true); };
            Loaded += delegate { FitToTarget(true); };
            ContentRendered += OnContentRendered;
            PreviewKeyDown += OnKey;
            MouseLeftButtonDown += delegate { Finish(); };
            Closed += delegate { CompositionTarget.Rendering -= OnRendering; failsafe.Stop(); watch.Stop(); };
            failsafe.Interval = TimeSpan.FromSeconds(Program.Duration+3);
            failsafe.Tick += delegate { Finish(); };
        }
        private void FitToTarget(bool force)
        {
            if (handle == IntPtr.Zero || closing) return;
            target = Native.FindTarget(target == null ? IntPtr.Zero : target.Handle);
            Native.Rect actual;
            bool physicalMismatch=force && (!Native.BoundsOf(handle,out actual) || !Native.Same(actual,target.Bounds));
            if (physicalMismatch || !hasBounds || !Native.Same(previousBounds, target.Bounds))
            {
                Native.Fit(handle, target.Bounds); previousBounds = target.Bounds; hasBounds = true;
            }
            if (options.WindowInfo!=null)
            {
                string directory=Path.GetDirectoryName(options.WindowInfo);
                if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(options.WindowInfo,target.ToJson(handle),new UTF8Encoding(false));
            }
        }
        private void OnContentRendered(object sender, EventArgs e)
        {
            if (started) return;
            started = true; watch.Start();
            CompositionTarget.Rendering += OnRendering; failsafe.Start();
            if (!options.Preview)
            {
                try
                {
                    if (options.Target != null)
                    {
                        if (!File.Exists(options.Target)) throw new FileNotFoundException("The configured Codex target does not exist.", options.Target);
                        Process.Start(new ProcessStartInfo(options.Target) { UseShellExecute=true });
                    }
                    else
                    {
                        if (String.IsNullOrWhiteSpace(options.AppId) || options.AppId.IndexOf('"') >= 0 || options.AppId.IndexOf('\r') >= 0 || options.AppId.IndexOf('\n') >= 0)
                            throw new ArgumentException("The configured Windows app ID is invalid.");
                        Process.Start(new ProcessStartInfo("explorer.exe", "\"shell:AppsFolder\\" + options.AppId + "\"") { UseShellExecute=true });
                    }
                }
                catch (Exception error) { Program.Log(error); launchFailure=error.Message; }
            }
        }
        private void OnRendering(object sender, EventArgs e)
        {
            double t = watch.Elapsed.TotalSeconds;
            if (t >= Program.Duration) { Finish(); return; }
            if (t-lastFit >= 0.125) { lastFit=t; FitToTarget(false); }
            if (t >= 0.3 && IsVisible)
            {
                bool escape=(Native.GetAsyncKeyState(0x1B)&0x8000)!=0;
                bool space=(Native.GetAsyncKeyState(0x20)&0x8000)!=0;
                if (!skipArmed) { if (!escape && !space) skipArmed=true; }
                else if (escape || space) { Finish(); return; }
            }
            // Fade the actual overlay, revealing the already running app below it.
            Opacity = 1 - Cinematic.Smooth(Program.FadeStart, Program.Duration, t);
            scene.Time=t; scene.InvalidateVisual();
        }
        private void OnKey(object sender, KeyEventArgs e)
        {
            if (skipArmed && !e.IsRepeat && (e.Key==Key.Escape || e.Key==Key.Space || e.Key==Key.Enter)) { e.Handled=true; Finish(); }
        }
        private void Finish()
        {
            if (closing) return;
            closing=true; CompositionTarget.Rendering -= OnRendering;
            failsafe.Stop(); watch.Stop(); Close();
            if (launchFailure != null) MessageBox.Show("The animation finished, but Codex could not be opened.\n\n" + launchFailure +
                "\n\nOpen Codex normally, or pass --target=\"C:\\path\\to\\Codex.exe\".", "Codex Cinematic Launcher V2", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal sealed class Cinematic : FrameworkElement
    {
        private readonly BitmapSource quiet, quietOpen, quietForeground, foreground, background;
        private readonly BitmapSource[] eyeStates;
        private static readonly Color Ice = Color.FromRgb(151,224,255);
        private static readonly Color Blue = Color.FromRgb(45,142,227);
        private static readonly Typeface Thin = new Typeface(new FontFamily("Segoe UI Light"), FontStyles.Normal, FontWeights.Light, FontStretches.Normal);
        private static readonly Typeface Mono = new Typeface("Consolas");
        // This is the actual silver reflection in quiet.png, not a separate ring graphic.
        private const double RingX=0.4695, RingY=0.731;
        public double Time;

        public Cinematic()
        {
            quietOpen=Load("quiet.png", true);
            quiet=Load("quiet-closed.png", false) ?? quietOpen;
            quietForeground=Load("quiet-foreground-aligned.png",true);
            eyeStates=BuildEyePatches(new BitmapSource[] { quiet, Load("quiet-eye25.png",false) ?? quiet,
                Load("quiet-eye60.png",false) ?? quietOpen, quietOpen },quietForeground);
            foreground=Load("hero-foreground-aligned.png", true);
            background=Load("hero-background.png", true);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }
        private static BitmapSource Load(string name, bool required)
        {
            string path=Path.Combine(Program.BasePath,"assets",name);
            if (!File.Exists(path)) { if (required) throw new FileNotFoundException("The cinematic artwork is missing: " + name, path); return null; }
            var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
            image.UriSource=new Uri(path,UriKind.Absolute); image.EndInit(); image.Freeze(); return image;
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth<=0 || ActualHeight<=0) return;
            double scale=ActualWidth/1120;
            dc.PushTransform(new ScaleTransform(scale,scale));
            Draw(dc,Time,1120,ActualHeight/scale,false); dc.Pop();
        }
        public void Draw(DrawingContext dc, double t, double w, double h, bool exported)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h)));
            dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,w,h));
            double pull=Smooth(0.16,2.85,t);
            // Begin near the final wide view: a gentle retreat, not a ring close-up.
            double zoom=Lerp(1.15,1.03,pull);
            Rect global=Project(quiet,w,h,1.03,0.5,0.5);
            Point ringAnchor=new Point(global.X+global.Width*RingX,global.Y+global.Height*RingY);
            double ratio=zoom/1.03;
            Rect projection=new Rect(ringAnchor.X-global.Width*ratio*RingX,
                ringAnchor.Y-global.Height*ratio*RingY,global.Width*ratio,global.Height*ratio);
            double exposure=Lerp(0.105,1.0,Smooth(0,2.8,t));
            dc.PushOpacity(exposure);
            DrawContinuousScene(dc,projection,t,w,h);
            dc.Pop();
            DrawRingGlint(dc,projection,t);
            DrawDepthParticles(dc,t,w,h);
            if (t>=7.15) DrawTitle(dc,t,w,h);
            double skip=Smooth(1.3,2,t)*(1-Smooth(8.0,8.8,t));
            TextSpaced(dc,"ESC · SKIP",9,Mono,Solid(Colors.White,skip*0.34),w-142,19,1.3);
            if (exported && t>Program.FadeStart) dc.DrawRectangle(Solid(Color.FromRgb(7,17,31),Smooth(Program.FadeStart,Program.Duration,t)),null,new Rect(0,0,w,h));
            dc.Pop();
        }
        private static Rect Project(BitmapSource source,double w,double h,double zoom,double fx,double fy)
        {
            double scale=Math.Max(w/source.PixelWidth,h/source.PixelHeight)*zoom;
            double iw=source.PixelWidth*scale, ih=source.PixelHeight*scale;
            double x=Clamp(w*0.5-iw*fx,w-iw,0), y=Clamp(h*0.5-ih*fy,h-ih,0);
            return new Rect(x,y,iw,ih);
        }
        private static byte[] AlignedPixels(BitmapSource source,int width,int height)
        {
            var drawing=new DrawingVisual();
            using (DrawingContext content=drawing.RenderOpen())
                content.DrawImage(source,new Rect(0,0,width,height));
            var raster=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
            raster.Render(drawing);
            var straight=new FormatConvertedBitmap(raster,PixelFormats.Bgra32,null,0);
            byte[] pixels=new byte[width*height*4];
            straight.CopyPixels(pixels,width*4,0);
            return pixels;
        }
        private static BitmapSource[] BuildEyePatches(BitmapSource[] frames,BitmapSource character)
        {
            int width=frames[0].PixelWidth,height=frames[0].PixelHeight,stride=width*4;
            byte[] closed=AlignedPixels(frames[0],width,height);
            byte[] skin=AlignedPixels(character,width,height);
            short[] lighting=new short[closed.Length];
            double[] kernel=new double[17];
            for (int k=-8;k<=8;++k) kernel[k+8]=Math.Exp(-k*k/32.0);
            var result=new BitmapSource[frames.Length];
            for (int state=0;state<frames.Length;++state)
            {
                byte[] frame=state==0?closed:AlignedPixels(frames[state],width,height);
                byte[] patch=new byte[closed.Length];
                for (int y=0;y<height;++y) for (int x=0;x<width;++x)
                {
                    // Tight tilted eyelid regions, feathered well before the edge.
                    // Coordinates use the original 1672 x 941 registered canvas.
                    double px=(x+0.5)*1672.0/width,py=(y+0.5)*941.0/height;
                    double leftX=(px-945.5)/38.0,leftY=(py-353.5+0.14*(px-945.5))/15.0;
                    double rightX=(px-1039.0)/36.0,rightY=(py-341.0+0.14*(px-1039.0))/16.0;
                    double radius=Math.Min(Math.Sqrt(leftX*leftX+leftY*leftY),Math.Sqrt(rightX*rightX+rightY*rightY));
                    if (radius>=1) continue;
                    int p=(y*width+x)*4;
                    double feather=1-Smooth(0.70,1,radius);
                    if (state==0) EyeLighting(skin,closed,lighting,x,y,width,height,kernel);
                    for (int c=0;c<3;++c)
                    {
                        // Match low-frequency illumination while replacing the clean
                        // eyelid lines. Per-pixel deltas would retain a second closed lid.
                        patch[p+c]=(byte)Math.Max(0,Math.Min(255,frame[p+c]+lighting[p+c]));
                    }
                    patch[p+3]=(byte)Math.Round(skin[p+3]*feather);
                }
                result[state]=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,patch,stride);
                result[state].Freeze();
            }
            return result;
        }
        private static void EyeLighting(byte[] skin,byte[] closed,short[] lighting,int x,int y,int width,int height,double[] kernel)
        {
            int p=(y*width+x)*4;
            for (int c=0;c<3;++c)
            {
                double sum=0,weight=0;
                for (int ky=-8;ky<=8;++ky) for (int kx=-8;kx<=8;++kx)
                {
                    int sx=x+kx,sy=y+ky;
                    if (sx<0 || sy<0 || sx>=width || sy>=height) continue;
                    int q=(sy*width+sx)*4;
                    if (skin[q+3]<250) continue;
                    int delta=skin[q+c]-closed[q+c];
                    if (Math.Abs(delta)>80) continue;
                    double w=kernel[kx+8]*kernel[ky+8];
                    sum+=delta*w; weight+=w;
                }
                lighting[p+c]=(short)Math.Round(weight>0?sum/weight:0);
            }
        }
        private void DrawEyes(DrawingContext dc,Rect projection,double t)
        {
            double open=Smooth(3.10,4.95,t);
            // Use the same corrected closed-eye geometry from the first frame,
            // avoiding any change in skin tone or baseline lines at opening onset.
            // Cached patches preserve the character's skin color and contain only
            // the local eyelid changes, with no full-face lighting overlay.
            double position=open*3;
            int state=Math.Min(2,(int)Math.Floor(position));
            double blend=position-state;
            dc.DrawImage(eyeStates[state],projection);
            dc.PushOpacity(blend); dc.DrawImage(eyeStates[state+1],projection); dc.Pop();
        }
        private void DrawContinuousScene(DrawingContext dc,Rect projection,double t,double w,double h)
        {
            // Both character layers use the very same registered pose and canvas.
            // The head is never isolated or masked; final motion applies to the
            // whole character and every eye patch together, after the reveal ends.
            dc.DrawImage(quiet,projection);
            double reveal=Smooth(3.1,6.35,t);
            double motion=Smooth(6.35,7.3,t);
            double phase=t-6.35;
            double bx=-Math.Sin(phase*0.90)*4.5*motion, by=Math.Sin(phase*0.71)*2.5*motion;
            dc.PushOpacity(reveal);
            dc.PushTransform(new RotateTransform(-Math.Sin(phase*0.90)*0.10*motion,w/2,h/2));
            dc.DrawImage(background,Shift(projection,1,bx,by)); dc.Pop(); dc.Pop();
            DrawEnergy(dc,t,w,h,false);
            Point ring=new Point(projection.X+projection.Width*RingX,projection.Y+projection.Height*RingY);
            double breath=1+Math.Sin(phase*1.10)*0.0018*motion;
            dc.PushTransform(new ScaleTransform(breath,breath,ring.X,ring.Y));
            dc.PushTransform(new RotateTransform(Math.Sin(phase*1.35)*0.22*motion,ring.X,ring.Y));
            dc.DrawImage(quietForeground,projection);
            DrawEyes(dc,projection,t);
            // Finish opening the eyes before the brighter character light fades in.
            double light=Smooth(4.95,6.35,t);
            dc.PushOpacity(light); dc.DrawImage(foreground,projection); dc.Pop();
            dc.Pop(); dc.Pop();
            DrawBladeSheen(dc,projection,t,motion);
            DrawEnergy(dc,t,w,h,true);
        }
        private static Rect Shift(Rect rect,double scale,double dx,double dy)
        {
            return new Rect(rect.X-(scale-1)*rect.Width/2+dx,rect.Y-(scale-1)*rect.Height/2+dy,rect.Width*scale,rect.Height*scale);
        }
        private static void DrawRingGlint(DrawingContext dc,Rect projection,double t)
        {
            double alpha=Smooth(0,0.24,t)*(1-Smooth(1.55,2.8,t));
            if (alpha<=0) return;
            Point anchor=new Point(projection.X+projection.Width*RingX,projection.Y+projection.Height*RingY);
            double size=8+projection.Width/1672*4;
            Glow(dc,anchor,size*3.8,size*3.8,Blue,alpha*0.36);
            Glow(dc,anchor,size*1.12,size*0.72,Ice,alpha*0.80);
            // Small feathered lens glare attached to the actual ring reflection.
            double flare=alpha*(0.82+0.18*Math.Sin(t*3.2));
            Glow(dc,anchor,size*4.2,size*0.13,Ice,flare*0.40);
            Glow(dc,anchor,size*0.11,size*1.9,Ice,flare*0.25);
            dc.DrawEllipse(Solid(Colors.White,alpha*0.90),null,anchor,Math.Max(1.0,size*0.17),Math.Max(0.8,size*0.10));
        }
        private static void DrawBladeSheen(DrawingContext dc,Rect projection,double t,double motion)
        {
            double alpha=Smooth(6.35,7.15,t)*(1-Smooth(8.45,9.2,t));
            if (alpha<=0) return;
            double phase=t-6.35;
            double dx=Math.Sin(phase*1.25)*1.4*motion,dy=Math.Sin(phase*1.70)*0.8*motion;
            Point a=new Point(projection.X+projection.Width*0.585+dx,projection.Y+projection.Height*0.640+dy);
            Point b=new Point(projection.X+projection.Width*1.045+dx,projection.Y+projection.Height*0.391+dy);
            var curve=new StreamGeometry();
            using (var path=curve.Open())
            {
                path.BeginFigure(a,false,false);
                path.BezierTo(new Point(projection.X+projection.Width*0.74+dx,projection.Y+projection.Height*0.560+dy),
                    new Point(projection.X+projection.Width*0.88+dx,projection.Y+projection.Height*0.470+dy),b,true,false);
            }
            var sheen=new LinearGradientBrush(); sheen.MappingMode=BrushMappingMode.Absolute;
            sheen.StartPoint=a; sheen.EndPoint=b;
            sheen.GradientStops.Add(new GradientStop(WithAlpha(Ice,0),0));
            sheen.GradientStops.Add(new GradientStop(WithAlpha(Blue,alpha*0.12),0.20));
            sheen.GradientStops.Add(new GradientStop(WithAlpha(Ice,alpha*0.28),0.55));
            sheen.GradientStops.Add(new GradientStop(WithAlpha(Ice,0),1));
            dc.DrawGeometry(null,new Pen(sheen,8),curve);
            dc.DrawGeometry(null,new Pen(sheen,1.1),curve);
        }
        private static void DrawEnergy(DrawingContext dc,double t,double w,double h,bool near)
        {
            double strength=Smooth(4.15,6.1,t)*(1-Smooth(8.35,9.1,t));
            if (near) strength*=Smooth(6.3,6.55,t);
            int count=near?1:3;
            for (int i=0;i<count;++i)
            {
                double phase=t*0.7+i*2.1;
                double sway=Math.Sin(phase)*h*0.018;
                var curve=new StreamGeometry();
                using (var context=curve.Open())
                {
                    context.BeginFigure(new Point(w*(0.15+i*0.045),h*(0.96+i*0.025)),false,false);
                    context.BezierTo(new Point(w*(0.34+i*0.03),h*0.57+sway),new Point(w*(0.56+i*0.08),h*0.91-sway),
                        new Point(w*(1.04+i*0.05),h*(0.15+i*0.08)),true,false);
                }
                double opacity=strength*(near?0.085:0.075);
                dc.DrawGeometry(null,new Pen(Solid(Blue,opacity*0.22),near?22:14),curve);
                dc.DrawGeometry(null,new Pen(Solid(Ice,opacity),near?1.1:0.7),curve);
            }
        }
        private static void DrawDepthParticles(DrawingContext dc,double t,double w,double h)
        {
            double alpha=Smooth(2,4.3,t)*(1-Smooth(8.4,9.1,t));
            for (int i=0;i<17;++i)
            {
                bool near=i>=12;
                double s=Seed(i,7.5),s2=Seed(i,23.1);
                double x=w*(0.06+s*0.89)+Math.Sin(t*0.6+i)*(near?8:2);
                double y=h*Fraction(s2-t*(near?0.028:0.008));
                double size=near?1.3+s:0.45+s*0.4;
                double opacity=alpha*(near?0.25:0.17)*(0.7+0.3*Math.Sin(t*0.8+i));
                if (near) Glow(dc,new Point(x,y),size*5,size*5,Blue,opacity*0.1);
                dc.DrawEllipse(Solid(Ice,opacity),null,new Point(x,y),size,size);
            }
        }
        private static void DrawTitle(DrawingContext dc,double t,double w,double h)
        {
            double opacity=Smooth(7.15,7.88,t);
            double settle=Lerp(-5,0,Smooth(7.15,8.0,t));
            TextSpaced(dc,"CODEX",72,Thin,Solid(Colors.White,opacity*0.94),w*0.073+settle,h*0.43,14);
        }
        private static void TextSpaced(DrawingContext dc,string value,double size,Typeface face,Brush brush,double x,double y,double spacing)
        {
            foreach (char c in value)
            {
                var glyph=new FormattedText(c.ToString(),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,size,brush,1.0);
                dc.DrawText(glyph,new Point(x,y)); x+=glyph.WidthIncludingTrailingWhitespace+spacing;
            }
        }
        private static void Glow(DrawingContext dc,Point center,double rx,double ry,Color color,double alpha)
        {
            var brush=new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(WithAlpha(color,alpha),0));
            brush.GradientStops.Add(new GradientStop(WithAlpha(color,alpha*0.24),0.45));
            brush.GradientStops.Add(new GradientStop(WithAlpha(color,0),1));
            dc.DrawEllipse(brush,null,center,rx,ry);
        }
        private static SolidColorBrush Solid(Color color,double alpha) { return new SolidColorBrush(WithAlpha(color,alpha)); }
        private static Color WithAlpha(Color c,double a) { return Color.FromArgb((byte)(Clamp(a,0,1)*255),c.R,c.G,c.B); }
        private static double Lerp(double a,double b,double p) { return a+(b-a)*p; }
        private static double Clamp(double t,double a,double b) { return Math.Max(a,Math.Min(b,t)); }
        private static double Fraction(double t) { return t-Math.Floor(t); }
        private static double Seed(int i,double salt) { return Fraction(Math.Sin(i*93.73+salt*17.1)*45291.384); }
        internal static double Smooth(double a,double b,double t) { double p=Clamp((t-a)/(b-a),0,1); return p*p*(3-2*p); }
    }
}
