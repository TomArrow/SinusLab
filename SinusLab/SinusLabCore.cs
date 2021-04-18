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

        public int samplerate = 48000;
        public double maxAmplitude = Math.Sqrt(2.0) / 2.0;
        public int lowerFrequency = 500;
        public int lowerFrequencyV2 = 3000; // In V2, we encode a low frequency luma offset signal into a 500Hz signal in the chroma so as to compensate for the kind of "centering" happening when converting to the analogue domain or applying many kinds of effects. We sacrifice a bit of hue precision for greater luma precision
        public int upperFrequency = 20000;
        public int upperFrequencyV2 = 20000;
        double lumaInChromaFrequencyV2 = 500.0;
        double waveLengthSmoothRadiusMultiplierEncodeV2 = 0.5;
        double waveLengthSmoothRadiusMultiplierDecodeV2 = 4;

        public double decodeGainMultiplier = 1.0;

        public enum LowFrequencyLumaCompensationMode
        {
            MULTIPLY,
            OFFSET
        }

        public int windowSize = 32;

        public enum FormatVersion {
            DEFAULT_LEGACY = 0,
            V2 = 1, 
            V2_NOLUMA_DECODE_ONLY = 2
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

        public byte[] RGB24ToStereoV2(byte[] sourceData)
        {

            double frequencyRange = upperFrequencyV2 - lowerFrequencyV2;


            //double distanceRatio = 2*(double)frequencyToGenerate/ (double)samplerate; 
            double[] output = new double[sourceData.Length / 3];
            double[] outputL = new double[sourceData.Length / 3];
            double[] LforSmooth = new double[sourceData.Length / 3];

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
                frequencyHere = lowerFrequencyV2 + frequencyRange * hueTo0to1Range; // Hue
                phaseLengthHere = ((double)samplerate) / frequencyHere / 2;
                phaseAdvancementHere = 1 / phaseLengthHere;
                phaseHere = lastPhase + phaseAdvancementHere;
                output[i] = (double)tmpV.Y / 100.0 * (maxAmplitude/2) * Math.Sin(phaseHere * Math.PI); // tmpV.Y is amplitude (chrominance/saturation)
                outputL[i] = ((double)tmpV.X - 50) * 2 / 100.0 * maxAmplitude; // tmpV.Y is amplitude (chrominance/saturation). we divide amplitude by 2 here because we're also adding the amplitude modulated 30hz luma offset and dont want clipping
                LforSmooth[i] = (double)tmpV.X/ 100.0; // tmpV.Y is amplitude (chrominance/saturation). we divide amplitude by 2 here because we're also adding the amplitude modulated 30hz luma offset and dont want clipping
                lastPhase = phaseHere % 2;
            }

            // Now encode a low frequency (about 10Hz) luma offset into the chroma.
            // For that, we first smooth the luma to represent thatt
            // Done with a simple box blue
            double[] smoothedLuma = new double[LforSmooth.Length];
            double encodingFrequencyWavelength = samplerate / lumaInChromaFrequencyV2;
            int averageSampleRadius = (int)Math.Ceiling(encodingFrequencyWavelength* waveLengthSmoothRadiusMultiplierEncodeV2);
            AverageHelper averageHelper = new AverageHelper();
            for (int i = 0; i < LforSmooth.Length; i++)
            {
                if (i == 0)
                {
                    for (int ii = 0; ii <= averageSampleRadius; ii++)
                    {
                        if((i+ii) >= LforSmooth.Length)
                        {
                            break;
                        }
                        averageHelper.totalValue += LforSmooth[i+ii];
                        averageHelper.multiplier += 1;
                    }
                } else { 
                    if (i > averageSampleRadius)
                    {
                        averageHelper.totalValue -= LforSmooth[i-averageSampleRadius-1];
                        averageHelper.multiplier -= 1;
                    }
                    if((i+averageSampleRadius) < LforSmooth.Length)
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
                output[i] += (maxAmplitude/2)*smoothedLuma[i] * Math.Sin(phaseAdvancement*i * Math.PI);
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


        public byte[] StereoToRGB24(byte[] sourceData)
        {
            double frequencyRange = upperFrequency - lowerFrequency;



            double[] decode = new double[sourceData.Length / 8 + windowSize]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            for (int i = 0; i < decodeL.Length; i++)
            {
                decodeL[i] = decodeGainMultiplier*BitConverter.ToSingle(sourceData, i * 4 * 2);
                decode[i + windowSize / 2/*+ windowSize*/] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
            }

            double[] audioPart = new double[windowSize];

            double[] freqs;

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(samplerate, fftMagnitude.Length);

            double tmpMaxIntensity = 0;
            int tmpMaxIntensityIndex = 0;

            double peakFrequencyHere = 0;
            double hue = 0;

            Vector3 tmpV;

            // decode c,h components
            for (int i = 0; i < decodeL.Length; i++)
            {
                Array.Copy(decode, i, audioPart, 0, windowSize);
                double[] window = FftSharp.Window.Hanning(audioPart.Length);
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
        
        
        public byte[] StereoToRGB24V2(byte[] sourceData, bool decodeLFLuma = true,bool superHighQuality = false)
        {
            double frequencyRange = upperFrequencyV2 - lowerFrequencyV2;

            double minimumWindowSizeRequiredForLFLumaCarrierFrequency = (1/lumaInChromaFrequencyV2*48000);
            int windowSizeForLFLuma = (int)Math.Pow(2,Math.Ceiling(Math.Log(minimumWindowSizeRequiredForLFLumaCarrierFrequency,2)));

            double[] decode = new double[sourceData.Length / 8 + windowSize]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeForLFLuma = new double[1]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            if (decodeLFLuma)
            {
                decodeForLFLuma = new double[sourceData.Length / 8 + windowSizeForLFLuma]; // leave windowSize amount of zeros at beginning to avoid if later.
                for (int i = 0; i < decodeL.Length; i++)
                {
                    decodeL[i] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSize / 2/*+ windowSize*/] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
                }
                Array.Copy(decode,windowSize/2,decodeForLFLuma,windowSizeForLFLuma/2,decodeL.Length);
            } else
            {
                for (int i = 0; i < decodeL.Length; i++)
                {
                    decodeL[i] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSize / 2/*+ windowSize*/] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
                }
            }
            



            

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] audioPart = new double[windowSize];
            double[] freqs;
            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(samplerate, fftMagnitude.Length);


            // For LF Luma decode
            double[] audioPartForLFLuma = new double[windowSizeForLFLuma];
            double[] freqsForLFLuma;
            double[] fftMagnitudeForLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
            freqsForLFLuma = FftSharp.Transform.FFTfreq(samplerate, fftMagnitudeForLFLuma.Length);



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

            double[] window = FftSharp.Window.Hanning(audioPart.Length);
            double[] windowForLFLuma = FftSharp.Window.Hanning(audioPartForLFLuma.Length);

            // decode c,h components and low frequency luma
            for (int i = 0; i < decodeL.Length; i++)
            {
                Array.Copy(decode, i, audioPart, 0, windowSize);
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
                    decodedLFLuma[i] = 100 * ((fftMagnitudeForLFLuma[1] / 0.16735944031697095) + (fftMagnitudeForLFLuma[2] / 0.13069097631912735)) / 2;
                } else
                {
                    decodedLFLuma[i] = 0.6 * 100 * ((fftMagnitude[0] / 0.05286) + (fftMagnitude[1] / 0.098528)) / 2;
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
            double encodingFrequencyWavelength = samplerate / lumaInChromaFrequencyV2;
            int averageSampleRadius = (int)Math.Ceiling(encodingFrequencyWavelength*waveLengthSmoothRadiusMultiplierDecodeV2);
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


        public byte[] StereoToRGB24V2Fast(byte[] sourceData, bool decodeLFLuma = true, bool superHighQuality = false, double fftSampleRateInRelationToWindowSize = 0.5, bool normalizeLFLuma = false, bool normalizeSaturation = false, uint subsample = 1, LowFrequencyLumaCompensationMode compensationMode = LowFrequencyLumaCompensationMode.OFFSET, SpeedReport speedReport = null,CancellationToken cancelToken = default)
        {

            if (speedReport != null)
            {
                speedReport.setPrefix("StereoToRGB24V2Fast");
            }

            /*if (superHighQuality)
            {
                throw new Exception("Super high quality currently not implemented properly in fast version.");
            }
            superHighQuality = false;// Currently doesnt work in the Fast version of this function. 
            */
            double frequencyRange = upperFrequencyV2 - lowerFrequencyV2;


            uint fftSamplingDistance = (uint)Math.Floor(fftSampleRateInRelationToWindowSize * (double)windowSize*(double)subsample);

            double minimumWindowSizeRequiredForLFLumaCarrierFrequency = (1/lumaInChromaFrequencyV2*48000);
            int windowSizeForLFLuma = (int)Math.Pow(2,Math.Ceiling(Math.Log(minimumWindowSizeRequiredForLFLumaCarrierFrequency,2)));

            double[] decode = new double[sourceData.Length / 8 + windowSize]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeForLFLuma = new double[1]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            if (decodeLFLuma)
            {
                decodeForLFLuma = new double[sourceData.Length / 8 + windowSizeForLFLuma]; // leave windowSize amount of zeros at beginning to avoid if later.
                for (int i = 0; i < decodeL.Length; i++)
                {
                    decodeL[i] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = (decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSize / 2/*+ windowSize*/] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
                }
                Array.Copy(decode,windowSize/2,decodeForLFLuma,windowSizeForLFLuma/2,decodeL.Length);
            } else
            {
                for (int i = 0; i < decodeL.Length; i++)
                {
                    decodeL[i] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                    decodeL[i] = ( decodeL[i] / 2 / maxAmplitude + 0.5) * 100;
                    decode[i + windowSize / 2/*+ windowSize*/] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
                }
            }
            



            

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] audioPart = new double[windowSize];
            double[] freqs;
            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(samplerate, fftMagnitude.Length);


            // For LF Luma decode
            double[] audioPartForLFLuma = new double[windowSizeForLFLuma];
            double[] freqsForLFLuma;
            double[] fftMagnitudeForLFLuma = FftSharp.Transform.FFTmagnitude(audioPartForLFLuma);
            freqsForLFLuma = FftSharp.Transform.FFTfreq(samplerate, fftMagnitudeForLFLuma.Length);



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

            double[] window = FftSharp.Window.Hanning(audioPart.Length);
            double[] windowForLFLuma = FftSharp.Window.Hanning(audioPartForLFLuma.Length);

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
                    Array.Copy(decode, fftMagnitudeLastIndex, audioPart, 0, windowSize);
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
                    Array.Copy(decode, fftMagnitudeNextIndex, audioPart, 0, windowSize);
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
                    decodedLFLuma[i] = 100 * ((fftMagnitudeForLFLuma[1] / 0.16735944031697095) + (fftMagnitudeForLFLuma[2] / 0.13069097631912735)) / 2;
                } else
                {
                    decodedLFLuma[i] = 0.63 * 100 * ((fftMagnitude[0] / 0.05286) + (fftMagnitude[1] / 0.098528)) / 2;
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

                // Copy pixel to others if we're subsampling
                // subsample value 1 == no subsampling
                for(int iii=1; iii < subsample; iii++)
                {
                    decodedLFLuma[i + iii] = decodedLFLuma[i];
                    decodedC[i + iii] = decodedC[i];
                    decodedH[i + iii] = decodedH[i];
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
            double encodingFrequencyWavelength = samplerate / lumaInChromaFrequencyV2;
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

                            output[(i + ii) * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                            output[(i + ii) * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                            output[(i + ii) * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
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

                            output[(i + ii) * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                            output[(i + ii) * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                            output[(i + ii) * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
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

                        output[(i + ii) * 3] = (byte)Math.Min(255, Math.Max(0, tmpV.X));
                        output[(i + ii) * 3 + 1] = (byte)Math.Min(255, Math.Max(0, tmpV.Y));
                        output[(i + ii) * 3 + 2] = (byte)Math.Min(255, Math.Max(0, tmpV.Z));
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


            return output;
        }
        
        public byte[] StereoToRGB24Fast(byte[] sourceData,double fftSampleRateInRelationToWindowSize = 0.5)
        {
            double frequencyRange = upperFrequency - lowerFrequency;

            uint fftSamplingDistance=  (uint)Math.Floor(fftSampleRateInRelationToWindowSize * (double)windowSize);

            double[] decode = new double[sourceData.Length / 8 + windowSize]; // leave windowSize amount of zeros at beginning to avoid if later.
            double[] decodeL = new double[sourceData.Length / 8];

            for (int i = 0; i < decodeL.Length; i++)
            {
                decodeL[i] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2);
                decode[i + windowSize / 2/*+ windowSize*/] = decodeGainMultiplier * BitConverter.ToSingle(sourceData, i * 4 * 2 + 4);
            }

            double[] audioPart = new double[windowSize];

            double[] freqs;

            //double[] c = new double[decodeL.Length];
            //double[] h = new double[decodeL.Length];

            byte[] output = new byte[decodeL.Length * 3];


            double[] fftMagnitude = FftSharp.Transform.FFTmagnitude(audioPart);
            freqs = FftSharp.Transform.FFTfreq(samplerate, fftMagnitude.Length);

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
                    Array.Copy(decode, fftMagnitudeLastIndex, audioPart, 0, windowSize);
                    double[] window = FftSharp.Window.Hanning(audioPart.Length);
                    FftSharp.Window.ApplyInPlace(window, audioPart);
                    fftMagnitudeLast = FftSharp.Transform.FFTmagnitude(audioPart);
                }
                if (fftMagnitudeNext == null)
                {
                    Array.Copy(decode, fftMagnitudeNextIndex, audioPart, 0, windowSize);
                    double[] window = FftSharp.Window.Hanning(audioPart.Length);
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

    }
}
