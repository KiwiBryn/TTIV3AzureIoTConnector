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
	using System.Text;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Extensions.Logging;

	public partial class Integration
	{
		private async Task AzureIoTHubClientReceiveMessageHandler(Message message, object userContext)
		{
			try
			{
				Models.AzureIoTHubReceiveMessageHandlerContext receiveMessageHandlerConext = (Models.AzureIoTHubReceiveMessageHandlerContext)userContext;

				if (!_DeviceClients.TryGetValue(receiveMessageHandlerConext.DeviceId, out DeviceClient deviceClient))
				{
					_logger.LogWarning("Downlink-DeviceID:{0} unknown", receiveMessageHandlerConext.DeviceId);
					return;
				}

				using (message)
				{
					string payloadText = Encoding.UTF8.GetString(message.GetBytes()).Trim();

					// Looks like it's Azure IoT hub message, Put the one mandatory message property first, just because
					if (!AzureDownlinkMessage.PortTryGet(message.Properties, out byte port))
					{
						_logger.LogWarning("Downlink-Port property is invalid");

						await deviceClient.RejectAsync(message);
						return;
					}

					if (!AzureDownlinkMessage.ConfirmedTryGet(message.Properties, out bool confirmed))
					{
						_logger.LogWarning("Downlink-Confirmed flag is invalid");

						await deviceClient.RejectAsync(message);
						return;
					}

					if (!AzureDownlinkMessage.PriorityTryGet(message.Properties, out Models.DownlinkPriority priority))
					{
						_logger.LogWarning("Downlink-Priority value is invalid");

						await deviceClient.RejectAsync(message);
						return;
					}

					if (!AzureDownlinkMessage.QueueTryGet(message.Properties, out Models.DownlinkQueue queue))
					{
						_logger.LogWarning("Downlink-Queue value is invalid");

						await deviceClient.RejectAsync(message);
						return;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Downlink-ReceiveMessge processing failed");
			}
		}
	}
}