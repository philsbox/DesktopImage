//DesktopImage - allows a semi-transparent image overlay on the windows desktop
//Config file DesktopImage.ini in same dir, will be created automatically if not present, can then be amended
//To automatically show when anyone logs on, create a shortcut to this program and move to
// C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp
//Some code from customdesktoplogo.wikidot.com  (Custom Desktop Logo, Eric Wong, licensed under GNU GPLv3). 



using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;

namespace DesktopImage
{
    class Program
    {
        static string sImgFile = "Cross.png";       //Image file relative to exe
        static double Xfrac = 0.5, Yfrac = 0.5;     //Image position, fraction of screen, 0-1
        static double Scale = 2.0, Opacity = 0.4;   //Image scale & opacity
        static double AspectXY = 0;         //Set to only show image if screen has this aspect ratio, eg 1.78
        static bool   RotateLock = false;   //Set to keep image physical position when rotate tablet
        static Int32  RenewSecs = 0;        //Set to renew image on timer, in case lost
        static AlphaImageObject image1;     //Class that places the image on the desktop

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // Read Config or set defaults
            string sLine = "", sTag = "";
            string sPath = Application.StartupPath;
            string sConfigFile = sPath+ "\\DesktopImage.ini";
            if (!File.Exists(sConfigFile))
            {
                MessageBox.Show("Config not found, writing defaults:\n" + sConfigFile);
                File.WriteAllText(sConfigFile, "ImgFile=Cross.png\nScale=2.0\nOpacity=0.4\nXratio=0.5\nYratio=0.5\nAspectXY=0\nRotateLock=0\nRenewSecs=60");
            }
            else try
            {
                StreamReader sr = File.OpenText(sConfigFile);
                while (!String.IsNullOrEmpty(sLine=sr.ReadLine()))
                {
                    String[] keyPair = sLine.Split(new char[] { '=' }, 2);
                    sTag = keyPair[0].Trim().ToUpper();
                    if (sTag=="IMGFILE")
                        sImgFile = keyPair[1];
                    else if (sTag == "SCALE")
                        Scale = Double.Parse(keyPair[1]);
                    else if (sTag == "OPACITY")
                        Opacity = Double.Parse(keyPair[1]);
                    else if (sTag == "XRATIO")
                        Xfrac = Double.Parse(keyPair[1]);
                    else if (sTag == "YRATIO")
                        Yfrac = Double.Parse(keyPair[1]);
                    else if (sTag == "ASPECTXY")
                        AspectXY = Double.Parse(keyPair[1]);
                    else if (sTag == "ROTATELOCK")
                        RotateLock = (Int16.Parse(keyPair[1]) != 0);
                    else if (sTag == "RENEWSECS")
                        RenewSecs = Int32.Parse(keyPair[1]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Config file read error [{sLine}]: {ex.Message}");
                Application.Exit();
            }
            if (sImgFile == "")
                MessageBox.Show("Image file not set");
            else if (!File.Exists(sImgFile= sPath+"\\"+sImgFile))
                MessageBox.Show("Image file not found:\n" + sImgFile);

            else try  // Show the image
           
            {
                    image1 = new AlphaImageObject(new System.Drawing.Bitmap(sImgFile), Scale, Opacity, Xfrac, Yfrac, AspectXY, RotateLock);
                    if (RenewSecs > 0)
                    {
                        // Create a timer to renew image, sometimes gets lost otherwise after a long period
                        System.Timers.Timer timer1 = new System.Timers.Timer();
                        timer1.Interval = RenewSecs*1000;  //Minutes before re-display
                        timer1.AutoReset = true; //Repeated events (default)
                        timer1.Enabled = true;  // Start the timer
                        timer1.Elapsed += OnTimedEvent; // Hook up event handler 
                    }
                    Application.Run();

                }
                catch (Exception ex)
            {
                MessageBox.Show("Fatal error " + ex.Message);
                Application.Exit();
            }
        }

        private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            //  MessageBox.Show("Timer at {0}", e.SignalTime.ToShortTimeString());
            renewImage("");
        }
        //Cannot directly renew image1 from timer because it would trigger on a different thread, giving error:
        //' Cross-thread operation not valid: Control accessed from a thread other than the thread it was created on'
        //Use cross-thread call via the Invoke method with a delegate
        //If InvokeRequired true, the renewImage fn passes the SafeCallDelegate to the Invoke method to make the actual call.
        //See https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/how-to-make-thread-safe-calls-to-windows-forms-controls
        private delegate void SafeCallDelegate(string text);
        private static void renewImage(string text)
        {
            if (image1.InvokeRequired)
            {
                var d = new SafeCallDelegate(renewImage);
                image1.Invoke(d, new object[] { text });
            }
            else
            {
                AlphaImageObject image2 = new AlphaImageObject(new System.Drawing.Bitmap(sImgFile), Scale, Opacity, Xfrac, Yfrac, AspectXY, RotateLock);
                image1.Dispose(); //Dispose of old image1 afterwards so no slight delay awaiting slower image2 creation
                image1 = image2;  //Reference new image now
                GC.Collect();     //Free up memory before go idle
            }
        }

    }



    /// <summary>
    /// Creates an alpha blended transparent image object.
    /// </summary>
    public class AlphaImageObject : AlphaForm
    {

        #region AlphaImageObject
        private Bitmap imgBitmap;           //Bitmap must be 32ppp with alpha-channel
        private WindowLevelTypes winLevel;  //WindowLevelTypes.Topmost
        private bool RotateLock;            //Keep image physical position when rotate tablet
        private double AspectXY;            //Show image only for this screen aspect ratio (x/y)
        private double RatioLeft, RatioTop; //Image position, fraction of screen dims, 0-1
        private int imgOpacity;             //integer 0-255
        private int posLeft, posTop;        //Image position, pixels
        private int screenX, screenY, screenR;

        public AlphaImageObject(Bitmap image, double Scale, double Opacity, double xRatio, double yRatio, double aspectXY, bool rotLock)
        {
            // Constructor
            imgBitmap = new Bitmap(image, new Size((int)Math.Round(image.Width * Scale), (int)Math.Round(image.Height * Scale)));

            winLevel = WindowLevelTypes.Topmost;
            imgOpacity = (int)Math.Round(Opacity * 255);  //Convert 0-255
            this.RotateLock = rotLock;
            this.AspectXY = aspectXY;
            RatioLeft = xRatio;
            RatioTop = yRatio;

            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(1, 1);
            this.Name = "TransparentObject";
            this.ResumeLayout(false);
            SetScreenPosition();
            SetBitmap();
            SetZLevel();
            if (this.RotateLock || (this.AspectXY > 0))
                //Keep image position when rotate tablet or change resolution, set event handler
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += new EventHandler(screenEvent);

        }

        void screenEvent(object sender, EventArgs e)
        {
            //MessageBox.Show("screenEvent");
            // Screen resize/rotate event: recalc image position
            SetScreenPosition();
            SetBitmap();
        }

        public void SetZLevel()
        {
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_NOSIZE = 0x1;
            const uint SWP_NOMOVE = 0x2;
            const uint SWP_NOREDRAW = 0x0008;
            const uint SWP_NOOWNERZORDER = 0x0200;  /* Don't do owner Z ordering */
            const uint SWP_NOSENDCHANGING = 0x0400; /* Don't send WM_WINDOWPOSCHANGING */
            Win32.SetWindowPos(this.Handle, (IntPtr)winLevel, 0, 0, 0, 0,
                                       SWP_NOMOVE | SWP_NOSIZE |
                                       SWP_NOACTIVATE | SWP_NOOWNERZORDER |
                                       SWP_NOREDRAW | SWP_NOSENDCHANGING);
        }

        protected override CreateParams CreateParams
        {
            /// Allows us to set the window styles at creation time to allow for widget type objects.
            get
            {
                //const int GWL_EXSTYLE = (-20);             // Extended window styles.
                const int WS_EX_LAYERED = 0x00080000;    // Creates a layered window. 
                const int WS_EX_TRANSPARENT = 0x00000020;    // Not painted until after siblings beneath the window 
                const int WS_EX_TOOLWINDOW = 0x00000080;    // Creates a floating toolbar window, does not appear in the taskba

                //Set the form to be a layered type for alpha blended graphics, and a toolwindow type to remove from taskbar & Alt-Tab list.
                CreateParams cp = base.CreateParams;
                cp.ExStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT;  // | Constants.WindowExStyles.WS_EX_NOACTIVATE;
                cp.Style = unchecked((int)0xD4000000);
                return cp;
            }
        }

        void SetScreenPosition()
        {
            screenX = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            screenY = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
            screenR = (int)SystemInformation.ScreenOrientation;  //0,1=90,2=180,3=270

            if ((AspectXY > 0) && (screenX > 0) && (screenY > 0) && (Math.Round(100.0 * AspectXY) != Math.Round(100.0 * Math.Max(screenX, screenY) / Math.Min(screenX, screenY))))
            {
                posLeft = posTop = -99;  //Wrong screen resolution, disable
            }
            else if (RotateLock && (screenR == 1))  //90deg
            {
                posLeft = (int)Math.Round((1.0 - RatioTop) * screenX - imgBitmap.Width / 2);
                posTop = (int)Math.Round(RatioLeft * screenY - imgBitmap.Height / 2);
            }
            else if (RotateLock && (screenR == 2))  //180deg
            {
                posLeft = (int)Math.Round((1.0 - RatioLeft) * screenX - imgBitmap.Width / 2);
                posTop = (int)Math.Round((1.0 - RatioTop) * screenY - imgBitmap.Height / 2);
            }
            else if (RotateLock && (screenR == 3))  //270deg
            {
                posLeft = (int)Math.Round(RatioTop * screenX - imgBitmap.Width / 2);
                posTop = (int)Math.Round((1.0 - RatioLeft) * screenY - imgBitmap.Height / 2);
            }
            else
            {
                posLeft = (int)Math.Round(RatioLeft * screenX - imgBitmap.Width / 2);
                posTop = (int)Math.Round(RatioTop * screenY - imgBitmap.Height / 2);
            }
            // MessageBox.Show($"screenEvent: W={screenX}, H={screenY}, Ori={screenR} =>L:{posLeft}, T={posTop}" );
        }
        #endregion


        #region AlphaBlending

        /// <summary> 
        /// Changes the current bitmap shown in the form with a custom opacity level and alpha blending.  
        /// Here is where all happens!
        /// The size of the bitmap drawn is equal to the size of the given "bitmap".
        /// </summary>
        public void SetBitmap()
        {
            if (posLeft < 0 || posTop < 0)
                return;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oBitmap = IntPtr.Zero;
            IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
            IntPtr memDc = Win32.CreateCompatibleDC(screenDc);

            try
            {
                if (imgBitmap == null)
                    imgBitmap = new Bitmap(10, 10);
                hBitmap = imgBitmap.GetHbitmap(Color.FromArgb(0));
                oBitmap = Win32.SelectObject(memDc, hBitmap);
                Size size = new Size(imgBitmap.Width, imgBitmap.Height);
                Point pointSource = new Point(0, 0);

                Win32.BLENDFUNCTION blend = new Win32.BLENDFUNCTION();
                blend.BlendOp = 0;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = (byte)imgOpacity;
                blend.AlphaFormat = 1;

                Point imgPos = new Point(posLeft, posTop);
                Win32.UpdateLayeredWindow(Handle, screenDc, ref imgPos, ref size, memDc, ref pointSource, 0, ref blend, Win32.ULW_ALPHA);
            }
            finally
            {
                Win32.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    Win32.SelectObject(memDc, oBitmap);
                    Win32.DeleteObject(hBitmap);
                }
                Win32.DeleteDC(memDc);
            }
        }
        #endregion
    }



    #region AlphaForm
    /// <summary>
    /// This is the basic class that other dock items/objects inherits. 
    /// Essentially, it contains methods that manage the setting of the image bitmaps to be displayed.
    /// </summary>
    public class AlphaForm : Form
    {


        /// <summary> 
        /// AlphaForm is the basis of alpha blended image objects.
        /// </summary>
        public AlphaForm()
        {

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            AllowDrop = false;
            EnableDoubleBuffering();
            StartPosition = FormStartPosition.Manual;
        }

        /// <summary>
        ///  Enable double-buffering
        /// </summary>
        public void EnableDoubleBuffering()
        {
            // Set the value of the double-buffering style bits to true.
            DoubleBuffered = true;
            this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint |
                            ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();
        }

    }
    #endregion



    #region "Win32 API"
    // Level values for SetWindowPos/hWndInsertAfter
    public enum WindowLevelTypes
    {
        Topmost = -1,
        Normal = -2,
        AlwaysOnBottom = 1
    }

    public class Win32
    {
        // Declarations for the User32 API Calls
        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0;
        public const byte AC_SRC_ALPHA = 1;

        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;

        }
        [DllImportAttribute("user32.dll")]
        public extern static bool UpdateLayeredWindow(IntPtr handle, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport(@"user32.dll", EntryPoint = "SetWindowPos", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImportAttribute("user32.dll")]
        public extern static IntPtr GetDC(IntPtr handle);

        [DllImportAttribute("user32.dll", ExactSpelling = true)]
        public extern static int ReleaseDC(IntPtr handle, IntPtr hDC);

        [DllImportAttribute("gdi32.dll")]
        public extern static IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImportAttribute("gdi32.dll")]
        public extern static bool DeleteDC(IntPtr hdc);

        [DllImportAttribute("gdi32.dll")]
        public extern static IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImportAttribute("gdi32.dll")]
        public extern static bool DeleteObject(IntPtr hObject);
    }
    #endregion
}

