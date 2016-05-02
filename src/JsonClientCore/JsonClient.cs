using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JsonClientCore.Exceptions;
using JsonClientCore.Extensions;
using JsonClientCore.Models;
using Newtonsoft.Json;

namespace JsonClientCore
{
	public class JsonClient
	{
		/// <summary>
		/// 
		/// </summary>
		public Options Options { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		public Uri BaseUri { get; private set; }
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="baseUri"></param>
		/// <param name="options"></param>
		public JsonClient(Uri baseUri = null, Options options = null)
		{
			BaseUri = baseUri;
			Options = options ?? new Options();
		}

		public async Task<object> RequestAsync(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
		{
			return await RequestAsync<object, object, object>(method, path, @params, null, options);
		}

		public async Task<TResponse> RequestAsync<TResponse>(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
			where TResponse : class, new()
		{
			return await RequestAsync<TResponse, object, object>(method, path, @params, null, options);
		}

		public async Task<TResponse> RequestAsync<TResponse, TError>(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
			where TResponse : class, new()
			where TError : class, new()
		{
			return await RequestAsync<TResponse, object, TError>(method, path, @params, null, options);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="method"></param>
		/// <param name="path"></param>
		/// <param name="params"></param>
		/// <param name="body"></param>
		/// <param name="options"></param>
		public async Task<TResponse> RequestAsync<TResponse, TRequest, TError>(string method, string path = null, 
			Dictionary<string, string> @params = null, TRequest body = null, Options options = null)
			where TResponse : class, new()
			where TRequest : class, new()
			where TError : class, new()
		{
			if (method == null)
				throw new ArgumentNullException(nameof(method));

			if (path == null && BaseUri == null)
				throw new ArgumentNullException(nameof(path), $"The {path} argument can not be null if the {nameof(BaseUri)} options is null.");

			// If the user didn't specify any options params, initalize them
			@params = @params ?? new Dictionary<string, string>();
			options = options ?? new Options();

			// Merge Headers
			foreach (var header in Options.Headers)
			{
				if (!options.Headers.ContainsKey(header.Value))
					options.Headers.Add(header.Key, header.Value);
			}

			using (var httpClient = new HttpClient())
			{
				// Set base address if appropriate
				Uri requestUri = BaseUri == null
					? new Uri(path)
					: BaseUri.Append(path);

				// Set Headers
				foreach(var header in options.Headers)
					httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);

				// Check if we're sending a body
				StringContent bodyContent = body == null 
					? null 
					: new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

				// Create request
				var request = new HttpRequestMessage(new HttpMethod(method), requestUri) { Content = bodyContent };

				// Execute request and get response
				var response = await httpClient.SendAsync(request);

				// Check response status
				if (response.IsSuccessStatusCode)
				{
					// Check if there is response we can try and parse into json)
					var responseString = await response.Content.ReadAsStringAsync();
					if (responseString == null || responseString.Length == 0)
						return null;

					return JsonConvert.DeserializeObject<TResponse>(responseString);
				}

				// Attempt to parse json body
				TError jsonErrorBody = null;
				try
				{
					var responseString = await response.Content.ReadAsStringAsync();
					if (responseString != null && responseString.Length == 0)
						jsonErrorBody = JsonConvert.DeserializeObject<TError>(responseString);
				}
				catch (JsonReaderException)
				{ }

				// Throw time
				throw new JsonClientException<TError>("An exception occured while executing the http request.")
				{
					Code = response.ReasonPhrase,
					StatusCode = response.StatusCode,
					ExceptionMetadata = new ExceptionMetadata<TError>
					{
						Code = response.StatusCode,
						Method = request.Method.ToString().ToUpperInvariant(),
						Uri = request.RequestUri,
						Data = jsonErrorBody
					}
				};
			}
		}
	}
}
