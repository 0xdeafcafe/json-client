using System;
using System.Net;
using JsonClientCore.Models;

namespace JsonClientCore.Exceptions
{
	public class JsonClientException<TError> : Exception
	{
		public JsonClientException(string message)
			: base(message)
		{ }

		public string Code { get; internal set; }

		public HttpStatusCode StatusCode { get; internal set; }

		public ExceptionMetadata<TError> ExceptionMetadata { get; internal set; }
	}
}
