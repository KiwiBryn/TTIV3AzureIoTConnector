// Copyright (c) October 2021, devMobile Software
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
// Base64 encoded payloads
//	AQIDBA== 0x01, 0x02, 0x03, 0x04
// BAMCAQ== 0x04, 0x03, 0x02, 0x01
//
// JSON Payloads
// {"value_0": 0,"value_1": 1,"value_2": 2}
// {"value_9": 9,"value_8": 1,"value_7": 7}
//
//---------------------------------------------------------------------------------
namespace devMobile.IoT.TheThingsIndustries.AzureIoTHub
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Client;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.Logging;

	public partial class Integration
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<Integration> _logger;

		private static readonly ConcurrentDictionary<string, DeviceClient> _DeviceClients = new ConcurrentDictionary<string, DeviceClient>();

		public Integration( IConfiguration configuration,ILogger<Integration> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}
	}
}
