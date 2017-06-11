using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;

namespace ChobiIot
{
    public static class ChobiIotConfig
    {
        // Device Entry Configuration
        public static string DeviceEntryEndPoint = "http://chobiiotma.azurewebsites.net";

        // GUIDの設定
        public static Guid DeviceId = new Guid(0x4d962a4f, 0x3060, 0x4691, 0xa9, 0x91, 0x90, 0x6c, 0x8f, 0xc3, 0x2, 0xbc);

        // 接続先WebサーバーのURL
        public static Uri WebUri = new Uri("http://egholservice.azurewebsites.net/api/DeviceConnect");

        // 緯度、経度
        public static double Latitude = 35.114807;
        public static double Longitude = 136.935743;

        // IoT Hub設定
        public static string IoTHubEndpoint = "ChobiHub.azure-devices.net";
        public static string DeviceKey = "EXDwdkFBotXfRR5idG9hCat0dDaUCAOZcpUY9yDTkuI=";

    }
}
