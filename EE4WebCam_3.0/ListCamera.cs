using Microsoft.Expression.Encoder.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EE4WebCam
{
    public partial class ListCamera : Form
    {
        public EncoderDevice VideoSelected = null;
        public EncoderDevice AudioSelected = null;

        public ListCamera()
        {
            InitializeComponent();
            InitializeDevices();
        }

        private void InitializeDevices()
        {
            ListDevice_Listbox.ClearSelected();
            foreach (EncoderDevice edv in EncoderDevices.FindDevices(EncoderDeviceType.Video))
            {
                ListDevice_Listbox.Items.Add(edv.Name);
            }
        }

        private void Cancel_Button_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void OK_Button_Click(object sender, EventArgs e)
        {
            if (ListDevice_Listbox.SelectedIndex < 0){
                MessageBox.Show("No Video capture devices have been selected.\nSelect a video device from the listboxes and try again.", "Warning");
                return;
            }

            // Get the selected video device            
            foreach (EncoderDevice edv in EncoderDevices.FindDevices(EncoderDeviceType.Video))
            {
                if (String.Compare(edv.Name, ListDevice_Listbox.SelectedItem.ToString()) == 0)
                {
                    VideoSelected = edv;
                    break;
                }
            }

            foreach (EncoderDevice eda in EncoderDevices.FindDevices(EncoderDeviceType.Audio))
            {
                AudioSelected = eda;
                break;
            }

            this.Close();
        }
    }
}
