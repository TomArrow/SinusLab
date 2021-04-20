using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinusLab
{

    class SpeedReport
    {

        struct MeasuredEvent
        {
            public long timeSpent;
            public string description;
        }

        private string prefix = "";

        long totalTimeSpent = 0;

        private List<MeasuredEvent> eventList = new List<MeasuredEvent>();

        private Stopwatch myWatch;
        public SpeedReport()
        {
            myWatch = Stopwatch.StartNew();
        }
        public void logEvent(string description)
        {
            MeasuredEvent myEvent = new MeasuredEvent();
            myEvent.timeSpent = myWatch.ElapsedMilliseconds;
            totalTimeSpent += myEvent.timeSpent;
            myEvent.description = prefix + ": "+description;
            eventList.Add(myEvent);
            myWatch.Restart();
        }

        public void Reset()
        {
            eventList = new List<MeasuredEvent>();
            totalTimeSpent = 0;
            prefix = "";
            myWatch.Restart();
        }

        public void setPrefix(string prefixA = "")
        {
            prefix = prefixA;
        }

        public string getFormattedList()
        {
            StringBuilder sb = new StringBuilder();
            foreach(MeasuredEvent myEvent in eventList)
            {
                sb.AppendLine(myEvent.description + ": "+ myEvent.timeSpent + "ms");
            }
            sb.AppendLine();
            sb.AppendLine("Total time: "+totalTimeSpent+" ms");
            return sb.ToString();
        }

        public void Stop()
        {
            myWatch.Stop();
        }

        ~SpeedReport()
        {
            myWatch.Stop();
        }
    }

    class SinusLabCore
    {

        public struct DecodeResult
        {
            public byte[] imageData;
            public float[] audioData;
        }

        public int samplerate = 48000;
        public double maxAmplitude = Math.Sqrt(2.0) / 2.0;
        public int lowerFrequency = 500;
        public int lowerFrequencyV2 = 3000; // In V2, we encode a low frequency luma offset signal into a 500Hz signal in the chroma so as to compensate for the kind of "centering" happening when converting to the analogue domain or applying many kinds of effects. We sacrifice a bit of hue precision for greater luma precision
        public int lowerFrequencyV3 = 3000; // Kept same as in V2
        public int upperFrequency = 20000;
        public int upperFrequencyV2 = 20000;
        public int upperFrequencyV3 = 18000;
        double lumaInChromaFrequencyV2 = 500.0;
        double waveLengthSmoothRadiusMultiplierEncodeV2 = 0.5;
        double waveLengthSmoothRadiusMultiplierDecodeV2 = 4;
        double audioSubcarrierFrequencyV3 = 22000.0; // formerly 23500 - created modulation problems at 48khz

        public double AudioSubcarrierFrequencyV3
        {
            get
            {
                return audioSubcarrierFrequencyV3;
            }
        }

        public double decodeGainMultiplier = 1.0;
        public double decodeChromaGainMultiplier = 1.0;
        public double decodeLumaGainMultiplier = 1.0;
        public double decodeLFLumaGainMultiplier = 1.0;
        public double decodeAudioSubcarrierGainMultiplier = 1.0;


        public enum WindowFunction
        {
            Rectangular,
            Hanning,
            Hamming,
            Blackman,
            BlackmanExact,
            BlackmanHarris,
            FlatTop,
            Bartlett,
            Cosine
        }

        public WindowFunction windowFunction = WindowFunction.Hanning;

        /*
         * Ideas for V3:
         * Reduce frequency range for color further, to a max of 18k
         * Then amplitude-modulate audio into a 23.5 kHz signal, should give at least a response of, what, 11 kHz? Or is that nyquist and its actually just about 5-6? Either would be cool tho!
         */

        public enum LowFrequencyLumaCompensationMode
        {
            MULTIPLY,
            OFFSET
        }

        public int windowSize = 32;
        bool windowSizeIsRelativeTo48k = true; // Because at higher sampling rates, we need a higher window size to capture low frequencies...

        public enum FormatVersion {
            DEFAULT_LEGACY = 0,
            V2 = 1,
            V2_NOLUMA_DECODE_ONLY = 2,
            V3
        };

        public byte[] RGB24ToStereo(byte[] sourceData)
        {

            double frequencyRange = upperFrequency - lowerFrequency;

            //double distanceRatio = 2*(double)frequencyToGenerate/ (double)samplerate; 
            double[] output = new double[sourceData.Length / 3];
            double[] outputL = new double[sourceData.Length / 3];

            double lastPhase = 0;
            double frequencyHere;
            double phaseLengthHere;
            double phaseAdvancementHere;
            double phaseHere;
            double hueTo0to1Range;
            //output[0] = 0;
            Vector3 tmpV;
            for (int i = 0; i < output.Length; i++)
            {
                tmpV.X = (float)((double)sourceData[i * 3]); // R
                tmpV.Y = (float)((double)sourceData[i * 3 + 1]);  // G
                tmpV.Z = (float)((double)sourceData[i * 3 + 2]); // B

                tmpV = Helpers.sRGBToCIELChab(tmpV); //cielchab = Luma, chroma, hue. hue is frequency, chroma is amplitude

                hueTo0to1Range = ((double)tmpV.Z + Math.PI) / Math.PI / 2;
                frequencyHere = lowerFrequency + frequencyRange * hueTo0to1Range; // Hue
                phaseLengthHere = ((double)samplerate) / frequencyHere / 2;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                output[i] = (double)tmpV.Y / 100.0 * maxAmplitude * Math.Sin(phaseHere * Math.PI); // tmpV.Y is amplitude (chrominance/saturation)
                outputL[i] = ((double)tmpV.X - 50) * 2 / 100.0 * maxAmplitude; // tmpV.Y is amplitude (chrominance/saturation)
                lastPhase = phaseHere % 2;
            }

            byte[] outputBytes = new byte[output.Length * 4 * 2];
            byte[] tmp;
            for (int i = 0; i < output.Length; i++)
            {
                tmp = BitConverter.GetBytes((float)outputL[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4 * 2, 4);
                tmp = BitConverter.GetBytes((float)output[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4 * 2 + 4, 4);
            }

            return outputBytes;

        }

        struct AverageHelper
        {
            public double totalValue;
            public double multiplier;
        }

        // Old-school signature if u dont care about audio continuity.
        public byte[] RGB24ToStereoV2(byte[] sourceData, bool isV3 = false, float[] inputAudioV3 = null)
        {
            double phaseOffset = 0;
            return RGB24ToStereoV2(sourceData, ref phaseOffset, isV3, inputAudioV3);
        }

        // Note: input audio is expected to contain half the audio from the previous and half from the following frame! If not available, provide zeros there. This is required due to possible lowpass filtering/blurring and if ommitted would be likely to cause periodic discontinuities/spikes.
        public byte[] RGB24ToStereoV2(byte[] sourceData, ref double audioEncodingPhaseOffset, bool isV3 = false, float[] inputAudioV3 = null)
        {

            int pixelCount = sourceData.Length / 3;
            double[] preparedAudioData = new double[pixelCount];

            double frequencyRange = upperFrequencyV2 - lowerFrequencyV2;
            int lowerFrequencyHere = lowerFrequencyV2;
            if (isV3)
            {
                frequencyRange = upperFrequencyV3 - lowerFrequencyV3;
                lowerFrequencyHere = lowerFrequencyV3;

                // V3 can be encoded without audio, but what's the point? That was the whole reason for its creation.
                if (inputAudioV3 != null) {
                    int audioSampleCount = inputAudioV3.Length / 2; // Divide by 2 because we're getting half of the previous and next frame too.
                    double audioCarrierPhaseLength = ((double)samplerate) / audioSubcarrierFrequencyV3 / 2;

                    double audioSamplesPerPixel = (double)audioSampleCount / (double)pixelCount;
                    double audioCarrierPhaseLengthInAudioSamples = audioCarrierPhaseLength * audioSamplesPerPixel;

                    double[] smoothedSamples = boxBlur(inputAudioV3, (uint)(audioCarrierPhaseLengthInAudioSamples * 4.0)); // I'm really just guessing here about how much I need to multiply the phase length with for the blur. It's not even a proper low pass filter or anything. But hey, this isn't science, this is games.

                    int readingOffset = audioSampleCount / 2; // Remember, half frame before and half after.

                    // We distinguish 3 cases now: there is more audio samples than pixels or less or equal.
                    //if (audioSampleCount == pixelCount)
                    //{
                    //   Array.Copy(smoothedSamples, readingOffset, preparedAudioData, 0, preparedAudioData.Length); // Cool. Nothing much to do here.
                    //} else { 
                    // Otherwise ... just pick the closest nearby value for now... nearest neighbor. 
                    for (uint i = 0; i < preparedAudioData.Length; i++)
                    {
                        preparedAudioData[i] = Math.Min(1.0, Math.Max(0.0, (smoothedSamples[readingOffset + ((int)Math.Round((double)i * audioSamplesPerPixel))] / 2.0 + 0.5))); // We're limiting the audio signal to -1 to 1 too.
                    }
                    //}
                }
            }

            //double distanceRatio = 2*(double)frequencyToGenerate/ (double)samplerate; 
            double[] output = new double[pixelCount];
            double[] outputL = new double[pixelCount];
            double[] LforSmooth = new double[pixelCount];

            double lastPhase = 0;
            double frequencyHere;
            double phaseLengthHere;
            double phaseAdvancementHere;
            double phaseHere;
            double hueTo0to1Range;
            //output[0] = 0;
            Vector3 tmpV;
            for (int i = 0; i < output.Length; i++)
            {
                tmpV.X = (float)((double)sourceData[i * 3]); // R
                tmpV.Y = (float)((double)sourceData[i * 3 + 1]);  // G
                tmpV.Z = (float)((double)sourceData[i * 3 + 2]); // B

                tmpV = Helpers.sRGBToCIELChab(tmpV); //cielchab = Luma, chroma, hue. hue is frequency, chroma is amplitude

                hueTo0to1Range = ((double)tmpV.Z + Math.PI) / Math.PI / 2;
                frequencyHere = lowerFrequencyHere + frequencyRange * hueTo0to1Range; // Hue
                phaseLengthHere = ((double)samplerate) / frequencyHere / 2;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                output[i] = (double)tmpV.Y / 100.0 * (maxAmplitude / 2) * Math.Sin(phaseHere * Math.PI); // tmpV.Y is amplitude (chrominance/saturation)
                outputL[i] = ((double)tmpV.X - 50) * 2 / 100.0 * maxAmplitude; // tmpV.Y is amplitude (chrominance/saturation). we divide amplitude by 2 here because we're also adding the amplitude modulated 30hz luma offset and dont want clipping
                LforSmooth[i] = (double)tmpV.X / 100.0; // tmpV.Y is amplitude (chrominance/saturation). we divide amplitude by 2 here because we're also adding the amplitude modulated 30hz luma offset and dont want clipping
                lastPhase = phaseHere % 2;
            }

            // Now encode a low frequency (about 10Hz) luma offset into the chroma.
            // For that, we first smooth the luma to represent thatt
            // Done with a simple box blue
            double[] smoothedLuma = new double[LforSmooth.Length];
            double encodingFrequencyWavelength = samplerate / lumaInChromaFrequencyV2;
            int averageSampleRadius = (int)Math.Ceiling(encodingFrequencyWavelength * waveLengthSmoothRadiusMultiplierEncodeV2);
            AverageHelper averageHelper = new AverageHelper();
            for (int i = 0; i < LforSmooth.Length; i++)
            {
                if (i == 0)
                {
                    for (int ii = 0; ii <= averageSampleRadius; ii++)
                    {
                        if ((i + ii) >= LforSmooth.Length)
                        {
                            break;
                        }
                        averageHelper.totalValue += LforSmooth[i + ii];
                        averageHelper.multiplier += 1;
                    }
                } else {
                    if (i > averageSampleRadius)
                    {
                        averageHelper.totalValue -= LforSmooth[i - averageSampleRadius - 1];
                        averageHelper.multiplier -= 1;
                    }
                    if ((i + averageSampleRadius) < LforSmooth.Length)
                    {
                        averageHelper.totalValue += LforSmooth[i + averageSampleRadius];
                        averageHelper.multiplier += 1;
                    }
                }
                smoothedLuma[i] = averageHelper.totalValue / averageHelper.multiplier;
            }

            // Now encode the smoother Luma via amplitude modulation into a 500 Hz signal added to chroma:
            double phaseLength = ((double)samplerate) / lumaInChromaFrequencyV2 / 2;
            double phaseAdvancement = 1 / phaseLength;
            for (int i = 0; i < smoothedLuma.Length; i++)
            {
                output[i] += (maxAmplitude / 2) * smoothedLuma[i] * Math.Sin(phaseAdvancement * i * Math.PI);
            }

            // For V3, encode a 23500 Hz signal into the image for audio
            // Only do this if samplerate bigger than 2x23500 Hz, otherwise Nyquist complains.
            if (isV3 && samplerate > (audioSubcarrierFrequencyV3 * 2) && inputAudioV3 != null)
            {
                phaseLength = ((double)samplerate) / audioSubcarrierFrequencyV3 / 2;
                phaseAdvancement = 1 / phaseLength;
                for (int i = 0; i < preparedAudioData.Length; i++)
                {
                    output[i] += (maxAmplitude / 2) * preparedAudioData[i] * Math.Sin((audioEncodingPhaseOffset + (double)phaseAdvancement * (double)i) * Math.PI);
                }
                audioEncodingPhaseOffset += (double)(preparedAudioData.Length) * phaseAdvancement;
                audioEncodingPhaseOffset %= 2;
            }

            byte[] outputBytes = new byte[output.Length * 4 * 2];
            byte[] tmp;
            for (int i = 0; i < output.Length; i++)
            {
                tmp = BitConverter.GetBytes((float)outputL[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4 * 2, 4);
                tmp = BitConverter.GetBytes((float)output[i]);
                Array.Copy(tmp, 0, outputBytes, i * 4 * 2 + 4, 4);
            }

            return outputBytes;

        }


        public byte[] StereoToRGB24(byte[] sourceData, double decodingSampleRate)
        {
            double frequencyRange = upperFrequency - lowerFrequency;

            int windowSizeHere = windowSizeIsRelativeTo48k ? (int)Math.Pow(2, Math.Ceiling(Math.Log((double)windowSize * (double)decodingSampleRate / 48000.0, 2.0))) : windowSize;

            double[] decode = new double[sourceData.Length / 8 + windowSizeHere]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            for (int i = 0; i < decodeL.Length; i++)
            {
                decodeL[i] = decodeLumaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                decode[i + windowSizeHere / 2/*+ windowSize*/] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
            }

            double[] audioPart = new double[windowSizeHere];

            double[] freqs;

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(decodingSampleRate, fftMagnitude.Length);

            double tmpMaxIntensity = 0;
            int tmpMaxIntensityIndex = 0;

            double peakFrequencyHere = 0;
            double hue = 0;

            Vector3 tmpV;

            // decode c,h components
            for (int i = 0; i < decodeL.Length; i++)
            {
                Array.Copy(decode, i, audioPart, 0, windowSizeHere);
                //double[] window = FftSharp.Window.Hanning(audioPart.Length);
                double[] window = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPart.Length);
                FftSharp.Window.ApplyInPlace(window, audioPart);
                fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);

                tmpMaxIntensity = 0;
                // find biggest frequency
                for (int b = 0; b < freqs.Length; b++)
                {
                    if (fftMagnitude[b] > tmpMaxIntensity)
                    {
                        tmpMaxIntensity = fftMagnitude[b];
                        tmpMaxIntensityIndex = b;
                    }
                }


                if (tmpMaxIntensityIndex == 0)
                {
                    peakFrequencyHere = (freqs[0] * fftMagnitude[0] + freqs[1] * fftMagnitude[1]) / (fftMagnitude[0] + fftMagnitude[1]);
                }
                else if (tmpMaxIntensityIndex == freqs.Length - 1)
                {
                    peakFrequencyHere = (freqs[freqs.Length - 1] * fftMagnitude[freqs.Length - 1] + freqs[freqs.Length - 2] * fftMagnitude[freqs.Length - 2]) / (fftMagnitude[freqs.Length - 1] + fftMagnitude[freqs.Length - 2]);
                }
                else
                {
                    peakFrequencyHere = (freqs[tmpMaxIntensityIndex - 1] * fftMagnitude[tmpMaxIntensityIndex - 1] + freqs[tmpMaxIntensityIndex] * fftMagnitude[tmpMaxIntensityIndex] + freqs[tmpMaxIntensityIndex + 1] * fftMagnitude[tmpMaxIntensityIndex + 1]) / (fftMagnitude[tmpMaxIntensityIndex - 1] + fftMagnitude[tmpMaxIntensityIndex] + fftMagnitude[tmpMaxIntensityIndex + 1]);

                }

                //hue = (((peakFrequencyHere-lowerFrequency)/frequencyRange)*Math.PI)-Math.PI/2;
                hue = (((peakFrequencyHere - lowerFrequency) / frequencyRange) * Math.PI * 2) - Math.PI;

                if (double.IsNaN(hue)) { hue = 0; } // Necessary for really dark/black areas, otherwise they just turn the entire image black because all other calculation fails as a result.

                tmpV.X = (float)(decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                //tmpV.Y = (float)Math.Sqrt(tmpMaxIntensity)*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/0.707f*100f; //experimental * 4, normally doesnt beong there.
                tmpV.Y = (float)tmpMaxIntensity / (0.637f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.707f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.637f * 0.637f) * 100f; //experimental * 4, normally doesnt beong there.
                tmpV.Z = (float)hue;

                tmpV = Helpers.CIELChabTosRGB(tmpV);

                output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
            }

            return output;
        }

        [Obsolete("Use fast version instead, this one hasn't been updated in a while.")]
        public byte[] StereoToRGB24V2(byte[] sourceData, double decodingSampleRate, bool decodeLFLuma = true, bool superHighQuality = false)
        {
            double frequencyRange = upperFrequencyV2 - lowerFrequencyV2;

            int windowSizeHere = windowSizeIsRelativeTo48k ? (int)Math.Pow(2, Math.Ceiling(Math.Log((double)windowSize * (double)decodingSampleRate / 48000.0, 2.0))) : windowSize;

            double minimumWindowSizeRequiredForLFLumaCarrierFrequency = (1 / lumaInChromaFrequencyV2 * decodingSampleRate);
            int windowSizeForLFLuma = (int)Math.Pow(2, Math.Ceiling(Math.Log(minimumWindowSizeRequiredForLFLumaCarrierFrequency, 2)));

            double[] decode = new double[sourceData.Length / 8 + windowSizeHere]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeForLFLuma = new double[1]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            if (decodeLFLuma)
            {
                decodeForLFLuma = new double[sourceData.Length / 8 + windowSizeForLFLuma]; // leave windowSize amount of zeros at beginning to avoid if later.
                for (int i = 0; i < decodeL.Length; i++)
                {
                    decodeL[i] = decodeLumaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSizeHere / 2/*+ windowSize*/] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
                }
                Array.Copy(decode, windowSizeHere / 2, decodeForLFLuma, windowSizeForLFLuma / 2, decodeL.Length);
            } else
            {
                for (int i = 0; i < decodeL.Length; i++)
                {
                    decodeL[i] = decodeLumaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSizeHere / 2/*+ windowSize*/] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
                }
            }






            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] audioPart = new double[windowSizeHere];
            double[] freqs;
            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(decodingSampleRate, fftMagnitude.Length);


            // For LF Luma decode
            double[] audioPartForLFLuma = new double[windowSizeForLFLuma];
            double[] freqsForLFLuma;
            double[] fftMagnitudeForLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
            freqsForLFLuma = FftSharp.Transform.FFTfreq(decodingSampleRate, fftMagnitudeForLFLuma.Length);



            double tmpMaxIntensity = 0;
            int tmpMaxIntensityIndex = 0;

            double peakFrequencyHere = 0;
            double hue = 0;

            Vector3 tmpV;

            //Vector3[] outputBuffer = new Vector3[decodeL.Length];
            //double decodedLFLuma;
            double lumaFixRatio;

            double[] decodedLFLuma = new double[decodeL.Length];
            double[] decodedC = new double[decodeL.Length];
            double[] decodedH = new double[decodeL.Length];

            //double[] window = FftSharp.Window.Hanning(audioPart.Length);
            double[] window = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPart.Length);
            double[] windowForLFLuma = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPartForLFLuma.Length);

            // decode c,h components and low frequency luma
            for (int i = 0; i < decodeL.Length; i++)
            {
                Array.Copy(decode, i, audioPart, 0, windowSizeHere);
                FftSharp.Window.ApplyInPlace(window, audioPart);
                fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);

                if (decodeLFLuma && superHighQuality)
                {
                    Array.Copy(decodeForLFLuma, i, audioPartForLFLuma, 0, windowSizeForLFLuma);
                    FftSharp.Window.ApplyInPlace(windowForLFLuma, audioPartForLFLuma);
                    fftMagnitudeForLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
                }

                tmpMaxIntensity = 0;
                // find biggest frequency
                for (int b = 0; b < freqs.Length; b++)
                {
                    if (freqs[b] < lowerFrequencyV2) continue; // We need to ignore low frequencies in V2 because they will carry the luma offset.
                    if (fftMagnitude[b] > tmpMaxIntensity)
                    {
                        tmpMaxIntensity = fftMagnitude[b];
                        tmpMaxIntensityIndex = b;
                    }
                }


                if (tmpMaxIntensityIndex == 0)
                {
                    peakFrequencyHere = (freqs[0] * fftMagnitude[0] + freqs[1] * fftMagnitude[1]) / (fftMagnitude[0] + fftMagnitude[1]);
                }
                else if (tmpMaxIntensityIndex == freqs.Length - 1)
                {
                    peakFrequencyHere = (freqs[freqs.Length - 1] * fftMagnitude[freqs.Length - 1] + freqs[freqs.Length - 2] * fftMagnitude[freqs.Length - 2]) / (fftMagnitude[freqs.Length - 1] + fftMagnitude[freqs.Length - 2]);
                }
                else
                {
                    peakFrequencyHere = (freqs[tmpMaxIntensityIndex - 1] * fftMagnitude[tmpMaxIntensityIndex - 1] + freqs[tmpMaxIntensityIndex] * fftMagnitude[tmpMaxIntensityIndex] + freqs[tmpMaxIntensityIndex + 1] * fftMagnitude[tmpMaxIntensityIndex + 1]) / (fftMagnitude[tmpMaxIntensityIndex - 1] + fftMagnitude[tmpMaxIntensityIndex] + fftMagnitude[tmpMaxIntensityIndex + 1]);

                }

                //hue = (((peakFrequencyHere-lowerFrequency)/frequencyRange)*Math.PI)-Math.PI/2;
                hue = (((peakFrequencyHere - lowerFrequencyV2) / frequencyRange) * Math.PI * 2) - Math.PI;

                if (double.IsNaN(hue)) { hue = 0; } // Necessary for really dark/black areas, otherwise they just turn the entire image black because all other calculation fails as a result.

                // For Window size 32:
                // 0.05286 = thats the decoded value for fftmagnitude[0] I get for luma = 1.0 (full)
                // 0.098528 = thats the decoded value for fftmagnitude[1] I get for luma = 1.0 (full)
                // Maybe average both?
                //decodedLFLuma[i] = 0.83*100 * ((fftMagnitude[0] / 0.05286) + (fftMagnitude[1] / 0.098528))/2;
                if (superHighQuality)
                {
                    decodedLFLuma[i] = decodeLFLumaGainMultiplier / decodeChromaGainMultiplier * 100 * ((fftMagnitudeForLFLuma[1] / 0.16735944031697095) + (fftMagnitudeForLFLuma[2] / 0.13069097631912735)) / 2;
                } else
                {
                    decodedLFLuma[i] = decodeLFLumaGainMultiplier / decodeChromaGainMultiplier * 0.6 * 100 * ((fftMagnitude[0] / 0.05286) + (fftMagnitude[1] / 0.098528)) / 2;
                }

                // For Window size 128: [1] is 0.16735944031697095  [2] is 0.13069097631912735
                //decodedLFLuma[i] = 100 * ((fftMagnitudeForLFLuma[1] / 0.16735944031697095) + (fftMagnitudeForLFLuma[2] / 0.13069097631912735))/2;
                //decodedLFLuma[i] = 100 * ((fftMagnitude[0] / 0.05286));
                decodedC[i] = (float)(tmpMaxIntensity * 2.0 / (0.637 / 2.0) * 100.0); // adds a 2x compared to V1 because it was also halved during encoding to avoid clipping
                decodedH[i] = (float)hue;
                /*
                tmpV.X = (float)(decodeL[i]*lumaFixRatio);
                //tmpV.Y = (float)Math.Sqrt(tmpMaxIntensity)*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/0.707f*100f; //experimental * 4, normally doesnt beong there.
                tmpV.Y = (float)tmpMaxIntensity / (0.637f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.707f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.637f * 0.637f) * 100f; //experimental * 4, normally doesnt beong there.
                tmpV.Z = (float)hue;

                tmpV = Helpers.CIELChabTosRGB(tmpV);

                output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                */
            }


            // Now we smooth the luma so we can calculate the correct offset.
            double[] smoothedLuma = new double[decodeL.Length];
            double encodingFrequencyWavelength = decodingSampleRate / lumaInChromaFrequencyV2;
            int averageSampleRadius = (int)Math.Ceiling(encodingFrequencyWavelength * waveLengthSmoothRadiusMultiplierDecodeV2);
            AverageHelper averageHelper = new AverageHelper();
            if (decodeLFLuma)
            {

                for (int i = 0; i < decodeL.Length; i++)
                {
                    if (i == 0)
                    {
                        for (int ii = 0; ii <= averageSampleRadius; ii++)
                        {
                            if ((i + ii) >= decodeL.Length)
                            {
                                break;
                            }
                            averageHelper.totalValue += decodeL[i + ii];
                            averageHelper.multiplier += 1;
                        }
                    }
                    else
                    {
                        if (i > averageSampleRadius)
                        {
                            averageHelper.totalValue -= decodeL[i - averageSampleRadius - 1];
                            averageHelper.multiplier -= 1;
                        }
                        if ((i + averageSampleRadius) < decodeL.Length)
                        {
                            averageHelper.totalValue += decodeL[i + averageSampleRadius];
                            averageHelper.multiplier += 1;
                        }
                    }
                    smoothedLuma[i] = averageHelper.totalValue / averageHelper.multiplier;
                }

                // Now we smooth the decoded LF luma so we can calculate the correct offset.
                //double[] smoothedLFLuma = decodedLFLuma;
                double[] smoothedLFLuma = new double[decodedLFLuma.Length];
                averageHelper = new AverageHelper();
                for (int i = 0; i < decodedLFLuma.Length; i++)
                {
                    if (i == 0)
                    {
                        for (int ii = 0; ii <= averageSampleRadius; ii++)
                        {
                            if ((i + ii) >= decodedLFLuma.Length)
                            {
                                break;
                            }
                            averageHelper.totalValue += decodedLFLuma[i + ii];
                            averageHelper.multiplier += 1;
                        }
                    }
                    else
                    {
                        if (i > averageSampleRadius)
                        {
                            averageHelper.totalValue -= decodedLFLuma[i - averageSampleRadius - 1];
                            averageHelper.multiplier -= 1;
                        }
                        if ((i + averageSampleRadius) < decodedLFLuma.Length)
                        {
                            averageHelper.totalValue += decodedLFLuma[i + averageSampleRadius];
                            averageHelper.multiplier += 1;
                        }
                    }
                    smoothedLFLuma[i] = averageHelper.totalValue / averageHelper.multiplier;
                }


                for (int i = 0; i < decodeL.Length; i++)
                {

                    lumaFixRatio = smoothedLFLuma[i] / smoothedLuma[i];
                    tmpV.X = (float)(decodeL[i] * lumaFixRatio);
                    tmpV.Y = (float)decodedC[i]; //experimental * 4, normally doesnt beong there.
                    tmpV.Z = (float)decodedH[i];

                    tmpV = Helpers.CIELChabTosRGB(tmpV);

                    output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                    output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                    output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                }
            } else
            {
                // If LF luma decoding not desired, just ignore.
                for (int i = 0; i < decodeL.Length; i++)
                {

                    tmpV.X = (float)(decodeL[i]);
                    tmpV.Y = (float)decodedC[i]; //experimental * 4, normally doesnt beong there.
                    tmpV.Z = (float)decodedH[i];

                    tmpV = Helpers.CIELChabTosRGB(tmpV);

                    output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                    output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                    output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                }
            }




            return output;
        }

        // Will chop of handles some functions use to have half of the previous and next frame
        public T[] removeHandles<T>(T[] inputArray)
        {
            T[] output = new T[inputArray.Length/2];

            Array.Copy(inputArray,inputArray.Length/4,output,0,output.Length);

            return output;
        }

        // This will automatically add the required handles filled with zero. Call as the other functions.
        public DecodeResult StereoToRGB24V2Fast(byte[] sourceData, double decodingSampleRate, bool decodeLFLuma = true, bool superHighQuality = false, double fftSampleIntervalInRelationToWindowSize = 0.5, bool normalizeLFLuma = false, bool normalizeSaturation = false, uint subsample = 1, LowFrequencyLumaCompensationMode compensationMode = LowFrequencyLumaCompensationMode.OFFSET, bool isV3 = false, bool tryRemoveAudioDecodeInterferenceFromFFTV3 = false, SpeedReport speedReport = null, CancellationToken cancelToken = default)
        {
            int pixelCount = sourceData.Length / 8;
            uint handleLength = (uint)Math.Ceiling((double)pixelCount/2.0);
            uint pixelCountDivisibleBy2 = handleLength * 2; // Must be divisible by 2 because how else will the function this is passed to calculate where exactly the data starts otherwise?
            byte[] sourceDataWithHandles = new byte[pixelCountDivisibleBy2 * 2 * 8];
            Array.Copy(sourceData,0,sourceDataWithHandles,handleLength*8,sourceData.Length);
            sourceData = null;

            return StereoToRGB24V2FastWithHandles(sourceDataWithHandles, decodingSampleRate, decodeLFLuma, superHighQuality, fftSampleIntervalInRelationToWindowSize, normalizeLFLuma, normalizeSaturation, subsample, compensationMode, isV3, tryRemoveAudioDecodeInterferenceFromFFTV3,  speedReport,  cancelToken);
        }

        // This version of the function needs the sourceData to have half the previous frame and half the next frame as a handle to improve fft continuity and as such the data itself should be divisible by 2 or unexpected errors might occur. Especially important for audio subcarrier decoding. Call the other function if you don't care about that, it will add the handles automatically (zeros, obviously).
        // set fftSampleIntervalInRelationToWindowSize to 0 or lower to disable fast processing.
        public DecodeResult StereoToRGB24V2FastWithHandles(byte[] sourceData, double decodingSampleRate, bool decodeLFLuma = true, bool superHighQuality = false, double fftSampleIntervalInRelationToWindowSize = 0.5, bool normalizeLFLuma = false, bool normalizeSaturation = false, uint subsample = 1, LowFrequencyLumaCompensationMode compensationMode = LowFrequencyLumaCompensationMode.OFFSET, bool isV3 = false, bool tryRemoveAudioDecodeInterferenceFromFFTV3 = false, SpeedReport speedReport = null, CancellationToken cancelToken = default)
        {

            if (speedReport != null)
            {
                speedReport.setPrefix("StereoToRGB24V2Fast");
            }

            int actualSourceDataLengthInPixels = sourceData.Length /8 / 2; // Divided by 2 because we get handles. Half the previous and half the next frame
            int handleLength = actualSourceDataLengthInPixels / 2;

            if(windowSize > handleLength / 2)
            {
                throw new Exception("If this error happened, you are doing something really really extreme.");
            }



            /*if (superHighQuality)
            {
                throw new Exception("Super high quality currently not implemented properly in fast version.");
            }
            superHighQuality = false;// Currently doesnt work in the Fast version of this function. 
            */
            double frequencyRange = upperFrequencyV2 - lowerFrequencyV2;
            int lowerFrequencyHere = lowerFrequencyV2;
            int upperFrequencyHere = upperFrequencyV2;
            if (isV3)
            {
                frequencyRange = upperFrequencyV3 - lowerFrequencyV3;
                lowerFrequencyHere = lowerFrequencyV3;
                upperFrequencyHere = upperFrequencyV3;
            }

            int windowSizeHere = windowSizeIsRelativeTo48k ? (int)Math.Pow(2, Math.Ceiling(Math.Log((double)windowSize * (double)decodingSampleRate / 48000.0, 2.0))) : windowSize;

            uint fftSamplingDistance = 1;
            if (fftSampleIntervalInRelationToWindowSize > 0)
            {

                fftSamplingDistance = (uint)Math.Floor(fftSampleIntervalInRelationToWindowSize * (double)windowSizeHere * (double)subsample);
            } 

            double minimumWindowSizeRequiredForLFLumaCarrierFrequency = (1/lumaInChromaFrequencyV2*decodingSampleRate);
            int windowSizeForLFLuma = (int)Math.Pow(2,Math.Ceiling(Math.Log(minimumWindowSizeRequiredForLFLumaCarrierFrequency,2)));

            double[] decode = new double[actualSourceDataLengthInPixels + windowSizeHere]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeForLFLuma = new double[1]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[actualSourceDataLengthInPixels];

            int startValue = -Math.Max(windowSizeHere/2,decodeLFLuma? windowSizeForLFLuma/2 : 0); // This way we include information from the handles.
            int handleLengthInBytes = handleLength * 2 * 4;
            int endValue = decodeL.Length + (-startValue*2);

            if (decodeLFLuma)
            {
                decodeForLFLuma = new double[actualSourceDataLengthInPixels + windowSizeForLFLuma]; // leave windowSize amount of zeros at beginning to avoid if later.
                for (int i = startValue; i < endValue; i++) // This all probably needs to be optimized with the many ifs inside the loop, but oh well...
                {
                    if (i >= 0 && i<decodeL.Length) // These ones don't get handles, they're just raw data.
                    {
                        decodeL[i] = decodeLumaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, (i+ handleLength) * 4 * 2);
                        decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    }
                    if ((i+windowSizeHere/2) >=0 && (i + windowSizeHere / 2) < decode.Length)
                    {
                        decode[i + windowSizeHere / 2/*+ windowSize*/] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, (i + handleLength) * 4 * 2 + 4);
                        decodeForLFLuma[i + windowSizeForLFLuma / 2/*+ windowSize*/] = decode[i + windowSizeHere / 2/*+ windowSize*/];
                    }
                    else if ((i+ windowSizeForLFLuma / 2) >=0 && (i + windowSizeForLFLuma / 2) < decodeForLFLuma.Length)
                    {
                        decodeForLFLuma[i + windowSizeForLFLuma / 2/*+ windowSize*/] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, (i + handleLength) * 4 * 2 + 4);
                    }
                    
                }
                Array.Copy(decode, windowSizeHere / 2,decodeForLFLuma,windowSizeForLFLuma/2,decodeL.Length);
            } else
            {
                for (int i = startValue; i < endValue; i++)
                {
                    if (i >= 0 && i < decodeL.Length) // These ones don't get handles, they're just raw data.
                    {
                        decodeL[i] = decodeLumaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, (i + handleLengthInBytes) * 4 * 2);
                        decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    }
                    if ((i + windowSizeHere / 2) >= 0 && (i + windowSizeHere / 2) < decode.Length)
                    {
                        decode[i + windowSizeHere / 2/*+ windowSize*/] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, (i + handleLength) * 4 * 2 + 4);
                    }
                    /*decodeL[i] = decodeLumaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = ( decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSizeHere / 2] = decodeChromaGainMultiplier * decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);*/
                }
            }



            double[] outputAudio;
            //if (isV3)
            //{
            if (isV3 && tryRemoveAudioDecodeInterferenceFromFFTV3)// Only really relevant for V3 but I prefer to lose the memory and not have to do an IF every time during the big loop, same as with LF Luma
            {
                outputAudio = new double[(int)Math.Pow(2, Math.Ceiling(Math.Log((double)decodeL.Length, 2.0)))];  // Trying to remove the offending frequency (4 khz at 22khz carrier freq at 48 khz samplerate) will require a array divisible by 2
            } else
            {

                outputAudio = new double[decodeL.Length]; 
            }
            //}



            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] decodedImage = new byte[decodeL.Length * 3];


            double[] audioPart = new double[windowSizeHere];
            double[] freqs;
            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(decodingSampleRate, fftMagnitude.Length);


            // Find closest frequency for audio.
            double smallestDifference = double.PositiveInfinity;
            int audioCarrierClosestFFTBinIndex = 0;
            for(int i = 0; i < freqs.Length; i++)
            {
                double distanceHere = Math.Abs(freqs[i] - audioSubcarrierFrequencyV3);
                if (distanceHere < smallestDifference)
                {
                    smallestDifference = distanceHere;
                    audioCarrierClosestFFTBinIndex = i;
                }
            }
            if(audioCarrierClosestFFTBinIndex == freqs.Length - 1)
            {
                audioCarrierClosestFFTBinIndex--; // There's some issue with the highest bin, dunno why. It regularly just goes to zero for no apparent reason. Maybe a bug.
            }


            // For LF Luma decode
            double[] audioPartForLFLuma = new double[windowSizeForLFLuma];
            double[] freqsForLFLuma;
            double[] fftMagnitudeForLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
            freqsForLFLuma = FftSharp.Transform.FFTfreq(decodingSampleRate, fftMagnitudeForLFLuma.Length);



            double tmpMaxIntensity = 0;
            int tmpMaxIntensityIndex = 0;

            double peakFrequencyHere = 0;
            double hue = 0;

            Vector3 tmpV;

            //Vector3[] outputBuffer = new Vector3[decodeL.Length];
            //double decodedLFLuma;


            double[] decodedLFLuma = new double[decodeL.Length];
            double[] decodedC = new double[decodeL.Length];
            double[] decodedH = new double[decodeL.Length];

            double[] window = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPart.Length);
            double[] windowForLFLuma = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPartForLFLuma.Length);

            // fftMagnitudeInterpolateHere will be interpolated from fftMagniLast and fftMagnitudeNext
            double[] fftMagnitudeLast = null;
            double[] fftMagnitudeLastLFLuma = null;
            uint fftMagnitudeLastIndex = 0;
            uint fftMagnitudeLastIndexPrevious = 0;
            double[] fftMagnitudeNext = null;
            double[] fftMagnitudeNextLFLuma = null;
            uint fftMagnitudeNextIndex = fftSamplingDistance;
            uint fftMagnitudeNextIndexPrevious = fftSamplingDistance;
            //double[] fftMagnitude = new double[freqs.Length];
            //bool reachedNextInterpolationStep = false;

            if (speedReport != null)
            {
                speedReport.logEvent("Initialization.");
            }

            double highestLFLumaValue = 0;

            // decode c,h components and low frequency luma
            for (uint i = 0; i <= (decodeL.Length-subsample); i+= subsample)
            {
                cancelToken.ThrowIfCancellationRequested();
                /*Array.Copy(decode, i, audioPart, 0, windowSize);
                FftSharp.Window.ApplyInPlace(window, audioPart);
                fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);

                if (decodeLFLuma && superHighQuality)
                {
                    Array.Copy(decodeForLFLuma, i, audioPartForLFLuma, 0, windowSizeForLFLuma);
                    FftSharp.Window.ApplyInPlace(windowForLFLuma, audioPartForLFLuma);
                    fftMagnitudeForLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
                }*/
                //
                fftMagnitudeLastIndex = i / fftSamplingDistance * fftSamplingDistance; // This relies on C# behavior that integer division will always round the result down! If porting to other language, take note.
                fftMagnitudeNextIndex = (uint)Math.Min(decodeL.Length - 1, fftMagnitudeLastIndex + fftSamplingDistance);
                /* Had to replace this because while elegant, it won't work with subsampling enabled...
                reachedNextInterpolationStep = i % fftSamplingDistance == 0 && i != 0; // If i%fftSamplingDistance is 0, that means for example that with a sampling distance of 10, we have now reached number 10. Thus next must be moved into last and next recalculated. But this does not apply if we're still at index 0 obviously.
                if (reachedNextInterpolationStep)
                {
                    fftMagnitudeLast = (double[])fftMagnitudeNext.Clone();
                    fftMagnitudeNext = null;
                    if (decodeLFLuma && superHighQuality)
                    {
                        fftMagnitudeLastLFLuma = (double[])fftMagnitudeNextLFLuma.Clone();
                        fftMagnitudeNextLFLuma = null;
                    }
                }*/
                if(fftMagnitudeLastIndex != fftMagnitudeLastIndexPrevious)
                {
                    fftMagnitudeLast = (fftMagnitudeLastIndex == fftMagnitudeNextIndexPrevious) ? (double[])fftMagnitudeNext.Clone() : null;
                    if (decodeLFLuma && superHighQuality)
                    {
                        fftMagnitudeLastLFLuma = (fftMagnitudeLastIndex == fftMagnitudeNextIndexPrevious) ? (double[])fftMagnitudeNextLFLuma.Clone() : null;
                    }
                }
                if(fftMagnitudeNextIndex != fftMagnitudeNextIndexPrevious)
                {
                    fftMagnitudeNext = null;
                    fftMagnitudeNextLFLuma = null;
                }
                if (fftMagnitudeLast == null)
                {
                    Array.Copy(decode, fftMagnitudeLastIndex, audioPart, 0, windowSizeHere);
                    //double[] window = FftSharp.Window.Hanning(audioPart.Length);
                    FftSharp.Window.ApplyInPlace(window, audioPart);
                    fftMagnitudeLast = FftSharp.Transform.FFTmagnitude(audioPart);
                    if (decodeLFLuma && superHighQuality)
                    {
                        Array.Copy(decodeForLFLuma, fftMagnitudeLastIndex, audioPartForLFLuma, 0, windowSizeForLFLuma);
                        FftSharp.Window.ApplyInPlace(windowForLFLuma, audioPartForLFLuma);
                        fftMagnitudeLastLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
                    }
                }
                if (fftMagnitudeNext == null)
                {
                    Array.Copy(decode, fftMagnitudeNextIndex, audioPart, 0, windowSizeHere);
                    //double[] window = FftSharp.Window.Hanning(audioPart.Length);
                    FftSharp.Window.ApplyInPlace(window, audioPart);
                    fftMagnitudeNext = FftSharp.Transform.FFTmagnitude(audioPart);
                    if (decodeLFLuma && superHighQuality)
                    {
                        Array.Copy(decodeForLFLuma, fftMagnitudeNextIndex, audioPartForLFLuma, 0, windowSizeForLFLuma);
                        FftSharp.Window.ApplyInPlace(windowForLFLuma, audioPartForLFLuma);
                        fftMagnitudeNextLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
                    }
                }
                fftMagnitudeLastIndexPrevious = fftMagnitudeLastIndex;
                fftMagnitudeNextIndexPrevious = fftMagnitudeNextIndex;

                // Now interpolate
                uint distanceToLast = i - fftMagnitudeLastIndex;
                uint distanceToNext = fftMagnitudeNextIndex - i;
                if (distanceToLast == 0)
                {
                    fftMagnitude = (double[])fftMagnitudeLast.Clone();
                    if (decodeLFLuma && superHighQuality)
                    {
                        fftMagnitudeForLFLuma = (double[])fftMagnitudeLastLFLuma.Clone();
                    }
                }
                else if (distanceToNext == 0) // does this ever even happen?
                {
                    fftMagnitude = (double[])fftMagnitudeNext.Clone();
                    if (decodeLFLuma && superHighQuality)
                    {
                        fftMagnitudeForLFLuma = (double[])fftMagnitudeNextLFLuma.Clone();
                    }
                }
                else
                {
                    uint totalDistance = distanceToLast + distanceToNext;
                    double lastRatio = (double)distanceToNext / (double)totalDistance;
                    double nextRatio = (double)distanceToLast / (double)totalDistance;
                    fftMagnitude = new double[freqs.Length];
                    for (uint b = 0; b < freqs.Length; b++)
                    {
                        fftMagnitude[b] = lastRatio * fftMagnitudeLast[b] + nextRatio * fftMagnitudeNext[b];
                    }
                    if (decodeLFLuma && superHighQuality)
                    {
                        fftMagnitudeForLFLuma = new double[freqs.Length];
                        for (uint b = 0; b < freqs.Length; b++)
                        {
                            fftMagnitudeForLFLuma[b] = lastRatio * fftMagnitudeLastLFLuma[b] + nextRatio * fftMagnitudeNextLFLuma[b];
                        }
                    }
                }

                tmpMaxIntensity = 0;
                // find biggest frequency
                for (int b = 0; b < freqs.Length; b++)
                {
                    if (freqs[b] < lowerFrequencyHere) continue; // We need to ignore low frequencies in V2 because they will carry the luma offset.
                    if (isV3 && freqs[b] > upperFrequencyHere) continue; // We need to ignore high frequencies in V2 because we might try something crazy?! // Actually nvm it ruins the image anyway...
                    if (fftMagnitude[b] > tmpMaxIntensity)
                    {
                        tmpMaxIntensity = fftMagnitude[b];
                        tmpMaxIntensityIndex = b;
                    }
                }


                if (tmpMaxIntensityIndex == 0)
                {
                    peakFrequencyHere = (freqs[0] * fftMagnitude[0] + freqs[1] * fftMagnitude[1]) / (fftMagnitude[0] + fftMagnitude[1]);
                }
                else if (tmpMaxIntensityIndex == freqs.Length - 1)
                {
                    peakFrequencyHere = (freqs[freqs.Length - 1] * fftMagnitude[freqs.Length - 1] + freqs[freqs.Length - 2] * fftMagnitude[freqs.Length - 2]) / (fftMagnitude[freqs.Length - 1] + fftMagnitude[freqs.Length - 2]);
                }
                else
                {
                    peakFrequencyHere = (freqs[tmpMaxIntensityIndex - 1] * fftMagnitude[tmpMaxIntensityIndex - 1] + freqs[tmpMaxIntensityIndex] * fftMagnitude[tmpMaxIntensityIndex] + freqs[tmpMaxIntensityIndex + 1] * fftMagnitude[tmpMaxIntensityIndex + 1]) / (fftMagnitude[tmpMaxIntensityIndex - 1] + fftMagnitude[tmpMaxIntensityIndex] + fftMagnitude[tmpMaxIntensityIndex + 1]);

                }

                //hue = (((peakFrequencyHere-lowerFrequency)/frequencyRange)*Math.PI)-Math.PI/2;
                hue = (((peakFrequencyHere - lowerFrequencyHere) / frequencyRange) * Math.PI * 2) - Math.PI;

                if (double.IsNaN(hue)) { hue = 0; } // Necessary for really dark/black areas, otherwise they just turn the entire image black because all other calculation fails as a result.

                // For Window size 32:
                // 0.05286 = thats the decoded value for fftmagnitude[0] I get for luma = 1.0 (full)
                // 0.098528 = thats the decoded value for fftmagnitude[1] I get for luma = 1.0 (full)
                // Maybe average both?
                //decodedLFLuma[i] = 0.83*100 * ((fftMagnitude[0] / 0.05286) + (fftMagnitude[1] / 0.098528))/2;
                if (superHighQuality)
                {
                    decodedLFLuma[i] = decodeLFLumaGainMultiplier / decodeChromaGainMultiplier * 100 * ((fftMagnitudeForLFLuma[1] / 0.16735944031697095) + (fftMagnitudeForLFLuma[2] / 0.13069097631912735)) / 2;
                } else
                {
                    decodedLFLuma[i] = decodeLFLumaGainMultiplier / decodeChromaGainMultiplier *  0.63 * 100 * ((fftMagnitude[0] / 0.05286) + (fftMagnitude[1] / 0.098528)) / 2;
                }
                if(decodedLFLuma[i] > highestLFLumaValue)
                {
                    highestLFLumaValue = decodedLFLuma[i];
                }
                
                // For Window size 128: [1] is 0.16735944031697095  [2] is 0.13069097631912735
                //decodedLFLuma[i] = 100 * ((fftMagnitudeForLFLuma[1] / 0.16735944031697095) + (fftMagnitudeForLFLuma[2] / 0.13069097631912735))/2;
                //decodedLFLuma[i] = 100 * ((fftMagnitude[0] / 0.05286));
                decodedC[i] = (float)(tmpMaxIntensity * 2.0 / (0.637 / 2.0) * 100.0); // adds a 2x compared to V1 because it was also halved during encoding to avoid clipping
                decodedH[i] = (float)hue;


                // Audio
                outputAudio[i] = ((6.0*decodeAudioSubcarrierGainMultiplier*fftMagnitude[audioCarrierClosestFFTBinIndex])-0.5); // The 4.0* is just a guess for now...

                // Copy pixel to others if we're subsampling
                // subsample value 1 == no subsampling
                for(int iii=1; iii < subsample; iii++)
                {
                    decodedLFLuma[i + iii] = decodedLFLuma[i];
                    decodedC[i + iii] = decodedC[i];
                    decodedH[i + iii] = decodedH[i];
                    outputAudio[i + iii] = outputAudio[i];
                }
                /*
                tmpV.X = (float)(decodeL[i]*lumaFixRatio);
                //tmpV.Y = (float)Math.Sqrt(tmpMaxIntensity)*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/0.707f*100f; //experimental * 4, normally doesnt beong there.
                tmpV.Y = (float)tmpMaxIntensity / (0.637f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.707f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.637f * 0.637f) * 100f; //experimental * 4, normally doesnt beong there.
                tmpV.Z = (float)hue;

                tmpV = Helpers.CIELChabTosRGB(tmpV);

                output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                */
            }

            if (speedReport != null)
            {
                speedReport.logEvent("FFT decode process.");
            }

            // Now we smooth the luma so we can calculate the correct offset.
            double[] smoothedLuma = new double[decodeL.Length];
            double encodingFrequencyWavelength = decodingSampleRate / lumaInChromaFrequencyV2;
            int averageSampleRadius = (int)Math.Ceiling(encodingFrequencyWavelength*waveLengthSmoothRadiusMultiplierDecodeV2);
            AverageHelper averageHelper = new AverageHelper();
            if (decodeLFLuma)
            {
                // Normalize decoded LF luma in case the highest value was over 100. Because that's impossible I think.
                if(highestLFLumaValue > 100 && (normalizeLFLuma || normalizeSaturation))
                {
                    double ratio = 100 / highestLFLumaValue;
                    if (normalizeLFLuma)
                    {

                        for (int i = 0; i < decodedLFLuma.Length; i++)
                        {
                            decodedLFLuma[i] = decodedLFLuma[i] * ratio;
                        }
                        if (speedReport != null)
                        {
                            speedReport.logEvent("Normalized LF Luma.");
                        }
                    }
                    if (normalizeSaturation)
                    {

                        // and for the extra fun, let's normalize saturation too, since it should have the same gain difference!
                        for (int i = 0; i < decodedC.Length; i++)
                        {
                            decodedC[i] = decodedC[i] * ratio;
                        }
                        if (speedReport != null)
                        {
                            speedReport.logEvent("Normalized saturation.");
                        }
                    }
                }


                for (int i = 0; i < decodeL.Length; i++)
                {
                    if (i == 0)
                    {
                        for (int ii = 0; ii <= averageSampleRadius; ii++)
                        {
                            if ((i + ii) >= decodeL.Length)
                            {
                                break;
                            }
                            averageHelper.totalValue += decodeL[i + ii];
                            averageHelper.multiplier += 1;
                        }
                    }
                    else
                    {
                        if (i > averageSampleRadius)
                        {
                            averageHelper.totalValue -= decodeL[i - averageSampleRadius - 1];
                            averageHelper.multiplier -= 1;
                        }
                        if ((i + averageSampleRadius) < decodeL.Length)
                        {
                            averageHelper.totalValue += decodeL[i + averageSampleRadius];
                            averageHelper.multiplier += 1;
                        }
                    }
                    smoothedLuma[i] = averageHelper.totalValue / averageHelper.multiplier;
                }

                // Now we smooth the decoded LF luma so we can calculate the correct offset.
                //double[] smoothedLFLuma = decodedLFLuma;
                double[] smoothedLFLuma = new double[decodedLFLuma.Length];
                averageHelper = new AverageHelper();
                for (int i = 0; i < decodedLFLuma.Length; i++)
                {
                    if (i == 0)
                    {
                        for (int ii = 0; ii <= averageSampleRadius; ii++)
                        {
                            if ((i + ii) >= decodedLFLuma.Length)
                            {
                                break;
                            }
                            averageHelper.totalValue += decodedLFLuma[i + ii];
                            averageHelper.multiplier += 1;
                        }
                    }
                    else
                    {
                        if (i > averageSampleRadius)
                        {
                            averageHelper.totalValue -= decodedLFLuma[i - averageSampleRadius - 1];
                            averageHelper.multiplier -= 1;
                        }
                        if ((i + averageSampleRadius) < decodedLFLuma.Length)
                        {
                            averageHelper.totalValue += decodedLFLuma[i + averageSampleRadius];
                            averageHelper.multiplier += 1;
                        }
                    }
                    smoothedLFLuma[i] = averageHelper.totalValue / averageHelper.multiplier;
                }

                if (speedReport != null)
                {
                    speedReport.logEvent("Luma smoothing.");
                }

                if(compensationMode == LowFrequencyLumaCompensationMode.MULTIPLY)
                {

                    double lumaFixRatio;
                    for (uint i = 0; i <= (decodeL.Length-subsample); i+=subsample)
                    {

                        cancelToken.ThrowIfCancellationRequested();
                        lumaFixRatio = smoothedLFLuma[i] / smoothedLuma[i];
                        tmpV.X = (float)(decodeL[i] * lumaFixRatio);
                        tmpV.Y = (float)decodedC[i]; //experimental * 4, normally doesnt beong there.
                        tmpV.Z = (float)decodedH[i];

                        tmpV = Helpers.CIELChabTosRGB(tmpV);

                        for (uint ii = 0; ii < subsample; ii++){

                            decodedImage[(i + ii) * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                            decodedImage[(i + ii) * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                            decodedImage[(i + ii) * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                        }
                    }
                } else
                {
                    double lumaFixOffset;
                    for (uint i = 0; i <= (decodeL.Length - subsample); i += subsample)
                    {

                        cancelToken.ThrowIfCancellationRequested();
                        lumaFixOffset = smoothedLFLuma[i] - smoothedLuma[i];
                        tmpV.X = (float)(decodeL[i] + lumaFixOffset);
                        tmpV.Y = (float)decodedC[i]; //experimental * 4, normally doesnt beong there.
                        tmpV.Z = (float)decodedH[i];

                        tmpV = Helpers.CIELChabTosRGB(tmpV);

                        for (uint ii = 0; ii < subsample; ii++)
                        {

                            decodedImage[(i + ii) * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                            decodedImage[(i + ii) * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                            decodedImage[(i + ii) * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                        }
                        //output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                        //output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                        //output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                    }
                }
            } else
            {
                // If LF luma decoding not desired, just ignore.
                for (uint i = 0; i <= (decodeL.Length - subsample); i += subsample)
                {

                    cancelToken.ThrowIfCancellationRequested();
                    tmpV.X = (float)(decodeL[i]);
                    tmpV.Y = (float)decodedC[i]; //experimental * 4, normally doesnt beong there.
                    tmpV.Z = (float)decodedH[i];

                    tmpV = Helpers.CIELChabTosRGB(tmpV);

                    for (uint ii = 0; ii < subsample; ii++)
                    {

                        decodedImage[(i + ii) * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                        decodedImage[(i + ii) * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                        decodedImage[(i + ii) * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                    }
                    //output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                    //output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                    //output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
                }
            }

            if (speedReport != null)
            {
                speedReport.logEvent("Final color conversions.");
            }

            // Do final audio processing and copying
            float[] audioOutputFloat = new float[decodeL.Length];
            if (isV3 && tryRemoveAudioDecodeInterferenceFromFFTV3) // this filtering is extremely slow and doesn't much matter for higher resolutions. Do this for realtime applications and such.
            {
                double nyquistFrequency = decodingSampleRate / 2;
                double offendingFrequency = 2*(nyquistFrequency - audioSubcarrierFrequencyV3); // don't ask me why, it's just an observation.

                cancelToken.ThrowIfCancellationRequested();
                outputAudio = FftSharp.Filter.BandStop(outputAudio, decodingSampleRate, offendingFrequency - 100, offendingFrequency + 100);
                if (speedReport != null)
                {
                    speedReport.logEvent("FFT filtering of artifact frequencies from decoding.");
                }
            }
            for(int i = 0; i < audioOutputFloat.Length; i++)
            {
                audioOutputFloat[i] = (float)outputAudio[i];
            }


            DecodeResult decodeResult = new DecodeResult();
            decodeResult.imageData = decodedImage;
            decodeResult.audioData = audioOutputFloat;
            return decodeResult;
        }
        
        public byte[] StereoToRGB24Fast(byte[] sourceData, double decodingSampleRate, double fftSampleIntervalInRelationToWindowSize = 0.5)
        {
            double frequencyRange = upperFrequency - lowerFrequency;

            int windowSizeHere = windowSizeIsRelativeTo48k ? (int)Math.Pow(2, Math.Ceiling(Math.Log((double)windowSize * (double)decodingSampleRate / 48000.0, 2.0))) : windowSize;

            uint fftSamplingDistance=  (uint)Math.Floor(fftSampleIntervalInRelationToWindowSize * (double)windowSizeHere);

            double[] decode = new double[sourceData.Length / 8 + windowSizeHere]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            for (int i = 0; i < decodeL.Length; i++)
            {
                decodeL[i] = decodeLumaGainMultiplier* decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                decode[i + windowSizeHere / 2/*+ windowSize*/] = decodeChromaGainMultiplier* decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
            }

            double[] audioPart = new double[windowSizeHere];

            double[] freqs;

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(decodingSampleRate, fftMagnitude.Length);

            double tmpMaxIntensity = 0;
            int tmpMaxIntensityIndex = 0;

            double peakFrequencyHere = 0;
            double hue = 0;

            Vector3 tmpV;

            // fftMagnitudeInterpolateHere will be interpolated from fftMagniLast and fftMagnitudeNext
            double[] fftMagnitudeLast = null;
            uint fftMagnitudeLastIndex = 0;
            double[] fftMagnitudeNext = null;
            uint fftMagnitudeNextIndex = fftSamplingDistance;
            //double[] fftMagnitude = new double[freqs.Length];

            bool reachedNextInterpolationStep = false;

            // decode c,h components
            for (uint i = 0; i < decodeL.Length; i++)
            {
                reachedNextInterpolationStep = i % fftSamplingDistance == 0 && i != 0; // If i%fftSamplingDistance is 0, that means for example that with a sampling distance of 10, we have now reached number 10. Thus next must be moved into last and next recalculated. But this does not apply if we're still at index 0 obviously.
                fftMagnitudeLastIndex = i/fftSamplingDistance*fftSamplingDistance; // This relies on C# behavior that integer division will always round the result down! If porting to other language, take note.
                fftMagnitudeNextIndex = (uint)Math.Min(decodeL.Length-1,fftMagnitudeLastIndex+fftSamplingDistance);
                if (reachedNextInterpolationStep)
                {
                    fftMagnitudeLast = (double[])fftMagnitudeNext.Clone();
                    fftMagnitudeNext = null;
                }
                if (fftMagnitudeLast == null)
                {
                    Array.Copy(decode, fftMagnitudeLastIndex, audioPart, 0, windowSizeHere);
                    double[] window = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPart.Length);
                    FftSharp.Window.ApplyInPlace(window, audioPart);
                    fftMagnitudeLast = FftSharp.Transform.FFTmagnitude(audioPart);
                }
                if (fftMagnitudeNext == null)
                {
                    Array.Copy(decode, fftMagnitudeNextIndex, audioPart, 0, windowSizeHere);
                    double[] window = FftSharp.Window.GetWindowByName(windowFunction.ToString(), audioPart.Length);
                    FftSharp.Window.ApplyInPlace(window, audioPart);
                    fftMagnitudeNext = FftSharp.Transform.FFTmagnitude(audioPart);
                }

                // Now interpolate
                uint distanceToLast = i - fftMagnitudeLastIndex;
                uint distanceToNext = fftMagnitudeNextIndex -i;
                if(distanceToLast == 0)
                {
                    fftMagnitude = (double[])fftMagnitudeLast.Clone();
                } else if(distanceToNext == 0)
                {
                    fftMagnitude = (double[])fftMagnitudeNext.Clone();
                } else
                {
                    uint totalDistance = distanceToLast + distanceToNext;
                    double lastRatio = (double)distanceToNext / (double)totalDistance;
                    double nextRatio = (double)distanceToLast / (double)totalDistance;
                    fftMagnitude = new double[freqs.Length];
                    for(uint b = 0; b < freqs.Length; b++)
                    {
                        fftMagnitude[b] = lastRatio * fftMagnitudeLast[b] + nextRatio * fftMagnitudeNext[b];
                    }
                }

                /*Array.Copy(decode, i, audioPart, 0, windowSize);
                double[] window = FftSharp.Window.Hanning(audioPart.Length);
                FftSharp.Window.ApplyInPlace(window, audioPart);
                fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);*/

                tmpMaxIntensity = 0;
                // find biggest frequency
                for (int b = 0; b < freqs.Length; b++)
                {
                    if (fftMagnitude[b] > tmpMaxIntensity)
                    {
                        tmpMaxIntensity = fftMagnitude[b];
                        tmpMaxIntensityIndex = b;
                    }
                }


                if (tmpMaxIntensityIndex == 0)
                {
                    peakFrequencyHere = (freqs[0] * fftMagnitude[0] + freqs[1] * fftMagnitude[1]) / (fftMagnitude[0] + fftMagnitude[1]);
                }
                else if (tmpMaxIntensityIndex == freqs.Length - 1)
                {
                    peakFrequencyHere = (freqs[freqs.Length - 1] * fftMagnitude[freqs.Length - 1] + freqs[freqs.Length - 2] * fftMagnitude[freqs.Length - 2]) / (fftMagnitude[freqs.Length - 1] + fftMagnitude[freqs.Length - 2]);
                }
                else
                {
                    peakFrequencyHere = (freqs[tmpMaxIntensityIndex - 1] * fftMagnitude[tmpMaxIntensityIndex - 1] + freqs[tmpMaxIntensityIndex] * fftMagnitude[tmpMaxIntensityIndex] + freqs[tmpMaxIntensityIndex + 1] * fftMagnitude[tmpMaxIntensityIndex + 1]) / (fftMagnitude[tmpMaxIntensityIndex - 1] + fftMagnitude[tmpMaxIntensityIndex] + fftMagnitude[tmpMaxIntensityIndex + 1]);

                }

                //hue = (((peakFrequencyHere-lowerFrequency)/frequencyRange)*Math.PI)-Math.PI/2;
                hue = (((peakFrequencyHere - lowerFrequency) / frequencyRange) * Math.PI * 2) - Math.PI;

                if (double.IsNaN(hue)) { hue = 0; } // Necessary for really dark/black areas, otherwise they just turn the entire image black because all other calculation fails as a result.

                tmpV.X = (float)(decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                //tmpV.Y = (float)Math.Sqrt(tmpMaxIntensity)*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity*100; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/0.707f*100f; //experimental * 4, normally doesnt beong there.
                tmpV.Y = (float)tmpMaxIntensity / (0.637f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.707f / 2f) * 100f; //experimental * 4, normally doesnt beong there.
                //tmpV.Y = (float)tmpMaxIntensity/ (0.637f * 0.637f) * 100f; //experimental * 4, normally doesnt beong there.
                tmpV.Z = (float)hue;

                tmpV = Helpers.CIELChabTosRGB(tmpV);

                output[i * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                output[i * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                output[i * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
            }

            return output;
        }

        public double[] boxBlur(double[] input, uint radius )
        {
            double[] output = new double[input.Length];
            AverageHelper averageHelper = new AverageHelper();
            for (int i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    for (int ii = 0; ii <= radius; ii++)
                    {
                        if ((i + ii) >= input.Length)
                        {
                            break;
                        }
                        averageHelper.totalValue += input[i + ii];
                        averageHelper.multiplier += 1;
                    }
                }
                else
                {
                    if (i > radius)
                    {
                        averageHelper.totalValue -= input[i - radius - 1];
                        averageHelper.multiplier -= 1;
                    }
                    if ((i + radius) < input.Length)
                    {
                        averageHelper.totalValue += input[i + radius];
                        averageHelper.multiplier += 1;
                    }
                }
                output[i] = averageHelper.totalValue / averageHelper.multiplier;
            }
            return output;
        }
        public float[] boxBlurFloat(float[] input, uint radius )
        {
            float[] output = new float[input.Length];
            AverageHelper averageHelper = new AverageHelper();
            for (int i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    for (int ii = 0; ii <= radius; ii++)
                    {
                        if ((i + ii) >= input.Length)
                        {
                            break;
                        }
                        averageHelper.totalValue += input[i + ii];
                        averageHelper.multiplier += 1;
                    }
                }
                else
                {
                    if (i > radius)
                    {
                        averageHelper.totalValue -= input[i - radius - 1];
                        averageHelper.multiplier -= 1;
                    }
                    if ((i + radius) < input.Length)
                    {
                        averageHelper.totalValue += input[i + radius];
                        averageHelper.multiplier += 1;
                    }
                }
                output[i] = (float)(averageHelper.totalValue / averageHelper.multiplier);
            }
            return output;
        }

        public double[] boxBlur(float[] input, uint radius )
        {
            double[] output = new double[input.Length];
            AverageHelper averageHelper = new AverageHelper();
            for (int i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    for (int ii = 0; ii <= radius; ii++)
                    {
                        if ((i + ii) >= input.Length)
                        {
                            break;
                        }
                        averageHelper.totalValue += input[i + ii];
                        averageHelper.multiplier += 1;
                    }
                }
                else
                {
                    if (i > radius)
                    {
                        averageHelper.totalValue -= input[i - radius - 1];
                        averageHelper.multiplier -= 1;
                    }
                    if ((i + radius) < input.Length)
                    {
                        averageHelper.totalValue += input[i + radius];
                        averageHelper.multiplier += 1;
                    }
                }
                output[i] = averageHelper.totalValue / averageHelper.multiplier;
            }
            return output;
        }

    }
}
