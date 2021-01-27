using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SE1_BUB_Tester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort sr;
        DispatcherTimer tmr;
        DispatcherTimer tmrStatus;
        DispatcherTimer tmrAuto;
        string strConfirmOK = "CMU,00000000|RFUFM,00000000|RFUS,00000000|RFUU1,00000000|RFUI,00000000|RFUU4,00000000";
        bool bRFIDConnected = false;
        string strFlag = "CMU,00000000";
        int iCounter = 1;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            sr = new SerialPort();
            sr.DataReceived += Sr_DataReceived;

            tmr = new DispatcherTimer();
            
            tmr.Interval = TimeSpan.FromSeconds(GetDelayTime());
            tmr.Tick += Tmr_Tick;

            tmrStatus = new DispatcherTimer();
            tmrStatus.Interval = TimeSpan.FromSeconds(2);
            tmrStatus.Tick += TmrStatus_Tick;

            tmrAuto = new DispatcherTimer();
            tmrAuto.Interval = TimeSpan.FromSeconds(GetDelayTime());
            tmrAuto.Tick += TmrAuto_Tick;

            string strPort = "";
            string strStatus = GetConnectedUSBCOM(ref strPort);
            if (!string.IsNullOrEmpty(strStatus))
                lblStatus.Content = strStatus;
            else
                txtCOMPort.Text = strPort.ToUpper().Replace("COM", "");
        }

        private void TmrAuto_Tick(object sender, EventArgs e)
        {
            if (chkAuto.IsChecked == true)
            {
                lblStatus.Content = "Status: Software trigger tag Auto reading started.";
                ReadTag();
            }
            else
                tmrAuto.Stop();
        }

        private double GetDelayTime()
        {
            if (txtDelay.Text == "")
            {
                lblStatus.Content = "Warning: Minimum delay time is 0.35 sec.";
                return 0;
            }

            try
            {
                Double dTime = Double.Parse(txtDelay.Text.Trim());
                if (dTime < 0.35)
                {
                    dTime = 0;
                    lblStatus.Content = "Warning: Minimum delay time is 0.35 sec.";
                }

                return dTime;
            }
            catch (Exception e)
            {
                lblStatus.Content = "Error : Read (sec) " + e.Message;

                return 0;
            }
        }

        private void TmrStatus_Tick(object sender, EventArgs e)
        {
            tmrStatus.Stop();

            if (!bRFIDConnected && sr != null && sr.IsOpen)
                lblStatus.Content = "Warning: Make sure SE1-BUB is connected.";
            else
            {
                tmrStatus.Interval = TimeSpan.FromSeconds(5);
                lblStatus.Content = "Stauts: ";

                if(rdoManual.IsChecked != true && rdoSoftware.IsChecked != true)
                    rdoManual.IsChecked = true;
            }
        }

        private void Tmr_Tick(object sender, EventArgs e)
        {
            tmr.Stop();
            sr.Write("RFUS\r");
            lblStatus.Content = "Status: Tag reading stopped.";
            tmrStatus.Start();
            if (rdoSoftware.IsChecked == true)
                btnReadStart.IsEnabled = true;
            else
                btnReadStart.IsEnabled = false;
        }

        private void btnCOMStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sr.IsOpen)
                    sr.Close();

                sr.PortName = "COM" + txtCOMPort.Text.Trim();
                sr.BaudRate = Int32.Parse(txtBaudRate.Text.Trim());

                sr.ReadBufferSize = 2048;
                sr.ReadTimeout = 5000;
                sr.Open();

                sr.DtrEnable = sr.RtsEnable = true;

                sr.NewLine = "\r";
                sr.DiscardInBuffer();
                sr.DiscardOutBuffer();

                string strPort = "";
                string strStatus = GetConnectedUSBCOM(ref strPort);
                if (!string.IsNullOrEmpty(strStatus))
                    lblStatus.Content = strStatus;
                else
                {
                    lblStatus.Content = "Status: ";
                    sr.Write("CMU\r");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Content = "Error: " + ex.Message;
            }
        }

        private void Sr_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (lst.Dispatcher.CheckAccess())
            {
                UpdateData(sr.ReadTo("\r"));
            }
            else
                lst.Dispatcher.Invoke(new Action(() => UpdateData(sr.ReadTo("\r"))));
        }

        private string GetConnectedUSBCOM(ref string strPORT)
        {
            try
            {
                strPORT = "";
                int iCount = 0;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_SerialPort"))
                {

                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        if (queryObj["Caption"] != null && queryObj["Caption"].ToString().Trim().Contains("DENSO WAVE"))
                        {
                            if (queryObj["Caption"].ToString().Trim().Contains("Connected") && queryObj["DeviceID"] != null)
                            {
                                strPORT = queryObj["DeviceID"].ToString();
                                iCount += 1;
                            }
                        }
                    }
                }

                if (iCount > 1)
                    return "Warning: More than one DENSO WAVE USB-COM devices are connected to this PC. Disconnect the one you don't need and try again.";
                else
                {
                    if (strPORT != "")
                        return null;
                    else
                        return "Warning: No DENSO WAVE USB-COM device is connected to this PC!";
                }
            }
            catch (ManagementException ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private void UpdateData(string strData)
        {
            if (strData == strFlag)
            {
                bRFIDConnected = true;
                btnCOMStart.IsEnabled = false;
                btnCOMStop.IsEnabled = true;
                lblStatus.Content = "Status: COM port opened. Reader mode changed to RFID scanning.";
                sr.Write("RFUFM,00\r");
                
                tmrStatus.Start();
            }

            if (strConfirmOK.Contains(strData))
                return;

            strData = (iCounter++).ToString() + ": " + strData;

            lst.Items.Add(strData);
            
            if (lst.Items.Count > 0)
            {
                lst.SelectedIndex = lst.Items.Count - 1;

                // Auto scroll solution from this link >> http://stackoverflow.com/questions/2337822/wpf-listbox-scroll-to-end-automatically
                ListBoxAutomationPeer svAutomation = (ListBoxAutomationPeer)ScrollViewerAutomationPeer.CreatePeerForElement(lst);
                IScrollProvider scrollInterface = (IScrollProvider)svAutomation.GetPattern(PatternInterface.Scroll);
                System.Windows.Automation.ScrollAmount scrollVertical = System.Windows.Automation.ScrollAmount.LargeIncrement;
                System.Windows.Automation.ScrollAmount scrollHorizontal = System.Windows.Automation.ScrollAmount.NoAmount;
                //If the vertical scroller is not available, the operation cannot be performed, which will raise an exception. 
                if (scrollInterface.VerticallyScrollable)
                    scrollInterface.Scroll(scrollHorizontal, scrollVertical);
            }

            if (rdoManual.IsChecked == true)
            {
                lblStatus.Content = "Info: Press trigger to read tag.";
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnCOMStop_Click(object sender, RoutedEventArgs e)
        {
            bRFIDConnected = false;
            sr.DiscardInBuffer();
            sr.DiscardOutBuffer();
            sr.Close();
            lblStatus.Content = "Status: COM port closed.";
            tmrStatus.Start();
            btnCOMStop.IsEnabled = btnReadStart.IsEnabled = false;
            btnCOMStart.IsEnabled = true;
            rdoManual.IsChecked = rdoSoftware.IsChecked = false;
        }

        private void btnReadClear_Click(object sender, RoutedEventArgs e)
        {
            lst.Items.Clear();
            iCounter = 1;
        }

        private void ReadTag()
        {
            if (GetDelayTime() > 0.3)
            {
                tmr.Interval = TimeSpan.FromSeconds(GetDelayTime());
                tmr.Start();
                tmrStatus.Stop();
                btnReadStart.IsEnabled = false;
                sr.Write("RFUI\r");
            }
        }

        private void btnReadStart_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Content = "Status: Software trigger tag reading started.";
            ReadTag();
            if (chkAuto.IsChecked == true)
            {
                tmrAuto.Interval = TimeSpan.FromSeconds(GetDelayTime());
                tmrAuto.Start();
            }
        }

        private void rdoManual_Checked(object sender, RoutedEventArgs e)
        {
            if (rdoManual.IsChecked == true && sr.IsOpen)
            {
                tmr.Stop();
                tmrAuto.Stop();
                tmrStatus.Stop();
                lblStatus.Content = "Status: Trigger mode changed to Manual trigger.";
                sr.Write("RFUS\r");
                sr.Write("RFUU1,01\r");
                sr.Write("RFUI\r");
                btnReadStart.IsEnabled = false;
                chkAuto.IsChecked = false;
                chkAuto.IsEnabled = false;
            }
        }

        private void rdoSoftware_Checked(object sender, RoutedEventArgs e)
        {
            if (rdoSoftware.IsChecked == true)
            {
                tmrStatus.Stop();
                btnReadStart.IsEnabled = true;
                chkAuto.IsEnabled = true;
                lblStatus.Content = "Status: Trigger mode changed to Software trigger.";
                sr.Write("RFUS\r");
                sr.Write("RFUU4\r");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sr != null && sr.IsOpen)
            {
                sr.Write("RFUS\r");
                sr.DiscardInBuffer();
                sr.DiscardOutBuffer();
                sr.Close();
                sr = null;
            }
        }
    }
}
