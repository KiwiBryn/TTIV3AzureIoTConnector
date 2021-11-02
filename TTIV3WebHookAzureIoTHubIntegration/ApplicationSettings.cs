﻿//---------------------------------------------------------------------------------
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
	using Newtonsoft.Json;

	public class DeviceProvisiongServiceSettings
	{
		[JsonProperty("IdScope")]
		public string IdScope { get; set; }
		[JsonProperty("GroupEnrollmentKey")]
		public string GroupEnrollmentKey { get; set; }
	}

	public class AzureSettings
	{
		[JsonProperty("IoTHubConnectionString")]
		public string IoTHubConnectionString { get; set; }

		[JsonProperty("DeviceProvisioningServiceSettings")]
		public DeviceProvisiongServiceSettings DeviceProvisioningServiceSettings { get; set; }
	}

	public class TheThingsIndustriesSettings
	{
		[JsonProperty("WebhookId")]
		public string WebhookId { get; set; }
		[JsonProperty("WebhookBaseURL")]
		public string WebhookBaseURL { get; set; }
		[JsonProperty("ApiKey")]
		public string ApiKey { get; set; }
	}
}