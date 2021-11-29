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
	using System.Threading;
	using System.Threading.Tasks;

	using Microsoft.Azure.Devices.Client;
	using Microsoft.Azure.Devices.Client.Exceptions;
	using Microsoft.Azure.Functions.Worker;
	using Microsoft.Azure.Functions.Worker.Http;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;

	public partial class Integration
	{
		[Function("Ack")]
		public static async Task<HttpResponseData> Ack([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext executionContext, CancellationToken cancellationToken)
		{
			var logger = executionContext.GetLogger("Ack");

			// Wrap all the processing in a try\catch so if anything blows up we have logged it.
			try
			{
				Models.DownlinkAckPayload payload;

				string payloadText = await req.ReadAsStringAsync();

				try
				{ 
					payload = JsonConvert.DeserializeObject<Models.DownlinkAckPayload>(payloadText);
				}
				catch (JsonException ex)
				{
					logger.LogError(ex, "Ack-Payload Invalid JSON:{payloadText}", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				if (payload == null)
				{
					logger.LogError("Ack-Payload invalid Payload:{payloadText}", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				logger.LogInformation("Ack-DeviceID:{deviceId} ApplicationID:{applicationId}", deviceId, applicationId);

				DeviceClient deviceClient = await _DeviceClients.GetAsync<DeviceClient>(deviceId);
				if (deviceClient == null)
				{
					logger.LogWarning("Ack-DeviceID:{deviceId} unknown", deviceId);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				if (!AzureLockToken.TryGet(payload.DownlinkAck.CorrelationIds, out string lockToken))
				{
					logger.LogWarning("Ack-DeviceID:{deviceId} LockToken missing from Payload:{payloadText}", deviceId, payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				try
				{
					await deviceClient.CompleteAsync(lockToken, cancellationToken);

					logger.LogInformation("Ack-DeviceID:{deviceId} CompleteAsync success LockToken:{lockToken}", deviceId, lockToken);
				}
				catch (DeviceMessageLockLostException)
				{
					logger.LogWarning("Ack-DeviceID:{deviceId} CompleteAsync timeout LockToken:{lockToken}", deviceId, lockToken);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Ack-message processing failed");

				return req.CreateResponse(HttpStatusCode.InternalServerError);
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}
	}
}
