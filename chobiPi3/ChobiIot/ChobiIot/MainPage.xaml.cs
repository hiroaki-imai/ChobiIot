using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using GHIElectronics.UWP.Shields;
using Microsoft.Azure.Devices.Client;
using Microsoft.WindowsAzure.MobileServices;
using ChobiIot.Models;
using Newtonsoft.Json;


// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace ChobiIot
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // デバイスID
        private string _deviceId;
        // FEZHATへのアクセス
        private FEZHAT _fezhat;
        // モバイル接続用
        private MobileServiceClient _mobileService;
        // IotServiceが有効かのフラグ
        private bool _ioTServiceAvailabled = true;
        // IotHub接続情報
        private DeviceClient _deviceClient;
        private string _iotHubConnectionString = "";

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_LoadedAsync;
        }

        /// <summary>
        /// メインページがロードされた処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MainPage_LoadedAsync(object sender, RoutedEventArgs e)
        {
            // ConfigのDeviceIDが未設定の場合
            if(Guid.Empty == ChobiIotConfig.DeviceId)
            {
                FixDeviceId();
                if (_deviceId == "chobiPi")
                {
                    Debug.Write("Please set devicdId or unique machine name");
                    throw new ArgumentOutOfRangeException("Please set devicdId or unique machine name");
                }
            }
            else
            {
                _deviceId = ChobiIotConfig.DeviceId.ToString();
            }
            tbDeviceId.Text = _deviceId.ToString();
            // fezHatの初期化
            _fezhat = await FEZHAT.CreateAsync();
            // LEDセンサーをOFFにする
            _fezhat.D2.TurnOff();
            _fezhat.D3.TurnOff();
            // Device認証サーバーへ接続します
            try
            {
                var result = await TryConnect();
                if (result)
                {
                    await InitializeUpload();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            // タイマーの初期化
            lastSensorReading = new List<SensorReadingBuffer>();
            measureTimer = new DispatcherTimer();
            measureTimer.Interval = TimeSpan.FromMilliseconds(measureIntervalMSec);
            measureTimer.Tick += MeasureTimer_Tick;
            measureTimer.Start();            
        }

        /// <summary>
        /// FEZからデータを取得して退避する。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MeasureTimer_Tick(object sender, object e)
        {
            double x, y, z;
            _fezhat.GetAcceleration(out x, out y, out z);
            double temp = _fezhat.GetTemperature();
            double brightness = _fezhat.GetLightLevel();
            var timestamp = DateTime.Now;
            lock(this)
            {
                lastSensorReading.Add(new SensorReadingBuffer()
                {
                    AccelX = x,
                    AccelY = y,
                    AccelZ = x,
                    Temperature = temp,
                    Brightness = brightness,
                    Timestamp = timestamp
                });
            }
            Debug.WriteLine("[" + timestamp.ToString("yyyyMMdd-hhmmss.fff") + "]T=" + temp + ",B=" + brightness + ",AccelX=" + x + ",AccelY=" + y + ",AccelZ=" + z);
        }

        /// <summary>
        ///  Raspbeerypiからデータ取得を行う間隔
        /// </summary>
        private int measureIntervalMSec = 600000;

        /// <summary>
        /// Raspbeerypiからデータ取得を行うタイマー
        /// </summary>
        DispatcherTimer measureTimer;


        DispatcherTimer uploadTimer;
        int uploadIntervalMSec = 1800000;
        int sendCount = 0;

        /// <summary>
        /// アップロードの設定を行う
        /// </summary>
        /// <returns></returns>
        private async Task InitializeUpload()
        {
            // デバイスの登録情報の取得を行います
            await EntryDevice();
            if (_ioTServiceAvailabled)
            {
                // IotHubの初期化
                SetupIoTHub();
                uploadTimer = new DispatcherTimer();
                uploadTimer.Interval = TimeSpan.FromMilliseconds(uploadIntervalMSec);
                uploadTimer.Tick += UploadTimer_Tick;
                uploadTimer.Start();
            }
        }

        /// <summary>
        /// アップロードを行うタイマー処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UploadTimer_Tick(object sender, object e)
        {
            uploadTimer.Stop();
            await SendEvent();
            uploadTimer.Start();
        }

        List<SensorReadingBuffer> lastSensorReading;

        /// <summary>
        /// 収集した情報をAzureへ転送する。
        /// </summary>
        /// <returns></returns>
        private async Task SendEvent()
        {
            List<SensorReadingBuffer> currentReadings = new List<SensorReadingBuffer>();
            lock (this)
            {
                foreach(var r in lastSensorReading)
                {
                    currentReadings.Add(new SensorReadingBuffer()
                    {
                        AccelX = r.AccelX,
                        AccelY = r.AccelY,
                        AccelZ = r.AccelZ,
                        Temperature = r.Temperature,
                        Brightness = r.Brightness,
                        Timestamp = r.Timestamp
                    });
                }
                lastSensorReading.Clear();
            }
            Debug.WriteLine("Device sending {0} message to IoTHu...\n", currentReadings.Count);

            try
            {
                List<SensorReading> sendingBuffers = new List<SensorReading>();
                for (int count = 0; count < currentReadings.Count; count++)
                {
                    var sensorReading = new SensorReading()
                    {
                        msgId = _deviceId.ToString() + currentReadings[count].Timestamp.ToString("yyyyMMddHHmmssfff"),
                        accelx = currentReadings[count].AccelX,
                        accely = currentReadings[count].AccelY,
                        accelz = currentReadings[count].AccelZ,
                        deviceId = _deviceId.ToString(),
                        temp = currentReadings[count].Temperature,
                        time = currentReadings[count].Timestamp,
                        Longitude = ChobiIotConfig.Longitude,
                        Latitude = ChobiIotConfig.Latitude
                    };
                    sendingBuffers.Add(sensorReading);
                }
                // JSONコンバート
                var payload = JsonConvert.SerializeObject(sendingBuffers);
                // JSONデータをモバイルサービスへ転送する。
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(payload));
                Debug.WriteLine("\t{0}> Sending message: {1}, Data: [{2}]", DateTime.Now.ToLocalTime(), currentReadings.Count, payload);

                await _deviceClient.SendEventAsync(eventMessage);
                tbSendStatus.Text = "Send[" + sendCount++ + "]@" + DateTime.Now.ToString();
                IndicateDebug(FEZHAT.Color.Blue, 10);
            }
            catch(Exception ex)
            {
                Debug.Write(ex.Message);
                IndicateDebug(FEZHAT.Color.Yellow, 3600);
            }            
        }

        /// <summary>
        /// IoT Hubの初期化
        /// </summary>
        private async void SetupIoTHub()
        {
            _iotHubConnectionString = "HostName=" + ChobiIotConfig.IoTHubEndpoint + ";DeviceId=" +
                                      ChobiIotConfig.DeviceId + ";SharedAccessKey=" + ChobiIotConfig.DeviceKey;
            try
            {
                _deviceClient = DeviceClient.CreateFromConnectionString(_iotHubConnectionString, TransportType.Http1);
                Debug.Write("IoT Hub Connected.");
                await ReceiveCommands();
                
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
        }

        private async Task ReceiveCommands()
        {
            Debug.WriteLine("\nDevice waiting for commands from IoTHub...\n");
            Message receivedMessage;
            string messageData;

            while (true)
            {
                try
                {
                    receivedMessage = await _deviceClient.ReceiveAsync();

                    if (receivedMessage != null)
                    {
                        messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                        Debug.WriteLine("\t{0}>Recieve message:{1}", DateTime.Now.ToLocalTime(), messageData);
                        tbReceiveStats.Text = "Recieve - " + messageData +
                                              " - @" + DateTime.Now;
                        var command = messageData.ToLower();
                        if (command.StartsWith("fezhat:"))
                        {
                            var units = command.Split(new char[] {':'});
                            var unit = units[1].Split(new char[] {','});
                            foreach (var order in unit)
                            {
                                FEZHAT.RgbLed targetLed = null;
                                var frags = order.Split(new char[] {'='});
                                switch (frags[0].ToUpper())
                                {
                                    case "D2":
                                        targetLed = _fezhat.D2;
                                        break;
                                    case "D3":
                                        targetLed = _fezhat.D3;
                                        break;
                                }
                                var orderedColor = FEZHAT.Color.Black;
                                switch (frags[1].ToLower())
                                {
                                    case "black":
                                        orderedColor = FEZHAT.Color.Black;
                                        break;
                                    case "red":
                                        orderedColor = FEZHAT.Color.Red;
                                        break;
                                    case "green":
                                        orderedColor = FEZHAT.Color.Green;
                                        break;
                                    case "yellow":
                                        orderedColor = FEZHAT.Color.Yellow;
                                        break;
                                    case "blue":
                                        orderedColor = FEZHAT.Color.Blue;
                                        break;
                                    case "magenta":
                                        orderedColor = FEZHAT.Color.Magneta;
                                        break;
                                    case "cyan":
                                        orderedColor = FEZHAT.Color.Cyan;
                                        break;
                                    case "white":
                                        orderedColor = FEZHAT.Color.White;
                                        break;
                                }
                                targetLed.Color = orderedColor;
                            }
                        }
                        await _deviceClient.CompleteAsync(receivedMessage);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    IndicateDebug(FEZHAT.Color.Red, 3600);
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        /// <summary>
        ///  エラー発生時のタイマー
        /// </summary>
        private DispatcherTimer debugTimer;
        /// <summary>
        /// ランプ転送回数
        /// </summary>
        private int debugCount = 0;
        /// <summary>
        /// 点滅するラウンド数
        /// </summary>
        private int debugRound = 0;
        /// <summary>
        /// 点滅する色
        /// </summary>
        private FEZHAT.Color debugColor;

        /// <summary>
        /// エラー発生をランプの点滅で伝達する
        /// </summary>
        /// <param name="color">色</param>
        /// <param name="round">ラウンド数</param>
        private void IndicateDebug(FEZHAT.Color color, int round)
        {
            if (debugTimer == null)
            {
                debugTimer = new DispatcherTimer();
                debugTimer.Interval = TimeSpan.FromMilliseconds(500);
                debugTimer.Tick += DebugTimer_Tick;
            }
            debugCount = 0;
            debugRound = round;
            debugColor = color;
            debugTimer.Start();
        }

        /// <summary>
        /// エラー発生時のタイマー制御
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DebugTimer_Tick(object sender, object e)
        {
            if ((debugCount % 2 == 0))
            {
                // エラーのランプを表示
                _fezhat.D3.Color = debugColor;
            }
            else
            {
                // エラーランプを消灯
                _fezhat.D3.TurnOff();
            }
            debugCount++;
            // 規定ラウンド終了
            if (debugCount > debugRound)
            {
                _fezhat.D3.TurnOff();
                debugTimer.Stop();
            }
        }


        /// <summary>
        /// デバイスの登録情報の取得および登録を行います。
        /// </summary>
        /// <returns></returns>
        private async Task EntryDevice()
        {

            if (_mobileService == null)
            {
                _mobileService = new MobileServiceClient(ChobiIotConfig.DeviceEntryEndPoint);
            }

            // 登録済みデバイスの一覧を取得
            var table = _mobileService.GetTable<Models.DeviceEntry>();
            var registered = await table.Where((de) => de.DeviceId == _deviceId).ToListAsync();

            var registed = false;
            if (registered != null && registered.Count > 0)
            {
                foreach (var re in registered)
                {
                    // サービスが有効か
                    if (re.ServiceAvailable)
                    {
                        // 登録済みの情報で上書き
                        ChobiIotConfig.IoTHubEndpoint = re.IoTHubEndpoint;
                        ChobiIotConfig.DeviceKey = re.DeviceKey;
                        Debug.WriteLine("IoT Hub Service Avaliabled");
                    }
                    _ioTServiceAvailabled = re.ServiceAvailable;
                    registed = true;
                    break;
                }
            }
            // 未登録の場合、登録を行う
            if (!registed)
            {
                var entry = new Models.DeviceEntry()
                {
                    DeviceId = _deviceId,
                    ServiceAvailable = false,
                    IoTHubEndpoint = ChobiIotConfig.IoTHubEndpoint,
                    DeviceKey = ChobiIotConfig.DeviceKey
                };
                await table.InsertAsync(entry);
            }
        }
    
        /// <summary>
        /// Device認証用サーバーへ接続します
        /// </summary>
        /// <returns></returns>
        private async Task<bool> TryConnect()
        {
            bool result = false;

            // 初期化
            var client = new Windows.Web.Http.HttpClient();
            client.DefaultRequestHeaders.Add("device-id",_deviceId);
            client.DefaultRequestHeaders.Add("device-message", "Hello from RPi2");
            var response = client.GetAsync(ChobiIotConfig.WebUri,
                Windows.Web.Http.HttpCompletionOption.ResponseContentRead);
            // Webサーバーへ接続
            response.AsTask().Wait();
            // Result結果の処理
            var responseResult = response.GetResults();
            if (responseResult.StatusCode == Windows.Web.Http.HttpStatusCode.Ok)
            {
                result = true;
                var received = await responseResult.Content.ReadAsStringAsync();
                Debug.WriteLine("Recieved - " + received);
            }
            else
            {
                Debug.WriteLine("TryConnect Failed - " + responseResult.StatusCode);
            }
            return result;
        }

        /// <summary>
        /// DeviceIdの取得
        /// </summary>
        private void FixDeviceId()
        {
            // ローカル コンピューターに関連付けられてたホスト名のリストを取得
            foreach (var hn in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
            {
                // ホスト名をdeviceIDとして使用
                IPAddress ipAddr;
                if (hn.DisplayName.EndsWith(".local") || IPAddress.TryParse(hn.DisplayName, out ipAddr)) continue;
                _deviceId = hn.DisplayName;
                break;
            }
        }
    }
}
