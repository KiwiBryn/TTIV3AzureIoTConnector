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
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public partial class Integration
    {
        [Function("Uplink")]
        public async Task<HttpResponseData> Uplink([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext executionContext)
        {
            Models.PayloadUplink payload;
            var logger = executionContext.GetLogger("Uplink");

            // Validate Azure IoT Hub and DeviceProvisioning Services configuration
            if ((_azureIoTSettings.DeviceProvisioningService != null) && (_azureIoTSettings.IoTHub != null))
            {
                logger.LogError("Uplink-Azure IoT both Azure Device Provisioning Service and IoT Hub configuration present");

                return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            }

            if ((_azureIoTSettings.DeviceProvisioningService == null) && (_azureIoTSettings.IoTHub == null))
            {
                logger.LogError("Uplink-Azure IoT neither Azure Device Provisioning Service or IoT Hub configuration present");

                return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            }

            // Validate Azure IoT Hub configuration
            if (_azureIoTSettings.IoTHub != null)
            {
                if (string.IsNullOrWhiteSpace(_azureIoTSettings.IoTHub.IoTHubConnectionString))
                {
                    logger.LogError("Uplink-IoT Hub connection string not configured");

                    return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                }

                if (_azureIoTSettings.IoTHub.Applications == null)
                {
                    logger.LogError("Uplink-IoT Hub Application settings not configured");

                    return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                }
            }

            // Validate DeviceProvisioning Services configuration
            if (_azureIoTSettings.DeviceProvisioningService != null)
            {
                if (string.IsNullOrWhiteSpace(_azureIoTSettings.DeviceProvisioningService.IdScope))
                {
                    logger.LogError("Uplink-Device Provisioning Service IdScope not configured");

                    return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                }

                if (_azureIoTSettings.DeviceProvisioningService.Applications == null)
                {
                    logger.LogError("Uplink-Device Provisioning Service Application settings not configured");

                    return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                }
            }

            string payloadText = await req.ReadAsStringAsync();

            try
            {
                payload = JsonConvert.DeserializeObject<Models.PayloadUplink>(payloadText);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Uplink-Payload Invalid JSON:{payloadText}", payloadText);

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (payload == null)
            {
                logger.LogWarning("Uplink-Payload invalid:{payloadText}", payloadText);

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
            string deviceId = payload.EndDeviceIds.DeviceId;

            if ((payload.UplinkMessage.Port == null) || (!payload.UplinkMessage.Port.HasValue) || (payload.UplinkMessage.Port.Value == 0))
            {
                logger.LogInformation("Uplink-DeviceID:{deviceId} ApplicationID:{applicationId} Control message Payload Raw:{PayloadRaw}", deviceId, applicationId, payload.UplinkMessage.PayloadRaw);

                return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            }

            int port = payload.UplinkMessage.Port.Value;

            logger.LogInformation("Uplink-DeviceID:{deviceId} ApplicationID:{applicationId} Port:{port}", deviceId, applicationId, port);

            // Validate The Things Industries Application configuaration
            if (!_theThingsIndustriesSettings.Applications.TryGetValue(applicationId, out TheThingsIndustriesSettingApplicationSetting ttiAppplicationSettings))
            {
                logger.LogError("Uplink-AppplicationID:{applicationId} no TTI Application settings configured", applicationId);

                return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            }

            if (string.IsNullOrEmpty(ttiAppplicationSettings.ApiKey))
            {
                logger.LogError("Uplink-AppplicationID:{applicationId} no TTI API Key configured", applicationId);

                return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            }

            if (string.IsNullOrEmpty(ttiAppplicationSettings.WebhookId))
            {
                logger.LogError("Uplink- AppplicationID:{applicationId} no TTI Webhook ID configured", applicationId);

                return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            }

            Models.AzureIoTHubDeviceClientContext context = new Models.AzureIoTHubDeviceClientContext()
            {
                DeviceId = deviceId,
                ApplicationId = applicationId,
                WebhookId = ttiAppplicationSettings.WebhookId,
                WebhookBaseURL = _theThingsIndustriesSettings.WebhookBaseURL,
                ApiKey = ttiAppplicationSettings.ApiKey
            };

            DeviceClient deviceClient = null;

            // Wrap all the processing in a try\catch so if anything blows up we have logged it.
            try
            {
                // Use the Azure IoT Hub Device configuration
                if (_azureIoTSettings.IoTHub != null)
                {
                    if (!_azureIoTSettings.IoTHub.Applications.TryGetValue(applicationId, out IoTHubApplicationSetting ioTHubApplicationSetting))
                    {
                        logger.LogError("Uplink-ApplicationID:{applicationId} IoTHub Application settings not configured", applicationId);

                        return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                    }

                    deviceClient = await _DeviceClients.GetOrAddAsync<DeviceClient>(deviceId, (ICacheEntry x) => IoTHubConnectAsync(context, ioTHubApplicationSetting.DtdlModelId, logger), memoryCacheEntryOptions);
                }

                // Use the Azure IoT Hub Device Provisioning Service configuration
                if (_azureIoTSettings.DeviceProvisioningService != null)
                {
                    if (!_azureIoTSettings.DeviceProvisioningService.Applications.TryGetValue(applicationId, out DeviceProvisiongServiceApplicationSetting dpsApplicationSetting))
                    {
                        logger.LogError("Uplink-ApplicationID:{applicationId} Device Provisioning Service Application settings not configured", applicationId);

                        return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                    }

                    if (string.IsNullOrEmpty(dpsApplicationSetting.GroupEnrollmentKey))
                    {
                        logger.LogError("Uplink-ApplicationID:{applicationId} Device Provisioning Service Application settings Group Enrollment not configured", applicationId);

                        return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                    }

                    deviceClient = await _DeviceClients.GetOrAddAsync<DeviceClient>(deviceId, (ICacheEntry x) => DeviceProvisioningServiceConnectAsync(context, dpsApplicationSetting.GroupEnrollmentKey, dpsApplicationSetting.DtdlModelId, logger), memoryCacheEntryOptions);
                }

                JObject telemetryEvent = new JObject
                {
                    { "ApplicationID", applicationId },
                    { "DeviceEUI" , payload.EndDeviceIds.DeviceEui},
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

                    logger.LogInformation("Uplink-DeviceID:{deviceId} SendEventAsync success", deviceId);
                }

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (IotHubCommunicationException iex)
            {
                // Azure IoT Hub device not found failure
                logger.LogError(iex, "Uplink-DeviceID:{deviceId} IoT Hub not found failure", deviceId);
            }
            catch (DeviceNotFoundException dex)
            {
                // Azure IoT Hub device not found failure wonder if this should return a 404, but that could be useful for "fishing".
                logger.LogError(dex, "Uplink-DeviceID:{deviceId} device not found failure", deviceId);
            }
            catch (ProvisioningTransportException pex)
            {
                // Azure IoT Hub DPS failure
                logger.LogError(pex, "Uplink-DeviceID:{deviceId} RegisterAsync failed IDScope and/or GroupEnrollmentKey invalid", deviceId);
            }
            catch (Exception ex)
            {
                // Catch all exception
                logger.LogError(ex, "Uplink-DeviceID:{deviceId} ApplicationID:{applicationId} failure", deviceId, applicationId);
            }

            // Remove from the cache and it will get tried again on the next message
            _DeviceClients.Remove(deviceId);

            return req.CreateResponse(HttpStatusCode.UnprocessableEntity);
        }

        private async Task<DeviceClient> IoTHubConnectAsync(Models.AzureIoTHubDeviceClientContext context, string dtdlModelId, ILogger logger)
        {
            DeviceClient deviceClient;

            if (string.IsNullOrEmpty(dtdlModelId))
            {
                logger.LogWarning("Uplink-ApplicationID:{applicationId} IoT Hub Application settings DTDL not configured", context.ApplicationId);

                deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.IoTHub.IoTHubConnectionString, context.DeviceId, TransportSettings);
            }
            else
            {
                ClientOptions clientOptions = new ClientOptions()
                {
                    ModelId = dtdlModelId,
                };

                deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.IoTHub.IoTHubConnectionString, context.DeviceId, TransportSettings, clientOptions);
            }

            await deviceClient.OpenAsync();

            await deviceClient.SetReceiveMessageHandlerAsync(AzureIoTHubClientReceiveMessageHandler, context);

            await deviceClient.SetMethodDefaultHandlerAsync(AzureIoTHubClientDefaultMethodHandler, context);

            return deviceClient;
        }

        private async Task<DeviceClient> DeviceProvisioningServiceConnectAsync(Models.AzureIoTHubDeviceClientContext context, string groupEnrollmentKey, string dtdlModelId, ILogger logger)
        {
            DeviceClient deviceClient;

            string deviceKey;
            using (var hmac = new HMACSHA256(Convert.FromBase64String(groupEnrollmentKey)))
            {
                deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(context.DeviceId)));
            }

            using (var securityProvider = new SecurityProviderSymmetricKey(context.DeviceId, deviceKey, null))
            {
                using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
                {
                    DeviceRegistrationResult result;

                    ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
                        Constants.AzureDpsGlobalDeviceEndpoint,
                        _azureIoTSettings.DeviceProvisioningService.IdScope,
                        securityProvider,
                        transport);

                    // If TTI application does have a DTDLV2 ID 
                    if (!string.IsNullOrEmpty(dtdlModelId))
                    {
                        ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
                        {
                            JsonData = PnpConvention.CreateDpsPayload(dtdlModelId)
                        };
                        result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData);
                    }
                    else
                    {
                        result = await provClient.RegisterAsync();
                    }

                    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                    {
                        logger.LogWarning("Uplink-DeviceID:{deviceId} Status:{result.Status} RegisterAsync failed ", context.DeviceId, result.Status);
                        return null;
                    }

                    IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                    deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportSettings);
                }
            }

            await deviceClient.OpenAsync();

            await deviceClient.SetReceiveMessageHandlerAsync(AzureIoTHubClientReceiveMessageHandler, context);

            await deviceClient.SetMethodDefaultHandlerAsync(AzureIoTHubClientDefaultMethodHandler, context);

            return deviceClient;
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

        private readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };

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
