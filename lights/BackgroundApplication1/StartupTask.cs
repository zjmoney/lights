using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Microsoft.Maker.Firmata;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using System.Threading.Tasks;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace CoolLights
{

    public sealed class StartupTask : IBackgroundTask
    {
        private const int NEOPIXEL_SET_COMMAND = 0x42;
        private const int NEOPIXEL_SHOW_COMMAND = 0x44;
        private const int NUMBER_OF_PIXELS = 30;

        public static IStream Connection
        {
            get;
            set;
        }

        public static UwpFirmata Firmata
        {
            get;
            set;
        }

        public static RemoteDevice Arduino
        {
            get;
            set;
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral Deferral = taskInstance.GetDeferral();
            // 
            // TODO: Insert code to start one or more asynchronous methods 
            //
            
        }

        public async static Task<double[]> Prepare(double[] wave)  // assume 16 bit for now 
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
            double[] data = await Prepare(wave);


            fft.RealFFT(data, true);

            //await ApplyLighting(data);
            //Debug.WriteLine("In ControlLightStrip " + data[0] + " " + data[1]);

        }
    }
}
