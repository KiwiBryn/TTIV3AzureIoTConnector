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
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;

	public class Program
	{
		public static void Main()
		{
			var host = new HostBuilder()
						.ConfigureAppConfiguration(e =>
							e.AddEnvironmentVariables()
						  .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build()
					 )
				 .ConfigureFunctionsWorkerDefaults()
				 .Build();

			host.Run();
		}
	}
}