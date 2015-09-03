using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Foundation;

namespace SuiteLights
{


    /// <summary>
    /// This is a temporary hack to allow us to test voice recording through to Halsey 
    /// </summary>
    public class Recorder
    {
        MediaCapture capture;
        IRandomAccessStream stream;
        const int BufferSize = 64000;
        bool recording;
        float volume = 100;

        public Recorder()
        {
        }

        public bool IsRecording { get { return recording; } }


        public float VolumePercent
        {
            get { return volume; }
            set { volume = value; }
        }


        /// <summary>
        /// This event is raised when recording has started.
        /// </summary>
        public event EventHandler RecordingStarted;

        public async Task StartRecordingAsync()
        {
            capture = new MediaCapture();

            stream = new InMemoryRandomAccessStream();

            capture.InitializeAsync().AsTask().Wait();

            capture.AudioDeviceController.VolumePercent = volume;

            MediaEncodingProfile profile = new MediaEncodingProfile();

            AudioEncodingProperties audioProperties = AudioEncodingProperties.CreatePcm(16000, 1, 16);
            profile.Audio = audioProperties;
            profile.Video = null;
            profile.Container = new ContainerEncodingProperties() { Subtype = MediaEncodingSubtypes.Wave };

            await capture.StartRecordToStreamAsync(profile, stream);

            recording = true;

            if (RecordingStarted != null)
            {
                RecordingStarted(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// This event is raised when a recording is available for processing.
        /// todo: move this to stream based.
        /// </summary>
        public event EventHandler<byte[]> RecordingAvailable;

        /// <summary>
        /// Stop recording and return the audio encoded bytes.
        /// </summary>
        /// <returns></returns>
        public async Task StopRecordingAsync()
        {
            if (recording)
            {
                await capture.StopRecordAsync();
                recording = false;
            }
            else
            {
                return;
            }

            byte[] wav = new byte[stream.Size];
            stream.Seek(0);
            await stream.ReadAsync(wav.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);

            SelectWave(wav);

        }

        public void SelectWave(byte[] wav)
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

            if (RecordingAvailable != null)
            {
                RecordingAvailable(this, data);
            }

        }

        public byte[] RiffHeader { get; set; }

    }
}
