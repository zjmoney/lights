using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Maker.Firmata;

namespace wra_neopixel_control
{
    public static class FFTProcessor
    {
        private static UwpFirmata firmata;
        //private static LightUpdater lights= new LightUpdater();
        // Writ
        public async static Task<double[]> prepare(double[] wave)  // assume 16 bit for now 
        {
            // Find length that is closest power of 2
            int length = wave.Length;
            length = (int)Math.Pow(2, (int)Math.Log(length, 2));

            double[] data = new double[length];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = wave[i];
                    //BitConverter.ToInt16(wave, i * 2) / 32768.0;
            }

            return data;
        }

        public static async Task ControlLightStrip(double[] wave)
        {
            var fft = new FFTFunctions();
            double[] data = await prepare(wave);


            fft.RealFFT(data, true);

            //await ApplyLighting(data);
            //Debug.WriteLine("In ControlLightStrip " + data[0] + " " + data[1]);

        }

        public static async Task ApplyLighting(double[] data)
        {
            double lowRangeVal = 0, highRangeVal = 0;

            for (int i = 0; i < data.Length/2; i+=2)
            {
                lowRangeVal += Math.Abs( data[i] );
            }
            for (int i = data.Length / 2; i < data.Length; i += 2)
            {
                highRangeVal += Math.Abs(data[i]);
            }

            
        }
    }
}
