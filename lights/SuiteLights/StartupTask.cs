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
using Windows.Media.SpeechRecognition;
using System.Diagnostics;
using Windows.Media.Devices;
using Windows.Media.Capture;
using Windows.ApplicationModel;
using Windows.Devices.Gpio;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Audio;
using Windows.Media.Effects;
using Windows.Media.Render;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.ComponentModel;
using Windows.Media.MediaProperties;
using static System.Diagnostics.Debug;
using Windows.Storage.Streams;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace SuiteLights
{
    public sealed class StartupTask : IBackgroundTask
    {
        private const int NEOPIXEL_SET_COMMAND = 0x42;
        private const int NEOPIXEL_SHOW_COMMAND = 0x44;
        private const int NUMBER_OF_PIXELS = 30;
        private const int BAUD_RATE = 115200;

        //private string SelectedItem = "USB";//{ get; private set; }

        // Audio Stuff
        private AudioGraph graph;
        private AudioFileInputNode fileInputNode;
        private AudioDeviceOutputNode deviceOutputNode;
        private PropertySet echoProperties;
        private Timer audioTimer;
        private bool playing;

        double maxLowVolume = 1;
        double maxHighVolume = 1;
        Random rand = new Random();
        

        public static UsbSerial Connection
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

        public byte[] RiffHeader { get; set; }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            System.Diagnostics.Debug.WriteLine("hi");
            // 
            // TODO: Insert code to start one or more asynchronous methods 
            //
            
            playing = false;
            DeviceInformation device = null;

            var result = UsbSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>().Result;

            if (result == null || result.Count == 0)
            {
                throw new InvalidOperationException("No USB FOUND");
            }
            else
            {
                // Assume first
                // TODO: Might not be first.
                device = result.FirstOrDefault();
            }
            WriteLine("device name: " + device.Name);
            Connection = new UsbSerial(device);

            Firmata = new UwpFirmata();
            Firmata.begin(Connection);
            Arduino = new RemoteDevice(Firmata);
            Connection.ConnectionEstablished += OnConnectionEstablished;


            
            Connection.begin(BAUD_RATE, SerialConfig.SERIAL_8N1);


            // Begin Recording from mic.
            this.StartRecording();

            
        }
        
        public void StartRecording()
        {
            for(int i = 0; i < 50; i++ )
            {
                this.PrintMicrophoneSample();
            }
        }

        public void PrintMicrophoneSample()
        {
            MediaCapture capture;
            IRandomAccessStream stream;
            const int BufferSize = 64000;
            bool recording;
            float volume = 100;



            capture = new MediaCapture();

            stream = new InMemoryRandomAccessStream();
            var captureInitSettings2 = new MediaCaptureInitializationSettings();
            captureInitSettings2.StreamingCaptureMode = StreamingCaptureMode.Audio;
            capture.InitializeAsync(captureInitSettings2).AsTask().Wait();

            capture.AudioDeviceController.VolumePercent = volume;

            MediaEncodingProfile profile = new MediaEncodingProfile();

            AudioEncodingProperties audioProperties = AudioEncodingProperties.CreatePcm(16000, 1, 16);
            profile.Audio = audioProperties;
            profile.Video = null;
            profile.Container = new ContainerEncodingProperties() { Subtype = MediaEncodingSubtypes.Wave };

            capture.StartRecordToStreamAsync(profile, stream).GetResults();

            recording = true;

            // waste time
            for (int i = 0; i < 5; i++)
            {
                i = i * 232323 + 89;// WriteLine(i);
            }

            capture.StopRecordAsync().GetResults();

            byte[] wav = new byte[stream.Size];
            stream.Seek(0);
            stream.ReadAsync(wav.AsBuffer(), (uint)stream.Size, InputStreamOptions.None).GetResults();

            int sum = 0;
            for(int i = 0; i < wav.Count(); i++)
            {
                sum += (int) wav[i];
            }
            WriteLine((double) wav.Count() / sum);
        }

        public void SelectWave([System.Runtime.InteropServices.WindowsRuntime.ReadOnlyArray] byte[] wav)
        {
            // trim off the wav header

            // Get past all the other sub chunks to get to the data subchunk:
            int pos = 12;   // First Subchunk ID from 12 to 16

            // Keep iterating until we find the data chunk (i.e. 64 61 74 61 ...... (i.e. 100 97 116 97 in decimal))
            while (!(wav[pos] == 100 && wav[pos + 1] == 97 && wav[pos + 2] == 116 && wav[pos + 3] == 97))
            {
                pos += 4;
                int chunkSize = wav[pos] + wav[pos + 1] * 256 + wav[pos + 2] * 65536 + wav[pos + 3] * 16777216;
                pos += 4 + chunkSize;
            }
            pos += 8;

            RiffHeader = new byte[pos];
            Array.Copy(wav, 0, RiffHeader, 0, pos);

            int len = wav.Length - pos;
            byte[] data = new byte[len];
            Array.Copy(wav, pos, data, 0, len);

            WriteLine("GOTIt: " + data.ToString());

        }


        private void AmplitudeReading(object sender, double reading)
        {
            WriteLine("AMPLITUDEREADING" + reading);
        }

        private void OnConnectionEstablished()
        {
            System.Diagnostics.Debug.WriteLine("connection esablished");
            while(true) { }
            // Sets to red
            SetAllPixelsAndUpdate(255, 0, 0);
            


            // OnNavigatedTo goes here


            // Audio Callbacks
            this.CreateAudioGraph();

            this.SelectInputFile();
            // add effect // Create a property set and add a property/value pair
            echoProperties = new PropertySet();
            echoProperties.Add("Mix", 0.5f);
            echoProperties.Add("Data", new double[512]);

            // starts the callback that plays th emusic.
            audioTimer = new Timer(Callback, null, 50, 0);

            TogglePlay();
        }
        
        


    /// <summary>
    /// Sets all the pixels to the given color values and calls UpdateStrip() to tell the NeoPixel library to show the set colors.
    /// </summary>
    /// <param name="red"></param>
    /// <param name="green"></param>
    /// <param name="blue"></param>
    private void SetAllPixelsAndUpdate(byte red, byte green, byte blue)
        {
            SetAllPixels(red, green, blue);
            UpdateStrip();
        }

        /// <summary>
        /// Sets all the pixels to the given color values
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void SetAllPixels(byte red, byte green, byte blue)
        {
            for (byte i = 0; i < NUMBER_OF_PIXELS; ++i)
            {
                SetPixel(i, red, green, blue);
            }
        }

        /// <summary>
        /// Sets a single pixel to the given color values
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void SetPixel(byte pixel, byte red, byte green, byte blue)
        {
            Firmata.beginSysex(NEOPIXEL_SET_COMMAND);
            Firmata.appendSysex(pixel);
            Firmata.appendSysex(red);
            Firmata.appendSysex(green);
            Firmata.appendSysex(blue);
            Firmata.endSysex();
        }

        /// <summary>
        /// Tells the NeoPixel strip to update its displayed colors.
        /// This function must be called before any colors set to pixels will be displayed.
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void UpdateStrip()
        {
            Firmata.beginSysex(NEOPIXEL_SHOW_COMMAND);
            Firmata.endSysex();
        }

        public static double[] Prepare([System.Runtime.InteropServices.WindowsRuntime.ReadOnlyArray] double[] wave)  // assume 16 bit for now 
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

        public void ControlLightStrip([System.Runtime.InteropServices.WindowsRuntime.ReadOnlyArray] double[] wave)
        {
            
            var fft = new FFTFunctions();
            double[] data = Prepare(wave);

            fft.RealFFT(data, true);

            //await ApplyLighting(data);
            //Debug.WriteLine("In ControlLightStrip " + data[0] + " " + data[1]);

        }




        private void SelectInputFile()
        {

            StorageFile file = this.GetPackagedFile(null, "audio.mp3").Result;

            // File can be null if cancel is hit in the file picker
            if (file == null)
            {
                return;
            }

            CreateAudioFileInputNodeResult fileInputNodeResult = graph.CreateFileInputNodeAsync(file).GetResults();
            if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                // Cannot read file
                return;
            }

            fileInputNode = fileInputNodeResult.FileInputNode;
            fileInputNode.AddOutgoingConnection(deviceOutputNode);

            // Event Handler for file completion
            fileInputNode.FileCompleted += FileInput_FileCompleted;

        }

        private void TogglePlay()
        {
            // Toggle playback
            if (playing == false)
            {
                System.Diagnostics.Debug.WriteLine("Playing");
                graph.Start();
                playing = true;
            }
            else
            {
                playing = false;
                graph.Stop();
            }
        }

        private async Task<StorageFile> GetPackagedFile(string folderName, string fileName)
        {
            StorageFolder installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            if (folderName != null)
            {
                StorageFolder subFolder = await installFolder.GetFolderAsync(folderName);
                return await subFolder.GetFileAsync(fileName);
            }
            else
            {
                return await installFolder.GetFileAsync(fileName);
            }
        }

        private void CreateAudioGraph()
        {
            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            CreateAudioGraphResult result = AudioGraph.CreateAsync(settings).GetResults();

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                return;
            }

            graph = result.Graph;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputResult = graph.CreateDeviceOutputNodeAsync().GetResults();

            if (deviceOutputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device output
                return;
            }

            deviceOutputNode = deviceOutputResult.DeviceOutputNode;

        }




        private async void Callback(Object state)
        {
            double redLowVolume = 0;
            double greenLowVolume = 0;
            double blueLowVolume = 0;

            double redHighVolume = 0;
            double greenHighVolume = 0;
            double blueHighVolume = 0;
            // Long running operation
            ControlLightStrip((double[])echoProperties["Data"]);

            var data = echoProperties["Data"] as double[];

            double lowRangeVal = 1, highRangeVal = 1;

            for (int i = 2; i < data.Length / 5; i++)
            {
                lowRangeVal += Math.Abs(data[i]);

            }
            for (int j = 0; j < data.Length / 3 / 3 + 15; j++)
            {
                redLowVolume += Math.Abs(data[j]);
            }
            for (int j = data.Length / 3 / 3; j < 2 * (data.Length / 3 / 3) + 15; j++)
            {
                greenLowVolume += Math.Abs(data[j]);
            }
            for (int j = 2 * (data.Length / 3 / 3); j < data.Length / 3 - 10; j++)
            {
                blueLowVolume += Math.Abs(data[j]);
            }



            for (int i = (4 * data.Length) / 5; i < data.Length; i++)
            {
                highRangeVal += Math.Abs(data[i]);
            }

            for (int j = (2 * data.Length) / 3 - 20; j < ((2 * data.Length) / 3) + (3 / 18) * data.Length + 10; j++)
            {
                redHighVolume += Math.Abs(data[j]);
            }
            for (int j = ((2 * data.Length) / 3) + (1 / 9) * data.Length - 20; j < ((2 * data.Length) / 3) + (5 / 18) * data.Length + 10; j++)
            {
                greenHighVolume += Math.Abs(data[j]);
            }
            for (int j = ((2 * data.Length) / 3) + (2 / 9) * data.Length; j < data.Length; j++)
            {
                blueHighVolume += Math.Abs(data[j]);
            }

            maxLowVolume = Math.Max(maxLowVolume, lowRangeVal);
            maxHighVolume = Math.Max(maxHighVolume, highRangeVal);


            redLowVolume = 6 * redLowVolume > 255 ? 255 : 6 * redLowVolume;
            greenLowVolume = 6 * greenLowVolume > 255 ? 255 : 6 * greenLowVolume;
            blueLowVolume = 5 * blueLowVolume > 255 ? 255 : 5 * blueLowVolume;

            redHighVolume = 9 * redHighVolume > 255 ? 255 : 9 * redHighVolume;
            greenHighVolume = 9 * greenHighVolume > 255 ? 255 : 9 * greenHighVolume;
            blueHighVolume = 6 * blueHighVolume > 255 ? 255 : 4 * blueHighVolume;

            // Lowers other volumes
            double maxColor = Math.Max(Math.Max(redLowVolume, greenLowVolume), blueLowVolume);
            if (Math.Abs(maxColor - redLowVolume) > 0.0001)
            {
                redLowVolume *= 1.2;

                blueLowVolume *= 0.3;
                greenLowVolume *= 0.3;
            }
            else if (Math.Abs(maxColor - greenLowVolume) > 0.0001)
            {
                greenLowVolume *= 1.2;
                blueLowVolume *= 0.3;
                redLowVolume *= 0.3;
            }
            else if (Math.Abs(maxColor - blueLowVolume) > 0.0001)
            {
                blueLowVolume *= 1.1;
                redLowVolume *= 0.3;
                greenLowVolume *= 0.3;
            }

            redHighVolume = rand.NextDouble() * 255;
            greenHighVolume = 255 - redHighVolume;
            // Lowers other volumes
            double maxColorHigh = Math.Max(Math.Max(redHighVolume, greenHighVolume), blueHighVolume);
            if (Math.Abs(maxColorHigh - redHighVolume) > 0.0001)
            {

                redHighVolume += 20;
                blueHighVolume *= 0.3;
                greenHighVolume *= 0.1;
            }
            else if (Math.Abs(maxColorHigh - greenHighVolume) > 0.0001)
            {
                greenHighVolume += 20;
                blueHighVolume *= 0.5;
                redHighVolume *= 0.1;
            }
            else if (Math.Abs(maxColorHigh - blueHighVolume) > 0.0001)
            {
                blueHighVolume *= 5.0;
                redHighVolume *= 0.2;
                greenHighVolume *= 0.5;
            }

            SetPixelRange(15 - (int)(lowRangeVal / maxLowVolume * 15), 15, redLowVolume, greenLowVolume, blueLowVolume);
            SetPixelRange(0, 15 - (int)(lowRangeVal / maxLowVolume * 15), 0, 0, 0);

            SetPixelRange(15, 15 + (int)(highRangeVal / maxHighVolume * 15), redHighVolume, greenHighVolume, blueHighVolume);
            SetPixelRange(15 + (int)(highRangeVal / maxHighVolume * 15), 30, 0, 0, 0);

            UpdateStrip();
            audioTimer.Change(50, 0);

        }

        private void SetPixelRange(int l, int h, double r, double g, double b)
        {
            for (int i = l; i < h; i++)
            {
                byte ib = (byte)i;
                byte rb = (byte)r;
                byte gb = (byte)g;
                byte bb = (byte)b;
                SetPixel(ib, rb, gb, bb);
            }

        }

        private async void FileInput_FileCompleted(AudioFileInputNode sender, object args)
        {
            // File playback is done. Stop the graph
            graph.Stop();

            // Reset the file input node so starting the graph will resume playback from beginning of the file
            sender.Reset();
        }


        // Already just use USB


        //private void RefreshDeviceList()
        //{
        //    //invoke the listAvailableDevicesAsync method of the correct Serial class. Since it is Async, we will wrap it in a Task and add a llambda to execute when finished
        //    Task<DeviceInformationCollection> task = null;

        //    switch ( SelectedItem)
        //    {
        //        default:
        //        case "Bluetooth":
        //            task = BluetoothSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>();
        //            break;

        //        case "USB":
        //            task = UsbSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>();
        //            break;

        //        case "Network":
        //            task = null;
        //            break;
        //    }

        //    if (task != null)
        //    {
        //        //store the returned DeviceInformation items when the task completes
        //        task.ContinueWith(listTask =>
        //        {
        //            //store the result and populate the device list on the UI thread
        //            //var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
        //            {

        //                var result = listTask.Result;
        //                if (result == null || result.Count == 0)
        //                {
        //                    //ConnectMessage.Text = "No items found.";
        //                }
        //                else
        //                {
        //                    foreach (DeviceInformation device in result)
        //                    {
        //                        //connections.Add(new Connection(device.Name, device));
        //                    }
        //                    //ConnectMessage.Text = "Select an item and press \"Connect\" to connect.";
        //                }

        //                //ConnectionList.ItemsSource = connections;
        //            };
        //        });
        //    }
        //}




        ///// <summary>
        ///// Called if the Connect button is pressed
        ///// </summary>
        ///// <param name="sender">The object invoking the event</param>
        ///// <param name="e">Arguments relating to the event</param>
        //private void ConnectButton_Click(object sender, RoutedEventArgs e)
        //{
        //    ////disable the buttons and set a timer in case the connection times out
        //    //SetUiEnabled( false );

        //    //DeviceInformation device = null;
        //    //if( ConnectionList.SelectedItem != null )
        //    //{
        //    //    var selectedConnection = ConnectionList.SelectedItem as Connection;
        //    //    device = selectedConnection.Source as DeviceInformation;
        //    //}
        //    //else if( ConnectionMethodComboBox.SelectedIndex != 2 )
        //    //{
        //    //    //if they haven't selected an item, but have chosen "usb" or "bluetooth", we can't proceed
        //    //    ConnectMessage.Text = "You must select an item to proceed.";
        //    //    SetUiEnabled( true );
        //    //    return;
        //    //}

        //    ////use the selected device to create our communication object
        //    //switch( ConnectionMethodComboBox.SelectedItem as String )
        //    //{
        //    //    default:
        //    //    case "Bluetooth":
        //    //        App.Connection = new BluetoothSerial( device );
        //    //        break;

        //    //    case "USB":
        //    //        App.Connection = new UsbSerial( device );
        //    //        break;

        //    //    case "Network":
        //    //        string host = NetworkHostNameTextBox.Text;
        //    //        string port = NetworkPortTextBox.Text;
        //    //        ushort portnum = 0;

        //    //        if( host == null || port == null )
        //    //        {
        //    //            ConnectMessage.Text = "You must enter host and IP.";
        //    //            return;
        //    //        }

        //    //        try
        //    //        {
        //    //            portnum = Convert.ToUInt16( port );
        //    //        }
        //    //        catch( FormatException )
        //    //        {
        //    //            ConnectMessage.Text = "You have entered an invalid port number.";
        //    //            return;
        //    //        }

        //    //        App.Connection = new NetworkSerial( new Windows.Networking.HostName( host ), portnum );
        //    //        break;
        //    //}

        //    //App.Firmata = new UwpFirmata();
        //    //App.Firmata.begin( App.Connection );
        //    //App.Arduino = new RemoteDevice( App.Firmata );

        //    //App.Connection.ConnectionEstablished += OnConnectionEstablished;
        //    //App.Connection.ConnectionFailed += OnConnectionFailed;
        //    //App.Connection.begin( 115200, SerialConfig.SERIAL_8N1 );

        //    ////start a timer for connection timeout
        //    //timeout = new DispatcherTimer();
        //    //timeout.Interval = new TimeSpan( 0, 0, 30 );
        //    //timeout.Tick += Connection_TimeOut;
        //    //timeout.Start();
        //}
    }
}
