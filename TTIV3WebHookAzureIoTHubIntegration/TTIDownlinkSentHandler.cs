﻿// Copyright (c) October 2021, devMobile Software
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
	using Microsoft.Azure.Functions.Worker;
	using Microsoft.Azure.Functions.Worker.Http;

	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;

	public partial class Integration
	{
		[Function("Sent")]
		public static async Task<HttpResponseData> Sent([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext executionContext)
		{
			var logger = executionContext.GetLogger("Sent");

			// Wrap all the processing in a try\catch so if anything blows up we have logged it. Will need to specialise for connectivity failues etc.
			try
			{
				Models.DownlinkAckPayload payload = JsonConvert.DeserializeObject<Models.DownlinkAckPayload>(await req.ReadAsStringAsync());
				if (payload == null)
				{
					logger.LogInformation("Sent: Payload {0} invalid", await req.ReadAsStringAsync());

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				logger.LogInformation("Sent-ApplicationID:{0} DeviceID:{1} ", applicationId, deviceId);

				if (!_DeviceClients.TryGetValue(deviceId, out DeviceClient deviceClient))
				{
					logger.LogInformation("Sent-Unknown device for ApplicationID:{0} DeviceID:{1}", applicationId, deviceId);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				if (!AzureLockToken.TryGet(payload.CorrelationIds, out string lockToken))
				{
					logger.LogWarning("Sent-DeviceID:{0} LockToken missing from payload:{1}", payload.EndDeviceIds.DeviceId, req.ReadAsStringAsync());

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				/*
				try
				{
					await deviceClient.RejectAsync(lockToken);
				}
				catch (DeviceMessageLockLostException)
				{
					logger.LogWarning("Sent-RejectAsync DeviceID:{0} LockToken:{1} timeout", payload.EndDeviceIds.DeviceId, lockToken);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}
				*/

				logger.LogInformation("Sent-Device{0} LockToken:{1} success", payload.EndDeviceIds.DeviceId, lockToken);
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