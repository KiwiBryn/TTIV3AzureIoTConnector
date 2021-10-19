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
// https://github.com/Azure/azure-functions-dotnet-worker/wiki/.NET-Worker-bindings
//---------------------------------------------------------------------------------
namespace devMobile.IoT.TheThingsIndustries.HttpInputStorageQueueOutput
{
	using System.Net;
	using System.Threading.Tasks;

	using Microsoft.Azure.Functions.Worker;
	using Microsoft.Azure.Functions.Worker.Http;
	using Microsoft.Azure.WebJobs;
	using Microsoft.Extensions.Logging;


	[StorageAccount("AzureWebJobsStorage")]
	public static class Webhooks
	{
		[Function("Uplink")]
		public static async Task<HttpTriggerUplinkOutputBindingType> Uplink([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
		{
			var logger = context.GetLogger("UplinkMessage");

			logger.LogInformation("Uplink processed");
			
			var response = req.CreateResponse(HttpStatusCode.OK);

			return new HttpTriggerUplinkOutputBindingType()
			{
				Name = await req.ReadAsStringAsync(),
				HttpReponse = response
			};
		}

		public class HttpTriggerUplinkOutputBindingType
		{
			[QueueOutput("uplink")]
			public string Name { get; set; }

			public HttpResponseData HttpReponse { get; set; }
		}

		[Function("Queued")]
		public static async Task<HttpTriggerQueuedOutputBindingType> Queued([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
		{
			var logger = context.GetLogger("UplinkMessage");

			logger.LogInformation("Uplink processed");

			var response = req.CreateResponse(HttpStatusCode.OK);

			return new HttpTriggerQueuedOutputBindingType()
			{
				Name = await req.ReadAsStringAsync(),
				HttpReponse = response
			};
		}

		public class HttpTriggerQueuedOutputBindingType
		{
			[QueueOutput("queued")]
			public string Name { get; set; }

			public HttpResponseData HttpReponse { get; set; }
		}


		[Function("Ack")]
		public static async Task<HttpTriggerAckOutputBindingType> Ack([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
		{
			var logger = context.GetLogger("Ack");

			logger.LogInformation("Ack processed");

			var response = req.CreateResponse(HttpStatusCode.OK);

			return new HttpTriggerAckOutputBindingType()
			{
				Name = await req.ReadAsStringAsync(),
				HttpReponse = response
			};
		}

		public class HttpTriggerAckOutputBindingType
		{
			[QueueOutput("ack")]
			public string Name { get; set; }

			public HttpResponseData HttpReponse { get; set; }
		}


		[Function("Nack")]
		public static async Task<HttpTriggerNackOutputBindingType> Nack([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
		{
			var logger = context.GetLogger("Nack");

			logger.LogInformation("Nack processed");

			var response = req.CreateResponse(HttpStatusCode.OK);

			return new HttpTriggerNackOutputBindingType()
			{
				Name = await req.ReadAsStringAsync(),
				HttpReponse = response
			};
		}

		public class HttpTriggerNackOutputBindingType
		{
			[QueueOutput("nack")]
			public string Name { get; set; }

			public HttpResponseData HttpReponse { get; set; }
		}


		[Function("Sent")]
		public static async Task<HttpTriggerSentOutputBindingType> Sent([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
		{
			var logger = context.GetLogger("Sent");

			logger.LogInformation("Send processed");

			var response = req.CreateResponse(HttpStatusCode.OK);

			return new HttpTriggerSentOutputBindingType()
			{
				Name = await req.ReadAsStringAsync(),
				HttpReponse = response
			};
		}

		public class HttpTriggerSentOutputBindingType
		{
			[QueueOutput("sent")]
			public string Name { get; set; }

			public HttpResponseData HttpReponse { get; set; }
		}


		[Function("Failed")]
		public static async Task<HttpTriggerFailedOutputBindingType> Failed([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
		{
			var logger = context.GetLogger("Failed");

			logger.LogInformation("Failed procssed");

			var response = req.CreateResponse(HttpStatusCode.OK);

			return new HttpTriggerFailedOutputBindingType()
			{
				Name = await req.ReadAsStringAsync(),
				HttpReponse = response
			};
		}

		public class HttpTriggerFailedOutputBindingType
		{
			[QueueOutput("failed")]
			public string Name { get; set; }

			public HttpResponseData HttpReponse { get; set; }
		}
	}
}
