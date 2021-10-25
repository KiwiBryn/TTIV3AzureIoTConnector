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
		public async Task<HttpResponseData> Sent([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
		{
			// Wrap all the processing in a try\catch so if anything blows up we have logged it.
			try
			{
				string payloadText = await req.ReadAsStringAsync();

				Models.DownlinkSentPayload payload = JsonConvert.DeserializeObject<Models.DownlinkSentPayload>(payloadText);
				if (payload == null)
				{
					_logger.LogInformation("Sent-Payload {0} invalid", payloadText);

					return req.CreateResponse(HttpStatusCode.BadRequest);
				}

				string applicationId = payload.EndDeviceIds.ApplicationIds.ApplicationId;
				string deviceId = payload.EndDeviceIds.DeviceId;

				_logger.LogInformation("Sent-ApplicationID:{0} DeviceID:{1} ", applicationId, deviceId);

				if (!_DeviceClients.TryGetValue(deviceId, out DeviceClient deviceClient))
				{
					_logger.LogInformation("Sent-Unknown device for ApplicationID:{0} DeviceID:{1}", applicationId, deviceId);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				if (!AzureLockToken.TryGet(payload.DownlinkSent.CorrelationIds, out string lockToken))
				{
					_logger.LogWarning("Sent-DeviceID:{0} LockToken missing from payload:{1}", payload.EndDeviceIds.DeviceId, payloadText);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}
				
				try
				{
					await deviceClient.CompleteAsync(lockToken);
				}
				catch (DeviceMessageLockLostException)
				{
					_logger.LogWarning("Sent-RejectAsync DeviceID:{0} LockToken:{1} timeout", payload.EndDeviceIds.DeviceId, lockToken);

					return req.CreateResponse(HttpStatusCode.Conflict);
				}

				_logger.LogInformation("Sent-DeviceID:{0} LockToken:{1} success", payload.EndDeviceIds.DeviceId, lockToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Sent message processing failed");

				return req.CreateResponse(HttpStatusCode.InternalServerError);
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}
	}
}
