using System;
using System.Net;

namespace JsonClientCore.Models
{
	public class ExceptionMetadata<TError>
	{
		public HttpStatusCode Code { get; internal set; }

		public string Method { get; internal set; }

		public Uri Uri { get; internal set; }

		public TError Data { get; internal set; }
	}
}
