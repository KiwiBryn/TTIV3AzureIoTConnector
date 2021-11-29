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
	using System.Text;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Extensions.Logging;

	public partial class Integration
	{
		private async Task<MethodResponse> AzureIoTHubClientDefaultMethodHandler(MethodRequest methodRequest, object userContext)
		{
			if (methodRequest.DataAsJson != null)
			{
				_logger.LogWarning("AzureIoTHubClientDefaultMethodHandler name:{Name} payload:{DataAsJson}", methodRequest.Name, methodRequest.DataAsJson);
			}
			else
			{
				_logger.LogWarning("AzureIoTHubClientDefaultMethodHandler name:{Name} payload:NULL", methodRequest.Name);
			}

			return new MethodResponse(Encoding.ASCII.GetBytes("{\"message\":\"The TTIV3 Connector does not support Direct Methods.\"}"), 400);
		}
	}
}
