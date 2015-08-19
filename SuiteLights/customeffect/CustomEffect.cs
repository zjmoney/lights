using System;
using System.Collections.Generic;
using Windows.Media.Effects;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.MediaProperties;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace CustomEffect
{
    // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array in an AudioFrame
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public sealed class AudioEchoEffect : IBasicAudioEffect, INotifyPropertyChanged
    {
        private AudioEncodingProperties currentEncodingProperties;
        private List<AudioEncodingProperties> supportedEncodingProperties;

        private float[] echoBuffer;
        private int currentActiveSampleIndex;
        private IPropertySet propertySet;
        private int count;

        public event PropertyChangedEventHandler PropertyChanged;

        // Mix does not have a set - all updates should be done through the property set.
        private float Mix
        {
            get { return (float)propertySet["Mix"]; }
        }

        private double[] Data
        {
            get { return (double[])propertySet["Data"]; }
            set { propertySet["Data"] = outputData; }
        }

        public bool UseInputFrameForOutput { get { return false; } }
        public bool TimeIndependent { get { return true; } }
        public bool IsReadyOnly { get { return true; } }

        public double[] outputData
        {
            get; private set;
        }

        // Set up constant members in the constructor
        public AudioEchoEffect()
        {
            // Support 44.1kHz and 48kHz mono float
            supportedEncodingProperties = new List<AudioEncodingProperties>();
            AudioEncodingProperties encodingProps1 = AudioEncodingProperties.CreatePcm(44100, 1, 32);
            encodingProps1.Subtype = MediaEncodingSubtypes.Float;
            AudioEncodingProperties encodingProps2 = AudioEncodingProperties.CreatePcm(48000, 1, 32);
            encodingProps2.Subtype = MediaEncodingSubtypes.Float;

            supportedEncodingProperties.Add(encodingProps1);
            supportedEncodingProperties.Add(encodingProps2);
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }



        public IReadOnlyList<AudioEncodingProperties> SupportedEncodingProperties
        {
            get
            {
                return supportedEncodingProperties;
            }
        }

        public void SetEncodingProperties(AudioEncodingProperties encodingProperties)
        {
            currentEncodingProperties = encodingProperties;

            // Create and initialize the echo array
            echoBuffer = new float[encodingProperties.SampleRate]; // exactly one second delay
            currentActiveSampleIndex = 0;
        }

        unsafe public void ProcessFrame(ProcessAudioFrameContext context)
        {
            AudioFrame inputFrame = context.InputFrame;
            AudioFrame outputFrame = context.OutputFrame;

            using (AudioBuffer inputBuffer = inputFrame.LockBuffer(AudioBufferAccessMode.Read),
                                outputBuffer = outputFrame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference inputReference = inputBuffer.CreateReference(),
                                            outputReference = outputBuffer.CreateReference())
            {
                byte* inputDataInBytes;
                byte* outputDataInBytes;
                uint inputCapacity;
                uint outputCapacity;

                ((IMemoryBufferByteAccess)inputReference).GetBuffer(out inputDataInBytes, out inputCapacity);
                ((IMemoryBufferByteAccess)outputReference).GetBuffer(out outputDataInBytes, out outputCapacity);

                float* inputDataInFloat = (float*)inputDataInBytes;
                float* outputDataInFloat = (float*)outputDataInBytes;

                
                
                // Process audio data
                int dataInFloatLength = (int)inputBuffer.Length / sizeof(float);

                outputData = new double[dataInFloatLength];

                for (int i = 0; i < dataInFloatLength; i++)
                {
                    outputData[i] = inputDataInFloat[i];
                    outputDataInFloat[i] = inputDataInFloat[i];
                }

                Data = outputData;
            }
        }

        public void Close(MediaEffectClosedReason reason)
        {
            // Clean-up any effect resources
            // This effect doesn't care about close, so there's nothing to do
        }

        public void DiscardQueuedFrames()
        {
            // Reset contents of the samples buffer
            Array.Clear(echoBuffer, 0, echoBuffer.Length - 1);
            currentActiveSampleIndex = 0;
        }

        public void SetProperties(IPropertySet configuration)
        {
            this.propertySet = configuration;
        }
    }
}