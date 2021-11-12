//---------------------------------------------------------------------------------
// Copyright (c) November 2021, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//---------------------------------------------------------------------------------
namespace devMobile.IoT.TheThingsIndustries.AzureIoTHub
{
	using System.Collections.Generic;

	public class IoTHubApplicationSetting
	{
		public string DtdlModelId { get; set; }
	}

	public class IoTHubSettings
	{
		public string IoTHubConnectionString { get; set; } = string.Empty;

		public Dictionary<string, IoTHubApplicationSetting> Applications { get; set; }
	}


	public class DeviceProvisiongServiceApplicationSetting
	{
		public string DtdlModelId { get; set; } = string.Empty;

		public string GroupEnrollmentKey { get; set; } = string.Empty;
	}

	public class DeviceProvisiongServiceSettings
	{
		public string IdScope { get; set; } = string.Empty;

		public Dictionary<string, DeviceProvisiongServiceApplicationSetting> Applications { get; set; }
	}


	public class IoTCentralMethodSetting
	{
		public byte Port { get; set; } = 0;

		public bool Confirmed { get; set; } = false;

		public Models.DownlinkPriority Priority { get; set; } = Models.DownlinkPriority.Normal;

		public Models.DownlinkQueue Queue { get; set; } = Models.DownlinkQueue.Replace;
	}

	public class IoTCentralSetting
	{
		public Dictionary<string, IoTCentralMethodSetting> Methods { get; set; }
	}

	public class AzureIoTSettings
	{
		public IoTHubSettings IoTHub { get; set; }

		public DeviceProvisiongServiceSettings DeviceProvisioningService { get; set; }

		public IoTCentralSetting IoTCentral { get; set; }
	}


	public class TheThingsIndustriesSettingApplicationSetting
	{
		public string ApiKey { get; set; } = string.Empty;

		public string WebhookId { get; set; } = string.Empty;
	}

	public class TheThingsIndustriesSettings
	{
		public string WebhookBaseURL { get; set; } = string.Empty;

		public Dictionary<string, TheThingsIndustriesSettingApplicationSetting> Applications { get; set; }
	}
}
