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
