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
	using System.Globalization;
	using System.Net;
	using System.Text;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Client.Exceptions;

	using Microsoft.Azure.Functions.Worker;
	using Microsoft.Azure.Functions.Worker.Http;

	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	public partial class Integration
	{
		[Function("Uplink")]
		public async Task<HttpResponseData> Uplink([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext executionContext)
		{
			var logger = executionContext.GetLogger("Queued");

			// Wrap all the processing in a try\catch so if anything blows up we have logged it.
			try
			{
				string payloadText = await req.ReadAsStringAsync();

				Models.PayloadUplink payload = JsonConvert.DeserializeObject<Models.PayloadUplink>(payloadText);
				if (payload == null)
				{
					logger.LogInformation("Uplink-Payload {0} invalid", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				if ((payload.UplinkMessage.Port == null ) || (!payload.UplinkMessage.Port.HasValue) || (payload.UplinkMessage.Port.Value == 0))
				{
					logger.LogInformation("Uplink-ApplicationID:{0} DeviceID:{1} Payload Raw:{2} Control message", applicationId, deviceId, payload.UplinkMessage.PayloadRaw);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				int port = payload.UplinkMessage.Port.Value;

				logger.LogInformation("Uplink-ApplicationID:{0} DeviceID:{1} Port:{2} Payload Raw:{3}", applicationId, deviceId, port, payload.UplinkMessage.PayloadRaw);

				if (!_DeviceClients.TryGetValue(deviceId, out DeviceClient deviceClient))
				{
					logger.LogInformation("Uplink-Unknown device for ApplicationID:{0} DeviceID:{1}", applicationId, deviceId);

					deviceClient = DeviceClient.CreateFromConnectionString(_azureSettings.IoTHubConnectionString, deviceId,
						new ITransportSettings[]
						{
							new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
							{
								AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
								{
									Pooling = true,
								}
							}
						});

					try
					{
						await deviceClient.OpenAsync();
					}
					catch (DeviceNotFoundException)
					{
						logger.LogWarning("Uplink-Unknown DeviceID:{0}", deviceId);

						return req.CreateResponse(HttpStatusCode.NotFound);
					}

					if (!_DeviceClients.TryAdd(deviceId, deviceClient))
					{
						logger.LogWarning("Uplink-TryAdd failed for ApplicationID:{0} DeviceID:{1}", applicationId, deviceId);

						return req.CreateResponse(HttpStatusCode.Conflict);
					}

					Models.AzureIoTHubReceiveMessageHandlerContext context = new Models.AzureIoTHubReceiveMessageHandlerContext()
					{ 
						DeviceId = deviceId,
						ApplicationId = applicationId,
						WebhookId = _theThingsIndustriesSettings.WebhookId,
						WebhookBaseURL = _theThingsIndustriesSettings.WebhookBaseURL,
						ApiKey = _theThingsIndustriesSettings.ApiKey 
					};

					await deviceClient.SetReceiveMessageHandlerAsync(AzureIoTHubClientReceiveMessageHandler, context);

					await deviceClient.SetMethodDefaultHandlerAsync(AzureIoTHubClientDefaultMethodHandler, context);
				}

				JObject telemetryEvent = new JObject
				{
					{ "ApplicationID", applicationId },
					{ "DeviceID", deviceId },
					{ "Port", port },
					{ "Simulated", payload.Simulated },
					{ "ReceivedAtUtc", payload.UplinkMessage.ReceivedAtUtc.ToString("s", CultureInfo.InvariantCulture) },
					{ "PayloadRaw", payload.UplinkMessage.PayloadRaw }
				};

				// If the payload has been decoded by payload formatter, put it in the message body.
				if (payload.UplinkMessage.PayloadDecoded != null)
				{
					telemetryEvent.Add("PayloadDecoded", payload.UplinkMessage.PayloadDecoded);
				}

				// Send the message to Azure IoT Hub
				using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
				{
					// Ensure the displayed time is the acquired time rather than the uploaded time. 
					ioTHubmessage.Properties.Add("iothub-creation-time-utc", payload.UplinkMessage.ReceivedAtUtc.ToString("s", CultureInfo.InvariantCulture));
					ioTHubmessage.Properties.Add("ApplicationId", applicationId);
					ioTHubmessage.Properties.Add("DeviceEUI", payload.EndDeviceIds.DeviceEui);
					ioTHubmessage.Properties.Add("DeviceId", deviceId);
					ioTHubmessage.Properties.Add("port", port.ToString());
					ioTHubmessage.Properties.Add("Simulated", payload.Simulated.ToString());

					await deviceClient.SendEventAsync(ioTHubmessage);

					logger.LogInformation("Uplink-DeviceID:{0} SendEventAsync success", payload.EndDeviceIds.DeviceId);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Uplink-Message processing failed");

				return req.CreateResponse(HttpStatusCode.InternalServerError);
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}
		/*
		private static async Task DeviceRegistration(string applicationId, string deviceId, string modelId, CancellationToken stoppingToken)
		{
			DeviceClient deviceClient = null;
			ITransportSettings[] transportSettings = new ITransportSettings[]
			{
				new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
				{
					AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
					{
						Pooling = true,
					}
				 }
			 };

			try
			{
				// See if AzureIoT hub connections string has been configured
				if (_programSettings.ConnectionStringResolve(applicationId, out string connectionString))
				{
					if (!string.IsNullOrEmpty(modelId))
					{
						ClientOptions clientoptions = new ClientOptions()
						{
							ModelId = modelId
						};
						deviceClient = DeviceClient.CreateFromConnectionString(connectionString, deviceId, transportSettings, clientoptions);
					}
					else
					{
						deviceClient = DeviceClient.CreateFromConnectionString(connectionString, deviceId, transportSettings);
					}
				}

				// See if DPS has been configured
				if (_programSettings.DeviceProvisioningServiceSettingsResolve(applicationId, out AzureDeviceProvisiongServiceSettings deviceProvisiongServiceSettings))
				{
					string deviceKey;

					using (var hmac = new HMACSHA256(Convert.FromBase64String(deviceProvisiongServiceSettings.GroupEnrollmentKey)))
					{
						deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(deviceId)));
					}

					using (var securityProvider = new SecurityProviderSymmetricKey(deviceId, deviceKey, null))
					{
						using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
						{
							ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
								Constants.AzureDpsGlobalDeviceEndpoint,
								deviceProvisiongServiceSettings.IdScope,
								securityProvider,
								transport);

							DeviceRegistrationResult result;

							if (!string.IsNullOrEmpty(modelId))
							{
								ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
								{
									JsonData = PnpConvention.CreateDpsPayload(modelId)
								};

								result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData, stoppingToken);
							}
							else
							{
								result = await provClient.RegisterAsync(stoppingToken);
							}

							if (result.Status != ProvisioningRegistrationStatusType.Assigned)
							{
								_logger.LogError("Config-DeviceID:{0} Status:{1} RegisterAsync failed ", deviceId, result.Status);

								return;
							}

							IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

							deviceClient = DeviceClient.Create(result.AssignedHub, authentication, transportSettings);
						}
					}
				}

				if (deviceClient == null)
				{
					_logger.LogError("Config-DeviceID:{0} DeviceClient.Create failed ", deviceId);

					return;
				}

				await deviceClient.OpenAsync(stoppingToken);

				if (!_DeviceClients.TryAdd(deviceId, deviceClient))
				{
					// Need to decide whether device cache add failure aborts startup
					_logger.LogError("Config-Device:{0} cache add failed", deviceId);

					return;
				}

				AzureIoTHubReceiveMessageHandlerContext context = new AzureIoTHubReceiveMessageHandlerContext()
				{
					TenantId = _programSettings.TheThingsIndustries.Tenant,
					DeviceId = deviceId,
					ApplicationId = applicationId,
					MethodSettings = _programSettings.Applications[applicationId].MethodSettings,
				};

				await deviceClient.SetReceiveMessageHandlerAsync(AzureIoTHubClientReceiveMessageHandler, context, stoppingToken);

				await deviceClient.SetMethodDefaultHandlerAsync(AzureIoTHubClientDefaultMethodHandler, context, stoppingToken);
			}
			catch (DeviceNotFoundException)
			{
				_logger.LogWarning("Config-Azure Device:{0} device not found connection failed", deviceId);

				return;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Config-Azure Device:{0} connection failed", deviceId);

				return;
			}

			return;
		}
		*/
	}
}
