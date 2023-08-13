using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NativeWindow = OpenTK.Windowing.Desktop.NativeWindow;
using Nitride;
using System.CodeDom;
using OpenTkStudy.Properties;
using Nitride.EE;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OpenTkStudy
{
    [DesignerCategory("Code")]
    public class GLPanel : Control
    {
        public GLPanel()
        {
            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            DoubleBuffered = false;

            // Redraw the screen every 1/20 of a second.
            _timer = new Timer();
            _timer.Tick += (sender, e) =>
            {
                vertices[0] += 0.1f;

                if (vertices[0] > 1.0f) vertices[0] = 0.0f;

                vertices[1] += 0.1f;

                if (vertices[1] > 1.0f) vertices[1] = 0.0f;

                vertices[2] += 0.1f;

                if (vertices[2] > 1.0f) vertices[2] = 0.0f;

                _angle += 0.5f;
                iTime+=0.1f;
                Render2();
            };
            _timer.Interval = 50;   // 1000 ms per sec / 50 ms per frame = 20 FPS


            // Console.WriteLine("PixelShaderCode = " + PixelShaderCode);

            _timer.Start();
        }

        private float iTime = 0;

        private NativeWindow NativeWindow = null!;

        private NativeWindowSettings NativeWindowSettings { get; } = new NativeWindowSettings()
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(4, 6, 0, 0),

            Flags = ContextFlags.Default,
            Profile = ContextProfile.Compatability,
            AutoLoadBindings = true,
            IsEventDriven = true,

            // SharedContext = null,
            // NumberOfSamples = 0,

            StencilBits = 8,
            DepthBits = 24,
            RedBits = 8,
            GreenBits = 8,
            BlueBits = 8,
            AlphaBits = 8,
            SrgbCapable = true,

            StartFocused = false,
            StartVisible = false,
            WindowBorder = WindowBorder.Hidden,
            WindowState = WindowState.Normal
        };

        private unsafe bool IsNativeInputEnabled(NativeWindow nativeWindow)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr hWnd = GLFW.GetWin32Window(nativeWindow.WindowPtr);
                IntPtr style = Win32.GetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE);
                return ((Win32.WindowStyles)(long)style & Win32.WindowStyles.WS_DISABLED) == 0;
            }
            else
                throw new NotSupportedException("The current operating system is not supported by this control.");
        }

        /// <summary>
        /// A fix for the badly-broken DesignMode property, this answers (somewhat more
        /// reliably) whether this is DesignMode or not.  This does *not* work when invoked
        /// from the GLControl's constructor.
        /// </summary>
        /// <returns>True if this is in design mode, false if it is not.</returns>
        private bool DetermineIfThisIsInDesignMode()
        {
            // The obvious test.
            if (DesignMode)
                return true;

            // This works on .NET Framework but no longer seems to work reliably on .NET Core.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return true;

            // Try walking the control tree to see if any ancestors are in DesignMode.
            for (Control control = this; control != null; control = control.Parent)
            {
                if (control.Site != null && control.Site.DesignMode)
                    return true;
            }

            // Try checking for `IDesignerHost` in the service collection.
            if (GetService(typeof(System.ComponentModel.Design.IDesignerHost)) != null)
                return true;

            // Last-ditch attempt:  Is the process named `devenv` or `VisualStudio`?
            // These are bad, hacky tests, but they *can* work sometimes.
            if (System.Reflection.Assembly.GetExecutingAssembly().Location.Contains("VisualStudio", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(System.Diagnostics.Process.GetCurrentProcess().ProcessName, "devenv", StringComparison.OrdinalIgnoreCase))
                return true;

            // Nope.  Not design mode.  Probably.  Maybe.
            return false;
        }

        public bool IsDesignMode => _isDesignMode ??= DetermineIfThisIsInDesignMode();
        private bool? _isDesignMode;

        /// <summary>
        /// Ensure that the required underlying GLFW window has been created.
        /// </summary>
        private void EnsureCreated()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);

            if (!IsHandleCreated)
            {
                CreateControl();

                if (NativeWindow is null)
                    throw new InvalidOperationException("Failed to create GLControl."
                        + " This is usually caused by trying to perform operations on the GLControl"
                        + " before its containing form has been fully created.  Make sure you are not"
                        + " invoking methods on it before the Form's constructor has completed.");
            }

            if (NativeWindow is null && !DesignMode)
            {
                RecreateHandle();

                if (NativeWindow is null)
                    throw new InvalidOperationException("Failed to recreate GLControl :-(");
            }
        }

        #region Create

        public void CreateNativeWindow()
        {
            NativeWindow = new(NativeWindowSettings);
            NativeWindow.FocusedChanged += OnNativeWindowFocused;

            /// <summary>
            /// Reparent the given NativeWindow to be a child of this GLControl.  This is a
            /// non-portable operation, as its name implies:  It works wildly differently
            /// between OSes.  The current implementation only supports Microsoft Windows.
            /// </summary>
            /// <param name="nativeWindow">The NativeWindow that must become a child of
            /// this control.</param>
            unsafe
            {
                IntPtr hWnd = GLFW.GetWin32Window(NativeWindow.WindowPtr);

                // Reparent the real HWND under this control.
                Win32.SetParent(hWnd, Handle);

                // Change the real HWND's window styles to be "WS_CHILD | WS_DISABLED" (i.e.,
                // a child of some container, with no input support), and turn off *all* the
                // other style bits (most of the rest of them could cause trouble).  In
                // particular, this turns off stuff like WS_BORDER and WS_CAPTION and WS_POPUP
                // and so on, any of which GLFW might have turned on for us.
                IntPtr style = (IntPtr)(long)(Win32.WindowStyles.WS_CHILD | Win32.WindowStyles.WS_DISABLED);
                Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE, style);

                // Change the real HWND's extended window styles to be "WS_EX_NOACTIVATE", and
                // turn off *all* the other extended style bits (most of the rest of them
                // could cause trouble).  We want WS_EX_NOACTIVATE because we don't want
                // Windows mistakenly giving the GLFW window the focus as soon as it's created,
                // regardless of whether it's a hidden window.
                style = (IntPtr)(long)Win32.WindowStylesEx.WS_EX_NOACTIVATE;
                Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_EXSTYLE, style);
            }

            // Force the newly child-ified GLFW window to be resized to fit this control.
            ResizeNativeWindow();


            InitShader2();

            // And now show the child window, since it hasn't been made visible yet.
            NativeWindow.IsVisible = true;

            // NativeWindow.Refresh


        }

        protected override void OnHandleCreated(EventArgs e)
        {
            CreateNativeWindow();

            base.OnHandleCreated(e);

            if (IsResizeEventCancelled)
            {
                OnResize(EventArgs.Empty);
                IsResizeEventCancelled = false;
            }

            if (Focused || (NativeWindow is NativeWindow nwin && nwin.IsFocused)) // (_nativeWindow?.IsFocused ?? false)
            {
                ForceFocusToCorrectWindow();
            }
        }

        /// <summary>
        /// This private object is used as the reference for the 'Load' handler in
        /// the Events collection, and is only needed if you use the 'Load' event.
        /// </summary>
        private static readonly object EVENT_LOAD = new object();

        /// <summary>
        /// The Load event is fired before the control becomes visible for the first time.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        protected virtual void OnLoad(EventArgs e)
        {
            // There is no good way to explain this event except to say
            // that it's just another name for OnControlCreated.
            ((EventHandler)Events[EVENT_LOAD])?.Invoke(this, e);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            ResizeNativeWindow();
            base.OnParentChanged(e);
        }

        private void DestroyNativeWindow()
        {
            // if (DesignMode)

            if (NativeWindow is NativeWindow win)
            {
                DeleteShader();
                win.Dispose();
                NativeWindow = null!;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            DestroyNativeWindow();

            base.OnHandleDestroyed(e);
        }

        #endregion Create

        #region Resize

        private void ResizeNativeWindow()
        {
            /*
            if (ClientSize.Height == 0)
                ClientSize = new System.Drawing.Size(ClientSize.Width, 1);*/

            if (NativeWindow is NativeWindow win)
            {
                if (Height > 0 && Width > 0)
                {
                    // win.Location = new Vector2i(0, 0); 
                    win.ClientRectangle = new Box2i(0, 0, Width, Height);
                }


            }
        }

        private bool IsResizeEventCancelled { get; set; } = false;

        protected override void OnResize(EventArgs e)
        {
            if (!IsHandleCreated)
            {
                IsResizeEventCancelled = true;
                return;
            }

            ResizeNativeWindow();

            if (NativeWindow is NativeWindow win)
            {
                win.MakeCurrent();

                GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);

                float aspect_ratio = Math.Max(ClientSize.Width, 1) / (float)Math.Max(ClientSize.Height, 1);
                Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);
                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadMatrix(ref perpective);

                // Console.WriteLine("NativeWindow Size = " + win.ClientRectangle + " | Location = " + win.Location);
            }

            base.OnResize(e);
        }

        #endregion Resize

        #region Focus

        private void ForceFocusToCorrectWindow()
        {

            unsafe
            {
                if (IsNativeInputEnabled(NativeWindow))
                {
                    // Focus should be on the NativeWindow inside the GLControl.
                    NativeWindow.Focus();
                }
                else
                {
                    // Focus should be on the GLControl itself.
                    Focus();
                }
            }
        }

        /// <summary>
        /// These EventArgs are used as a safety check to prevent unexpected recursion
        /// in OnGotFocus.
        /// </summary>
        private static readonly EventArgs _noRecursionSafetyArgs = new();

        private void OnNativeWindowFocused(FocusedChangedEventArgs e)
        {
            if (e.IsFocused)
            {
                ForceFocusToCorrectWindow();
                OnGotFocus(_noRecursionSafetyArgs);
            }
            else
            {
                OnLostFocus(EventArgs.Empty);
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            if (!ReferenceEquals(e, _noRecursionSafetyArgs))
            {
                ForceFocusToCorrectWindow();
            }
        }


        #endregion Focus

        private float _angle = 0.0f;
        private Timer _timer = null!;

        public void Render()
        {
            if (NativeWindow is NativeWindow win)
            {
                EnsureCreated();
                win.MakeCurrent();

                GL.ClearColor(Color4.Teal);
                GL.Enable(EnableCap.DepthTest);

                Matrix4 lookat = Matrix4.LookAt(0, 5, 5, 0, 0, 0, 0, 1, 0);
                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadMatrix(ref lookat);

                GL.Rotate(_angle, 0.0f, 1.0f, 0.0f);

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                GL.Begin(PrimitiveType.Quads);

                GL.Color4(Color4.Silver);
                GL.Vertex3(-1.0f, -1.0f, -1.0f);
                GL.Vertex3(-1.0f, 1.0f, -1.0f);
                GL.Vertex3(1.0f, 1.0f, -1.0f);
                GL.Vertex3(1.0f, -1.0f, -1.0f);

                GL.Color4(Color4.Honeydew);
                GL.Vertex3(-1.0f, -1.0f, -1.0f);
                GL.Vertex3(1.0f, -1.0f, -1.0f);
                GL.Vertex3(1.0f, -1.0f, 1.0f);
                GL.Vertex3(-1.0f, -1.0f, 1.0f);

                GL.Color4(Color4.Moccasin);
                GL.Vertex3(-1.0f, -1.0f, -1.0f);
                GL.Vertex3(-1.0f, -1.0f, 1.0f);
                GL.Vertex3(-1.0f, 1.0f, 1.0f);
                GL.Vertex3(-1.0f, 1.0f, -1.0f);

                GL.Color4(Color4.IndianRed);
                GL.Vertex3(-1.0f, -1.0f, 1.0f);
                GL.Vertex3(1.0f, -1.0f, 1.0f);
                GL.Vertex3(1.0f, 1.0f, 1.0f);
                GL.Vertex3(-1.0f, 1.0f, 1.0f);

                GL.Color4(Color4.PaleVioletRed);
                GL.Vertex3(-1.0f, 1.0f, -1.0f);
                GL.Vertex3(-1.0f, 1.0f, 1.0f);
                GL.Vertex3(1.0f, 1.0f, 1.0f);
                GL.Vertex3(1.0f, 1.0f, -1.0f);

                GL.Color4(Color4.ForestGreen);
                GL.Vertex3(1.0f, -1.0f, -1.0f);
                GL.Vertex3(1.0f, 1.0f, -1.0f);
                GL.Vertex3(1.0f, 1.0f, 1.0f);
                GL.Vertex3(1.0f, -1.0f, 1.0f);

                GL.End();

                EnsureCreated();
                win.Context.SwapBuffers();

                // Console.WriteLine("Render");
            }
        }

        #region 2D Shader Test

        float[] vertices = new float[]
        {
            0.0f, 0.5f, 0f, // v0
            0.5f, -0.5f, 0f, // v1
            -0.5f, -0.5f, 0f, // v2
        };

        private int VertexBufferHandle;

        private int VertexArrayHandle;

        string VertexShaderCode => Resources.VertexShader2;

        string PixelShaderCode => Resources.PixelShader2;

        int ShaderProgramHandle; // = GL.CreateP

        public void InitShader3()
        {
            if (NativeWindow is NativeWindow win)
            {
                EnsureCreated();
                win.MakeCurrent();

                VertexBufferHandle = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferHandle);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StreamDraw); // Static
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0); // Unbind it??

                VertexArrayHandle = GL.GenVertexArray();
                GL.BindVertexArray(VertexArrayHandle);

                GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferHandle);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                GL.BindVertexArray(0);

                int vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vertexShaderHandle, VertexShaderCode);
                GL.CompileShader(vertexShaderHandle);
                Console.WriteLine("vertexShader Log = " + GL.GetShaderInfoLog(vertexShaderHandle));

                int pixelShaderHandle = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(pixelShaderHandle, PixelShaderCode);
                GL.CompileShader(pixelShaderHandle);
                Console.WriteLine("pixelShader Log = " + GL.GetShaderInfoLog(pixelShaderHandle));

                ShaderProgramHandle = GL.CreateProgram();
                GL.AttachShader(ShaderProgramHandle, vertexShaderHandle);
                GL.AttachShader(ShaderProgramHandle, pixelShaderHandle);
                GL.LinkProgram(ShaderProgramHandle);
                
                GL.DetachShader(ShaderProgramHandle, vertexShaderHandle);
                GL.DetachShader(ShaderProgramHandle, pixelShaderHandle);

                GL.DeleteShader(vertexShaderHandle);
                GL.DeleteShader(pixelShaderHandle);

                Console.WriteLine("ShaderProgram Log = " + GL.GetShaderInfoLog(ShaderProgramHandle));
            }
        }

        public void InitShader2()
        {
            if (NativeWindow is NativeWindow win)
            {
                int status;

                EnsureCreated();
                win.MakeCurrent();

                int vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vertexShaderHandle, Resources.VertexShaderFt);
                GL.CompileShader(vertexShaderHandle);
                Console.WriteLine("vertexShader Log = " + GL.GetShaderInfoLog(vertexShaderHandle));
                GL.GetShader(vertexShaderHandle, ShaderParameter.CompileStatus, out status);
                
                if (status != 1)
                {
                    Console.WriteLine("vertex shader error");
                }

                int pixelShaderHandle = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(pixelShaderHandle, Resources.PixelShaderFt);
                GL.CompileShader(pixelShaderHandle);
                Console.WriteLine("pixelShader Log = " + GL.GetShaderInfoLog(pixelShaderHandle));
                GL.GetShader(pixelShaderHandle, ShaderParameter.CompileStatus, out status);
                if (status != 1)
                {
                    Console.WriteLine("fragment shader error");
                }

                ShaderProgramHandle = GL.CreateProgram();
                GL.AttachShader(ShaderProgramHandle, vertexShaderHandle);
                GL.AttachShader(ShaderProgramHandle, pixelShaderHandle);
                GL.LinkProgram(ShaderProgramHandle);

                GL.DetachShader(ShaderProgramHandle, vertexShaderHandle);
                GL.DetachShader(ShaderProgramHandle, pixelShaderHandle);

                GL.DeleteShader(vertexShaderHandle);
                GL.DeleteShader(pixelShaderHandle);

                Console.WriteLine("ShaderProgram Log = " + GL.GetShaderInfoLog(ShaderProgramHandle));
                GL.GetProgram(ShaderProgramHandle, GetProgramParameterName.LinkStatus, out status);
                if (status != 1)
                {
                    Console.WriteLine("ShaderProgram shader error");
                }

      
            }
        }

        public void Render2()
        {
            if (NativeWindow is NativeWindow win)
            {
                EnsureCreated();
                win.MakeCurrent();

                GL.ClearColor(Color4.Orange);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                GL.UseProgram(ShaderProgramHandle);

                GL.Uniform3(GL.GetUniformLocation(ShaderProgramHandle, "iResolution"), (float)ClientSize.Width, (float)ClientSize.Height, 0.0f);
                GL.Uniform1(GL.GetUniformLocation(ShaderProgramHandle, "iTime"), iTime);
                GL.Begin(PrimitiveType.Quads);
                GL.Vertex3(-1.0f, 1.0f, 0.0f);
                GL.Vertex3(-1.0f, -1.0f, 0.0f);
                GL.Vertex3(1.0f, -1.0f, 0.0f);
                GL.Vertex3(1.0f, 1.0f, 0.0f);
                GL.End();

                EnsureCreated();
                win.Context.SwapBuffers();
            }
        }

        public void DeleteShader()
        {
            if (NativeWindow is NativeWindow win)
            {
                EnsureCreated();
                win.MakeCurrent();
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.DeleteBuffer(VertexBufferHandle);

                GL.UseProgram(0);
                GL.DeleteProgram(ShaderProgramHandle);

                VertexBufferHandle = 0;
                ShaderProgramHandle = 0;
            }
        }

        public void Render3()
        {
            if (NativeWindow is NativeWindow win)
            {
                EnsureCreated();
                win.MakeCurrent();

                GL.ClearColor(Color4.Orange);
                GL.Clear(ClearBufferMask.ColorBufferBit);


                GL.UseProgram(ShaderProgramHandle);

                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StreamDraw); // Static

                GL.BindVertexArray(VertexArrayHandle);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                //Console.WriteLine("ShaderProgramHandle = " + ShaderProgramHandle + " | VertexArrayHandle = " + VertexArrayHandle);

                EnsureCreated();
                win.Context.SwapBuffers();
            }
        }

        #endregion 2D Shader Test

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureCreated();

            Render2();

            // Console.WriteLine("NativeWindow Size = " + win.ClientRectangle + " | Location = " + win.Location);
            base.OnPaint(e);
        }

    }

    /// <summary>
    /// P/Invoke functions and declarations for Microsoft Windows (32-bit and 64-bit).
    /// </summary>
    internal static class Win32
    {
        #region Enums

        public enum WindowLongs : int
        {
            GWL_EXSTYLE = -20,
            GWLP_HINSTANCE = -6,
            GWLP_HWNDPARENT = -8,
            GWL_ID = -12,
            GWL_STYLE = -16,
            GWL_USERDATA = -21,
            GWL_WNDPROC = -4,
            DWLP_DLGPROC = 4,
            DWLP_MSGRESULT = 0,
            DWLP_USER = 8,
        }

        [Flags]
        public enum WindowStyles : uint
        {
            WS_BORDER = 0x800000,
            WS_CAPTION = 0xc00000,
            WS_CHILD = 0x40000000,
            WS_CLIPCHILDREN = 0x2000000,
            WS_CLIPSIBLINGS = 0x4000000,
            WS_DISABLED = 0x8000000,
            WS_DLGFRAME = 0x400000,
            WS_GROUP = 0x20000,
            WS_HSCROLL = 0x100000,
            WS_MAXIMIZE = 0x1000000,
            WS_MAXIMIZEBOX = 0x10000,
            WS_MINIMIZE = 0x20000000,
            WS_MINIMIZEBOX = 0x20000,
            WS_OVERLAPPED = 0x0,
            WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_SIZEFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
            WS_POPUP = 0x80000000u,
            WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
            WS_SIZEFRAME = 0x40000,
            WS_SYSMENU = 0x80000,
            WS_TABSTOP = 0x10000,
            WS_VISIBLE = 0x10000000,
            WS_VSCROLL = 0x200000,
        }

        [Flags]
        public enum WindowStylesEx : uint
        {
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_COMPOSITED = 0x02000000,
            WS_EX_CONTEXTHELP = 0x00000400,
            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_LAYERED = 0x00080000,
            WS_EX_LAYOUTRTL = 0x00400000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_MDICHILD = 0x00000040,
            WS_EX_NOACTIVATE = 0x08000000,
            WS_EX_NOINHERITLAYOUT = 0x00100000,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_NOREDIRECTIONBITMAP = 0x00200000,
            WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
            WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            WS_EX_RIGHT = 0x00001000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_TRANSPARENT = 0x00000020,
            WS_EX_WINDOWEDGE = 0x00000100,
        }

        #endregion

        #region Miscellaneous User32 stuff

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        #endregion

        #region Miscellaneous Kernel32 stuff

        public static int GetLastError()
            => Marshal.GetLastWin32Error();     // This alias isn't strictly needed, but it reads better.

        #endregion

        #region GetWindowLong/SetWindowLong and friends

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, WindowLongs nIndex)
            => GetWindowLongPtr(hWnd, (int)nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongs nIndex, IntPtr dwNewLong)
            => SetWindowLongPtr(hWnd, (int)nIndex, dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        #endregion
    }
}
