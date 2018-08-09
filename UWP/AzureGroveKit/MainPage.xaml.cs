﻿using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AzureGroveKit
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        CancellationTokenSource ctsForStart;
        IotHubClient iotClient;
        SensorController sensorController;
        int callMeCounter;
        int sendMessageCounter;
        string deviceId;

        private static int sendMessageDelay = 3000; //milliseconds

        public MainPage()
        {
            this.InitializeComponent();
            callMeCounter = 0;
            sendMessageCounter = 0;
            sensorController = new SensorController();
        }

        private async void runbutton_Click(object sender, RoutedEventArgs e)
        {
            ctsForStart = new CancellationTokenSource();
            runbutton.IsEnabled = false;
            try
            {  
                iotClient = new IotHubClient(CallMeLogger, null);
                deviceId = iotClient.getDeviceId();
                await iotClient.Start();
                new Task(sendMessage).Start();
                new Task(SendButtonEvent).Start();
            }
            catch (Microsoft.Azure.Devices.Client.Exceptions.UnauthorizedException ex)
            {
                ErrorDialog(ex.Message + "\n\nPlease double check the date and time is correct?");
                ctsForStart.Cancel();
                runbutton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ErrorDialog(ex.Message);
                ctsForStart.Cancel();
                runbutton.IsEnabled = true;
            }
        }

        private async void stopButton_Click(object sender, RoutedEventArgs e)
        {
            ctsForStart.Cancel();
            runbutton.IsEnabled = true;
            await iotClient.CloseAsync();
        }

        private void SendButtonEvent()
        {
            bool lastButtonState = false;
            while (true)
            {
                if (ctsForStart.IsCancellationRequested)
                {
                    break;
                }
                Task.Delay(1).Wait();

                if (sensorController.lockState) continue;
                sensorController.lockState = true;
                bool buttonState = false;
                try {
                    buttonState = sensorController.GetButtonValue();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
                sensorController.lockState = false;

                if (buttonState != lastButtonState)
                {
                    if (buttonState)
                    {
                        ButtonEvent buttonEvent = new ButtonEvent();
                        buttonEvent.Click = true;
                        buttonEvent.DeviceId = deviceId;
                        buttonEvent.Timestamp = DateTime.Now.ToString();
                        var messageSerialized = JsonConvert.SerializeObject(buttonEvent);
                        var encodedMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageSerialized));
                        try
                        {
                            iotClient.SendDeviceToCloudMessagesAsync(encodedMessage).Wait();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }
                        SendMessageLoger(messageSerialized);
                    }
                    else
                    {
                        // on to off
                    }
                    Task.Delay(50).Wait();
                }
                lastButtonState = buttonState;
            }
        }

        private void sendMessage()
        {
            while (true)
            {
                if (ctsForStart.IsCancellationRequested)
                {
                    return;
                }
                if (sensorController.lockState) continue;
                sensorController.lockState = true;
                GroveMessage groveMessage = sensorController.GetSensorValue();
                sensorController.lockState = false;

                groveMessage.DeviceId = deviceId;
                var messageSerialized = JsonConvert.SerializeObject(groveMessage);
                var encodedMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageSerialized));
                try
                {
                    iotClient.SendDeviceToCloudMessagesAsync(encodedMessage).Wait();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
                SendMessageLoger(messageSerialized);
                Task.Delay(sendMessageDelay).Wait();
            }
        }

        private async Task receiveMessageAsync(String iotHubUri, String iotHubConnectString)
        {
            while (true)
            {
                if (ctsForStart.IsCancellationRequested)
                {
                    return;
                }

                Microsoft.Azure.Devices.Client.Message message = await iotClient.ReceiveC2dAsync();
                if (message == null)
                    continue;
                Debug.WriteLine("Received message: " + Encoding.ASCII.GetString(message.GetBytes()));
            }
        }

        private void SendMessageLoger(object element)
        {
            AddItemToListBox(messageSendList, string.Format("[{0}] {1}", sendMessageCounter++, element));
        }

        private void CallMeLogger(object element)
        {
            AddItemToListBox(methodCallList, string.Format("[{0}] {1}", callMeCounter++, element));
        }

        private void AddItemToListBox(ListBox list, string item)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    list.Items.Add(item);

                    var selectedIndex = list.Items.Count - 1;
                    if (selectedIndex < 0)
                        return;

                    list.SelectedIndex = selectedIndex;
                    list.UpdateLayout();

                    list.ScrollIntoView(list.SelectedItem);
                });
        }

        private void clearButton_Click(object sender, RoutedEventArgs e)
        {
            callMeCounter = 0;
            sendMessageCounter = 0;
            messageSendList.Items.Clear();
            methodCallList.Items.Clear();
        }

        private async void ErrorDialog(string content)
        {
            ContentDialog dialog = new ContentDialog
            {
                //Title = "Sorry, an unexpected error occured:",
                Title = "Sorry...",
                Content = content + "\n\n\nContact us for help.",
                CloseButtonText = "Close"
            };
            ContentDialogResult result = await dialog.ShowAsync();
        }
    }
}
