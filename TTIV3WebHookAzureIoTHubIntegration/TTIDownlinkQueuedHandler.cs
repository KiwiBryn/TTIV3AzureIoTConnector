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
		[Function("Queued")]
		public async Task<HttpResponseData> Queued([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext executionContext)
		{
			var logger = executionContext.GetLogger("Queued");

			// Wrap all the processing in a try\catch so if anything blows up we have logged it.
			try
			{
				string payloadText = await req.ReadAsStringAsync();

				Models.DownlinkQueuedPayload payload = JsonConvert.DeserializeObject<Models.DownlinkQueuedPayload>(payloadText);
				if (payload == null)
				{
					logger.LogInformation("Queued-Payload {0} invalid", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				logger.LogInformation("Queued-ApplicationID:{0} DeviceID:{1} ", applicationId, deviceId);

				if (!_DeviceClients.TryGetValue(deviceId, out DeviceClient deviceClient))
				{
					logger.LogInformation("Queued-Unknown device for ApplicationID:{0} DeviceID:{1}", applicationId, deviceId);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				// If the message is not confirmed "complete" it as soon as with network
				if (!payload.DownlinkQueued.Confirmed)
				{
					if (!AzureLockToken.TryGet(payload.DownlinkQueued.CorrelationIds, out string lockToken))
					{
						logger.LogWarning("Queued-DeviceID:{0} LockToken missing from payload:{1}", payload.EndDeviceIds.DeviceId, payloadText);

						return req.CreateResponse(HttpStatusCode.Conflict);
					}

					try
					{
						await deviceClient.CompleteAsync(lockToken);
					}
					catch (DeviceMessageLockLostException)
					{
						logger.LogWarning("Queued-CompleteAsync DeviceID:{0} LockToken:{1} timeout", payload.EndDeviceIds.DeviceId, lockToken);

						return req.CreateResponse(HttpStatusCode.Conflict);
					}

					logger.LogInformation("Queued-DeviceID:{0} LockToken:{1} success", payload.EndDeviceIds.DeviceId, lockToken);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Queued message processing failed");

				return req.CreateResponse(HttpStatusCode.InternalServerError);
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}
	}
}
