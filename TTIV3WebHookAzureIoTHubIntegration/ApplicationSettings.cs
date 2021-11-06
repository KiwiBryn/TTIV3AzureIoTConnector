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

	public class MethodSetting
	{
		public byte Port { get; set; }

		public bool Confirmed { get; set; }

		public Models.DownlinkPriority Priority{ get; set; }

		public Models.DownlinkQueue Queue { get; set; }
	}

	public class IoTCentralSetting
	{
		public Dictionary<string,MethodSetting> Methods { get; set; }
	}

	public class DeviceProvisiongServiceSettings
	{
		public string IdScope { get; set; }

		public string GroupEnrollmentKey { get; set; }
	}

	public class AzureIoTSettings
	{
		public string IoTHubConnectionString { get; set; }

		public DeviceProvisiongServiceSettings DeviceProvisioningService { get; set; }

		public IoTCentralSetting IoTCentral { get; set; }
	}

	public class TheThingsIndustriesSettings
	{
		public string WebhookId { get; set; }
		public string WebhookBaseURL { get; set; }

		public string ApiKey { get; set; }
	}
}
