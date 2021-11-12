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
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Client.Exceptions;
	using Microsoft.Azure.Devices.Provisioning.Client;
	using Microsoft.Azure.Devices.Provisioning.Client.PlugAndPlay;
	using Microsoft.Azure.Devices.Provisioning.Client.Transport;
	using Microsoft.Azure.Devices.Shared;
	using Microsoft.Azure.Functions.Worker;
	using Microsoft.Azure.Functions.Worker.Http;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	public partial class Integration
	{
		[Function("Uplink")]
		public async Task<HttpResponseData> Uplink([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext executionContext)
		{
			Models.PayloadUplink payload;
			var logger = executionContext.GetLogger("Queued");

			// Wrap all the processing in a try\catch so if anything blows up we have logged it.
			try
			{
				string payloadText = await req.ReadAsStringAsync();

				try
				{
					payload = JsonConvert.DeserializeObject<Models.PayloadUplink>(payloadText);
				}
				catch (JsonException ex)
				{
					logger.LogInformation(ex, "Uplink-Payload Invalid JSON:{0}", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				if (payload == null)
				{
					logger.LogInformation("Uplink-Payload invalid:{0}", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				if ((payload.UplinkMessage.Port == null) || (!payload.UplinkMessage.Port.HasValue) || (payload.UplinkMessage.Port.Value == 0))
				{
					logger.LogInformation("Uplink-ApplicationID:{0} DeviceID:{1} Payload Raw:{2} Control message", applicationId, deviceId, payload.UplinkMessage.PayloadRaw);

					return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
				}

				int port = payload.UplinkMessage.Port.Value;

				logger.LogInformation("Uplink-ApplicationID:{0} DeviceID:{1} Port:{2} Payload Raw:{3}", applicationId, deviceId, port, payload.UplinkMessage.PayloadRaw);

				if (!_DeviceClients.TryGetValue(deviceId, out DeviceClient deviceClient))
				{
					// Validate The Things Industries configuaration
					if (string.IsNullOrEmpty(_theThingsIndustriesSettings.WebhookBaseURL))
					{
						_logger.LogError("Uplink-The Things Industries no Webhook Base URL configured");

						return req.CreateResponse(HttpStatusCode.Conflict);
					}

					if (_theThingsIndustriesSettings.Applications == null)
					{
						_logger.LogError("Uplink-The Things Industries no applications configured");

						return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
					}

					// Validate The Things Industries Application configuaration
					if (!_theThingsIndustriesSettings.Applications.TryGetValue(applicationId, out TheThingsIndustriesSettingApplicationSetting ttiAppplicationSettings))
					{
						_logger.LogError("Uplink-DeviceID:{0} AppplicationID:{1} no Application settings configured", deviceId, applicationId);

						return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
					}

					if (string.IsNullOrEmpty(ttiAppplicationSettings.ApiKey))
					{
						_logger.LogError("Uplink-DeviceID:{0} AppplicationID:{1} no API Key configured", deviceId, applicationId);

						return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
					}

					if (string.IsNullOrEmpty(ttiAppplicationSettings.WebhookId))
					{
						_logger.LogError("Uplink-DeviceID:{0} AppplicationID:{1} no Webhook ID configured", deviceId, applicationId);

						return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
					}

					// Validate Azure IoT Hub and DeviceProvisioning Services configuration
					if ((_azureIoTSettings.DeviceProvisioningService != null) && (_azureIoTSettings.IoTHub != null))
					{
						_logger.LogError("Uplink-Azure IoT neither Azure Device Provisioning Service or IoT Hub configuration");

						return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
					}

					if ((_azureIoTSettings.DeviceProvisioningService == null) && (_azureIoTSettings.IoTHub == null))
					{
						_logger.LogError("Uplink-Azure IoT both Azure Device Provisioning Service or IoT Hub configuration");

						return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
					}

					// Validate the Azure IoT Hub Device configuration
					if (_azureIoTSettings.IoTHub != null)
					{
						if (_azureIoTSettings.IoTHub.Applications == null)
						{
							logger.LogError("Uplink-Device Provisioning Service Application settings not configured");

							return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
						}

						if (!_azureIoTSettings.IoTHub.Applications.TryGetValue( applicationId, out IoTHubApplicationSetting ioTHubApplicationSetting))
						{
							logger.LogError("Uplink-Device Provisioning Service Application settings not configured");

							return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
						}

						if (string.IsNullOrEmpty(ioTHubApplicationSetting.DtdlModelId))
						{
							logger.LogWarning("Uplink-Device Provisioning Service Application settings DTDL not configured");

							deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.IoTHub.IoTHubConnectionString, deviceId, TransportSettings);
						}
						else
						{
							ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
							{
								JsonData = PnpConvention.CreateDpsPayload(ioTHubApplicationSetting.DtdlModelId)
							};
							deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.IoTHub.IoTHubConnectionString, deviceId, TransportSettings);
						}

						try
						{
							await deviceClient.OpenAsync();
						}
						catch (DeviceNotFoundException)
						{
							logger.LogWarning("Uplink-Unknown DeviceID:{0}", deviceId);

							return req.CreateResponse(HttpStatusCode.NotFound);
						}
					}

					// Validate the Azure IoT Hub Device Provisioning Service configuration
					if (_azureIoTSettings.DeviceProvisioningService != null)
					{
						if (_azureIoTSettings.DeviceProvisioningService.Applications == null)
						{
							logger.LogError("Uplink-Device Provisioning Service Application settings not configured");

							return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
						}

						if (!_azureIoTSettings.DeviceProvisioningService.Applications.TryGetValue(applicationId, out DeviceProvisiongServiceApplicationSetting dpsApplicationSetting))
						{
							logger.LogError("Uplink-Device Provisioning Service Application settings not configured");

							return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
						}

						if (string.IsNullOrEmpty(dpsApplicationSetting.GroupEnrollmentKey))
						{
							logger.LogError("Uplink-Device Provisioning Service Application settings Group Enrollment not configured");

							return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
						}

						if (string.IsNullOrEmpty(dpsApplicationSetting.DtdlModelId))
						{
							logger.LogWarning("Uplink-Device Provisioning Service Application settings DTDL not configured");
						}

						string deviceKey;
						using (var hmac = new HMACSHA256(Convert.FromBase64String(dpsApplicationSetting.GroupEnrollmentKey)))
						{
							deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(deviceId)));
						}

						using (var securityProvider = new SecurityProviderSymmetricKey(deviceId, deviceKey, null))
						{
							using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
							{
								DeviceRegistrationResult result;

								ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
									Constants.AzureDpsGlobalDeviceEndpoint,
									dpsApplicationSetting.GroupEnrollmentKey,
									securityProvider,
									transport);

								try
								{
									// If TTI application doesn't have a DTDLV2 ID 
									if (!string.IsNullOrEmpty(dpsApplicationSetting.DtdlModelId))
									{
										ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
										{
											JsonData = PnpConvention.CreateDpsPayload(dpsApplicationSetting.DtdlModelId)
										};
										result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData);
									}
									else
									{
										result = await provClient.RegisterAsync();
									}
								}
								catch(ProvisioningTransportException ex)
								{
									logger.LogInformation(ex, "Uplink-DeviceID:{0} RegisterAsync failed IDScope and/or GroupEnrollmentKey invalid", deviceId);

									return req.CreateResponse(HttpStatusCode.Unauthorized);
								}

								if (result.Status != ProvisioningRegistrationStatusType.Assigned)
								{
									_logger.LogError("Uplink-DeviceID:{0} Status:{1} RegisterAsync failed ", deviceId, result.Status);

									return req.CreateResponse(HttpStatusCode.FailedDependency);
								}

								IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

								deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportSettings);

								await deviceClient.OpenAsync();

								logger.LogInformation("Uplink-DeviceID:{0} Azure IoT Hub with Device Provisioning Service connected", deviceId);
							}
						}
					}
					
					if (!_DeviceClients.TryAdd(deviceId, deviceClient))
					{
						logger.LogWarning("Uplink-DeviceID:{1} TryAdd failed", deviceId);

						return req.CreateResponse(HttpStatusCode.Conflict);
					}

					Models.AzureIoTHubReceiveMessageHandlerContext context = new Models.AzureIoTHubReceiveMessageHandlerContext()
					{
						DeviceId = deviceId,
						ApplicationId = applicationId,
						WebhookId = ttiAppplicationSettings.WebhookId,
						WebhookBaseURL = _theThingsIndustriesSettings.WebhookBaseURL,
						ApiKey = ttiAppplicationSettings.ApiKey
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
					EnumerateChildren(telemetryEvent, payload.UplinkMessage.PayloadDecoded);
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

		private void EnumerateChildren(JObject jobject, JToken token)
		{
			if (token is JProperty property)
			{
				if (token.First is JValue)
				{
					// Temporary dirty hack for Azure IoT Central compatibility
					if (token.Parent is JObject possibleGpsProperty)
					{
						// TODO Need to check if similar approach necessary accelerometer and gyro LPP payloads
						if (possibleGpsProperty.Path.StartsWith("GPS_", StringComparison.OrdinalIgnoreCase))
						{
							if (string.Compare(property.Name, "Latitude", true) == 0)
							{
								jobject.Add("lat", property.Value);
							}
							if (string.Compare(property.Name, "Longitude", true) == 0)
							{
								jobject.Add("lon", property.Value);
							}
							if (string.Compare(property.Name, "Altitude", true) == 0)
							{
								jobject.Add("alt", property.Value);
							}
						}
					}
					jobject.Add(property.Name, property.Value);
				}
				else
				{
					JObject parentObject = new JObject();
					foreach (JToken token2 in token.Children())
					{
						EnumerateChildren(parentObject, token2);
						jobject.Add(property.Name, parentObject);
					}
				}
			}
			else
			{
				foreach (JToken token2 in token.Children())
				{
					EnumerateChildren(jobject, token2);
				}
			}
		}

		private readonly ITransportSettings[] TransportSettings = new ITransportSettings[]
		{
			new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
			{
				AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
				{
					Pooling = true,
				}
			 }
		};
	}
}
