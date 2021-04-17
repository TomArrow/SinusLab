using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Accord.Video.FFMPEG;

namespace SinusLab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        SinusLabCore core = new SinusLabCore();

        public MainWindow()
        {
            InitializeComponent();

            /*SuperWAV myWav = new SuperWAV(@"test24pcm2.w64",SuperWAV.WavFormat.WAVE64,48000,2,SuperWAV.AudioFormat.LPCM,24,48000);
            myWav.checkAndIncreaseDataSize(96000);
            myWav[0] = new double[2] { 0.5, 1.0 };
            myWav[1] = new double[2] { 0.0, 0.0 };
            myWav[2] = new double[2] { -0.5, -1.0};*/

            ThreadTests();
            
        }

        private async void ThreadTests()
        {
            // Gives 3 times the result "3"
            /*for(int i = 0; i < 3; i++)
            {
                Task.Run(() => {
                    Dispatcher.Invoke(()=> {
                        MessageBox.Show(i.ToString());
                    });
                });
            }*/

            /*for (int i = 0; i < 3; i++)
            {
                Task.Run(blah(i));
            }*//*
            for(int i = 0; i < 3; i++)
            {
                Task.Run(((int a)=>  {
                    return (Action)(()=>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(a.ToString());
                        });
                    });
                })(i));
            }*/
            /*
            Func<int,Action> blah = ((int a ) => {
                return (Action)(() => {
                    System.Threading.Thread.Sleep(2500);
                    Dispatcher.Invoke(() => {
                        MessageBox.Show(a.ToString());
                    });
                });
            });

            for (int i = 0; i < 3; i++)
            {
                Task.Run(blah(i));
            }*/

            // Works:
            /*
            for (int i = 0; i < 3; i++)
            {
                Task.Run(((Func<int,Action>)(((int a) => {
                    return (() => {
                        System.Threading.Thread.Sleep(2500);
                        Dispatcher.Invoke(() => {
                            MessageBox.Show(a.ToString());
                        });
                    });
                })))(i));
            }*/

        }
        /*
        public Action blah(int abc)
        {
            return () => {
                System.Threading.Thread.Sleep(2500);
                Dispatcher.Invoke(() => {
                    MessageBox.Show(abc.ToString());
                });
            };
        }*/

        LinearAccessByteImageUnsignedHusk referenceImageHusk = null;

        private void btnImageToRaw_Click(object sender, RoutedEventArgs e)
        {
            imageToRaw();
        }

        private void imageToRaw()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if(ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.2audio32fl.raw";
                if (sfd.ShowDialog() == true)
                {

                    
                    Bitmap loadedImage = new Bitmap(1, 1);

                    try
                    {
                        loadedImage = (Bitmap)Bitmap.FromFile(ofd.FileName);
                    } catch (Exception e)
                    {
                        MessageBox.Show("Error: "+e.Message);
                        return;
                    }

                    LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(loadedImage);

                    referenceImageHusk = byteImg.toHusk();
                    //btnRawToImage.IsEnabled = true;
                    //btnWavToImage.IsEnabled = true;
                    //btnWavToImageFast.IsEnabled = true;
                    buttonsWavToImage.IsEnabled = true;

                    byte[] audioData = core.RGB24ToStereo(byteImg.imageData);
                
                    File.WriteAllBytes(sfd.FileName,audioData);
                }
            }
        }

        private void btnRawToImage_Click(object sender, RoutedEventArgs e)
        {
            rawToImage();
        }

        private void rawToImage()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select raw 32 bit float audio data coresponding to the loaded reference image";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24.png";
                if (sfd.ShowDialog() == true)
                {
                    byte[] sourceData = File.ReadAllBytes(ofd.FileName);
                    byte[] output = core.StereoToRGB24(sourceData);
                    LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, referenceImageHusk);
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    imgBitmap.Save(sfd.FileName);
                }
            }

        }

        private void btnLoadReferenceImage_Click(object sender, RoutedEventArgs e)
        {
            loadReferenceImage();
        }

        private void loadReferenceImage()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if (ofd.ShowDialog() == true)
            {

                Bitmap loadedImage = new Bitmap(1, 1);

                try
                {
                    loadedImage = (Bitmap)Bitmap.FromFile(ofd.FileName);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error: " + e.Message);
                    return;
                }

                LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(loadedImage);

                referenceImageHusk = byteImg.toHusk();

                //btnRawToImage.IsEnabled = true;
                //btnWavToImage.IsEnabled = true;
                //btnWavToImageFast.IsEnabled = true;

                buttonsWavToImage.IsEnabled = true;
            }
        }

        private void btnWavToImage_Click(object sender, RoutedEventArgs e)
        {
            wavToImage();
        }
        
        private void wavToImage(bool fast = false,SinusLabCore.SinusLabFormatVersion formatVersion = SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select wav file coresponding to the loaded reference image";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.SinusLabFormatVersion.V2:
                        suffix = "V2";
                        break;
                    case SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY:
                        suffix = "V2noLum";
                        break;
                    case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24"+ suffix + ".png";
                if (sfd.ShowDialog() == true)
                {
                    byte[] srcDataByte;

                    using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                    {
                        //float[] srcData = wavFile.getEntireFileAs32BitFloat();
                        float[] srcData = wavFile.getAs32BitFloatFast(0,(UInt64)referenceImageHusk.width* (UInt64)referenceImageHusk.height-1);
                        srcDataByte = new byte[srcData.Length * 4];
                        Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                    }

                    SpeedReport mySpeedReport = new SpeedReport();
                    mySpeedReport.setPrefix("wavToImage");
                    byte[] output;
                    switch (formatVersion)
                    {
                        case SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY:
                            output = fast ? core.StereoToRGB24V2Fast(srcDataByte, false, false, 0.5, speedReport: mySpeedReport) : core.StereoToRGB24V2(srcDataByte, false); 
                            break;
                        case SinusLabCore.SinusLabFormatVersion.V2:
                            output = fast ? core.StereoToRGB24V2Fast(srcDataByte, true, false, 0.5, speedReport: mySpeedReport) : core.StereoToRGB24V2(srcDataByte);
                            break;
                        case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                        default:
                            output = fast ? core.StereoToRGB24Fast(srcDataByte) : core.StereoToRGB24(srcDataByte);
                            break;
                    }
                    mySpeedReport.setPrefix("wavToImage");
                    mySpeedReport.logEvent("Processing done.");


                    LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, referenceImageHusk);
                    Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                    imgBitmap.Save(sfd.FileName);

                    mySpeedReport.logEvent("Converted to Bitmap & saved");
                    mySpeedReport.Stop();
                    MessageBox.Show(mySpeedReport.getFormattedList());
                }
            }
        }

        private void btnImageToWav_Click(object sender, RoutedEventArgs e)
        {
            imageToWav();
        }

        private void imageToWav(SinusLabCore.SinusLabFormatVersion formatVersion = SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an RGB24 image";

            if (ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.SinusLabFormatVersion.V2:
                        suffix = "V2";
                        break;
                    case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.2audio16pcm"+ suffix + ".wav";
                if (sfd.ShowDialog() == true)
                {


                    Bitmap loadedImage = new Bitmap(1, 1);

                    try
                    {
                        loadedImage = (Bitmap)Bitmap.FromFile(ofd.FileName);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Error: " + e.Message);
                        return;
                    }

                    LinearAccessByteImageUnsignedNonVectorized byteImg = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(loadedImage);

                    referenceImageHusk = byteImg.toHusk();
                    //btnRawToImage.IsEnabled = true;
                    //btnWavToImage.IsEnabled = true;
                    //btnWavToImageFast.IsEnabled = true;
                    buttonsWavToImage.IsEnabled = true;

                    byte[] audioData;
                    switch (formatVersion)
                    {
                        case SinusLabCore.SinusLabFormatVersion.V2:
                            audioData = core.RGB24ToStereoV2(byteImg.imageData);
                            break;
                        case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                        default:
                            audioData = core.RGB24ToStereo(byteImg.imageData);
                            break;
                    }

                    float[] audioDataFloat = new float[audioData.Length / 4];

                    Buffer.BlockCopy(audioData, 0, audioDataFloat, 0, audioData.Length);

                    using (SuperWAV myWav = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE, 48000, 2, SuperWAV.AudioFormat.LPCM, 16, (UInt64)audioDataFloat.Length / 2))
                    {
                        //myWav.writeFloatArray(audioDataFloat);
                        myWav.writeFloatArrayFast(audioDataFloat);
                    }
                        
                    //File.WriteAllBytes(sfd.FileName, audioData);
                }
            }
        }

        private void btnVideoToW64_Click(object sender, RoutedEventArgs e)
        {
            videoToW64();
        }

        Accord.Math.Rational videoFrameRate = 24;
        LinearAccessByteImageUnsignedHusk videoReferenceFrame = null;

        private void videoToW64(SinusLabCore.SinusLabFormatVersion formatVersion = SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a video";

            if (ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.SinusLabFormatVersion.V2:
                        suffix = "V2";
                        break;
                    case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.2audio16pcm"+ suffix + ".w64";
                if (sfd.ShowDialog() == true)
                {
                    long frameCount;
#if !DEBUG
                    try
                    {
#endif

                        VideoFileReader reader = new VideoFileReader();
                        reader.Open(ofd.FileName);

                        /*
                        Console.WriteLine("width:  " + reader.Width);
                        Console.WriteLine("height: " + reader.Height);
                        Console.WriteLine("fps:    " + reader.FrameRate);
                        Console.WriteLine("codec:  " + reader.CodecName);
                        Console.WriteLine("length:  " + reader.FrameCount);
                        */

                        frameCount = reader.FrameCount;

                        UInt64 currentFrame = 0;

                        //loadedVideo = new LinearAccessByteImage[frameCount];
                        videoFrameRate = reader.FrameRate;

                        //videoReferenceFrame = null;
                        //btnW64ToVideo.IsEnabled = false;
                        //btnW64ToVideoFast.IsEnabled = false;
                        //btnW64ToVideoFastAsync.IsEnabled = false;
                        buttonsAudioToVideo.IsEnabled = false;

                        using (SuperWAV myWav = new SuperWAV(sfd.FileName, SuperWAV.WavFormat.WAVE64, 48000, 2, SuperWAV.AudioFormat.LPCM, 16))
                        {


                            while (true)
                            {
                                using (Bitmap videoFrame = reader.ReadVideoFrame())
                                {
                                    if (videoFrame == null)
                                        break;

                                    LinearAccessByteImageUnsignedNonVectorized frameData = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(videoFrame);

                                    if(videoReferenceFrame == null)
                                    {

                                        videoReferenceFrame = frameData.toHusk();
                                        //btnW64ToVideo.IsEnabled = true;
                                        //btnW64ToVideoFast.IsEnabled = true;
                                        //btnW64ToVideoFastAsync.IsEnabled = true;
                                        buttonsAudioToVideo.IsEnabled = true;
                                    }

                                    byte[] audioData = new byte[1];
                                    switch (formatVersion)
                                    {
                                        case SinusLabCore.SinusLabFormatVersion.V2:
                                            audioData = core.RGB24ToStereoV2(frameData.imageData);
                                            break;
                                        case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                                        default:
                                            audioData = core.RGB24ToStereo(frameData.imageData);
                                            break;
                                    }

                                    float[] audioDataFloat = new float[audioData.Length / 4];

                                    Buffer.BlockCopy(audioData, 0, audioDataFloat, 0, audioData.Length);
                                    //myWav.writeFloatArray(audioDataFloat, currentFrame*(UInt64)audioDataFloat.Length/2);
                                    myWav.writeFloatArrayFast(audioDataFloat, currentFrame*(UInt64)audioDataFloat.Length/2);

                                    /*if (currentFrame % 1000 == 0)
                                    {
                                        progress.Report("Loading video: " + currentFrame + "/" + frameCount + " frames");
                                    }*/


                                    currentFrame++;
                                    // process the frame here

                                }
                            }
                        }

                        reader.Close();

                        // If the video delivered less frames than it promised (can happen for whatever reason) then we chip off the last parts of the array
                        /*if (currentFrame < frameCount)
                        {
                            tooFewFramesDelivered = (int)frameCount - currentFrame;
                            Array.Resize<LinearAccessByteImage>(ref loadedVideo, currentFrame);
                        }*/
#if !DEBUG
                    }
                    catch (Exception e)
                    {
                        //failed = true;
                        MessageBox.Show(e.Message);
                        videoReferenceFrame = null;
                        btnW64ToVideo.IsEnabled = false;
                        btnW64ToVideoFast.IsEnabled = false;
                        btnW64ToVideoFastAsync.IsEnabled = false;
                    }
#endif
                }
            }
        }

        private void btnLoadReferenceVideo_Click(object sender, RoutedEventArgs e)
        {
            loadReferenceVideo();
        }

        private void loadReferenceVideo()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a video";

            if (ofd.ShowDialog() == true)
            {

                long frameCount;
                try
                {

                    VideoFileReader reader = new VideoFileReader();
                    reader.Open(ofd.FileName);

                    /*
                    Console.WriteLine("width:  " + reader.Width);
                    Console.WriteLine("height: " + reader.Height);
                    Console.WriteLine("fps:    " + reader.FrameRate);
                    Console.WriteLine("codec:  " + reader.CodecName);
                    Console.WriteLine("length:  " + reader.FrameCount);
                    */

                    frameCount = reader.FrameCount;


                    //loadedVideo = new LinearAccessByteImage[frameCount];
                    videoFrameRate = reader.FrameRate;


                    using (Bitmap videoFrame = reader.ReadVideoFrame())
                    {
                        if (videoFrame == null)
                            throw new Exception("No video frame in video.");

                        LinearAccessByteImageUnsignedNonVectorized frameData = Helpers.BitmapToLinearAccessByteArraUnsignedNonVectorizedy(videoFrame);


                        videoReferenceFrame = frameData.toHusk();
                        //btnW64ToVideo.IsEnabled = true;
                        //btnW64ToVideoFast.IsEnabled = true;
                        //btnW64ToVideoFastAsync.IsEnabled = true;
                        buttonsAudioToVideo.IsEnabled = true;

                        /*if (currentFrame % 1000 == 0)
                        {
                            progress.Report("Loading video: " + currentFrame + "/" + frameCount + " frames");
                        }*/


                        // process the frame here

                    }

                    reader.Close();

                    // If the video delivered less frames than it promised (can happen for whatever reason) then we chip off the last parts of the array
                    /*if (currentFrame < frameCount)
                    {
                        tooFewFramesDelivered = (int)frameCount - currentFrame;
                        Array.Resize<LinearAccessByteImage>(ref loadedVideo, currentFrame);
                    }*/

                }
                catch (Exception e)
                {
                    //failed = true;
                    MessageBox.Show(e.Message);
                    videoReferenceFrame = null;
                    //btnW64ToVideo.IsEnabled = false;
                    //btnW64ToVideoFast.IsEnabled = false;
                    //btnW64ToVideoFastAsync.IsEnabled = false;
                    buttonsAudioToVideo.IsEnabled = false;
                }
                
            }
        }

        private void btnW64ToVideo_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideo();
        }

        private void w64ToVideo(bool fast = false)
        {
            if(videoReferenceFrame == null)
            {
                MessageBox.Show("No reference video loaded.");
                return;
            }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select w64 file coresponding to the loaded reference video";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24.mkv";
                if (sfd.ShowDialog() == true)
                {

                    UInt64 frameCount;
#if !DEBUG
                    try
                    {
#endif
                        using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                        {
                            //float[] srcData = wavFile.getEntireFileAs32BitFloat();
                            //srcDataByte = new byte[srcData.Length * 4];
                            //Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                        
                            VideoFileWriter writer = new VideoFileWriter();
                            writer.Open(sfd.FileName, videoReferenceFrame.width, videoReferenceFrame.height, videoFrameRate, VideoCodec.FFV1);

                            /*
                            Console.WriteLine("width:  " + reader.Width);
                            Console.WriteLine("height: " + reader.Height);
                            Console.WriteLine("fps:    " + reader.FrameRate);
                            Console.WriteLine("codec:  " + reader.CodecName);
                            Console.WriteLine("length:  " + reader.FrameCount);
                            */

                            UInt64 imageLength = (UInt64)videoReferenceFrame.width * (UInt64)videoReferenceFrame.height;
                            frameCount = wavFile.DataLengthInTicks/(imageLength);

                            byte[] srcDataByte;
                            byte[] output;
                            Bitmap imgBitmap; 
                            float[] srcData;
                            LinearAccessByteImageUnsignedNonVectorized image;

                            //int currentFrame = 0;

                            //int frameToWrite = 0;
                            for (UInt64 i = 0; i < frameCount; i++)
                            {
                                //srcData = wavFile.getAs32BitFloat(imageLength*i, imageLength*(i+1)-1); 
                                srcData = wavFile.getAs32BitFloatFast(imageLength*i, imageLength*(i+1)-1); 
                                srcDataByte = new byte[srcData.Length * 4]; 
                                Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                                output = fast ? core.StereoToRGB24Fast(srcDataByte) : core.StereoToRGB24(srcDataByte);
                                image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame);
                                imgBitmap = Helpers.ByteArrayToBitmap(image);
                                writer.WriteVideoFrame(imgBitmap,(uint)i);
                                writer.Flush();
                                /*if (currentFrame % 1000 == 0)
                                {
                                    progress.Report("Saving video: " + i + "/" + frameCount + " frames");
                                }*/
                            }

                            writer.Close();
                        }
#if !DEBUG
                    }
                    catch (Exception e)
                    {
                       MessageBox.Show(e.Message);
                    }
#endif
                   
                }
            }
        }
        
        private async void w64ToVideoMultiThreadedAsync(bool fast = false,SinusLabCore.SinusLabFormatVersion formatVersion = SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY)
        {
            if(videoReferenceFrame == null)
            {
                MessageBox.Show("No reference video loaded.");
                return;
            }

            int threads = Environment.ProcessorCount;
            int bufferSize = threads * 2;
            int mainLoopTimeout = 200;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select w64 file coresponding to the loaded reference video";

            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();

                string suffix = "";
                switch (formatVersion)
                {
                    case SinusLabCore.SinusLabFormatVersion.V2:
                        suffix = "V2";
                        break;
                    case SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY:
                        suffix = "V2noLum";
                        break;
                    case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                    default:
                        suffix = "V1";
                        break;
                }

                sfd.FileName = ofd.FileName + ".sinuslab.32flaudiotorgb24"+suffix+".mkv";
                if (sfd.ShowDialog() == true)
                {

                    ConcurrentDictionary<int, Bitmap> writeBuffer = new ConcurrentDictionary<int, Bitmap>();
                    bool[] imagesProcessed = new bool[1];
                    bool[] imagesProcessing = new bool[1];

                    Func<float[],int,bool, Action> convertWorker = ((float[] audioData,int index,bool fastMode) => {
                        return (Action)(() => {
                            // Actual code:
                            byte[] srcDataByte = new byte[audioData.Length * 4];
                            Buffer.BlockCopy(audioData, 0, srcDataByte, 0, srcDataByte.Length);
                            audioData = null;

                            byte[] output;
                            switch (formatVersion)
                            {
                                case SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY:
                                    output = fast ? core.StereoToRGB24V2Fast(srcDataByte, false) : core.StereoToRGB24V2(srcDataByte, false);
                                    break;
                                case SinusLabCore.SinusLabFormatVersion.V2:
                                    output = fast ? core.StereoToRGB24V2Fast(srcDataByte) : core.StereoToRGB24V2(srcDataByte);
                                    break;
                                case SinusLabCore.SinusLabFormatVersion.DEFAULT_LEGACY:
                                default:
                                    output = fast ? core.StereoToRGB24Fast(srcDataByte) : core.StereoToRGB24(srcDataByte);
                                    break;
                            }
                            //byte[] output = fast ? core.StereoToRGB24Fast(srcDataByte) : core.StereoToRGB24(srcDataByte);


                            srcDataByte = null;
                            LinearAccessByteImageUnsignedNonVectorized image = new LinearAccessByteImageUnsignedNonVectorized(output, videoReferenceFrame);
                            Bitmap imgBitmap = Helpers.ByteArrayToBitmap(image);
                            image = null;
                            bool addingSucceeded = false;
                            while (!addingSucceeded)
                            {
                                addingSucceeded = writeBuffer.TryAdd(index, imgBitmap); 
                                System.Threading.Thread.Sleep(mainLoopTimeout);
                            }
                            //writeBuffer.Add(index, imgBitmap);
                            imgBitmap = null;
                            imagesProcessing[index] = false;
                            imagesProcessed[index] = true;
                        });
                    });

                    UInt64 frameCount;
#if !DEBUG
                    try
                    {
#endif
                        using (SuperWAV wavFile = new SuperWAV(ofd.FileName))
                        {
                            //float[] srcData = wavFile.getEntireFileAs32BitFloat();
                            //srcDataByte = new byte[srcData.Length * 4];
                            //Buffer.BlockCopy(srcData, 0, srcDataByte, 0, srcDataByte.Length);
                        
                            VideoFileWriter writer = new VideoFileWriter();
                            writer.Open(sfd.FileName, videoReferenceFrame.width, videoReferenceFrame.height, videoFrameRate, VideoCodec.FFV1);

                            /*
                            Console.WriteLine("width:  " + reader.Width);
                            Console.WriteLine("height: " + reader.Height);
                            Console.WriteLine("fps:    " + reader.FrameRate);
                            Console.WriteLine("codec:  " + reader.CodecName);
                            Console.WriteLine("length:  " + reader.FrameCount);
                            */

                            UInt64 imageLength = (UInt64)videoReferenceFrame.width * (UInt64)videoReferenceFrame.height;
                            frameCount = wavFile.DataLengthInTicks/(imageLength);

                            //byte[] srcDataByte;
                            //byte[] output;
                            //Bitmap imgBitmap; 
                            float[] srcData;
                            //LinearAccessByteImageUnsignedNonVectorized image;

                            //int currentFrame = 0;

                            //int frameToWrite = 0;
                            UInt64 imagesLeft = frameCount;
                            imagesProcessed = new bool[frameCount];
                            imagesProcessing = new bool[frameCount];

                            Int64 lastFrameWrittenIntoVideo = -1;

                            Bitmap tmpBitmap;

                            while (imagesLeft > 0)
                            {
                                int numberOfThreadsCurrentlyProcessing = 0;
                                foreach(bool imageProcessing in imagesProcessing)
                                {
                                    if (imageProcessing)
                                    {
                                        numberOfThreadsCurrentlyProcessing++;
                                    }
                                }

                                int writeBufferFillStatusWorstCase = writeBuffer.Count+numberOfThreadsCurrentlyProcessing;

                                int numberOfThreadsToSpawn = Math.Min(threads - numberOfThreadsCurrentlyProcessing,bufferSize - writeBufferFillStatusWorstCase);

                                for(int i = 0; i < numberOfThreadsToSpawn; i++)
                                {
                                    for(UInt64 a = 0; a < frameCount; a++)
                                    {
                                        if (!imagesProcessed[a] && !imagesProcessing[a]) {
                                            imagesProcessing[a] = true;

                                            srcData = wavFile.getAs32BitFloatFast(imageLength * a, imageLength * (a + 1) - 1);

                                            Task.Run(convertWorker(srcData,(int)a,fast));
                                            srcData = null;
                                            break;
                                        }
                                    }
                                }

                                // Do actual video writing
                                bool framesAvailable = true;
                                while (framesAvailable && lastFrameWrittenIntoVideo < (Int64)(frameCount-1))
                                {
                                    UInt64 nextFrameToBeWritten = (UInt64)(lastFrameWrittenIntoVideo + 1);
                                    if (imagesProcessed[nextFrameToBeWritten] && writeBuffer.ContainsKey((int)nextFrameToBeWritten))
                                    {
                                        bool readingSucceeded = writeBuffer.TryRemove((int)nextFrameToBeWritten,out tmpBitmap); // We're using remove here because that returns a bitmap whether we want to or not anyway...
                                        if (!readingSucceeded)
                                        {
                                            framesAvailable = false;
                                        } else
                                        {
                                            writer.WriteVideoFrame(tmpBitmap, (uint)nextFrameToBeWritten);
                                            tmpBitmap.Dispose();
                                            tmpBitmap = null;
                                            lastFrameWrittenIntoVideo++;
                                            imagesLeft--;
                                            writer.Flush();
                                        }

                                    } else
                                    {
                                        framesAvailable = false;
                                    }
                                }

                                System.Threading.Thread.Sleep(mainLoopTimeout);
                            }
                            /*for (UInt64 i = 0; i < frameCount; i++)
                            {
                                //srcData = wavFile.getAs32BitFloat(imageLength*i, imageLength*(i+1)-1); 
                                srcData = wavFile.getAs32BitFloatFast(imageLength*i, imageLength*(i+1)-1); 
                                


                                writer.WriteVideoFrame(imgBitmap,(uint)i);
                                writer.Flush();
                                /*if (currentFrame % 1000 == 0)
                                {
                                    progress.Report("Saving video: " + i + "/" + frameCount + " frames");
                                }*/
                            //}*/

                            writer.Close();
                        }
#if !DEBUG
                    }
                    catch (Exception e)
                    {
                       MessageBox.Show(e.Message);
                    }
#endif
                   
                }
            }
        }

        private void btnW64ToVideoFast_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideo(true);
        }

        private void btnWavToImageFast_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true);
        }

        private void btnW64ToVideoFastAsync_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true);
        }

        private void btnImageToWavV2_Click(object sender, RoutedEventArgs e)
        {
            imageToWav(SinusLabCore.SinusLabFormatVersion.V2);
        }

        private void btnWavToImageV2_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(false,SinusLabCore.SinusLabFormatVersion.V2);
        }

        private void btnWavToImageV2NoLFLuma_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(false, SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY);
        }

        private void btnWavToImageV2Fast_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true, SinusLabCore.SinusLabFormatVersion.V2);
        }

        private void btnWavToImageV2FastNoFLLuma_Click(object sender, RoutedEventArgs e)
        {
            wavToImage(true, SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY);
        }

        private void btnVideoToW64V2_Click(object sender, RoutedEventArgs e)
        {

            videoToW64(SinusLabCore.SinusLabFormatVersion.V2);
        }

        private void btnW64ToVideoFastAsyncV2_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true,SinusLabCore.SinusLabFormatVersion.V2);
        }

        private void btnW64ToVideoFastAsyncV2NoLuma_Click(object sender, RoutedEventArgs e)
        {
            w64ToVideoMultiThreadedAsync(true, SinusLabCore.SinusLabFormatVersion.V2_NOLUMA_DECODE_ONLY);
        }
    }
}
