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
	using System.Net;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Client.Exceptions;
	using Microsoft.Azure.Functions.Worker;
	using Microsoft.Azure.Functions.Worker.Http;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;

	public partial class Integration
	{
		[Function("Sent")]
		public static async Task<HttpResponseData> Sent([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext executionContext)
		{
			var logger = executionContext.GetLogger("Sent");

			// Wrap all the processing in a try\catch so if anything blows up we have logged it.
			try
			{
				Models.DownlinkSentPayload payload;

				string payloadText = await req.ReadAsStringAsync();

				try
				{
					payload = JsonConvert.DeserializeObject<Models.DownlinkSentPayload>(payloadText);
				}
				catch (JsonException ex)
				{
					logger.LogInformation(ex, "Sent-Payload Invalid JSON:{payloadText}", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				if (payload == null)
				{
					logger.LogInformation("Sent-Payload invalid Payload:{payloadText}", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				logger.LogInformation("Sent-DeviceID:{deviceId} ApplicationID:{applicationId} ", deviceId, applicationId);

				DeviceClient deviceClient = await _DeviceClients.GetAsync<DeviceClient>(deviceId);
				if (deviceClient == null)
				{
					logger.LogInformation("Sent-DeviceID:{deviceId} unknown", deviceId);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				if (!AzureLockToken.TryGet(payload.DownlinkSent.CorrelationIds, out string lockToken))
				{
					logger.LogWarning("Sent-DeviceID:{DeviceId} LockToken missing from Payload:{payloadText}", payload.EndDeviceIds.DeviceId, payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}
				
				try
				{
					await deviceClient.CompleteAsync(lockToken);

					logger.LogInformation("Sent-DeviceID:{DeviceId} CompleteAsync success LockToken:{lockToken}", payload.EndDeviceIds.DeviceId, lockToken);
				}
				catch (DeviceMessageLockLostException)
				{
					logger.LogWarning("Sent-DeviceID:{DeviceId} CompleteAsync timeout LockToken:{lockToken}", payload.EndDeviceIds.DeviceId, lockToken);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Sent message processing failed");

				return req.CreateResponse(HttpStatusCode.InternalServerError);
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}
	}
}
