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
	using System.Collections.Generic;
	using System.Net;
	using System.Text;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Client.Exceptions;
	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

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

						await deviceClient.RejectAsync(message.LockToken);
						return;
					}

					Models.Downlink downlink = new Models.Downlink()
					{
						Confirmed = confirmed,
						Priority = priority,
						Port = port,
						CorrelationIds = AzureLockToken.Add(message.LockToken),
					};

					// Split over multiple lines in an attempt to improve readability. In this scenario a valid JSON string should start/end with {/} for an object or [/] for an array
					if ((payloadText.StartsWith("{") && payloadText.EndsWith("}"))
															||
						((payloadText.StartsWith("[") && payloadText.EndsWith("]"))))
					{
						try
						{
							downlink.PayloadDecoded = JToken.Parse(payloadText);
						}
						catch (JsonReaderException)
						{
							downlink.PayloadRaw = payloadText;
						}
					}
					else
					{
						downlink.PayloadRaw = payloadText;
					}

					_logger.LogInformation("Downlink-IoT Hub DeviceID:{0} MessageID:{2} LockToken:{3} Port:{4} Confirmed:{5} Priority:{6} Queue:{7}",
						receiveMessageHandlerConext.DeviceId,
						message.MessageId,
						message.LockToken,
						downlink.Port,
						downlink.Confirmed,
						downlink.Priority,
						queue);

					Models.DownlinkPayload Payload = new Models.DownlinkPayload()
					{
						Downlinks = new List<Models.Downlink>()
						{
							downlink
						}
					};

					string url = $"{receiveMessageHandlerConext.WebhookBaseURL}/{receiveMessageHandlerConext.ApplicationId}/webhooks/{receiveMessageHandlerConext.WebhookId}/devices/{receiveMessageHandlerConext.DeviceId}/down/{queue}".ToLower();

					using (var client = new WebClient())
					{
						client.Headers.Add("Authorization", $"Bearer {receiveMessageHandlerConext.ApiKey}");

						client.UploadString(new Uri(url), JsonConvert.SerializeObject(Payload));

						if (!downlink.Confirmed)
						{
							try
							{
								await deviceClient.CompleteAsync(message.LockToken);
							}
							catch (DeviceMessageLockLostException)
							{
								_logger.LogWarning("Downlink-CompleteAsync DeviceID:{0} MessageID:{1} LockToken:{2} timeout", receiveMessageHandlerConext.DeviceId, message.MessageId, message.LockToken);
							}
						}
					}

					_logger.LogInformation("Downlink-DeviceID:{0} LockToken:{1} success", receiveMessageHandlerConext.DeviceId, message.LockToken);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Downlink-ReceiveMessge processing failed");
			}
		}
	}
}