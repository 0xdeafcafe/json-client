using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
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
		/// Options to be passed into every request.
		/// </summary>
		public Options Options { get; private set; }

		/// <summary>
		/// The base url to be prefixed to every request.
		/// </summary>
		public Uri BaseUri { get; private set; }

		/// <summary>
		/// Initializes a new JsonClient.
		/// </summary>
		/// <param name="baseUri">The base url to be prefixed to every request.</param>
		/// <param name="options">Options to be passed into every request.</param>
		public JsonClient(Uri baseUri = null, Options options = null)
		{
			BaseUri = baseUri;
			Options = options ?? new Options();
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public async Task<object> RequestAsync(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
		{
			return await RequestAsync<object, object, Error>(method, path, @params, null, options);
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <typeparam name="TResponse">The type of the response object.</typeparam>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public async Task<TResponse> RequestAsync<TResponse>(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
			where TResponse : class, new()
		{
			return await RequestAsync<TResponse, object, Error>(method, path, @params, null, options);
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <typeparam name="TResponse">The type of the response object.</typeparam>
		/// <typeparam name="TError">The type of the error object.</typeparam>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public async Task<TResponse> RequestAsync<TResponse, TError>(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
			where TResponse : class, new()
			where TError : class, new()
		{
			return await RequestAsync<TResponse, object, TError>(method, path, @params, null, options);
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <typeparam name="TResponse">The type of the response object.</typeparam>
		/// <typeparam name="TRequest">The type of the reques object.</typeparam>
		/// <typeparam name="TError">The type of the error object.</typeparam>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
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

			// If the user didn't specify any options params, initialize them
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
				// Add Params to url
				if (@params.Any())
					path += "?" + string.Join("&",
						@params.Select(kvp =>
							string.Format("{0}={1}", kvp.Key, kvp.Value)));

				// Set base address if appropriate
				Uri requestUri = BaseUri == null
					? new Uri(path)
					: BaseUri.Append(path);

				// Set Headers
				foreach (var header in options.Headers)
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
					if (responseString != null && responseString.Length > 0)
						jsonErrorBody = JsonConvert.DeserializeObject<TError>(responseString);
				}
				catch (JsonReaderException)
				{ }

				Exception innerException = null;
				try
				{
					response.EnsureSuccessStatusCode();
				}
				catch (Exception ex)
				{
					innerException = ex;
				}
				
				throw new JsonClientException<TError>("An exception occurred while executing the http request.", innerException)
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

		#region Sync Wrapper

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public object Request(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
		{
			return Request<object, object, Error>(method, path, @params, null, options);
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <typeparam name="TResponse">The type of the response object.</typeparam>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public TResponse Request<TResponse>(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
			where TResponse : class, new()
		{
			return Request<TResponse, object, Error>(method, path, @params, null, options);
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <typeparam name="TResponse">The type of the response object.</typeparam>
		/// <typeparam name="TError">The type of the error object.</typeparam>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public TResponse Request<TResponse, TError>(string method, string path = null,
			Dictionary<string, string> @params = null, Options options = null)
			where TResponse : class, new()
			where TError : class, new()
		{
			return Request<TResponse, object, TError>(method, path, @params, null, options);
		}

		/// <summary>
		/// Makes an async web request.
		/// </summary>
		/// <typeparam name="TResponse">The type of the response object.</typeparam>
		/// <typeparam name="TRequest">The type of the request object.</typeparam>
		/// <typeparam name="TError">The type of the error object.</typeparam>
		/// <param name="method">The HTTP request verb.</param>
		/// <param name="path">The path to be appended to the base url (can be absolute).</param>
		/// <param name="params">The query string params to be appended to the path.</param>
		/// <param name="body">The http body of the request.</param>
		/// <param name="options">Options to be merged with the base options.</param>
		public TResponse Request<TResponse, TRequest, TError>(string method, string path = null, 
			Dictionary<string, string> @params = null, TRequest body = null, Options options = null)
			where TResponse : class, new()
			where TRequest : class, new()
			where TError : class, new()
		{
			try
			{
				return RequestAsync<TResponse, TRequest, TError>(method, path, @params, body, options).Result;
			}
			catch (AggregateException ex)
			{
				ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions.First()).Throw();

				// Required, but never hit
				return null;
			}
		}
		
		#endregion
	}
}
