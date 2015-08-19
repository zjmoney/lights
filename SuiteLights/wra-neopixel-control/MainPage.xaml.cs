using Microsoft.Maker.Firmata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace wra_neopixel_control
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int NEOPIXEL_SET_COMMAND = 0x42;
        private const int NEOPIXEL_SHOW_COMMAND = 0x44;
        private const int NUMBER_OF_PIXELS = 30;

        private UwpFirmata firmata;

        /// <summary>
        /// This page uses advanced features of the Windows Remote Arduino library to carry out custom commands which are
        /// defined in the NeoPixel_StandardFirmata.ino sketch. This is a customization of the StandardFirmata sketch which
        /// implements the Firmata protocol. The customization defines the behaviors of the custom commands invoked by this page.
        /// 
        /// To learn more about Windows Remote Arduino, refer to the GitHub page at: https://github.com/ms-iot/remote-wiring/
        /// To learn more about advanced behaviors of WRA and how to define your own custom commands, refer to the
        /// advanced documentation here: https://github.com/ms-iot/remote-wiring/blob/develop/advanced.md
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            firmata = App.Firmata;
        }

        /// <summary>
        /// This button callback is invoked when the buttons are pressed on the UI. It determines which
        /// button is pressed and sets the LEDs appropriately
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Color_Click( object sender, RoutedEventArgs e )
        {
            var button = sender as Button;
            switch( button.Name )
            {
                case "Red":
                    SetAllPixelsAndUpdate( 255, 0, 0 );
                    break;
                
                case "Green":
                    SetAllPixelsAndUpdate( 0, 255, 0 );
                    break;

                case "Blue":
                    SetAllPixelsAndUpdate( 0, 0, 255 );
                    break;

                case "Yellow":
                    SetAllPixelsAndUpdate( 255, 255, 0 );
                    break;

                case "Cyan":
                    SetAllPixelsAndUpdate( 0, 255, 255 );
                    break;

                case "Magenta":
                    SetAllPixelsAndUpdate( 255, 0, 255 );
                    break;
            }
        }

        /// <summary>
        /// Sets all the pixels to the given color values and calls UpdateStrip() to tell the NeoPixel library to show the set colors.
        /// </summary>
        /// <param name="red"></param>
        /// <param name="green"></param>
        /// <param name="blue"></param>
        private void SetAllPixelsAndUpdate( byte red, byte green, byte blue )
        {
            SetAllPixels( red, green, blue );
            UpdateStrip();
        }

        /// <summary>
        /// Sets all the pixels to the given color values
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void SetAllPixels( byte red, byte green, byte blue )
        {
            for( byte i = 0; i < NUMBER_OF_PIXELS; ++i )
            {
                SetPixel( i, red, green, blue );
            }
        }

        /// <summary>
        /// Sets a single pixel to the given color values
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void SetPixel( byte pixel, byte red, byte green, byte blue )
        {
            firmata.beginSysex( NEOPIXEL_SET_COMMAND );
            firmata.appendSysex( pixel );
            firmata.appendSysex( red );
            firmata.appendSysex( green );
            firmata.appendSysex( blue );
            firmata.endSysex();
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
            firmata.beginSysex( NEOPIXEL_SHOW_COMMAND );
            firmata.endSysex();
        }
    }
}
