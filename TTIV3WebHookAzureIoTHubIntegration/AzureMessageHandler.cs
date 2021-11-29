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
	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	public partial class Integration
	{
		private async Task AzureIoTHubClientReceiveMessageHandler(Message message, object userContext) 
		{
			try
			{
				Models.AzureIoTHubDeviceClientContext context = (Models.AzureIoTHubDeviceClientContext)userContext;

				DeviceClient deviceClient = await _DeviceClients.GetAsync<DeviceClient>(context.DeviceId);
				if (deviceClient==null)
				{
					_logger.LogWarning("Downlink-DeviceID:{DeviceId} unknown", context.DeviceId);

					return;
				}

				using (message)
				{
					Models.Downlink downlink;
					Models.DownlinkQueue queue;

					string payloadText = Encoding.UTF8.GetString(message.GetBytes()).Trim();

					if (message.Properties.ContainsKey("method-name"))
					{
						#region Azure IoT Central C2D message processing
						string methodName = message.Properties["method-name"];

						if (string.IsNullOrWhiteSpace(methodName))
						{
							_logger.LogWarning("Downlink-DeviceID:{DeviceId} MessagedID:{MessageId} LockToken:{LockToken} method-name property empty", context.DeviceId, message.MessageId, message.LockToken);

							await deviceClient.RejectAsync(message);
							return;
						}

						// Look up the method settings to get confirmed, port, priority, and queue
						if ((_azureIoTSettings == null) || (_azureIoTSettings.IoTCentral == null) || !_azureIoTSettings.IoTCentral.Methods.TryGetValue(methodName, out IoTCentralMethodSetting methodSetting))
						{
							_logger.LogWarning("Downlink-DeviceID:{DeviceId} MessagedID:{MessageId} LockToken:{LockToken} method-name:{methodName} has no settings", context.DeviceId, message.MessageId, message.LockToken, methodName);

							await deviceClient.RejectAsync(message);
							return;
						}

						downlink = new Models.Downlink()
						{
							Confirmed = methodSetting.Confirmed,
							Priority = methodSetting.Priority,
							Port = methodSetting.Port,
							CorrelationIds = AzureLockToken.Add(message.LockToken),
						};

						queue = methodSetting.Queue;

						// Check to see if special case for Azure IoT central command with no request payload
						if (payloadText.IsPayloadEmpty())
						{
							if (methodSetting.Payload.IsPayloadValidJson())
							{
								downlink.PayloadDecoded = JToken.Parse(methodSetting.Payload);
							}
							else
							{
								_logger.LogWarning("Downlink-DeviceID:{DeviceId} MessagedID:{MessageId} LockToken:{LockToken} method-name:{methodName} payload invalid {Payload}", context.DeviceId, message.MessageId, message.LockToken, methodName, methodSetting.Payload);

								await deviceClient.RejectAsync(message);
								return;
							}
						}

						if (!payloadText.IsPayloadEmpty())
						{
							if (payloadText.IsPayloadValidJson())
							{
								downlink.PayloadDecoded = JToken.Parse(payloadText);
							}
							else
							{
								// Normally wouldn't use exceptions for flow control but, I can't think of a better way...
								try
								{
									downlink.PayloadDecoded = new JObject(new JProperty(methodName, JProperty.Parse(payloadText)));
								}
								catch(JsonException ex)
								{
									downlink.PayloadDecoded = new JObject(new JProperty(methodName, payloadText));
								}
							}
						}

						_logger.LogInformation("Downlink-IoT Central DeviceID:{DeviceId} Method:{methodName} MessageID:{MessageId} LockToken:{LockToken} Port:{Port} Confirmed:{Confirmed} Priority:{Priority} Queue:{queue}",
							context.DeviceId,
							methodName,
							message.MessageId,
							message.LockToken,
							downlink.Port,
							downlink.Confirmed,
							downlink.Priority,
							queue);
						#endregion
					}
					else
					{
						#region Azure IoT Hub C2D message processing
						if (!AzureDownlinkMessage.PortTryGet(message.Properties, out byte port))
						{
							_logger.LogWarning("Downlink-MessagedID:{MessageId} LockToken:{LockToken} Port property is invalid", message.MessageId, message.LockToken);

							await deviceClient.RejectAsync(message);
							return;
						}

						if (!AzureDownlinkMessage.ConfirmedTryGet(message.Properties, out bool confirmed))
						{
							_logger.LogWarning("Downlink-MessagedID:{MessageId} LockToken:{LockToken} Confirmed property is invalid", message.MessageId, message.LockToken);

							await deviceClient.RejectAsync(message);
							return;
						}

						if (!AzureDownlinkMessage.PriorityTryGet(message.Properties, out Models.DownlinkPriority priority))
						{
							_logger.LogWarning("Downlink-MessagedID:{MessageId} LockToken:{LockToken} Priority property is invalid", message.MessageId, message.LockToken);

							await deviceClient.RejectAsync(message);
							return;
						}

						if (!AzureDownlinkMessage.QueueTryGet(message.Properties, out queue))
						{
							_logger.LogWarning("Downlink-MessagedID:{MessageId} LockToken:{LockToken} Queue property is invalid", message.MessageId, message.LockToken);

							await deviceClient.RejectAsync(message.LockToken);
							return;
						}

						downlink = new Models.Downlink()
						{
							Confirmed = confirmed,
							Priority = priority,
							Port = port,
							CorrelationIds = AzureLockToken.Add(message.LockToken),
						};

						if (payloadText.IsPayloadValidJson())
						{
							downlink.PayloadDecoded = JToken.Parse(payloadText);
						}
						else
						{
							downlink.PayloadRaw = payloadText;
						}

						_logger.LogInformation("Downlink-IoT Hub DeviceID:{DeviceId} MessageID:{MessageId} LockToken:{LockToken} Port:{Port} Confirmed:{Confirmed} Priority:{Priority} Queue:{queue}",
							context.DeviceId,
							message.MessageId,
							message.LockToken,
							downlink.Port,
							downlink.Confirmed,
							downlink.Priority,
							queue);
						#endregion
					}

					Models.DownlinkPayload Payload = new Models.DownlinkPayload()
					{
						Downlinks = new List<Models.Downlink>()
						{
							downlink
						}
					};

					string url = $"{context.WebhookBaseURL}/{context.ApplicationId}/webhooks/{context.WebhookId}/devices/{context.DeviceId}/down/{queue}".ToLower();

					using (var client = new WebClient())
					{
						client.Headers.Add("Authorization", $"Bearer {context.ApiKey}");

						//await deviceClient.CompleteAsync(message);

						client.UploadString(new Uri(url), JsonConvert.SerializeObject(Payload));
					}

					_logger.LogInformation("Downlink-DeviceID:{DeviceId} MessageID:{MessageId} LockToken:{LockToken} success", context.DeviceId, message.MessageId, message.LockToken);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Downlink-ReceiveMessge processing failed");
			}
		}
	}
}