using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Emgu.CV;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using EE4WebCam;
using System.Drawing.Imaging;
using System.Media;
using System.Threading;

using System.Runtime.InteropServices;

// Reference path for the following assemblies --> C:\Program Files\Microsoft Expression\Encoder 4\SDK\
using Microsoft.Expression.Encoder.Devices;
using Microsoft.Expression.Encoder.Live;
using Microsoft.Expression.Encoder;

// Reference path for playing video
using Microsoft.DirectX.AudioVideoPlayback;
using System.Reflection;

namespace EE4Test
{
    public partial class frmEE4WebCam : Form
    {

        #region DEVICES PARAMETERS

        // Dau vao
        private EncoderDevice videoSelectedIn = null;
        private EncoderDevice audioSelectedIn = null;
        private LiveJob jobIn = null;
        private LiveDeviceSource deviceSourceIn = null;
        private bool isStartIn = false;

        // Dau ra
        private EncoderDevice videoSelectedOut = null;
        private EncoderDevice audioSelectedOut = null;
        private LiveJob jobOut = null;
        private LiveDeviceSource deviceSourceOut = null;
        private bool isStartOut = false;

        // Thong bao khi xe vao ra
        SoundPlayer simpleSoundIn = null;
        private bool isPlayingSoundIn = false;

        SoundPlayer simpleSoundOut = null;
        private bool isPlayingSoundOut = false;

        #endregion

        #region DETECTOR PARAMETERS
            
            // Static parameters
            private static double DELTA_TIME_NOT_DETECT_PLATE = 3;
            private static double TOTAL_TIME_VEHICLE_IN = 10;

            // Thiet lap bo phat hien dau vao
            private OCRHandler ocrHandlerIn = null;
            private long TimeVehicleIn_Start = 0;
            private long TimeVehicleIn_Previous = 0;
            private long TimeVehicleIn_End = 0;
            private bool IsStillDetectedIn = false;
            private bool AnnounceFirstTimeIn = false;
            private bool UpdateAnnounceLabelIn = false;

            // Thiet lap bo phat hien dau ra
            private OCRHandler ocrHandlerOut = null;
            private long TimeVehicleOut_Start = 0;
            private long TimeVehicleOut_Previous = 0;
            private long TimeVehicleOut_End = 0;
            private bool IsStillDetectedOut = false;
            private bool AnnounceFirstTimeOut = false;
            private bool UpdateAnnounceLabelOut = false;

            // Thiet lap database
            private PlateDatabase plateDatabase = null;
            private PlateDatabase defaultDatabase = null;

        #endregion

        
        public frmEE4WebCam()
        {
            InitializeComponent();
            DefaultVideo();
            stopJobIn();

            InitializeTimer();
            InitializeSound();
            InitializeDetectors();
            InitializeDefaultDatabase();
        }

        private void DefaultVideo()
        {
            jobIn = new LiveJob();

            // Create a new device source. We use the first audio and video devices on the system
            deviceSourceIn = jobIn.AddDeviceSource(EncoderDevices.FindDevices(EncoderDeviceType.Video)[0], EncoderDevices.FindDevices(EncoderDeviceType.Audio)[0]);

            // Get the properties of the device video
            SourceProperties sp = deviceSourceIn.SourcePropertiesSnapshot();

            // Resize the preview panel to match the video device resolution set
            VideoPreviewIn_Panel.Size = new Size(sp.Size.Width, sp.Size.Height);

            // Setup the output video resolution file as the preview
            jobIn.OutputFormat.VideoProfile.Size = new Size(sp.Size.Width, sp.Size.Height);

            // Sets preview window to winform panel hosted by xaml window
            deviceSourceIn.PreviewWindow = new PreviewWindow(new HandleRef(VideoPreviewIn_Panel, VideoPreviewIn_Panel.Handle));

            // Make this source the active one
            jobIn.ActivateSource(deviceSourceIn);
        }

        // Thiet lap default database
        //[DllImport("WindowsBase.dll")]
        //[DllImport("Microsoft.Expression.Encoder.Api2.dll")]
        //[DllImport("EncoderCore.dll")]
        //[DllImport("Microsoft.Expression.Licensing.dll")]
        //[DllImport("Microsoft.Expression.Framework.dll")]
        private void InitializeDefaultDatabase()
        {
            defaultDatabase = new PlateDatabase();

            defaultDatabase.AddPlate(new Image<Bgr, Byte>("..\\..\\default_plate\\plate1.jpg"), "53P93623");
            defaultDatabase.AddPlate(new Image<Bgr, Byte>("..\\..\\default_plate\\plate2.jpg"), "59B113944");
        }

        // Thiet lap bo detector
        private void InitializeDetectors()
        {
            ocrHandlerIn = new OCRHandler();
            ocrHandlerOut = new OCRHandler();

            plateDatabase = new PlateDatabase();
        }

        // Thiet lap thong tin am thanh bao dau vao va ra
        private void InitializeSound()
        {
            simpleSoundIn = new SoundPlayer("Sound.wav");
            simpleSoundOut = new SoundPlayer("Sound.wav");
        }

        // Thiet lap thong tin timer
        private void InitializeTimer()
        {
            DetectorIn_Timer.Interval = 1000;
            DetectorIn_Timer.Enabled = false;
            SoundIn_Timer.Interval = 3000;
            SoundIn_Timer.Enabled = false;

            DetectorOut_Timer.Interval = 1000;
            DetectorOut_Timer.Enabled = false;
            SoundOut_Timer.Interval = 3000;
            SoundOut_Timer.Enabled = false;
        }

        // Thiet lap camera dau vao
        private void ChooseCameraIn_Button_Click(object sender, EventArgs e)
        {
            ListCamera listCameraForm = new ListCamera();
            listCameraForm.ShowDialog();
            videoSelectedIn = listCameraForm.VideoSelected;
            audioSelectedIn = listCameraForm.AudioSelected;
        }

        // Thiet lap camera dau ra
        private void ChooseCameraOut_Button_Click(object sender, EventArgs e)
        {
            ListCamera listCameraForm = new ListCamera();
            listCameraForm.ShowDialog();
            videoSelectedOut = listCameraForm.VideoSelected;
            audioSelectedOut = listCameraForm.AudioSelected;
        }

        // Dung camera dau vao
        private void stopJobIn()
        {
            if (jobIn != null)
            {
                if (deviceSourceIn != null)
                {
                    // Remove the Device Source and destroy the job
                    jobIn.RemoveDeviceSource(deviceSourceIn);

                    // Destroy the device source
                    deviceSourceIn.PreviewWindow = null;
                    deviceSourceIn = null;
                }
            }
        }

        // Dung camera dau ra
        private void stopJobOut()
        {
            if (jobOut != null)
            {
                if (deviceSourceOut != null)
                {
                    // Remove the Device Source and destroy the job
                    jobOut.RemoveDeviceSource(deviceSourceOut);

                    // Destroy the device source
                    deviceSourceOut.PreviewWindow = null;
                    deviceSourceOut = null;
                }
            }
        }

        // Phat camera dau vao
        private void StartStopCameraIn_Button_Click(object sender, EventArgs e)
        {
            if (!isStartIn)
            {
                if (videoSelectedIn == null)
                {
                    MessageBox.Show("No Video capture devices have been selected.\nSelect a video device and try again.", "Warning");
                    return;
                }

                jobIn = new LiveJob();

                // Create a new device source. We use the first audio and video devices on the system
                deviceSourceIn = jobIn.AddDeviceSource(videoSelectedIn, audioSelectedIn);

                // Get the properties of the device video
                SourceProperties sp = deviceSourceIn.SourcePropertiesSnapshot();

                // Resize the preview panel to match the video device resolution set
                VideoPreviewIn_Panel.Size = new Size(sp.Size.Width, sp.Size.Height);

                // Setup the output video resolution file as the preview
                jobIn.OutputFormat.VideoProfile.Size = new Size(sp.Size.Width, sp.Size.Height);

                // Sets preview window to winform panel hosted by xaml window
                deviceSourceIn.PreviewWindow = new PreviewWindow(new HandleRef(VideoPreviewIn_Panel, VideoPreviewIn_Panel.Handle));

                // Make this source the active one
                jobIn.ActivateSource(deviceSourceIn);

                StartStopCameraIn_Button.Text = "Dừng phát camera";
                isStartIn = true;

                // Start DetectorTimer
                DetectorIn_Timer.Enabled = true;
            }
            else
            {
                stopJobIn();

                StartStopCameraIn_Button.Text = "Phát camera";
                isStartIn = false;

                // Stop DetectorTimer
                DetectorIn_Timer.Enabled = false;
            }
        }

        // Phat camera dau ra
        private void StartStopCameraOut_Button_Click(object sender, EventArgs e)
        {
            if (!isStartOut)
            {
                if (videoSelectedOut == null)
                {
                    MessageBox.Show("No Video capture devices have been selected.\nSelect a video device and try again.", "Warning");
                    return;
                }

                jobOut = new LiveJob();

                // Create a new device source. We use the first audio and video devices on the system
                deviceSourceOut = jobOut.AddDeviceSource(videoSelectedOut, audioSelectedOut);

                // Get the properties of the device video
                SourceProperties sp = deviceSourceOut.SourcePropertiesSnapshot();

                // Resize the preview panel to match the video device resolution set
                VideoPreviewOut_Panel.Size = new Size(sp.Size.Width, sp.Size.Height);

                // Setup the output video resolution file as the preview
                jobOut.OutputFormat.VideoProfile.Size = new Size(sp.Size.Width, sp.Size.Height);

                // Sets preview window to winform panel hosted by xaml window
                deviceSourceOut.PreviewWindow = new PreviewWindow(new HandleRef(VideoPreviewOut_Panel, VideoPreviewOut_Panel.Handle));

                // Make this source the active one
                jobOut.ActivateSource(deviceSourceOut);

                StartStopCameraOut_Button.Text = "Dừng phát camera";
                isStartOut = true;

                // Start DetectorTimer
                DetectorOut_Timer.Enabled = true;
            }
            else
            {
                stopJobOut();

                StartStopCameraOut_Button.Text = "Phát camera";
                isStartOut = false;

                // Stop DetectorTimer
                DetectorOut_Timer.Enabled = false;
            }
        }
        
        // Kiem tra xe con trong he thong dau vao
        private void checkVehicleInSystemIn(bool isPlateDetected)
        {
            DateTime currentTime = DateTime.Now;
            if (isPlateDetected)
            {
                if (TimeVehicleIn_Start == 0)
                {
                    TimeVehicleIn_Start = currentTime.Ticks;
                }
                TimeVehicleIn_End = currentTime.Ticks;
                double secs = Math.Floor(TimeSpan.FromTicks(TimeVehicleIn_End - TimeVehicleIn_Start).TotalSeconds);

                TimeVehicleIn_Previous = TimeVehicleIn_End;
                IsStillDetectedIn = true;

                if (!UpdateAnnounceLabelIn)
                {
                    AnnounceIn_Label.Text = "Xe đang trong hệ thống trong " + secs + " giây";
                }
            }
            else
            {
                TimeVehicleIn_End = currentTime.Ticks;
                double secs = TimeSpan.FromTicks(TimeVehicleIn_End - TimeVehicleIn_Previous).TotalSeconds;
                if (secs > DELTA_TIME_NOT_DETECT_PLATE)
                {
                    TimeVehicleIn_Start = 0;
                    IsStillDetectedIn = false;
                    AnnounceFirstTimeIn = false;

                    AnnounceIn_Label.Text = "Xe đang trong hệ thống trong 0 giây";
                    TimeVehicleIn_Label.Text = "Giờ xe vào: ";
                    LicensePlateIn_Label.Text = "Chuỗi biển số xe: ";

                    UpdateAnnounceLabelIn = false;
                }
            }
        }

        // Timer phat hien xe dau vao
        private void DetectorIn_Timer_Tick(object sender, EventArgs e)
        {
            Size size = VideoPreviewIn_Panel.Size;
            Bitmap bitmap = new Bitmap(size.Width, size.Height);

            System.Drawing.Rectangle rectangle = VideoPreviewIn_Panel.Bounds;
            System.Drawing.Point pt = VideoPreviewIn_Panel.PointToScreen(new System.Drawing.Point(VideoPreviewIn_Panel.ClientRectangle.X, VideoPreviewIn_Panel.ClientRectangle.Y));

            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(pt, System.Drawing.Point.Empty, rectangle.Size);
                Image<Bgr, Byte> image = new Image<Bgr, byte>(bitmap);

                using (Image<Gray, byte> gray = image.Convert<Gray, Byte>())
                {
                    List<String> licensePlateText = ocrHandlerIn.RecognizeCharInScene(image);
                    if (licensePlateText.Count == 1)
                    {
                        checkVehicleInSystemIn(true);
                        double secs = Math.Floor(TimeSpan.FromTicks(TimeVehicleIn_End - TimeVehicleIn_Start).TotalSeconds);
                        if (!AnnounceFirstTimeIn && secs >= 5)
                        {
                            AnnounceFirstTimeIn = true;
                            announcementIn();

                            PlateInfo plateInfo = plateDatabase.GetPlate(ocrHandlerIn.DetectedPlates[0].Clone());
                            if (plateInfo == null)
                            {
                                PlateInfo defaultPlateInfo = defaultDatabase.GetPlate(ocrHandlerIn.DetectedPlates[0].Clone());
                                PlateInfo newPlate = null;
                                if (defaultPlateInfo == null)
                                {
                                    newPlate = plateDatabase.AddPlate(ocrHandlerIn.DetectedPlates[0], licensePlateText[0]);
                                }
                                else
                                {
                                    newPlate = plateDatabase.AddPlate(ocrHandlerIn.DetectedPlates[0], defaultPlateInfo.LicensePlate);
                                }

                                // Thong so xe vao bai
                                AnnounceIn_Label.Text = "MỜI XE VÀO BÃI";
                                TimeVehicleIn_Label.Text = "Giờ xe vào: " + newPlate.TimeIn.ToShortDateString() + " " + newPlate.TimeIn.ToLongTimeString();
                                LicensePlateIn_Label.Text = "Chuỗi biển số xe: " + newPlate.LicensePlate;

                                UpdateAnnounceLabelIn = true;
                            }
                            else
                            {
                                AnnounceIn_Label.Text = "XE ĐÃ CÓ TRONG BÃI!";

                                UpdateAnnounceLabelIn = true;
                            }
                        }
                    }
                    else
                    {
                        checkVehicleInSystemIn(false);
                    }
                }
            }
        }

        // Kiem tra xe con trong he thong dau vao
        private void checkVehicleInSystemOut(bool isPlateDetected)
        {
            DateTime currentTime = DateTime.Now;
            if (isPlateDetected)
            {
                if (TimeVehicleOut_Start == 0)
                {
                    TimeVehicleOut_Start = currentTime.Ticks;
                }
                TimeVehicleOut_End = currentTime.Ticks;
                double secs = Math.Floor(TimeSpan.FromTicks(TimeVehicleOut_End - TimeVehicleOut_Start).TotalSeconds);

                TimeVehicleOut_Previous = TimeVehicleOut_End;
                IsStillDetectedOut = true;

                if (!UpdateAnnounceLabelOut)
                {
                    AnnounceOut_Label.Text = "Xe đang trong hệ thống trong " + secs + " giây";
                }
            }
            else
            {
                TimeVehicleOut_End = currentTime.Ticks;
                double secs = TimeSpan.FromTicks(TimeVehicleOut_End - TimeVehicleOut_Previous).TotalSeconds;
                if (secs > DELTA_TIME_NOT_DETECT_PLATE)
                {
                    TimeVehicleOut_Start = 0;
                    IsStillDetectedOut = false;
                    AnnounceFirstTimeOut = false;

                    AnnounceOut_Label.Text = "Xe đang trong hệ thống trong 0 giây";
                    TimeVehicleOut_Label.Text = "Giờ xe vào: ";
                    LicensePlateOut_Label.Text = "Chuỗi biển số xe: ";

                    UpdateAnnounceLabelOut = false;
                }
            }
        }

        // Timer phat hien xe dau ra
        private void DetectorOut_Timer_Tick(object sender, EventArgs e)
        {
            Size size = VideoPreviewOut_Panel.Size;
            Bitmap bitmap = new Bitmap(size.Width, size.Height);

            System.Drawing.Rectangle rectangle = VideoPreviewOut_Panel.Bounds;
            System.Drawing.Point pt = VideoPreviewOut_Panel.PointToScreen(new System.Drawing.Point(VideoPreviewOut_Panel.ClientRectangle.X, VideoPreviewOut_Panel.ClientRectangle.Y));

            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(pt, System.Drawing.Point.Empty, rectangle.Size);
                Image<Bgr, Byte> image = new Image<Bgr, byte>(bitmap);

                using (Image<Gray, byte> gray = image.Convert<Gray, Byte>())
                {
                    List<String> licensePlateText = ocrHandlerOut.RecognizeCharInScene(image);
                    if (licensePlateText.Count == 1)
                    {
                        checkVehicleInSystemOut(true);
                        double secs = Math.Floor(TimeSpan.FromTicks(TimeVehicleOut_End - TimeVehicleOut_Start).TotalSeconds);
                        if (!AnnounceFirstTimeOut && secs >= 5)
                        {
                            AnnounceFirstTimeOut = true;
                            announcementOut();

                            PlateInfo plateInfo = plateDatabase.GetPlate(ocrHandlerOut.DetectedPlates[0].Clone());
                            if (plateInfo != null)
                            {
                                // Thong so xe ra bai
                                AnnounceOut_Label.Text = "MỜI XE RA" + "\n" + "Giá tiền: " + plateDatabase.GetFee(plateInfo).ToString();
                                TimeVehicleOut_Label.Text = "Giờ xe vào: " + plateInfo.TimeIn.ToShortDateString() + " " + plateInfo.TimeIn.ToLongTimeString();
                                LicensePlateOut_Label.Text = "Chuỗi biển số xe: " + plateInfo.LicensePlate;

                                plateDatabase.RemovePlate(plateInfo);
                                UpdateAnnounceLabelOut = true;
                            }
                            else
                            {
                                AnnounceOut_Label.Text = "XE KHÔNG CÓ TRONG BÃI!";
                                UpdateAnnounceLabelOut = true;
                            }
                        }
                    }
                    else
                    {
                        checkVehicleInSystemOut(false);
                    }
                }
            }
        }

        // Thong bao am thanh vao
        private void announcementIn()
        {
            if (!isPlayingSoundIn)
            {
                simpleSoundIn.PlayLooping();
                SoundIn_Timer.Enabled = true;
                isPlayingSoundIn = true;
            }
        }

        // Timer phat am thanh dau vao
        private void SoundIn_Timer_Tick(object sender, EventArgs e)
        {
            simpleSoundIn.Stop();
            SoundIn_Timer.Enabled = false;
            isPlayingSoundIn = false;
        }

        // Thong bao am thanh ra
        private void announcementOut()
        {
            if (!isPlayingSoundOut)
            {
                simpleSoundOut.PlayLooping();
                SoundOut_Timer.Enabled = true;
                isPlayingSoundOut = true;
            }
        }

        // Timer phat am thanh dau ra
        private void SoundOut_Timer_Tick(object sender, EventArgs e)
        {
            simpleSoundOut.Stop();
            SoundOut_Timer.Enabled = false;
            isPlayingSoundOut = false;
        }

        [STAThread]
        private void OpenImageIn_Button_Click(object sender, EventArgs e)
        {
            // Dung camera dau vao
            stopJobIn();

            OpenFileDialog openImageFileDialog = new OpenFileDialog();
            openImageFileDialog.RestoreDirectory = true;
            openImageFileDialog.ShowHelp = true;

            if (openImageFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Bgr drawColor = new Bgr(Color.Blue);

                try
                {
                    Image<Bgr, Byte> image = new Image<Bgr, byte>(openImageFileDialog.FileName);
                    Image<Bgr, Byte> imageClone = image.Clone();
                    VideoPreviewIn_Panel.BackgroundImage = imageClone.Resize(VideoPreviewIn_Panel.Width, VideoPreviewIn_Panel.Height, Inter.Cubic).Bitmap;
                    List<String> licensePlateText = ocrHandlerIn.RecognizeCharInScene(image);

                    if (licensePlateText.Count == 1)
                    {
                        announcementIn();

                        PlateInfo plateInfo = plateDatabase.GetPlate(ocrHandlerIn.DetectedPlates[0].Clone());
                        if (plateInfo == null)
                        {
                            PlateInfo defaultPlateInfo = defaultDatabase.GetPlate(ocrHandlerIn.DetectedPlates[0].Clone());
                            PlateInfo newPlate = null;
                            if (defaultPlateInfo == null)
                            {
                                newPlate = plateDatabase.AddPlate(ocrHandlerIn.DetectedPlates[0], licensePlateText[0]);
                            }
                            else
                            {
                                newPlate = plateDatabase.AddPlate(ocrHandlerIn.DetectedPlates[0], defaultPlateInfo.LicensePlate);
                            }

                            // Thong so xe vao bai
                            AnnounceIn_Label.Text = "MỜI XE VÀO BÃI";
                            TimeVehicleIn_Label.Text = "Giờ xe vào: " + newPlate.TimeIn.ToShortDateString() + " " + newPlate.TimeIn.ToLongTimeString();
                            LicensePlateIn_Label.Text = "Chuỗi biển số xe: " + newPlate.LicensePlate;
                        }
                        else
                        {
                            AnnounceIn_Label.Text = "XE ĐÃ CÓ TRONG BÃI!";
                        }                        
                    }

                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                }
            }
        }

        [STAThread]
        private void OpenImageOut_Button_Click(object sender, EventArgs e)
        {
            // Dung camera dau vao
            stopJobOut();

            OpenFileDialog openImageFileDialog = new OpenFileDialog();
            openImageFileDialog.RestoreDirectory = true;
            openImageFileDialog.ShowHelp = true;

            if (openImageFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Bgr drawColor = new Bgr(Color.Blue);

                try
                {
                    Image<Bgr, Byte> image = new Image<Bgr, byte>(openImageFileDialog.FileName);
                    Image<Bgr, Byte> imageClone = image.Clone();
                    VideoPreviewOut_Panel.BackgroundImage = imageClone.Resize(VideoPreviewOut_Panel.Width, VideoPreviewOut_Panel.Height, Inter.Cubic).Bitmap;
                    List<String> licensePlateText = ocrHandlerOut.RecognizeCharInScene(image);

                    if (licensePlateText.Count == 1)
                    {
                        announcementOut();

                        PlateInfo plateInfo = plateDatabase.GetPlate(ocrHandlerOut.DetectedPlates[0].Clone());
                        if (plateInfo != null)
                        {
                            // Thong so xe ra bai
                            AnnounceOut_Label.Text = "MỜI XE RA" + "\n" + "Giá tiền: " + plateDatabase.GetFee(plateInfo).ToString();
                            TimeVehicleOut_Label.Text = "Giờ xe vào: " + plateInfo.TimeIn.ToShortDateString() + " " + plateInfo.TimeIn.ToLongTimeString();
                            LicensePlateOut_Label.Text = "Chuỗi biển số xe: " + plateInfo.LicensePlate;

                            plateDatabase.RemovePlate(plateInfo);
                        }
                        else
                        {
                            AnnounceOut_Label.Text = "XE KHÔNG CÓ TRONG BÃI!";
                        }
                    }

                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message);
                }
            }
        }
        
    }
}
